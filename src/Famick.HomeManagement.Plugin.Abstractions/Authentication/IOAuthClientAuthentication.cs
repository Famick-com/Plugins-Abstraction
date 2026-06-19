namespace Famick.HomeManagement.Plugin.Abstractions.Authentication;

/// <summary>
/// Optional capability interface for plugins that require a user-driven OAuth
/// authorization-code flow (a browser redirect to the provider and back to a
/// registered redirect URI). A plugin needs the OAuth link <b>only if</b> it
/// implements this interface; the host detects support by presence.
///
/// Features that work with app-level client credentials (no browser redirect)
/// must NOT depend on this interface — they should function whether or not the
/// user has completed the OAuth link.
/// </summary>
public interface IOAuthClientAuthentication
{
    /// <summary>
    /// Get the OAuth authorization URL to redirect the user to for authentication.
    /// </summary>
    /// <param name="redirectUri">The URI to redirect back to after authentication</param>
    /// <param name="state">State parameter for CSRF protection (should include shopping location ID)</param>
    /// <returns>The full authorization URL to redirect the user to</returns>
    string GetAuthorizationUrl(string redirectUri, string state);

    /// <summary>
    /// Exchange an authorization code for access and refresh tokens.
    /// Called after the user completes OAuth authentication.
    /// </summary>
    /// <param name="code">The authorization code from the OAuth callback</param>
    /// <param name="redirectUri">The redirect URI (must match what was used in GetAuthorizationUrl)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Token result with access token, refresh token, and expiry</returns>
    Task<OAuthTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct = default);

    /// <summary>
    /// Refresh an expired access token using the refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Token result with new access token, possibly new refresh token, and expiry</returns>
    Task<OAuthTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}
