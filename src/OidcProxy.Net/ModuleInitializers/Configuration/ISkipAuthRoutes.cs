namespace OidcProxy.Net.ModuleInitializers.Configuration
{
    public interface ISkipAuthRoutes
    {
        bool ShouldBypass(string method, string path);
    }
}
