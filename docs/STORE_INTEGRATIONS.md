# Store Integration Development Guide

This document describes how to create and integrate store plugins with the HomeManagement application.

## Overview

Store integrations allow users to connect their HomeManagement instance to external store APIs (like Kroger, Walmart, etc.) for:
- Searching for store locations near them
- Looking up product prices and availability
- Adding items to their store's shopping cart
- Downloading product images for local storage

Store search and product price/availability lookups work with **app-level client credentials** (the plugin's own client id/secret) and need **no** user OAuth link. A plugin only needs the user OAuth (authorization-code) link for **user-context** features such as shopping-cart write. A plugin declares it supports that link by implementing the optional `IOAuthClientAuthentication` capability interface (from the `Famick.HomeManagement.Plugin.Abstractions.Authentication` namespace); the host detects support by interface presence. There is no longer a blanket `RequiresOAuth` flag.

This lets price/availability work in environments without a stable public HTTPS callback (e.g. Home Assistant Ingress), while cart linking remains available wherever a registered redirect URI is reachable.

## Architecture

### Token Management

User OAuth tokens are stored in the `TenantIntegrationTokens` table, keyed by `(TenantId, PluginId)`. This means:
- One OAuth connection per tenant per integration (used only for user-context features like cart)
- All stores using the same integration share the same token
- Token refresh is automatic with fallback to re-authentication
- Product/price/availability reads do **not** require a token; the plugin uses client credentials when the access token is null

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

    // Store Location Methods (client credentials — no user OAuth)
    Task<List<StoreLocationResult>> SearchStoresByZipAsync(string zipCode, int radiusMiles, CancellationToken ct);
    Task<List<StoreLocationResult>> SearchStoresByCoordinatesAsync(double lat, double lon, int radiusMiles, CancellationToken ct);

    // Product Methods — pass a null accessToken to use client credentials
    Task<List<StoreProductResult>> SearchProductsAsync(string? accessToken, string? storeLocationId, string query, int maxResults, CancellationToken ct);
    Task<StoreProductResult?> GetProductAsync(string? accessToken, string storeLocationId, string productId, CancellationToken ct);
    Task<StoreProductResult?> LookupProductByBarcodeAsync(string? accessToken, string storeLocationId, Barcode barcode, CancellationToken ct);

    // Shopping Cart Methods (require a user OAuth token)
    Task<ShoppingCartResult?> GetShoppingCartAsync(string accessToken, string storeLocationId, CancellationToken ct);
    Task<ShoppingCartResult?> AddToCartAsync(string accessToken, string storeLocationId, List<CartItemRequest> items, CancellationToken ct);
    Task<ShoppingCartResult?> UpdateCartItemAsync(string accessToken, string storeLocationId, string productId, int quantity, CancellationToken ct);
    Task<ShoppingCartResult?> RemoveFromCartAsync(string accessToken, string storeLocationId, string productId, CancellationToken ct);
}
```

The OAuth (authorization-code) methods are a separate, optional capability — implement them only if your plugin needs the user link:

```csharp
// namespace Famick.HomeManagement.Plugin.Abstractions.Authentication
public interface IOAuthClientAuthentication
{
    string GetAuthorizationUrl(string redirectUri, string state);
    Task<OAuthTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct);
    Task<OAuthTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct);
}
```

A plugin that supports cart write implements **both** `IStoreIntegrationPlugin` and `IOAuthClientAuthentication`. A client-credentials-only plugin implements just `IStoreIntegrationPlugin`.

### Capabilities

Each plugin declares its feature capabilities via `StoreIntegrationCapabilities` (`HasProductLookup`, `HasStoreProductLookup`, `HasShoppingCart`, `CanReadShoppingCart`, etc.). Whether the user OAuth link is required is **not** a capability flag — it is determined by whether the plugin implements `IOAuthClientAuthentication`.

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

## OAuth Flow (optional — cart / user-context features only)

Implemented by plugins via `IOAuthClientAuthentication`. Product/price/availability features do **not** use this flow.

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
