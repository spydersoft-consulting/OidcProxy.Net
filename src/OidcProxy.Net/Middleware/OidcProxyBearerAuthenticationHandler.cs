using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OidcProxy.Net.Jwt;
using OidcProxy.Net.Jwt.SignatureValidation;
using OidcProxy.Net.ModuleInitializers;

namespace OidcProxy.Net.Middleware;

public sealed class OidcProxyBearerAuthenticationHandler(
    ITokenParser tokenParser,
    ProxyOptions proxyOptions,
    IHttpContextAccessor httpContextAccessor,
    IJwtSignatureValidator jwtSignatureValidator,
    IOptionsMonitor<OidcProxyBearerAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<OidcProxyBearerAuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemaName = "Bearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (httpContextAccessor.HttpContext == null)
            {
                return AuthenticateResult.NoResult();
            }

            if (!proxyOptions.SkipJwtBearerTokens)
            {
                return AuthenticateResult.NoResult(); 
            }

            var header = httpContextAccessor.HttpContext.Request.Headers.Authorization.FirstOrDefault();
            if (header == null || !AuthenticationHeaderValue.TryParse(header, out var headerValue) || headerValue.Scheme != SchemaName || string.IsNullOrEmpty(headerValue.Parameter)) {
                return AuthenticateResult.NoResult();
            }

            var token = headerValue.Parameter;


            var isSignatureValid = await jwtSignatureValidator.Validate(token);
            if (!isSignatureValid)
            {
                throw new AuthenticationFailureException("Failed to authenticate. " +
                                                         "The access_token jwt signature is invalid.");
            }

            var payload = tokenParser.ParseJwtPayload(token);

            if (payload == null)
            {
                throw new AuthenticationFailureException("Failed to authenticate. " +
                                                         "The access_token jwt does not have a payload.");
            }
            
            var claims = payload
                .Select(x => new Claim(x.Key, x.Value?.ToString() ?? string.Empty))
                .ToArray();
            
            if (!claims.Any())
            {
                throw new AuthenticationFailureException("Failed to authenticate. " +
                                                         "The access_token jwt does not contain any claims.");
            }

            var claimsIdentity = new ClaimsIdentity(claims, SchemaName, proxyOptions.NameClaim, proxyOptions.RoleClaim);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            var ticket = new AuthenticationTicket(claimsPrincipal, SchemaName);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception e)
        {
            return AuthenticateResult.Fail(e);
        }
    }
}