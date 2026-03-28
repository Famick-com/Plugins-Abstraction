using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using Famick.HomeManagement.Plugin.Tester;

// Parse --config argument
var configPath = ParseConfigArg(args);
if (configPath == null)
{
    Console.WriteLine("Usage: dotnet run -- --config <path/to/plugins.json>");
    return 1;
}

// Load plugins
Console.WriteLine();
Console.WriteLine("Loading plugins...");
List<LoadedPlugin> plugins;
try
{
    plugins = await PluginLoader.LoadAsync(configPath, CancellationToken.None);
}
catch (Exception ex)
{
    ConsoleRenderer.PrintError("Startup", ex.Message);
    return 1;
}

if (plugins.Count == 0)
{
    ConsoleRenderer.PrintError("Startup", "No plugins were loaded. Check your config file.");
    return 1;
}

Console.WriteLine();

var lookupCount = plugins.Count(p => p.IsProductLookup);
var storeCount = plugins.Count(p => p.IsStoreIntegration);
ConsoleRenderer.PrintBanner(lookupCount, storeCount);
ConsoleRenderer.PrintHelp();

// Token storage for store integration — load cached tokens and auto-refresh
var tokenCache = new TokenCache();
Console.WriteLine("Restoring cached tokens...");
var tokens = await tokenCache.LoadAndRefreshAsync(plugins, CancellationToken.None);
if (tokens.Count > 0)
    tokenCache.Save(tokens); // Persist refreshed tokens immediately

// Interactive REPL
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("> ");
    Console.ResetColor();

    var input = Console.ReadLine();
    if (input == null) break;

    var trimmed = input.Trim();
    if (string.IsNullOrEmpty(trimmed)) continue;

    var lower = trimmed.ToLowerInvariant();
    if (lower is "quit" or "exit") break;
    if (lower == "help") { ConsoleRenderer.PrintHelp(); continue; }
    if (lower == "plugins") { ConsoleRenderer.PrintPlugins(plugins); continue; }

    // Reset cancellation for each command
    if (cts.IsCancellationRequested)
    {
        cts.TryReset();
    }

    try
    {
        if (lower.StartsWith("store "))
        {
            await HandleStoreCommand(trimmed[6..].Trim(), plugins, tokens, tokenCache, cts.Token);
        }
        else
        {
            await HandleLookup(trimmed, plugins, cts.Token);
        }
    }
    catch (OperationCanceledException)
    {
        ConsoleRenderer.PrintWarning("Cancelled.");
    }
    catch (Exception ex)
    {
        ConsoleRenderer.PrintError("Command", ex.Message);
    }
}

return 0;

// ── Helpers ──────────────────────────────────────────────

static string? ParseConfigArg(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--config")
            return args[i + 1];
    }
    return null;
}

static async Task HandleLookup(string input, List<LoadedPlugin> plugins, CancellationToken ct)
{
    var isBarcode = BarcodeParser.TryParse(input, out var barcode);
    var searchType = isBarcode ? ProductLookupSearchType.Barcode : ProductLookupSearchType.Name;
    var query = input;

    ConsoleRenderer.PrintSearchType(query, isBarcode, barcode);

    var (context, lookupMs, enrichMs) = await PipelineRunner.RunAsync(
        query, searchType, barcode, plugins, ct);

    ConsoleRenderer.PrintLookupResults(context, lookupMs, enrichMs);
}

