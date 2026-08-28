namespace OidcProxy.Net.OpenIdConnect;

public class Scopes : List<string>
{
    public Scopes(IEnumerable<string> scopes)
    {
        AddRange(scopes);

        const string openId = "openid";
        if (!Contains(openId))
        {
            Add(openId);
        }
    }
}