namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Exception thrown when a store API rejects a call because the caller is being
/// throttled (e.g. an HTTP 429 response).
/// <para>
/// This is deliberately distinct from <see cref="StoreAuthenticationException"/>:
/// being throttled is temporary and retryable, and the credentials are fine. Refreshing
/// the token — which is what the service layer does for an authentication failure —
/// would not help and would add another request to an already throttled account.
/// </para>
/// <para>
/// The service layer should surface this to the caller rather than absorbing it, so the
/// caller can back off for <see cref="RetryAfter"/> and try again.
/// </para>
/// </summary>
public class StoreRateLimitException : Exception
{
    /// <summary>
    /// The plugin ID that was throttled.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// How long the store asked the caller to wait before retrying, taken from the
    /// <c>Retry-After</c> response header. Null when the store did not say.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public StoreRateLimitException(string pluginId, string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        PluginId = pluginId;
        RetryAfter = retryAfter;
    }

    public StoreRateLimitException(string pluginId, string message, Exception innerException, TimeSpan? retryAfter = null)
        : base(message, innerException)
    {
        PluginId = pluginId;
        RetryAfter = retryAfter;
    }
}