static async Task HandleStoreCommand(
    string commandLine,
    List<LoadedPlugin> plugins,
    Dictionary<string, OAuthTokenResult> tokens,
    TokenCache tokenCache,
    CancellationToken ct)
{
    var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) { ConsoleRenderer.PrintHelp(); return; }

    var subCommand = parts[0].ToLowerInvariant();

    switch (subCommand)
    {
        case "plugins":
            ConsoleRenderer.PrintStorePlugins(plugins);
            break;

        case "auth":
            if (!RequireArgs(parts, 2, "store auth <pluginId>")) return;
            var authPlugin = FindStorePlugin(plugins, parts[1]);
            if (authPlugin == null) return;
            var authUrl = authPlugin.GetAuthorizationUrl(OAuthCallbackListener.RedirectUri, Guid.NewGuid().ToString());
            Console.WriteLine();
            ConsoleRenderer.PrintSuccess("  Open this URL in your browser to authenticate:");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"  {authUrl}");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Redirect URI: {OAuthCallbackListener.RedirectUri}");
            Console.WriteLine("  (Ensure this redirect URI is registered with the OAuth provider)");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  Waiting for callback...");
            try
            {
                var code = await OAuthCallbackListener.WaitForCallbackAsync(ct);
                ConsoleRenderer.PrintSuccess($"  Authorization code received.");
                Console.WriteLine("  Exchanging code for token...");
                var authTokenResult = await authPlugin.ExchangeCodeForTokenAsync(code, OAuthCallbackListener.RedirectUri, ct);
                if (authTokenResult.Success) { tokens[parts[1]] = authTokenResult; tokenCache.Save(tokens); }
                ConsoleRenderer.PrintOAuthToken(authTokenResult);
            }
            catch (InvalidOperationException ex)
            {
                ConsoleRenderer.PrintError("OAuth", ex.Message);
            }
            break;

        case "token":
            if (!RequireArgs(parts, 3, "store token <pluginId> <code>")) return;
            var tokenPlugin = FindStorePlugin(plugins, parts[1]);
            if (tokenPlugin == null) return;
            var tokenResult = await tokenPlugin.ExchangeCodeForTokenAsync(parts[2], OAuthCallbackListener.RedirectUri, ct);
            if (tokenResult.Success) { tokens[parts[1]] = tokenResult; tokenCache.Save(tokens); }
            ConsoleRenderer.PrintOAuthToken(tokenResult);
            break;

        case "refresh":
            if (!RequireArgs(parts, 3, "store refresh <pluginId> <refreshToken>")) return;
            var refreshPlugin = FindStorePlugin(plugins, parts[1]);
            if (refreshPlugin == null) return;
            var refreshResult = await refreshPlugin.RefreshTokenAsync(parts[2], ct);
            if (refreshResult.Success) { tokens[parts[1]] = refreshResult; tokenCache.Save(tokens); }
            ConsoleRenderer.PrintOAuthToken(refreshResult);
            break;

        case "locations":
            if (!RequireArgs(parts, 3, "store locations <pluginId> <zip> or <lat> <lon>")) return;
            var locPlugin = FindStorePlugin(plugins, parts[1]);
            if (locPlugin == null) return;

            List<StoreLocationResult> locations;
            if (parts.Length >= 4 && double.TryParse(parts[2], out var lat) && double.TryParse(parts[3], out var lon))
                locations = await locPlugin.SearchStoresByCoordinatesAsync(lat, lon, ct: ct);
            else
                locations = await locPlugin.SearchStoresByZipAsync(parts[2], ct: ct);

            ConsoleRenderer.PrintStoreLocations(locations);
            break;

        case "search":
            if (!RequireArgs(parts, 4, "store search <pluginId> <locationId> <query>")) return;
            var searchPlugin = FindStorePlugin(plugins, parts[1]);
            if (searchPlugin == null) return;
            var accessToken = GetToken(tokens, parts[1]);
            if (accessToken == null) return;
            var searchQuery = string.Join(' ', parts[3..]);
            var products = await searchPlugin.SearchProductsAsync(accessToken, parts[2], searchQuery, ct: ct);
            ConsoleRenderer.PrintStoreProducts(products);
            break;

        case "product":
            if (!RequireArgs(parts, 4, "store product <pluginId> <locationId> <productId>")) return;
            var prodPlugin = FindStorePlugin(plugins, parts[1]);
            if (prodPlugin == null) return;
            var prodToken = GetToken(tokens, parts[1]);
            if (prodToken == null) return;
            var product = await prodPlugin.GetProductAsync(prodToken, parts[2], parts[3], ct);
            if (product != null) ConsoleRenderer.PrintStoreProduct(product);
            else ConsoleRenderer.PrintWarning("Product not found.");
            break;

        case "barcode":
            if (!RequireArgs(parts, 4, "store barcode <pluginId> <locationId> <barcode>")) return;
            var bcPlugin = FindStorePlugin(plugins, parts[1]);
            if (bcPlugin == null) return;
            var bcToken = GetToken(tokens, parts[1]);
            var barcode = BarcodeParser.Parse(parts[3]);
            var bcProduct = await bcPlugin.LookupProductByBarcodeAsync(bcToken, parts[2], barcode, ct);
            if (bcProduct != null) ConsoleRenderer.PrintStoreProduct(bcProduct);
            else ConsoleRenderer.PrintWarning("Product not found.");
            break;

        case "cart":
            await HandleCartCommand(parts[1..], plugins, tokens, ct);
            break;

        default:
            ConsoleRenderer.PrintWarning($"Unknown store command: {subCommand}");
            break;
    }
}

