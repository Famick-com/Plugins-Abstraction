namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Result from a product lookup search
/// </summary>
public class ProductLookupResultV2 : ProductLookupResult
{
    /// <summary>
    /// Barcode (UPC/EAN) if available - this is the barcode returned by the plugin
    /// </summary>
    public new IEnumerable<Barcode> Barcode { get; set; } = [];

    /// <summary>
    /// The originally scanned/searched barcode (if different from plugin-returned barcode).
    /// </summary>
    public new Barcode? OriginalSearchBarcode { get; set; }
}
