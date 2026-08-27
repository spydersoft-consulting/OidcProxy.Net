using System.Net.Http.Json;
using OidcProxy.Net.IdentityProviders;
using OidcProxy.Net.Logging;
using OidcProxy.Net.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Client;

namespace OidcProxy.Net.EntraId;

public class EntraIdIdentityProvider(
    ILogger logger,
    IMemoryCache cache,
    IHttpClientFactory httpClientFactory,
    EntraIdConfig configuration)
    : OpenIdConnectIdentityProvider(logger, cache, httpClientFactory, configuration)
{
    protected override string DiscoveryEndpointAddress => configuration.DiscoveryEndpoint;

    public override async Task<AuthorizeRequest> GetAuthorizeUrlAsync(string redirectUri)
    {
        var app = ConfidentialClientApplicationBuilder.Create(configuration.ClientId)
            .WithClientSecret(configuration.ClientSecret)
            .Build();

        var startUrl = await app.GetAuthorizationRequestUrl(configuration.Scopes)
            .WithPkce(out var verifier)
            .WithRedirectUri(redirectUri)
            .WithTenantId(configuration.TenantId)
            .ExecuteAsync();
        
        return new AuthorizeRequest(startUrl, verifier);
    }

    public override Task RevokeAsync(string token, string traceIdentifier)
    {
        return Task.CompletedTask; // not supported by Azure
    }

    protected override async Task<DiscoveryDocument?> ObtainDiscoveryDocument(string endpointAddress)
    {
        using var httpClient = httpClientFactory.CreateClient();
        var httpResponse = await httpClient.GetAsync(endpointAddress);
        return await httpResponse.Content.ReadFromJsonAsync<DiscoveryDocument>();
    }
}