using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OidcProxy.Net.IdentityProviders;
using OidcProxy.Net.Jwt;
using OidcProxy.Net.Jwt.SignatureValidation;
using OidcProxy.Net.Logging;
using OidcProxy.Net.ModuleInitializers;
using OidcProxy.Net.OpenIdConnect;
using System.Security.Cryptography;

namespace OidcProxy.Net.Endpoints;

internal static class CallbackEndpoint
{
    public static async Task<IResult> Get(HttpContext context,
        [FromServices] AuthSession authSession,
        [FromServices] ILogger logger,
        [FromServices] IRedirectUriFactory redirectUriFactory,
        [FromServices] ProxyOptions proxyOptions,
        [FromServices] IIdentityProvider identityProvider,
        [FromServices] ITokenParser tokenParser,
        [FromServices] IJwtSignatureValidator jwtSignatureValidator,
        [FromServices] IAuthenticationCallbackHandler authenticationCallbackHandler,
        [FromServices] IPendingTokenStore pendingTokenStore)
    {
        try
        {
            var userPreferredLandingPage = authSession.GetUserPreferredLandingPage();

            var code = context.Request.Query["code"].SingleOrDefault();
            if (string.IsNullOrEmpty(code))
            {
                await logger.InformAsync("Unable to obtain access token. Querystring parameter 'code' has no value.");

                var redirectUri = $"{proxyOptions.ErrorPage}{context.Request.QueryString}";
                return await authenticationCallbackHandler.OnAuthenticationFailed(context, redirectUri, userPreferredLandingPage);
            }

            var endpointName = context.Request.Path.RemoveQueryString().TrimEnd("/callback");
            var redirectUrl = redirectUriFactory.DetermineRedirectUri(context, endpointName);

            var codeVerifier = authSession.GetCodeVerifier();

            await logger.InformAsync("Exchanging code for access_token.");
            var tokenResponse = await identityProvider.GetTokenAsync(redirectUrl, code, codeVerifier, context.TraceIdentifier);

            if (!(await jwtSignatureValidator.Validate(tokenResponse.access_token)))
            {
                return await authenticationCallbackHandler.OnAuthenticationFailed(context,
                    proxyOptions.LandingPage.ToString(),
                    userPreferredLandingPage);
            }

            // Phase 1: Session regeneration to prevent session fixation attacks
            // Store tokens temporarily and redirect to complete with a fresh session
            var pendingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            await pendingTokenStore.StoreAsync(pendingKey, tokenResponse, userPreferredLandingPage);

            // Clear old session and delete the session cookie
            context.Session.Clear();
            await context.Session.CommitAsync();

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Domain = proxyOptions.CookieDomain,
                Secure = proxyOptions.CookieSecure ?? false,
                SameSite = proxyOptions.CookieSameSite ?? SameSiteMode.Unspecified,
                Path = "/"
            };
            context.Response.Cookies.Delete(proxyOptions.CookieName, cookieOptions);

            // Redirect to session complete endpoint - browser will get a new session
            var baseAddress = redirectUriFactory.DetermineHostName(context);
            var sessionCompleteUrl = $"{baseAddress}/{proxyOptions.EndpointName}/session-complete?key={Uri.EscapeDataString(pendingKey)}";

            await logger.InformAsync($"Session regeneration phase 1 complete. Redirect to session-complete endpoint.");

            return Results.Redirect(sessionCompleteUrl);
        }
        catch (Exception e)
        {
            await logger.ErrorAsync(e);
            await authenticationCallbackHandler.OnError(context, e);
            throw;
        }
        finally
        {
            await authSession.RemoveCodeVerifierAsync();
        }
    }
}