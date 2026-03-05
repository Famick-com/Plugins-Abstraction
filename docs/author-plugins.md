# Plugin Authoring Guide

This guide explains how to create product lookup plugins for the Famick Home Management self-hosted application. Plugins let you add new external data sources — nutrition databases, regional food databases, specialty product APIs — that integrate into the product lookup pipeline.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [IPlugin Interface](#iplugin-interface)
- [IProductLookupPlugin Interface](#iproductlookupplugin-interface)
- [Attribution](#attribution)
- [Pipeline Context](#pipeline-context)
- [Result Models](#result-models)
- [Configuration](#configuration)
- [Registration](#registration)
- [Store Integration Plugins](#store-integration-plugins)
- [Testing](#testing)
- [Docker Deployment](#docker-deployment)

---

## Overview

The plugin system supports two types of plugins:

| Type | Interface | Purpose |
|------|-----------|----------|
| **Product Lookup** | `IProductLookupPlugin` | Search external databases for product information (nutrition, images, barcodes) |
| **Store Integration** | `IStoreIntegrationPlugin` | Connect to grocery store APIs for pricing, availability, and shopping carts |

This guide focuses on **product lookup plugins**, which are the most common type for community contributors. For store integration plugins, see [Store Integration Plugins](#store-integration-plugins).

### Pipeline Architecture

Product lookup uses a **two-phase pipeline** for optimal performance:

1. **Parallel Lookup** — All enabled plugins call their external APIs concurrently via `LookupAsync`. Each plugin receives the search query and returns a list of `ProductLookupResult`. Plugins must NOT access the pipeline context during this phase.

2. **Sequential Enrichment** — After all lookups complete, each plugin's `EnrichPipelineAsync` is called in `config.json` order. This phase merges lookup results into the shared pipeline context using the "first plugin wins" pattern (`??=`).

**Example flow** (barcode scan for a US food product):

```
Phase 1 (parallel):
  USDA FoodData Central  →  Returns result with nutrition data (no image)
  Open Food Facts         →  Returns result with product image + nutrition
  Your Custom Plugin      →  Returns result with regional data

Phase 2 (sequential, in config.json order):
  1. USDA enrichment      →  Adds USDA result to pipeline context
  2. OFF enrichment       →  Finds matching barcode, enriches USDA result with image
  3. Your plugin          →  Finds matching barcode, enriches with regional data
```

---

## Quick Start

Here's a minimal product lookup plugin that queries a fictional "My Nutrition API":

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces.Plugins;

namespace MyNutritionPlugin;

public class MyNutritionPlugin : IProductLookupPlugin
{
    private readonly HttpClient _httpClient = new();
    private string _apiKey = string.Empty;
    private bool _isInitialized;

    public string PluginId => "mynutrition";
    public string DisplayName => "My Nutrition API";
    public string Version => "1.0.0";
    public bool IsAvailable => _isInitialized && !string.IsNullOrEmpty(_apiKey);

    public PluginAttribution? Attribution => new()
    {
        Url = "https://mynutritionapi.example.com",
        LicenseText = "CC BY 4.0",
        Description = "My Nutrition API provides regional nutrition data. "
            + "Data is licensed under Creative Commons Attribution 4.0.",
        ProductUrlTemplate = "https://mynutritionapi.example.com/product/{barcode}"
    };

    public Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default)
    {
        if (pluginConfig.HasValue)
        {
            var config = pluginConfig.Value;
            if (config.TryGetProperty("apiKey", out var apiKey))
                _apiKey = apiKey.GetString() ?? string.Empty;
        }

        _isInitialized = true;
        return Task.CompletedTask;
    }

    public async Task<List<ProductLookupResult>> LookupAsync(
        string query,
        ProductLookupSearchType searchType,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        if (!IsAvailable) return new List<ProductLookupResult>();

        if (searchType != ProductLookupSearchType.Barcode)
            return new List<ProductLookupResult>();

        var url = $"https://mynutritionapi.example.com/v1/barcode/{query}?key={_apiKey}";
        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return new List<ProductLookupResult>();

        var data = await response.Content.ReadFromJsonAsync<MyApiResponse>(ct);
        if (data == null) return new List<ProductLookupResult>();

        return new List<ProductLookupResult>
        {
            new()
            {
                DataSources = { { DisplayName, data.ProductId } },
                Name = data.ProductName,
                Barcode = query,
                Nutrition = new ProductLookupNutrition
                {
                    Source = PluginId,
                    Calories = data.Calories,
                    Protein = data.Protein,
                    TotalFat = data.Fat
                },
                AttributionMarkdown = BuildAttributionMarkdown(query)
            }
        };
    }

    public Task EnrichPipelineAsync(
        ProductLookupPipelineContext context,
        List<ProductLookupResult> lookupResults,
        CancellationToken ct = default)
    {
        foreach (var result in lookupResults)
        {
            var existing = context.FindMatchingResult(barcode: result.Barcode);

            if (existing != null)
            {
                // Enrich existing result (first plugin wins via ??=)
                existing.Nutrition ??= result.Nutrition;
                existing.DataSources.TryAdd(DisplayName, result.Barcode ?? "");

                // Merge attribution
                if (!string.IsNullOrEmpty(result.AttributionMarkdown))
                {
                    existing.AttributionMarkdown = existing.AttributionMarkdown != null
                        ? existing.AttributionMarkdown + "\n\n" + result.AttributionMarkdown
                        : result.AttributionMarkdown;
                }
            }
            else
            {
                context.AddResult(result);
            }
        }
        return Task.CompletedTask;
    }

    private string BuildAttributionMarkdown(string barcode)
    {
        var productUrl = Attribution!.ProductUrlTemplate!.Replace("{barcode}", barcode);
        return $"Data from [{DisplayName}]({Attribution.Url}) ({Attribution.LicenseText}). [View product]({productUrl})";
    }

    // Your API response model (private to this plugin)
    private record MyApiResponse(
        string ProductId,
        string ProductName,
        decimal? Calories,
        decimal? Protein,
        decimal? Fat);
}
```

To deploy this plugin, compile it as a class library DLL, place it in the `plugins/` folder, and add an entry to `plugins/config.json`. See [Configuration](#configuration) and [Docker Deployment](#docker-deployment) for details.

---

## IPlugin Interface

All plugins implement `IPlugin`, the base interface:

```csharp
public interface IPlugin
{
    string PluginId { get; }
    string DisplayName { get; }
    string Version { get; }
    bool IsAvailable { get; }
    PluginAttribution? Attribution { get; }
    Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default);
}
```

| Property | Description |
|----------|-------------|
| `PluginId` | Unique identifier used as the key in `plugins/config.json` (e.g., `"usda"`, `"openfoodfacts"`) |
| `DisplayName` | Human-readable name shown in the UI and stored in `DataSources` (e.g., `"USDA FoodData Central"`) |
| `Version` | Semantic version string for the plugin |
| `IsAvailable` | Whether the plugin is ready to process requests. Return `false` if required configuration (like an API key) is missing. |
| `Attribution` | Licensing and attribution metadata. Return `null` only if the plugin uses no external data that requires attribution. See [Attribution](#attribution). |
| `InitAsync` | Called once at startup with the plugin's `config` section from `plugins/config.json` as a `JsonElement`. Parse your configuration here. |

### InitAsync

The `pluginConfig` parameter contains the `"config"` object from the plugin's entry in `config.json`, or `null` if no config section exists.

Here is how the built-in USDA plugin reads its configuration:

```csharp
public Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default)
{
    if (pluginConfig.HasValue)
    {
        var config = pluginConfig.Value;

        if (config.TryGetProperty("apiKey", out var apiKey))
            _apiKey = apiKey.GetString() ?? string.Empty;

        if (config.TryGetProperty("baseUrl", out var baseUrl))
            _baseUrl = baseUrl.GetString() ?? _baseUrl;

        if (config.TryGetProperty("defaultMaxResults", out var maxResults))
            _defaultMaxResults = maxResults.GetInt32();
    }

    _isInitialized = true;
    return Task.CompletedTask;
}
```

Each plugin defines its own configuration schema — there is no fixed format beyond what you choose to support.

---

## IProductLookupPlugin Interface

Product lookup plugins extend `IPlugin` with two methods:

```csharp
public interface IProductLookupPlugin : IPlugin
{
    /// Fetch product data from the external source.
    /// Runs in parallel across all plugins — do NOT access the pipeline context here.
    Task<List<ProductLookupResult>> LookupAsync(
        string query,
        ProductLookupSearchType searchType,
        int maxResults = 20,
        CancellationToken ct = default);

    /// Merge this plugin's lookup results into the shared pipeline context.
    /// Called sequentially in config.json order after all lookups complete.
    Task EnrichPipelineAsync(
        ProductLookupPipelineContext context,
        List<ProductLookupResult> lookupResults,
        CancellationToken ct = default);
}
```

### LookupAsync

Called during Phase 1 (parallel). Receives the search query and returns results from your external API. This method runs concurrently with all other plugins, so it must NOT access the pipeline context.

**Important**: Return an empty list on errors — don't throw exceptions. The pipeline orchestrator wraps each call in a try/catch, but handling errors yourself gives you better logging.

### EnrichPipelineAsync

Called during Phase 2 (sequential). Receives the pipeline context and the results your `LookupAsync` returned. Merge your results into the shared context.

Use `??=` (null-coalescing assignment) to avoid overwriting data from earlier plugins. The convention is **first plugin to provide a value wins**.

---

## Attribution

If your plugin fetches data from any external source, you should provide attribution via two mechanisms:

1. **`Attribution` property** — metadata displayed on the settings/about page
2. **`AttributionMarkdown` on results** — per-product attribution text stored with the product

### PluginAttribution

```csharp
public class PluginAttribution
{
    public required string Url { get; set; }
    public required string LicenseText { get; set; }
    public string? Description { get; set; }
    public string? ProductUrlTemplate { get; set; }
}
```

---

## Pipeline Context

`ProductLookupPipelineContext` is the shared state used during the enrichment phase of the pipeline. It is only accessed in `EnrichPipelineAsync`, never in `LookupAsync`.

### Key Methods

- `FindMatchingResult(barcode, externalId, dataSource)` — Find an existing result to enrich
- `AddResult(result)` / `AddResults(results)` — Add new results to the pipeline
- `FindResultsByBarcode(barcode)` — Find all results matching a barcode
- `NormalizeBarcode(barcode)` — Static: Normalize a barcode for comparison
- `HasValidCheckDigit(barcode, isEan)` — Static: Validate a check digit
- `CalculateCheckDigit(digits)` — Static: Calculate a check digit
- `GenerateBarcodeVariants(barcode)` — Static: Generate all format variants

---

## Configuration

### plugins/config.json

Plugins are configured in the `plugins/config.json` file. The order of entries determines the pipeline execution order.

```json
{
  "plugins": [
    {
      "id": "usda",
      "enabled": true,
      "builtin": true,
      "displayName": "USDA FoodData Central",
      "config": {
        "apiKey": "YOUR_USDA_API_KEY"
      }
    },
    {
      "id": "mynutrition",
      "enabled": true,
      "builtin": false,
      "assembly": "MyNutritionPlugin.dll",
      "displayName": "My Nutrition API",
      "config": {
        "apiKey": "your-api-key-here"
      }
    }
  ]
}
```

---

## Registration

### Built-in Plugins

Built-in plugins are registered in `InfrastructureStartup.cs` as singleton services.

### External Plugins (DLLs)

External plugins are loaded from DLL files at runtime. They must have a **parameterless constructor** since they are instantiated via `Activator.CreateInstance`.

### Creating an External Plugin Project

```bash
dotnet new classlib -n MyNutritionPlugin -f net10.0
cd MyNutritionPlugin
```

Reference the abstractions NuGet package:

```xml
<ItemGroup>
  <PackageReference Include="Famick.HomeManagement.Plugin.Abstractions" Version="1.0.0" />
</ItemGroup>
```

Build and copy the DLL to the `plugins/` folder:

```bash
dotnet build -c Release
cp bin/Release/net10.0/MyNutritionPlugin.dll /path/to/app/plugins/
```

---

## Store Integration Plugins

Store integration plugins implement `IStoreIntegrationPlugin` and provide OAuth-based connections to grocery store APIs (Kroger, Walmart, etc.) for pricing, availability, store location lookup, and shopping cart management.

For a complete implementation example, see the [Plugin-Kroger](https://github.com/Famick-com/Plugin-Kroger) repository.

For the full store integration guide, see [STORE_INTEGRATIONS.md](./STORE_INTEGRATIONS.md).

---

## Testing

Test your plugin in isolation using the two-method pattern. Use `MockHttpMessageHandler` to mock HTTP calls.

---

## Docker Deployment

For self-hosted Docker deployments, mount your plugin DLL and config using a Docker volume:

```yaml
services:
  famick:
    image: famick/homemanagement:latest
    volumes:
      - ./my-plugins:/app/plugins
```

---

## Summary

| Step | Action |
|------|--------|
| 1 | Create a .NET class library targeting `net10.0` |
| 2 | Reference `Famick.HomeManagement.Plugin.Abstractions` NuGet package |
| 3 | Implement `IProductLookupPlugin` with `LookupAsync` and `EnrichPipelineAsync` |
| 4 | Provide `PluginAttribution` if using external data |
| 5 | Set `AttributionMarkdown` on each `ProductLookupResult` in `LookupAsync` |
| 6 | Merge attribution in `EnrichPipelineAsync` when enriching existing results |
| 7 | Handle both `Barcode` and `Name` search types in `LookupAsync` |
| 8 | Use `FindMatchingResult` in `EnrichPipelineAsync` to enrich existing results instead of duplicating |
| 9 | Build the DLL and place it in the `plugins/` folder |
| 10 | Add an entry to `plugins/config.json` with `"builtin": false` and `"assembly"` pointing to your DLL |
| 11 | Restart the application |
