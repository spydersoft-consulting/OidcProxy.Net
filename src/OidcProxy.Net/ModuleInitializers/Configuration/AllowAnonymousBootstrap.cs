using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OidcProxy.Net.Middleware;

namespace OidcProxy.Net.ModuleInitializers.Configuration;

internal class AllowAnonymousBootstrap : IBootstrap
{
    public void Configure(ProxyOptions options, IServiceCollection services)
    {
        services.AddSingleton<ISkipAuthRoutes>(c => new SkipAuthRoutes(options.SkipAuthRoutes))
            .AddSingleton<IApiRoutes>(c => new SkipAuthRoutes(options.ApiRoutes))
            .AddTransient<AnonymousAccessMiddleware>();
    }

    public void Configure(ProxyOptions options, WebApplication app)
    {        
        if (!options.AllowAnonymousAccess)
        {
            app.UseMiddleware<AnonymousAccessMiddleware>();
        }
    }
}