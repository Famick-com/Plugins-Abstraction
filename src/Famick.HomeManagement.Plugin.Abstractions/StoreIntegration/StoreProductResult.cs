namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Result from a store product search or lookup
/// </summary>
public class StoreProductResult
{
    /// <summary>
    /// Store's internal product ID/SKU
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Product name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Brand name
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// Product barcode (UPC/EAN)
    /// </summary>
    public IEnumerable<Barcode> Barcodes { get; set; } = [];

    /// <summary>
    /// URL to product image
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Current price
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Price unit (e.g., "each", "lb", "oz")
    /// </summary>
    public string? PriceUnit { get; set; }

    /// <summary>
    /// Sale/promotional price (if on sale)
    /// </summary>
    public decimal? SalePrice { get; set; }

    /// <summary>
    /// Aisle location in the store
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Shelf location in the store
    /// </summary>
    public string? Shelf { get; set; }

    /// <summary>
    /// Department or category
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Whether the product is currently in stock
    /// </summary>
    public bool? InStock { get; set; }

    /// <summary>
    /// Product size/weight description
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// URL to the product page on the store's website
    /// </summary>
    public string? ProductUrl { get; set; }

    /// <summary>
    /// Categories of the product
    /// </summary>
    public List<string> Categories {get;set;} = new();

    /// <summary>
    /// Description of the product
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// How long this data should be cached, derived from HTTP Cache-Control headers.
    /// Null means no cache duration was provided by the API.
    /// </summary>
    public TimeSpan? CacheDuration { get; set; }
}
