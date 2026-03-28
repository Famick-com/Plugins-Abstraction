using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

/// <summary>
/// Type of product lookup search
/// </summary>
public enum ProductLookupSearchType
{
    Barcode,
    Name
}

/// <summary>
/// Context passed between plugins in the lookup pipeline.
/// Contains accumulated results and search parameters.
/// </summary>
public class ProductLookupPipelineContext
{
    /// <summary>
    /// The original search query (barcode or name)
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// Type of search being performed
    /// </summary>
    public ProductLookupSearchType SearchType { get; }

    /// <summary>
    /// Maximum results requested
    /// </summary>
    public int MaxResults { get; }

    /// <summary>
    /// Accumulated results from previous plugins in the pipeline.
    /// Plugins can add new results or enrich existing ones.
    /// </summary>
    public List<ProductLookupResult> Results { get; }

    public ProductLookupPipelineContext(
        string query,
        ProductLookupSearchType searchType,
        int maxResults = 20)
    {
        Query = query;
        SearchType = searchType;
        MaxResults = maxResults;
        Results = new List<ProductLookupResult>();
    }

    /// <summary>
    /// Find an existing result that matches the given criteria.
    /// Matches by barcode (normalized) or by externalId+dataSource combination.
    /// </summary>
    public ProductLookupResult? FindMatchingResult(
        Barcode? barcode = null,
        string? externalId = null,
        string? dataSource = null)
    {
        // Priority 1: Match by barcode (normalized for different formats)
        if (barcode != null)
        {
            var byBarcode = Results.FirstOrDefault(r =>
                r.Barcodes.Any(bc => bc.Equals(barcode)));
            if (byBarcode != null) return byBarcode;
        }

        // Priority 2: Match by externalId + dataSource (for same-source enrichment)
        if (!string.IsNullOrEmpty(externalId) && !string.IsNullOrEmpty(dataSource))
        {
            return Results.FirstOrDefault(r =>
                r.DataSources.TryGetValue(dataSource, out var id) &&
                id.Equals(externalId, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }


    /// <summary>
    /// Find all results that match the given barcode (using normalized comparison).
    /// </summary>
    public IEnumerable<ProductLookupResult> FindResultsByBarcode(Barcode barcode)
    {
        if (barcode is null) yield break;

        foreach (var result in Results)
        {
            if (result.Barcodes.Any(bc => bc.Equals(barcode)))
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Add a new result to the pipeline.
    /// </summary>
    public void AddResult(ProductLookupResult result)
    {
        Results.Add(result);
    }

    /// <summary>
    /// Add multiple results to the pipeline.
    /// </summary>
    public void AddResults(IEnumerable<ProductLookupResult> results)
    {
        Results.AddRange(results);
    }
}
