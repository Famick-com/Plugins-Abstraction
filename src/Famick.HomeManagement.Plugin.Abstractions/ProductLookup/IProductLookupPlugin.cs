namespace Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

/// <summary>
/// Interface for product lookup plugins that search external databases
/// (USDA FoodData Central, Open Food Facts, etc.)
/// Plugins execute in two phases:
///   1. LookupAsync — fetch data from external API (runs in parallel across all plugins)
///   2. EnrichPipelineAsync — merge results into pipeline context (runs sequentially in config.json order)
/// </summary>
public interface IProductLookupPlugin : IPlugin
{
    /// <summary>
    /// Fetch product data using the barcode provided
    /// </summary>
    /// <param name="barcode">The barcode to search</param>
    /// <param name="maxResults">The maximum number of items to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of found items</returns>
    Task<List<ProductLookupResult>> LookupAsync(
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
    Task<List<ProductLookupResult>> LookupAsync(
        string searchTerm,
        int maxResults = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Merge this plugin's lookup results into the shared pipeline context.
    /// Called sequentially in config.json order after all lookups complete.
    /// </summary>
    /// <param name="context">Pipeline context with accumulated results from previous plugins</param>
    /// <param name="lookupResults">The results returned by this plugin's LookupAsync</param>
    /// <param name="ct">Cancellation token</param>
    Task EnrichPipelineAsync(
        ProductLookupPipelineContext context,
        List<ProductLookupResult> lookupResults,
        CancellationToken ct = default);
}
