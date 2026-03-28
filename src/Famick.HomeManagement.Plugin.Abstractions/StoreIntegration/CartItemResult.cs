namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Represents an item in the shopping cart
/// </summary>
public class CartItemResult
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
    /// Quantity in cart
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Price per item
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// URL to product image
    /// </summary>
    public string? ImageUrl { get; set; }
}
