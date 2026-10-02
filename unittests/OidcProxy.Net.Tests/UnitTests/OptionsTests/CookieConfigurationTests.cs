using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OidcProxy.Net.ModuleInitializers;
using OidcProxy.Net.ModuleInitializers.Configuration;

namespace OidcProxy.Net.Tests.UnitTests.OptionsTests;

public class CookieConfigurationTests
{
    [Fact]
    public void Apply_WithCookieMaxAgeAndSameSite_ShouldMapToOptions()
    {
        var config = new ProxyConfig
        {
            Mode = Mode.AuthenticateOnly,
            CookieMaxAge = TimeSpan.FromDays(30),
            CookieSameSite = SameSiteMode.Strict
        };
        var options = new ProxyOptions();

        config.Apply(options);

        options.CookieMaxAge.Should().Be(TimeSpan.FromDays(30));
        options.CookieSameSite.Should().Be(SameSiteMode.Strict);
    }

    [Fact]
    public void Apply_WithoutCookieMaxAgeAndSameSite_ShouldLeaveOptionsUntouched()
    {
        var config = new ProxyConfig { Mode = Mode.AuthenticateOnly };
        var options = new ProxyOptions
        {
            CookieMaxAge = TimeSpan.FromDays(1),
            CookieSameSite = SameSiteMode.Lax
        };

        config.Apply(options);

        options.CookieMaxAge.Should().Be(TimeSpan.FromDays(1));
        options.CookieSameSite.Should().Be(SameSiteMode.Lax);
    }

    [Fact]
    public void NewProxyOptions_ShouldNotHaveCookieMaxAge()
    {
        new ProxyOptions().CookieMaxAge.Should().BeNull();
    }

    [Fact]
    public void SessionBootstrap_WithCookieMaxAge_ShouldSetMaxAgeOnSessionCookie()
    {
        var sessionOptions = BuildSessionOptions(new ProxyOptions { CookieMaxAge = TimeSpan.FromDays(30) });

        sessionOptions.Cookie.MaxAge.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void SessionBootstrap_WithoutCookieMaxAge_ShouldProduceSessionCookie()
    {
        var sessionOptions = BuildSessionOptions(new ProxyOptions());

        sessionOptions.Cookie.MaxAge.Should().BeNull();
        sessionOptions.Cookie.Expiration.Should().BeNull();
    }

    [Fact]
    public void SessionBootstrap_WithCookieSameSite_ShouldSetSameSiteOnSessionCookie()
    {
        var sessionOptions = BuildSessionOptions(new ProxyOptions { CookieSameSite = SameSiteMode.None });

        sessionOptions.Cookie.SameSite.Should().Be(SameSiteMode.None);
    }

    [Fact]
    public async Task SessionCookie_WithCookieMaxAge_ShouldBeIssuedWithMaxAgeOnlyOnce()
    {
        using var host = await StartHost(new ProxyOptions { CookieMaxAge = TimeSpan.FromDays(30) });
        var client = host.GetTestClient();

        var first = await client.GetAsync("/");
        var setCookie = first.Headers.GetValues("Set-Cookie").Single();
        setCookie.Should().Contain($"max-age={(int)TimeSpan.FromDays(30).TotalSeconds}");

        // Subsequent requests in the same session do not re-issue the cookie: MaxAge is fixed, not sliding.
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", setCookie.Split(';')[0]);
        var second = await client.SendAsync(request);

        second.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task SessionCookie_WithoutCookieMaxAge_ShouldBeIssuedWithoutMaxAge()
    {
        using var host = await StartHost(new ProxyOptions());

        var response = await host.GetTestClient().GetAsync("/");

        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        setCookie.ToLowerInvariant().Should().NotContain("max-age").And.NotContain("expires");
    }

    private static SessionOptions BuildSessionOptions(ProxyOptions options)
    {
        var services = new ServiceCollection();
        new SessionBootstrap().Configure(options, services);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<SessionOptions>>().Value;
    }

    private static async Task<IHost> StartHost(ProxyOptions options)
    {
        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddLogging();
                    new SessionBootstrap().Configure(options, services);
                })
                .Configure(app =>
                {
                    app.UseSession();
                    app.Run(context =>
                    {
                        context.Session.SetString("key", "value");
                        return Task.CompletedTask;
                    });
                }))
            .StartAsync();
    }
}
