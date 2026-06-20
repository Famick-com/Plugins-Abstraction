namespace Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

/// <summary>
/// Optional store-location context for a product lookup. Carries the external
/// location id together with its <see cref="Source"/> — the
/// <see cref="IPlugin.SourceId"/> of the plugin family that owns the location —
/// so each lookup plugin can decide whether the location is relevant to it.
///
/// A lookup plugin uses the location only when <see cref="Source"/> matches its
/// own <see cref="IPlugin.SourceId"/>; otherwise it ignores it and returns
/// general (non-store-specific) data.
/// </summary>
public sealed class ProductLookupLocation
{
    /// <summary>
    /// The <see cref="IPlugin.SourceId"/> of the plugin family that owns this location.
    /// </summary>
    public required Guid Source { get; init; }

    /// <summary>
    /// The external location id within that source (e.g. a Kroger locationId).
    /// </summary>
    public required string ExternalLocationId { get; init; }
}
