using System.Net.Http.Json;
using OidcProxy.Net.IdentityProviders;
using OidcProxy.Net.Logging;
using OidcProxy.Net.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;

namespace OidcProxy.Net.EntraId;

public class EntraIdIdentityProvider : OpenIdConnectIdentityProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EntraIdConfig _configuration;

    public EntraIdIdentityProvider(
        ILogger logger,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        EntraIdConfig configuration)
        : base(logger, cache, httpClientFactory, configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    protected override string DiscoveryEndpointAddress => _configuration.DiscoveryEndpoint;

    public override async Task<AuthorizeRequest> GetAuthorizeUrlAsync(string redirectUri)
    {
        var app = ConfidentialClientApplicationBuilder.Create(_configuration.ClientId)
            .WithClientSecret(_configuration.ClientSecret)
            .Build();

        var startUrl = await app.GetAuthorizationRequestUrl(_configuration.Scopes)
            .WithPkce(out var verifier)
            .WithRedirectUri(redirectUri)
            .WithTenantId(_configuration.TenantId)
            .ExecuteAsync();
        
        return new AuthorizeRequest(startUrl, verifier);
    }

    public override Task RevokeAsync(string token, string traceIdentifier)
    {
        return Task.CompletedTask; // not supported by Azure
    }

    protected override async Task<DiscoveryDocument?> ObtainDiscoveryDocument(string endpointAddress)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var httpResponse = await httpClient.GetAsync(endpointAddress);
        return await httpResponse.Content.ReadFromJsonAsync<DiscoveryDocument>();
    }
}