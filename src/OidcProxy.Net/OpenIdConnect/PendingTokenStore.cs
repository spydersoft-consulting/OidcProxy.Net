using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OidcProxy.Net.IdentityProviders;

namespace OidcProxy.Net.OpenIdConnect;

/// <summary>
/// Implementation of IPendingTokenStore using distributed cache.
/// Tokens are stored with a short TTL to minimize security exposure.
/// </summary>
internal class PendingTokenStore : IPendingTokenStore
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(1);
    private const string KeyPrefix = "pending_token:";

    public PendingTokenStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task StoreAsync(string key, TokenResponse tokenResponse, string? userPreferredLandingPage)
    {
        var entry = new PendingTokenEntry
        {
            AccessToken = tokenResponse.access_token,
            IdToken = tokenResponse.id_token,
            RefreshToken = tokenResponse.refresh_token,
            ExpiryDate = tokenResponse.ExpiryDate,
            UserPreferredLandingPage = userPreferredLandingPage
        };

        var json = JsonSerializer.Serialize(entry);
        
        await _cache.SetStringAsync(
            KeyPrefix + key,
            json,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TokenTtl
            });
    }

    public async Task<(TokenResponse? TokenResponse, string? UserPreferredLandingPage)> RetrieveAndRemoveAsync(string key)
    {
        var fullKey = KeyPrefix + key;
        var json = await _cache.GetStringAsync(fullKey);
        
        if (string.IsNullOrEmpty(json))
        {
            return (null, null);
        }

        // Remove immediately to prevent replay
        await _cache.RemoveAsync(fullKey);

        var entry = JsonSerializer.Deserialize<PendingTokenEntry>(json);
        if (entry == null)
        {
            return (null, null);
        }

        var tokenResponse = new TokenResponse(
            entry.AccessToken,
            entry.IdToken,
            entry.RefreshToken,
            entry.ExpiryDate
        );

        return (tokenResponse, entry.UserPreferredLandingPage);
    }

    private class PendingTokenEntry
    {
        public string? AccessToken { get; set; }
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string? UserPreferredLandingPage { get; set; }
    }
}
