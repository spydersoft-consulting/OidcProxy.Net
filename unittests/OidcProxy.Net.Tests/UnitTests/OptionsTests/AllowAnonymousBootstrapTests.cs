using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OidcProxy.Net.Cryptography;
using OidcProxy.Net.IdentityProviders;
using OidcProxy.Net.Middleware;
using OidcProxy.Net.ModuleInitializers;
using OidcProxy.Net.ModuleInitializers.Configuration;

namespace OidcProxy.Net.Tests.UnitTests.OptionsTests;

public class AllowAnonymousBootstrapTests
{
    [Theory]
    [InlineData(Mode.Proxy, true)]
    [InlineData(Mode.Proxy, false)]
    [InlineData(Mode.AuthenticateOnly, true)]
    [InlineData(Mode.AuthenticateOnly, false)]
    public void ServiceProvider_ShouldBuildWithValidation(Mode mode, bool allowAnonymousAccess)
    {
        var services = BuildServices(new ProxyOptions { Mode = mode, AllowAnonymousAccess = allowAnonymousAccess });

        Action actual = () => services
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true })
            .Dispose();

        actual.Should().NotThrow();
    }

    [Theory]
    [InlineData(Mode.Proxy)]
    [InlineData(Mode.AuthenticateOnly)]
    public void SkipAuthRoutesAndApiRoutes_ShouldReflectOptions(Mode mode)
    {
        var options = new ProxyOptions
        {
            Mode = mode,
            SkipAuthRoutes = ["GET=/public/.*"],
            ApiRoutes = ["/api/.*"]
        };
        using var provider = BuildServices(options).BuildServiceProvider();

        var skipAuthRoutes = provider.GetRequiredService<ISkipAuthRoutes>();
        var apiRoutes = provider.GetRequiredService<IApiRoutes>();

        skipAuthRoutes.ShouldBypass("GET", "/public/file").Should().BeTrue();
        skipAuthRoutes.ShouldBypass("POST", "/public/file").Should().BeFalse();
        skipAuthRoutes.ShouldBypass("GET", "/private").Should().BeFalse();
        apiRoutes.Matches("GET", "/api/values").Should().BeTrue();
        apiRoutes.Matches("GET", "/other").Should().BeFalse();
    }

    [Fact]
    public async Task Request_WithoutAnonymousAccess_ShouldRedirectUnauthenticatedUser()
    {
        var idp = new TestIdp();
        await using var app = await StartApp(idp, allowAnonymousAccess: false);

        var response = await app.GetTestClient().GetAsync("/private");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(TestIdp.AuthorizeUri);
        idp.AuthorizeCalls.Should().Be(1);
    }

    [Fact]
    public async Task Request_WithoutAnonymousAccess_ShouldReturn401OnApiRoute()
    {
        await using var app = await StartApp(new TestIdp(), allowAnonymousAccess: false);

        var response = await app.GetTestClient().GetAsync("/api/values");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Request_WithoutAnonymousAccess_ShouldAllowSkipAuthRoute()
    {
        await using var app = await StartApp(new TestIdp(), allowAnonymousAccess: false);

        var response = await app.GetTestClient().GetAsync("/public/file");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_WithAnonymousAccess_ShouldAllowUnauthenticatedUser()
    {
        await using var app = await StartApp(new TestIdp(), allowAnonymousAccess: true);

        var response = await app.GetTestClient().GetAsync("/private");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static ProxyOptions Configure(ProxyOptions options)
    {
        options.RegisterIdentityProvider<TestIdp, TestConfig>(new TestConfig());
        return options;
    }

    private static IServiceCollection BuildServices(ProxyOptions options)
    {
        // A real host builder supplies the framework services (server, routing) the bootstraps build upon.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var services = builder.Services;
        foreach (var bootstrap in Configure(options).GetConfiguration())
        {
            bootstrap.Configure(options, services);
        }

        return services;
    }

    private static async Task<WebApplication> StartApp(TestIdp idp, bool allowAnonymousAccess)
    {
        var options = Configure(new ProxyOptions
        {
            Mode = Mode.AuthenticateOnly,
            AllowAnonymousAccess = allowAnonymousAccess,
            SkipAuthRoutes = ["/public/.*"],
            ApiRoutes = ["/api/.*"]
        });

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        foreach (var bootstrap in options.GetConfiguration())
        {
            bootstrap.Configure(options, builder.Services);
        }

        builder.Services.AddSingleton<IIdentityProvider>(idp);

        var app = builder.Build();
        foreach (var bootstrap in options.GetConfiguration())
        {
            bootstrap.Configure(options, app);
        }

        app.MapGet("/{**path}", () => Results.Ok());

        await app.StartAsync();
        return app;
    }

    private class TestConfig;

    private class TestIdp : IIdentityProvider
    {
        public static readonly Uri AuthorizeUri = new("https://idp.example.com/authorize");

        public int AuthorizeCalls { get; private set; }

        public Task<AuthorizeRequest> GetAuthorizeUrlAsync(string redirectUri)
        {
            AuthorizeCalls++;
            return Task.FromResult(new AuthorizeRequest(AuthorizeUri));
        }

        public Task<TokenResponse> GetTokenAsync(string redirectUri, string code, string? codeVerifier, string traceIdentifier)
            => throw new NotImplementedException();

        public Task<IEnumerable<KeySet>> GetJwksAsync(bool invalidateCache)
            => throw new NotImplementedException();

        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, string traceIdentifier)
            => throw new NotImplementedException();

        public Task RevokeAsync(string token, string traceIdentifier)
            => throw new NotImplementedException();

        public Task<Uri> GetEndSessionEndpointAsync(string? idToken, string baseAddress)
            => throw new NotImplementedException();
    }
}
