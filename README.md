# Plugins-Abstraction

[![NuGet](https://img.shields.io/nuget/v/Famick.HomeManagement.Plugin.Abstractions)](https://www.nuget.org/packages/Famick.HomeManagement.Plugin.Abstractions)

Plugin interface contracts for [Famick Home Management](https://github.com/Famick-com/FamickHomeManagement). Defines `IPlugin`, `IProductLookupPlugin`, and `IStoreIntegrationPlugin` interfaces for building product lookup and store integration plugins.

## Installation

```bash
dotnet add package Famick.HomeManagement.Plugin.Abstractions
```

Or in your `.csproj`:

```xml
<PackageReference Include="Famick.HomeManagement.Plugin.Abstractions" Version="1.0.0" />
```

## Interfaces

| Interface | Purpose |
|---|---|
| `IPlugin` | Base interface — identity, availability, initialization |
| `IProductLookupPlugin` | Product search from external databases (nutrition, images, barcodes) |
| `IStoreIntegrationPlugin` | Grocery store API integration (OAuth, pricing, cart) |

## Documentation

- [Plugin Authoring Guide](docs/author-plugins.md) — How to build a product lookup plugin
- [Store Integration Guide](docs/STORE_INTEGRATIONS.md) — How to build a store integration plugin
- [Plugin-Kroger](https://github.com/Famick-com/Plugin-Kroger) — Reference implementation of a store integration plugin

## License

Apache-2.0
