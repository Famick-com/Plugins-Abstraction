namespace Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

/// <summary>
/// Request to add/update items in a shopping cart
/// </summary>
public class CartItemRequest
{
    /// <summary>
    /// Store's internal product ID/SKU
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Quantity to add/set
    /// </summary>
    public int Quantity { get; set; }
}
