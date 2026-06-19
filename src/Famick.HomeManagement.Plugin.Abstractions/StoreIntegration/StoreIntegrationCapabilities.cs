namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Describes the capabilities supported by a store integration plugin.
/// </summary>
public class StoreIntegrationCapabilities
{
    /// <summary>
    /// Can search products by name/barcode
    /// </summary>
    public bool HasProductLookup { get; set; }

    /// <summary>
    /// Can search products at specific store locations
    /// </summary>
    public bool HasStoreProductLookup { get; set; }

    /// <summary>
    /// Can add items to user's shopping cart (single cart per user)
    /// </summary>
    public bool HasShoppingCart { get; set; }

    /// <summary>
    /// Can read user's current shopping cart
    /// </summary>
    public bool CanReadShoppingCart { get; set; }

    /// <summary>
    /// Can remove from a user's shopping cart
    /// </summary>
    public bool CanRemoveFromShoppingCart { get; set; }

    /// <summary>
    /// Can update an item in the user's shopping cart
    /// </summary>
    public bool CanUpdateShoppingCart { get; set; }

    /// <summary>
    /// Can download product images for local storage
    /// </summary>
    public bool CanDownloadProductImages { get; set; }

    /// <summary>
    /// Creates a default capabilities instance with all features disabled
    /// </summary>
    public static StoreIntegrationCapabilities None => new();

    /// <summary>
    /// Creates a capabilities instance for plugins that only support product lookup
    /// (client credentials; no user OAuth link)
    /// </summary>
    public static StoreIntegrationCapabilities ProductLookupOnly => new()
    {
        HasProductLookup = true,
        HasStoreProductLookup = true,
        CanDownloadProductImages = true
    };

    /// <summary>
    /// Creates a capabilities instance for plugins with full feature support, including
    /// shopping cart (which requires the user OAuth link via
    /// <see cref="Authentication.IOAuthClientAuthentication"/>)
    /// </summary>
    public static StoreIntegrationCapabilities Full => new()
    {
        HasProductLookup = true,
        HasStoreProductLookup = true,
        HasShoppingCart = true,
        CanReadShoppingCart = true,
        CanDownloadProductImages = true
    };
}
