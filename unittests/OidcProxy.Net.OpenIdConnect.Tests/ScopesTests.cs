using FluentAssertions;

namespace OidcProxy.Net.OpenIdConnect.Tests;

public class ScopesTests
{
    [Fact]
    public void Constructor_AlwaysIncludesOpenId()
    {
        var scopes = new Scopes(["profile"]);

        scopes.Should().Contain("openid");
    }

    [Fact]
    public void Constructor_DoesNotAddOfflineAccessWhenNotRequested()
    {
        var scopes = new Scopes(["openid", "profile"]);

        scopes.Should().NotContain("offline_access");
    }

    [Fact]
    public void Constructor_KeepsOfflineAccessWhenExplicitlyRequested()
    {
        var scopes = new Scopes(["openid", "profile", "offline_access"]);

        scopes.Should().Contain("offline_access");
        scopes.Should().HaveCount(3);
    }

    [Fact]
    public void Constructor_DoesNotDuplicateOpenId()
    {
        var scopes = new Scopes(["openid", "profile"]);

        scopes.Should().ContainSingle(x => x == "openid");
    }
}
