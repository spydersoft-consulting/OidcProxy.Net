using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OidcProxy.Net.IdentityProviders;
using OidcProxy.Net.Jwt.SignatureValidation;
using OidcProxy.Net.ModuleInitializers;
using OidcProxy.Net.Tests.ComponentIntegrationTests.Login;
using Yarp.ReverseProxy.Configuration;
using Browser = OidcProxy.Net.Tests.ComponentIntegrationTests.Login.PkceLoginRoundTripTests.Browser;
using PkceIdentityProvider = OidcProxy.Net.Tests.ComponentIntegrationTests.Login.PkceLoginRoundTripTests.PkceIdentityProvider;

namespace OidcProxy.Net.Tests.ComponentIntegrationTests.Proxy;

public class AccessTokenForwardingTests
{
    private const string Echo = "/downstream/echo";

    [Fact]
    public async Task ProxiedRequest_AfterLogin_ShouldCarryTheAccessTokenAsBearerToken()
    {
        var idp = new PkceIdentityProvider();
        await using var downstream = await StartDownstream();
        await using var proxy = await StartProxy(idp, downstream.Urls.Single());
        using var browser = new Browser(proxy);

        var authorizeUri = await browser.SignIn();
        var callback = await browser.Callback(authorizeUri);
        await browser.Get(callback.Headers.Location!);

        var response = await browser.Get(new Uri(Echo, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be($"Bearer {idp.IssuedAccessToken}");
    }

    [Fact]
    public async Task ProxiedRequest_WithoutSession_ShouldNotCarryAnAuthorizationHeader()
    {
        await using var downstream = await StartDownstream();
        await using var proxy = await StartProxy(new PkceIdentityProvider(), downstream.Urls.Single());
        using var browser = new Browser(proxy);

        var response = await browser.Get(new Uri(Echo, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>A real HTTP server that echoes the Authorization header it receives.</summary>
    private static async Task<WebApplication> StartDownstream()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.MapGet("/echo", (HttpContext context) => Results.Text(context.Request.Headers.Authorization.ToString()));

        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> StartProxy(IIdentityProvider idp, string downstreamAddress)
    {
        var options = new ProxyOptions { Mode = Mode.Proxy, AlwaysRedirectToHttps = false };
        options.RegisterIdentityProvider<PkceIdentityProvider, PkceLoginRoundTripTests.TestConfig>(new PkceLoginRoundTripTests.TestConfig());
        options.ConfigureYarp(
            [
                new RouteConfig
                {
                    RouteId = "downstream",
                    ClusterId = "downstream",
                    Match = new RouteMatch { Path = "/downstream/{**catch-all}" },
                    Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = "/downstream" }],
                }
            ],
            [
                new ClusterConfig
                {
                    ClusterId = "downstream",
                    Destinations = new Dictionary<string, DestinationConfig> { ["d"] = new() { Address = downstreamAddress } },
                }
            ]);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        foreach (var bootstrap in options.GetConfiguration())
        {
            bootstrap.Configure(options, builder.Services);
        }

        // Registered last, so these replace the registrations made by the bootstraps.
        builder.Services.AddSingleton<IIdentityProvider>(idp);
        builder.Services.AddSingleton<IJwtSignatureValidator, PkceLoginRoundTripTests.AcceptAnySignature>();

        var app = builder.Build();
        foreach (var bootstrap in options.GetConfiguration())
        {
            bootstrap.Configure(options, app);
        }

        await app.StartAsync();
        return app;
    }
}
