namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Result from a store location search
/// </summary>
public class StoreLocationResult
{
    /// <summary>
    /// External store location ID from the integration provider
    /// </summary>
    public string ExternalLocationId { get; set; } = string.Empty;

    /// <summary>
    /// Store name (e.g., "Kroger #12345")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Chain/brand identifier (e.g., "kroger", "ralphs", "fred-meyer")
    /// </summary>
    public string? ChainId { get; set; }

    /// <summary>
    /// Chain/brand display name (e.g., "Kroger", "Ralphs", "Fred Meyer")
    /// </summary>
    public string? ChainName { get; set; }

    /// <summary>
    /// Full street address
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State/province code
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// ZIP/postal code
    /// </summary>
    public string? ZipCode { get; set; }

    /// <summary>
    /// Store phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Store latitude
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Store longitude
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Distance from search location in miles
    /// </summary>
    public double? DistanceMiles { get; set; }

    /// <summary>
    /// Formats the full address as a single line
    /// </summary>
    public string? FullAddress => !string.IsNullOrEmpty(Address)
        ? $"{Address}, {City}, {State} {ZipCode}"
        : null;
}
