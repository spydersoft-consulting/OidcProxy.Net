using OidcProxy.Net.IdentityProviders;

namespace OidcProxy.Net.OpenIdConnect;

/// <summary>
/// Temporary store for token responses during session regeneration.
/// Tokens are stored briefly between callback and session completion.
/// </summary>
internal interface IPendingTokenStore
{
    /// <summary>
    /// Stores a token response with a unique key.
    /// </summary>
    /// <param name="key">A unique identifier for the pending token.</param>
    /// <param name="tokenResponse">The token response to store.</param>
    /// <param name="userPreferredLandingPage">The user's preferred landing page.</param>
    /// <returns>A task representing the async operation.</returns>
    Task StoreAsync(string key, TokenResponse tokenResponse, string? userPreferredLandingPage);

    /// <summary>
    /// Retrieves and removes a token response by key.
    /// </summary>
    /// <param name="key">The unique identifier for the pending token.</param>
    /// <returns>The token response and landing page, or null if not found or expired.</returns>
    Task<(TokenResponse? TokenResponse, string? UserPreferredLandingPage)> RetrieveAndRemoveAsync(string key);
}
