using OidcProxy.Net.ModuleInitializers;

// ReSharper disable once CheckNamespace
namespace OidcProxy.Net;

public class ProxyConfig : IAppSettingsSection
{
    public Mode Mode { get; set; }
    public string? EndpointName { get; set; }
    public string? ErrorPage { get; set; }
    public string? LandingPage { get; set; }
    public string? NameClaim { get; set; }
    public string? RoleClaim { get; set; }
    public IEnumerable<string> AllowedLandingPages { get; set; } = Array.Empty<string>();
    public bool EnableUserPreferredLandingPages { get; set; } = false;
    public bool? AlwaysRedirectToHttps { get; set; }
    public bool? AllowAnonymousAccess { get; set; }
    public Uri? CustomHostName { get; set; }
    public string? CookieName { get; set; }
    public TimeSpan? SessionIdleTimeout { get; set; }

    public bool? CookieSecure { get; set; }

    public string? CookieDomain { get; set; }
    
    public YarpConfig? ReverseProxy { get; set; }

    public IList<string> SkipAuthRoutes { get; set; } = new List<string>();

    public IList<string> ApiRoutes { get; set; } = new List<string>();

    public virtual bool Validate(out IEnumerable<string> errors)
    {
        errors = Array.Empty<string>();
        return true;
    }

    public virtual void Apply(ProxyOptions options)
    {
        AssignIfNotNull(ErrorPage, options.SetAuthenticationErrorPage);
        AssignIfNotNull(LandingPage, options.SetLandingPage);
        AssignIfNotNull(CustomHostName, options.SetCustomHostName);
        AssignIfNotNull(CookieName, cookieName => options.CookieName = cookieName);
        AssignIfNotNull(NameClaim, nameClaim => options.NameClaim = nameClaim);
        AssignIfNotNull(RoleClaim, roleClaim => options.RoleClaim = roleClaim);

        options.Mode = Mode;
        options.EnableUserPreferredLandingPages = EnableUserPreferredLandingPages;
        options.AlwaysRedirectToHttps = !AlwaysRedirectToHttps.HasValue || AlwaysRedirectToHttps.Value;
        options.AllowAnonymousAccess = !AllowAnonymousAccess.HasValue || AllowAnonymousAccess.Value;
        options.EndpointName = EndpointName ?? "oauth2";
        options.SetAllowedLandingPages(AllowedLandingPages);
        options.SkipAuthRoutes = SkipAuthRoutes;
        options.CookieSecure = CookieSecure;
        options.CookieDomain = CookieDomain;
        options.ApiRoutes = ApiRoutes;
        options.SkipJwtBearerTokens = SkipJwtBearerTokens;

        if (SessionIdleTimeout.HasValue)
        {
            options.SessionIdleTimeout = SessionIdleTimeout.Value;
        }
        
        if (options.Mode != Mode.AuthenticateOnly)
        {
            var routes = ReverseProxy?.Routes.ToRouteConfig();
            var clusters = ReverseProxy?.Clusters.ToClusterConfig();

            options.ConfigureYarp(routes, clusters);
        }
    }

    public bool SkipJwtBearerTokens { get; set; } = false;

    private static void AssignIfNotNull<T>(T? value, Action<T> @do)
    {
        if (value != null)
        {
            @do(value);
        }
    }
}