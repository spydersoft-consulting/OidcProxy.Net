using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Threading.Tasks.Dataflow;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using OidcProxy.Net.Cryptography;
using OidcProxy.Net.Jwt;
using OidcProxy.Net.Jwt.SignatureValidation;
using OidcProxy.Net.Middleware;

namespace OidcProxy.Net.ModuleInitializers.Configuration;

internal class AuthorizationBootstrap : IBootstrap
{
    private Action<IServiceCollection> _applyJwtParser = s => s
        .AddTransient<ITokenParser, JwtParser>()
        .AddSingleton<IEncryptionKey>(_ => null!);
    
    private Action<IServiceCollection> _applyJwtValidator = s => s
        .AddTransient<IJwtSignatureValidator, JwtSignatureValidator>();
    
    private Action<IServiceCollection> _applyHs256SignatureValidator = s => s
        .AddTransient<Hs256SignatureValidator>(_ => null!);
    
    public AuthorizationBootstrap WithTokenParser<TTokenParser>() where TTokenParser : class, ITokenParser
    {
        _applyJwtParser = s => s
            .AddTransient<ITokenParser, TTokenParser>();

        return this;
    }

    public AuthorizationBootstrap WithEncryptionKey(IEncryptionKey key)
    {
        _applyJwtParser = s => s
            .AddTransient<ITokenParser, JweParser>()
            .AddSingleton(key);

        return this;
    }
    
    public AuthorizationBootstrap WithSigningKey(SymmetricKey key)
    {
        _applyHs256SignatureValidator = s => s
            .AddTransient<Hs256SignatureValidator>(_ => new Hs256SignatureValidator(key));

        return this;
    }
    
    public AuthorizationBootstrap WithSignatureValidator<T>() where T : class, IJwtSignatureValidator
    {
        _applyJwtValidator = s => s.AddTransient<IJwtSignatureValidator, T>();

        return this;
    }

    public void Configure(ProxyOptions options, IServiceCollection services)
    {
        services
            .AddAuthorization()
            .AddAuthentication("Cookie_OR_Bearer")
            .AddScheme<OidcProxyAuthenticationSchemeOptions, OidcProxyAuthenticationHandler>(
                OidcProxyAuthenticationHandler.SchemaName, null)
            .AddScheme<OidcProxyBearerAuthenticationSchemeOptions,
                OidcProxyBearerAuthenticationHandler>(OidcProxyBearerAuthenticationHandler.SchemaName, null)
            .AddPolicyScheme("Cookie_OR_Bearer", "Cookie_OR_Bearer", 
        opt =>
            {
                opt.ForwardDefaultSelector = context =>
                {
                    string authorization = context.Request.Headers[HeaderNames.Authorization];
                    if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
                    {
                        return OidcProxyBearerAuthenticationHandler.SchemaName;
                    }

                    return OidcProxyAuthenticationHandler.SchemaName;
                };
            });

        services.AddRequestTimeouts();
        _applyJwtParser(services); 
        _applyJwtValidator(services);
        _applyHs256SignatureValidator(services);
        services.AddScoped<ISkipJwtBearerTokens>(c =>
            new SkipJwtBearerTokens(c.GetRequiredService<IJwtSignatureValidator>(),
                c.GetRequiredService<ITokenParser>(), options.SkipJwtBearerTokens, null));
    }

    public void Configure(ProxyOptions options, WebApplication app)
    {
        app.UseRouting();
        app.UseRequestTimeouts();
        app.UseAuthentication();
        app.UseAuthorization();
    }
}