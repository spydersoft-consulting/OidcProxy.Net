using OidcProxy.Net.Logging;
using Microsoft.AspNetCore.Http;
using OidcProxy.Net.ModuleInitializers.Configuration;
using OidcProxy.Net.OpenIdConnect;

namespace OidcProxy.Net.Middleware;

internal class TokenRenewalMiddleware(TokenFactory tokenFactory, ILogger logger, ISkipAuthRoutes skipAuthRoutes) : IYarpMiddleware
{
    public async Task Apply(HttpContext context, Func<HttpContext, Task> next)
    {
        if (!skipAuthRoutes.ShouldBypass(context.Request.Method, context.Request.Path))
        {
            // Check expiry again because another thread may have updated the token
            try
            {
                await tokenFactory.RenewAccessTokenIfExpiredAsync(context.TraceIdentifier);
            }
            catch (TokenRenewalFailedException e)
            {
                await logger.ErrorAsync(e);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(@"{ ""reason"": ""token_renewal_failed"" }");
                return; // And stop the pipeline here. The request will not be forwarded down-stream.
            }
        }

        await next(context);
    }
}