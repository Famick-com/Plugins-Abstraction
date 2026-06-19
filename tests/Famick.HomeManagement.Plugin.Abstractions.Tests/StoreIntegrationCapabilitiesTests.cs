using Famick.HomeManagement.Plugin.Abstractions.Authentication;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

namespace Famick.HomeManagement.Plugin.Abstractions.Tests;

public class StoreIntegrationCapabilitiesTests
{
    [Fact]
    public void None_HasAllFeaturesDisabled()
    {
        var caps = StoreIntegrationCapabilities.None;

        Assert.False(caps.HasProductLookup);
        Assert.False(caps.HasStoreProductLookup);
        Assert.False(caps.HasShoppingCart);
        Assert.False(caps.CanReadShoppingCart);
        Assert.False(caps.CanRemoveFromShoppingCart);
        Assert.False(caps.CanUpdateShoppingCart);
        Assert.False(caps.CanDownloadProductImages);
    }

    [Fact]
    public void ProductLookupOnly_EnablesProductFeatures_NoCart()
    {
        var caps = StoreIntegrationCapabilities.ProductLookupOnly;

        Assert.True(caps.HasProductLookup);
        Assert.True(caps.HasStoreProductLookup);
        Assert.True(caps.CanDownloadProductImages);
        Assert.False(caps.HasShoppingCart);
    }

    [Fact]
    public void Full_EnablesCart()
    {
        var caps = StoreIntegrationCapabilities.Full;

        Assert.True(caps.HasProductLookup);
        Assert.True(caps.HasStoreProductLookup);
        Assert.True(caps.HasShoppingCart);
        Assert.True(caps.CanReadShoppingCart);
    }

    [Fact]
    public void OAuthCapableStorePlugin_IsDetectableByInterfacePresence()
    {
        IStoreIntegrationPlugin cartPlugin = new FakeCartStorePlugin();
        IStoreIntegrationPlugin lookupPlugin = new FakeLookupOnlyStorePlugin();

        Assert.True(cartPlugin is IOAuthClientAuthentication);
        Assert.False(lookupPlugin is IOAuthClientAuthentication);
    }

    // Minimal fakes that exercise only the capability-detection surface.
    private sealed class FakeLookupOnlyStorePlugin : FakeStorePluginBase { }

    private sealed class FakeCartStorePlugin : FakeStorePluginBase, IOAuthClientAuthentication
    {
        public string GetAuthorizationUrl(string redirectUri, string state) => string.Empty;
        public Task<OAuthTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct = default)
            => Task.FromResult(OAuthTokenResult.Fail("not implemented"));
        public Task<OAuthTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
            => Task.FromResult(OAuthTokenResult.Fail("not implemented"));
    }

    private abstract class FakeStorePluginBase : IStoreIntegrationPlugin
    {
        public string PluginId => "fake";
        public string DisplayName => "Fake";
        public string Version => "1.0.0";
        public bool IsAvailable => true;
        public PluginAttribution? Attribution => null;
        public string? HelpUrl => null;
        public StoreIntegrationCapabilities Capabilities => StoreIntegrationCapabilities.None;

        public Task InitAsync(System.Text.Json.JsonElement? pluginConfig, CancellationToken ct = default) => Task.CompletedTask;

        public Task<List<StoreLocationResult>> SearchStoresByZipAsync(string zipCode, int radiusMiles = 10, CancellationToken ct = default)
            => Task.FromResult(new List<StoreLocationResult>());
        public Task<List<StoreLocationResult>> SearchStoresByCoordinatesAsync(double latitude, double longitude, int radiusMiles = 10, CancellationToken ct = default)
            => Task.FromResult(new List<StoreLocationResult>());
        public Task<List<StoreProductResult>> SearchProductsAsync(string? accessToken, string? storeLocationId, string query, int maxResults = 20, CancellationToken ct = default)
            => Task.FromResult(new List<StoreProductResult>());
        public Task<StoreProductResult?> GetProductAsync(string? accessToken, string storeLocationId, string productId, CancellationToken ct = default)
            => Task.FromResult<StoreProductResult?>(null);
        public Task<StoreProductResult?> LookupProductByBarcodeAsync(string? accessToken, string storeLocationId, Barcode barcode, CancellationToken ct = default)
            => Task.FromResult<StoreProductResult?>(null);
        public Task<ShoppingCartResult?> GetShoppingCartAsync(string accessToken, string storeLocationId, CancellationToken ct = default)
            => Task.FromResult<ShoppingCartResult?>(null);
        public Task<ShoppingCartResult?> AddToCartAsync(string accessToken, string storeLocationId, List<CartItemRequest> items, CancellationToken ct = default)
            => Task.FromResult<ShoppingCartResult?>(null);
        public Task<ShoppingCartResult?> UpdateCartItemAsync(string accessToken, string storeLocationId, string productId, int quantity, CancellationToken ct = default)
            => Task.FromResult<ShoppingCartResult?>(null);
        public Task<ShoppingCartResult?> RemoveFromCartAsync(string accessToken, string storeLocationId, string productId, CancellationToken ct = default)
            => Task.FromResult<ShoppingCartResult?>(null);
    }
}
