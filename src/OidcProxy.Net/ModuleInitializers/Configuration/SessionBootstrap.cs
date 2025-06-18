using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OidcProxy.Net.Locking;
using OidcProxy.Net.Locking.Distributed.Redis;
using OidcProxy.Net.Locking.InMemory;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace OidcProxy.Net.ModuleInitializers.Configuration;

internal class SessionBootstrap : IBootstrap
{
    private ConnectionMultiplexer? _connectionMultiplexer;

    public SessionBootstrap WithRedis(ConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
        return this;
    }

    public void Configure(ProxyOptions options, IServiceCollection services)
    {
        services
            .AddDistributedMemoryCache()
            .AddMemoryCache()
            .AddSession(o =>
            {
                o.IdleTimeout = options.SessionIdleTimeout;
                o.Cookie.HttpOnly = true;
                o.Cookie.IsEssential = true;
                o.Cookie.Name = options.CookieName;
                o.Cookie.Domain = options.CookieDomain;
                if (options.CookieSecure.HasValue && options.CookieSecure.Value)
                {
                    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                }

                if (options.CookieSameSite.HasValue)
                {
                    o.Cookie.SameSite = options.CookieSameSite.Value;
                }

            });

        if (_connectionMultiplexer == null)
        {
            services.AddTransient<IConcurrentContext, InMemoryConcurrentContext>();
        }
        else
        {
            services
                .AddDataProtection()
                .PersistKeysToStackExchangeRedis(_connectionMultiplexer, options.CookieName);

            services.AddStackExchangeRedisCache(redisCacheOptions =>
            {
                redisCacheOptions.Configuration = _connectionMultiplexer.Configuration;
                redisCacheOptions.InstanceName = options.CookieName;
            });

            services
                .AddTransient<IConcurrentContext, RedisConcurrentContext>()
                .AddSingleton<IDistributedLockFactory>(_ => RedLockFactory.Create(new List<RedLockMultiplexer>() { _connectionMultiplexer }));
        }
    }

    public void Configure(ProxyOptions options, WebApplication app)
    {
        app.UseSession();

        app.Use(async (context, next) =>
        {
            await context.Session.LoadAsync();
            await next();
        });
    }
}