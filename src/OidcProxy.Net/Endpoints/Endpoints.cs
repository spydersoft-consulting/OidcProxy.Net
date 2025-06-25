using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace OidcProxy.Net.Endpoints;

internal static class Endpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app, string endpointName)
    {
        app.MapGet($"/{endpointName}/userinfo", MeEndpoint.Get);
        
        app.MapGet($"/{endpointName}/sign_in", LoginEndpoint.Get);

        app.MapGet($"/{endpointName}/callback", CallbackEndpoint.Get);

        app.MapGet($"/{endpointName}/error", () => Results.Text("Login failed."));

        app.MapGet($"/{endpointName}/sign_out", EndSessionEndpoint.Get);

        app.Map($"/{endpointName}/auth", AuthEndpoint.Request);
    }
}

