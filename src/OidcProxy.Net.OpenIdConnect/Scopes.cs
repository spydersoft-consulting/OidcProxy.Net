namespace OidcProxy.Net.OpenIdConnect;

public class Scopes : List<string>
{
    public Scopes(IEnumerable<string> scopes, bool requestOfflineAccessScope = true)
    {
        AddRange(scopes);

        const string openId = "openid";
        if (!Contains(openId))
        {
            Add(openId);
        }

        const string offlineAccessScope = "offline_access";
        if (requestOfflineAccessScope && !Contains(offlineAccessScope))
        {
            Add(offlineAccessScope);
        }
    }
}