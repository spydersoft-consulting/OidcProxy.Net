// <copyright file="AuthEndpoint.cs" company="ZoralLabs">
//   Copyright Zoral Limited 2024 all rights reserved.
//   Copyright Zoral Inc. 2024 all rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OidcProxy.Net.Jwt.SignatureValidation;
using OidcProxy.Net.Logging;
using OidcProxy.Net.ModuleInitializers;

namespace OidcProxy.Net.Endpoints;

/// <summary>
/// Endpoint for NGINX auth_request module.
/// </summary>
internal static class AuthEndpoint
{
    public static async Task<IResult> Request(HttpContext context,
        [FromServices] AuthSession authSession,
        [FromServices] ILogger logger,
        [FromServices] ProxyOptions proxyOptions,
        [FromServices] IJwtSignatureValidator jwtSignatureValidator)
    {
        if (proxyOptions.SkipJwtBearerTokens && context.Request.Headers.ContainsKey("Authorization"))
        {
            await logger.InformAsync("Skip JWT Bearer token validation, because SkipJwtBearerTokens is set to true.");
            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", string.Empty);
            if (!string.IsNullOrEmpty(token))
            {
                if (await jwtSignatureValidator.Validate(token))
                {
                    return Results.Ok();
                }

                await logger.InformAsync("Invalid JWT token.");
                return Results.Unauthorized();
            }
        }

        if (authSession.HasAccessToken())
        {
            return Results.Ok();
        }

        return Results.Forbid();
    }
}