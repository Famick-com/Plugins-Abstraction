namespace Famick.HomeManagement.Core.Interfaces.Plugins;

public interface IProductLookupPluginV2 : IProductLookupPlugin
{
    /// <summary>
    /// Fetch product data using the barcode provided
    /// </summary>
    /// <param name="barcode">The barcode to search</param>
    /// <param name="maxResults">The maximum number of items to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of found items</returns>
    Task<List<ProductLookupResultV2>> LookupAsync(
        Barcode barcode,
        int maxResults = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch product data from the external source.
    /// Runs in parallel across all plugins — must NOT access the pipeline context.
    /// </summary>
    /// <param name="query">The search query (barcode or product name)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of lookup results from this plugin's external source</returns>
    Task<List<ProductLookupResultV2>> LookupAsync(
        string searchTerm,
        int maxResults = 20,
        CancellationToken ct = default);
}