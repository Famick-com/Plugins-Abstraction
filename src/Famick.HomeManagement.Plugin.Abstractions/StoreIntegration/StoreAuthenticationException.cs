namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Exception thrown when a store API call fails due to authentication issues (e.g., expired token, 401 response).
/// This allows the service layer to catch this specific exception and attempt token refresh with retry.
/// </summary>
public class StoreAuthenticationException : Exception
{
    /// <summary>
    /// The plugin ID that encountered the authentication failure
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// The HTTP status code returned by the API, if applicable
    /// </summary>
    public int? HttpStatusCode { get; }

    public StoreAuthenticationException(string pluginId, string message, int? httpStatusCode = null)
        : base(message)
    {
        PluginId = pluginId;
        HttpStatusCode = httpStatusCode;
    }

    public StoreAuthenticationException(string pluginId, string message, Exception innerException, int? httpStatusCode = null)
        : base(message, innerException)
    {
        PluginId = pluginId;
        HttpStatusCode = httpStatusCode;
    }
}
