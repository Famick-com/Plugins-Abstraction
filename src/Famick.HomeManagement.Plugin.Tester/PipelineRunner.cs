using System.Diagnostics;
using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;

namespace Famick.HomeManagement.Plugin.Tester;

internal static class PipelineRunner
{
    public static async Task<(ProductLookupPipelineContext Context, long LookupMs, long EnrichMs)> RunAsync(
        string query,
        ProductLookupSearchType searchType,
        Barcode? parsedBarcode,
        IReadOnlyList<LoadedPlugin> plugins,
        CancellationToken ct)
    {
        var lookupPlugins = plugins
            .Where(p => p.IsProductLookup && p.Plugin.IsAvailable)
            .ToList();

        // Phase 1: Parallel Lookup
        var sw = Stopwatch.StartNew();

        var lookupTasks = lookupPlugins.Select(async p =>
        {
            try
            {
                List<ProductLookupResult> results;

                if (parsedBarcode != null && p.LookupPlugin is not null)
                {
                    results = await p.LookupPlugin.LookupAsync(parsedBarcode, maxResults: 20, ct: ct);
                }
                else if (searchType == ProductLookupSearchType.Name && p.LookupPlugin is not null)
                {
                    results = await p.LookupPlugin.LookupAsync(query, maxResults: 20, ct: ct);
                }
                else
                {
                    throw new NotSupportedException("Cannot determine how to search");
                }

                return (Plugin: p, Results: results, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Plugin: p, Results: new List<ProductLookupResult>(), Error: (Exception?)ex);
            }
        });

        var lookupResults = await Task.WhenAll(lookupTasks);
        var lookupMs = sw.ElapsedMilliseconds;

        foreach (var result in lookupResults.Where(r => r.Error != null))
        {
            ConsoleRenderer.PrintError(
                $"Lookup ({result.Plugin.Config.Id})",
                result.Error!.Message);
        }

        // Phase 2: Sequential Enrichment
        sw.Restart();
        var context = new ProductLookupPipelineContext(query, searchType, maxResults: 20);

        foreach (var p in lookupPlugins)
        {
            var thisResult = lookupResults.FirstOrDefault(r => r.Plugin == p);
            if (thisResult.Results.Count == 0) continue;

            try
            {
                await p.LookupPlugin!.EnrichPipelineAsync(context, thisResult.Results, ct);
            }
            catch (Exception ex)
            {
                ConsoleRenderer.PrintError($"Enrichment ({p.Config.Id})", ex.Message);
            }
        }

        var enrichMs = sw.ElapsedMilliseconds;
        return (context, lookupMs, enrichMs);
    }
}
