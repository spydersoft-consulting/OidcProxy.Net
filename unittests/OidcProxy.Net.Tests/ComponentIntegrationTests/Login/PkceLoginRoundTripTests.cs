using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OidcProxy.Net.Cryptography;
using OidcProxy.Net.IdentityProviders;
using OidcProxy.Net.Jwt.SignatureValidation;
using OidcProxy.Net.ModuleInitializers;

namespace OidcProxy.Net.Tests.ComponentIntegrationTests.Login;

public class PkceLoginRoundTripTests
{
    private static readonly Uri BaseAddress = new("http://localhost");

    [Fact]
    public async Task Login_ShouldSendTheOriginalCodeVerifierOnTheTokenRequest()
    {
        var idp = new PkceIdentityProvider();
        await using var app = await StartApp(idp);
        using var browser = new Browser(app);

        var authorizeUri = await browser.SignIn();
        var response = await browser.Callback(authorizeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        idp.ReceivedCodeVerifier.Should().NotBeNullOrEmpty("the verifier must survive the round trip to the identity provider");
        PkceIdentityProvider.CreateChallenge(idp.ReceivedCodeVerifier!)
            .Should().Be(QueryHelpers.ParseQuery(authorizeUri.Query)["code_challenge"].ToString());
    }

    [Fact]
    public async Task Login_ShouldRedirectToTheUserPreferredLandingPage()
    {
        await using var app = await StartApp(new PkceIdentityProvider(), enableUserPreferredLandingPages: true);
        using var browser = new Browser(app);

        var authorizeUri = await browser.SignIn("/oauth2/sign_in?landingpage=/dashboard");
        var callback = await browser.Callback(authorizeUri);
        var complete = await browser.Get(callback.Headers.Location!);

        complete.StatusCode.Should().Be(HttpStatusCode.Redirect);
        complete.Headers.Location!.OriginalString.Should().Be("/dashboard");
    }

    [Fact]
    public async Task Login_ShouldIssueANewSessionAndRejectThePreLoginSession()
    {
        await using var app = await StartApp(new PkceIdentityProvider());
        using var browser = new Browser(app);

        var authorizeUri = await browser.SignIn();
        var preLoginSession = browser.SessionCookie;
        preLoginSession.Should().NotBeNullOrEmpty();

        var callback = await browser.Callback(authorizeUri);
        var complete = await browser.Get(callback.Headers.Location!);
        complete.StatusCode.Should().Be(HttpStatusCode.Redirect);

        browser.SessionCookie.Should().NotBeNullOrEmpty().And.NotBe(preLoginSession);

        // The authenticated session works for the browser that finished the login...
        (await browser.Get(new Uri("/oauth2/userinfo", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);

        // ...but the session id that existed before login cannot be used to read authenticated state.
        using var attacker = new Browser(app, preLoginSession);
        (await attacker.Get(new Uri("/oauth2/userinfo", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WhenSigningInTwice_ShouldCompleteTheLastSignIn()
    {
        var idp = new PkceIdentityProvider();
        await using var app = await StartApp(idp);
        using var browser = new Browser(app);

        // Two tabs share one session, so the second sign_in replaces the verifier of the first: the last sign_in wins
        // and the first tab's authorization code can no longer be redeemed.
        await browser.SignIn();
        var secondAuthorizeUri = await browser.SignIn();

        var response = await browser.Callback(secondAuthorizeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        idp.TokenRequestAccepted.Should().BeTrue();
    }

    private static async Task<WebApplication> StartApp(IIdentityProvider idp, bool enableUserPreferredLandingPages = false)
    {
        var options = new ProxyOptions
        {
            Mode = Mode.AuthenticateOnly,
            AlwaysRedirectToHttps = false,
            EnableUserPreferredLandingPages = enableUserPreferredLandingPages,
        };
        options.RegisterIdentityProvider<PkceIdentityProvider, TestConfig>(new TestConfig());

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        foreach (var bootstrap in options.GetConfiguration())
        {
            bootstrap.Configure(options, builder.Services);
        }

        // Registered last, so these replace the registrations made by the bootstraps.
        builder.Services.AddSingleton<IIdentityProvider>(idp);
        builder.Services.AddSingleton<IJwtSignatureValidator, AcceptAnySignature>();

        var app = builder.Build();
        foreach (var bootstrap in options.GetConfiguration())
        {
            bootstrap.Configure(options, app);
        }

        await app.StartAsync();
        return app;
    }

    private class TestConfig;

    private class AcceptAnySignature : IJwtSignatureValidator
    {
        public Task<bool> Validate(string? token) => Task.FromResult(true);
    }

    /// <summary>A browser with a cookie jar that follows redirects by hand, so every hop can be asserted.</summary>
    private sealed class Browser : IDisposable
    {
        private const string CookieName = "oidcproxy.cookie";
        private readonly CookieContainer _cookies = new();
        private readonly HttpClient _client;

        public Browser(WebApplication app, string? sessionCookie = null)
        {
            _client = new HttpClient(new CookieHandler(_cookies, app.GetTestServer().CreateHandler()))
            {
                BaseAddress = BaseAddress,
            };

            if (sessionCookie != null)
            {
                _cookies.Add(BaseAddress, new Cookie(CookieName, sessionCookie));
            }
        }

        public string? SessionCookie => _cookies.GetCookies(BaseAddress)[CookieName]?.Value;

        public async Task<Uri> SignIn(string path = "/oauth2/sign_in")
        {
            var response = await Get(new Uri(path, UriKind.Relative));
            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            return response.Headers.Location!;
        }

        public Task<HttpResponseMessage> Callback(Uri authorizeUri)
        {
            var state = QueryHelpers.ParseQuery(authorizeUri.Query)["state"].ToString();
            return Get(new Uri($"/oauth2/callback?code=the-code&state={state}", UriKind.Relative));
        }

        public Task<HttpResponseMessage> Get(Uri uri)
        {
            var relative = uri.IsAbsoluteUri ? new Uri(uri.PathAndQuery, UriKind.Relative) : uri;
            return _client.GetAsync(relative);
        }

        public void Dispose() => _client.Dispose();
    }

    private sealed class CookieHandler(CookieContainer cookies, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var header = cookies.GetCookieHeader(request.RequestUri!);
            if (!string.IsNullOrEmpty(header))
            {
                request.Headers.Add("Cookie", header);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    cookies.SetCookies(request.RequestUri!, setCookie);
                }
            }

            return response;
        }
    }

    /// <summary>An identity provider that, like a real one, redeems a code only for the verifier matching its challenge.</summary>
    private class PkceIdentityProvider : IIdentityProvider
    {
        private string? _challenge;

        public string? ReceivedCodeVerifier { get; private set; }

        public bool TokenRequestAccepted { get; private set; }

        public static string CreateChallenge(string verifier) =>
            WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        public Task<AuthorizeRequest> GetAuthorizeUrlAsync(string redirectUri)
        {
            var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _challenge = CreateChallenge(verifier);
            var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

            var uri = new Uri($"https://idp.example.com/authorize?state={state}&code_challenge={_challenge}" +
                              $"&code_challenge_method=S256&redirect_uri={Uri.EscapeDataString(redirectUri)}");

            return Task.FromResult(new AuthorizeRequest(uri, verifier));
        }

        public Task<TokenResponse> GetTokenAsync(string redirectUri, string code, string? codeVerifier, string traceIdentifier)
        {
            ReceivedCodeVerifier = codeVerifier;
            TokenRequestAccepted = !string.IsNullOrEmpty(codeVerifier) && CreateChallenge(codeVerifier) == _challenge;
            if (!TokenRequestAccepted)
            {
                throw new ApplicationException("Unable to retrieve token. OIDC server responded BadRequest: {\"error\":\"invalid_grant\"}");
            }

            var jwt = $"{Encode("{\"alg\":\"none\"}")}.{Encode(JsonSerializer.Serialize(new { sub = "user" }))}.";
            return Task.FromResult(new TokenResponse(jwt, jwt, "refresh", DateTime.UtcNow.AddHours(1)));
        }

        private static string Encode(string json) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));

        public Task<IEnumerable<KeySet>> GetJwksAsync(bool invalidateCache) => throw new NotImplementedException();

        public Task<TokenResponse> RefreshTokenAsync(string refreshToken, string traceIdentifier) => throw new NotImplementedException();

        public Task RevokeAsync(string token, string traceIdentifier) => throw new NotImplementedException();

        public Task<Uri> GetEndSessionEndpointAsync(string? idToken, string baseAddress) => throw new NotImplementedException();
    }
}
