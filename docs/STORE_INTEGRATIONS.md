# Store Integration Development Guide

This document describes how to create and integrate store plugins with the HomeManagement application.

## Overview

Store integrations allow users to connect their HomeManagement instance to external store APIs (like Kroger, Walmart, etc.) for:
- Searching for store locations near them
- Looking up product prices and availability
- Adding items to their store's shopping cart
- Downloading product images for local storage

Some store integrations require OAuth authentication (like Kroger), while others work with public APIs that don't require user authentication. Plugins declare whether OAuth is required via the `RequiresOAuth` capability flag.

## Architecture

### Token Management

OAuth tokens are stored in the `TenantIntegrationTokens` table, keyed by `(TenantId, PluginId)`. This means:
- One OAuth connection per tenant per integration
- All stores using the same integration share the same token
- Token refresh is automatic with fallback to re-authentication

### Plugin Interface

All store integration plugins implement `IStoreIntegrationPlugin` (from the `Famick.HomeManagement.Plugin.Abstractions` NuGet package):

```csharp
public interface IStoreIntegrationPlugin
{
    // Identity
    string PluginId { get; }
    string DisplayName { get; }
    string Version { get; }
    bool IsAvailable { get; }

    // Capabilities
    StoreIntegrationCapabilities Capabilities { get; }

    // Initialization
    Task InitAsync(JsonElement? pluginConfig, CancellationToken ct);

    // OAuth Methods
    string GetAuthorizationUrl(string redirectUri, string state);
    Task<OAuthTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct);
    Task<OAuthTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct);

    // Store Location Methods
    Task<List<StoreLocationResult>> SearchStoresByZipAsync(string zipCode, int radiusMiles, CancellationToken ct);
    Task<List<StoreLocationResult>> SearchStoresByCoordinatesAsync(double lat, double lon, int radiusMiles, CancellationToken ct);

    // Product Methods
    Task<List<StoreProductResult>> SearchProductsAsync(string accessToken, string storeLocationId, string query, int maxResults, CancellationToken ct);
    Task<StoreProductResult?> GetProductAsync(string accessToken, string storeLocationId, string productId, CancellationToken ct);
    Task<StoreProductResult?> LookupProductByBarcodeAsync(string? accessToken, string storeLocationId, string barcode, CancellationToken ct);

    // Shopping Cart Methods
    Task<ShoppingCartResult?> GetShoppingCartAsync(string accessToken, string storeLocationId, CancellationToken ct);
    Task<ShoppingCartResult?> AddToCartAsync(string accessToken, string storeLocationId, List<CartItemRequest> items, CancellationToken ct);
    Task<ShoppingCartResult?> UpdateCartItemAsync(string accessToken, string storeLocationId, string productId, int quantity, CancellationToken ct);
    Task<ShoppingCartResult?> RemoveFromCartAsync(string accessToken, string storeLocationId, string productId, CancellationToken ct);
}
```

### Capabilities

Each plugin declares its capabilities via `StoreIntegrationCapabilities`.

## Creating a New Plugin

### Step 1: Create Plugin Project

```bash
dotnet new classlib -n MyStore.Plugin -f net10.0
```

Add the abstractions NuGet package:

```xml
<PackageReference Include="Famick.HomeManagement.Plugin.Abstractions" Version="1.0.0" />
```

### Step 2: Implement IStoreIntegrationPlugin

See the [Plugin-Kroger](https://github.com/Famick-com/Plugin-Kroger) repository for a complete implementation example.

### Step 3: Configure in plugins/config.json

```json
{
  "storeIntegrations": [
    {
      "id": "mystore",
      "enabled": true,
      "builtin": false,
      "assembly": "MyStore.Plugin.dll",
      "displayName": "My Store",
      "config": {
        "clientId": "your-client-id",
        "clientSecret": "your-client-secret"
      }
    }
  ]
}
```

## OAuth Flow

### Authorization URL

1. Call `GetAuthorizationUrl(redirectUri, state)`
2. Redirect user to the returned URL
3. Store API redirects back with authorization code

### Token Exchange

1. Parse the authorization code from callback
2. Call `ExchangeCodeForTokenAsync(code, redirectUri)`
3. Tokens are automatically stored in `TenantIntegrationTokens`

### Token Refresh

Token refresh is handled automatically by `StoreIntegrationService`.

## Error Handling

- Return `OAuthTokenResult.Fail(errorMessage)` for OAuth failures
- Throw `InvalidOperationException` for configuration issues
- Throw `StoreAuthenticationException` for auth failures (enables automatic token refresh + retry)
- Return `null` for "not found" scenarios in product/cart operations
- Log all errors with appropriate context

## Security Considerations

- Client secrets should be stored securely (not in source control)
- OAuth state parameters should be validated
- Tokens are stored per-tenant, never shared across tenants
- Use HTTPS for all API calls