static async Task HandleCartCommand(
    string[] parts,
    List<LoadedPlugin> plugins,
    Dictionary<string, OAuthTokenResult> tokens,
    CancellationToken ct)
{
    if (parts.Length == 0) { ConsoleRenderer.PrintWarning("Usage: store cart <pluginId> <locationId>"); return; }

    var subCommand = parts[0].ToLowerInvariant();

    switch (subCommand)
    {
        case "add":
            if (!RequireArgs(parts, 4, "store cart add <pluginId> <locationId> <productId> [qty]")) return;
            var addPlugin = FindStorePlugin(plugins, parts[1]);
            if (addPlugin == null) return;

            if (!addPlugin.Capabilities.HasShoppingCart)
            {
                ConsoleRenderer.PrintWarning($"{addPlugin.DisplayName} does not support shopping carts.");
                return;
            }

            var addToken = GetToken(tokens, parts[1]);
            if (addToken == null) return;
            var qty = parts.Length >= 5 && int.TryParse(parts[4], out var q) ? q : 1;
            var addItems = new List<CartItemRequest> { new() { ExternalProductId = parts[3], Quantity = qty } };
            var addResult = await addPlugin.AddToCartAsync(addToken, parts[2], addItems, ct);
            if (addPlugin.Capabilities.CanReadShoppingCart)
            {
                ConsoleRenderer.PrintShoppingCart(addResult);
            }
            break;

        case "update":
            if (!RequireArgs(parts, 5, "store cart update <pluginId> <locationId> <productId> <qty>")) return;
            var updatePlugin = FindStorePlugin(plugins, parts[1]);
            if (updatePlugin == null) return;

            if (!updatePlugin.Capabilities.HasShoppingCart)
            {
                ConsoleRenderer.PrintWarning($"{updatePlugin.DisplayName} does not support updating a shopping cart.");
            }

            var updateToken = GetToken(tokens, parts[1]);
            if (updateToken == null) return;
            if (!int.TryParse(parts[4], out var updateQty)) { ConsoleRenderer.PrintError("cart update", "Invalid quantity."); return; }
            var updateResult = await updatePlugin.UpdateCartItemAsync(updateToken, parts[2], parts[3], updateQty, ct);
            if (updatePlugin.Capabilities.CanReadShoppingCart)
            {
                ConsoleRenderer.PrintShoppingCart(updateResult);
            }
            break;

        case "remove":
            if (!RequireArgs(parts, 4, "store cart remove <pluginId> <locationId> <productId>")) return;
            var removePlugin = FindStorePlugin(plugins, parts[1]);
            if (removePlugin == null) return;

            if (!removePlugin.Capabilities.CanRemoveFromShoppingCart)
            {
                ConsoleRenderer.PrintWarning($"{removePlugin.DisplayName} does not support removing from a shopping cart.");
                return;
            }

            var removeToken = GetToken(tokens, parts[1]);
            if (removeToken == null) return;
            var removeResult = await removePlugin.RemoveFromCartAsync(removeToken, parts[2], parts[3], ct);
            if (removePlugin.Capabilities.CanReadShoppingCart)
            {
                ConsoleRenderer.PrintShoppingCart(removeResult);
            }
            break;

        default:
            // "store cart <pluginId> <locationId>" — view cart
            if (parts.Length < 2) { ConsoleRenderer.PrintWarning("Usage: store cart <pluginId> <locationId>"); return; }
            var viewPlugin = FindStorePlugin(plugins, parts[0]);
            if (viewPlugin == null) return;

            if (!viewPlugin.Capabilities.CanReadShoppingCart)
            {
                ConsoleRenderer.PrintWarning($"{viewPlugin.DisplayName} does not support viewing a shopping cart");
                return;                
            }

            var viewToken = GetToken(tokens, parts[0]);
            if (viewToken == null) return;
            var cart = await viewPlugin.GetShoppingCartAsync(viewToken, parts[1], ct);
            ConsoleRenderer.PrintShoppingCart(cart);
            break;
    }
}

static IStoreIntegrationPlugin? FindStorePlugin(List<LoadedPlugin> plugins, string pluginId)
{
    var plugin = plugins.FirstOrDefault(p =>
        p.IsStoreIntegration &&
        p.Config.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

    if (plugin == null)
    {
        ConsoleRenderer.PrintError("Store", $"No store plugin found with ID '{pluginId}'.");
        return null;
    }

    if (!plugin.Plugin.IsAvailable)
    {
        ConsoleRenderer.PrintWarning($"Plugin '{pluginId}' is loaded but not available.");
        return null;
    }

    return plugin.StorePlugin;
}

static string? GetToken(Dictionary<string, OAuthTokenResult> tokens, string pluginId)
{
    if (tokens.TryGetValue(pluginId, out var token) && token.Success && !string.IsNullOrEmpty(token.AccessToken))
        return token.AccessToken;

    ConsoleRenderer.PrintWarning($"No token for '{pluginId}'. Use 'store auth' and 'store token' first.");
    return null;
}

static bool RequireArgs(string[] parts, int required, string usage)
{
    if (parts.Length >= required) return true;
    ConsoleRenderer.PrintWarning($"Usage: {usage}");
    return false;
}
