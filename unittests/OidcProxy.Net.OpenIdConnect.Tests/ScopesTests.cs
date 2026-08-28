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
    public void Constructor_AddsOfflineAccessByDefault()
    {
        var scopes = new Scopes(["openid", "profile"]);

        scopes.Should().Contain("offline_access");
    }

    [Fact]
    public void Constructor_DoesNotAddOfflineAccessWhenDisabled()
    {
        var scopes = new Scopes(["openid", "profile"], requestOfflineAccessScope: false);

        scopes.Should().NotContain("offline_access");
    }

    [Fact]
    public void Constructor_KeepsOfflineAccessWhenExplicitlyRequestedEvenIfDisabled()
    {
        var scopes = new Scopes(["openid", "profile", "offline_access"], requestOfflineAccessScope: false);

        scopes.Should().Contain("offline_access");
        scopes.Should().HaveCount(3);
    }

    [Fact]
    public void Constructor_DoesNotDuplicateOfflineAccessWhenAlreadyPresent()
    {
        var scopes = new Scopes(["openid", "profile", "offline_access"]);

        scopes.Should().ContainSingle(x => x == "offline_access");
    }

    [Fact]
    public void Constructor_DoesNotDuplicateOpenId()
    {
        var scopes = new Scopes(["openid", "profile"]);

        scopes.Should().ContainSingle(x => x == "openid");
    }
}
