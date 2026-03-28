namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Result from an OAuth token exchange or refresh operation
/// </summary>
public class OAuthTokenResult
{
    /// <summary>
    /// Whether the token operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The access token for API calls
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The refresh token for obtaining new access tokens
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful token result
    /// </summary>
    public static OAuthTokenResult Ok(string accessToken, string? refreshToken, DateTime expiresAt) => new()
    {
        Success = true,
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresAt = expiresAt
    };

    /// <summary>
    /// Creates a failed token result
    /// </summary>
    public static OAuthTokenResult Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
