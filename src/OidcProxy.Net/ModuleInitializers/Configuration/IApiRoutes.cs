namespace OidcProxy.Net.ModuleInitializers.Configuration
{
    public interface IApiRoutes
    {
        bool Matches(string method, string path);
    }
}
