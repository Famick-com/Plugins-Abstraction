namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Result from getting a user's shopping cart (single cart per user)
/// </summary>
public class ShoppingCartResult
{
    /// <summary>
    /// Items currently in the cart
    /// </summary>
    public List<CartItemResult> Items { get; set; } = new();

    /// <summary>
    /// Subtotal of all items in the cart
    /// </summary>
    public decimal? Subtotal { get; set; }

    /// <summary>
    /// The store location ID the cart is associated with
    /// </summary>
    public string? StoreLocationId { get; set; }
}
