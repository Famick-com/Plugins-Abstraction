using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

namespace Famick.HomeManagement.Plugin.Tester;

internal static class ConsoleRenderer
{
    private const string Separator = "───────────────────────────────────────────";

    public static void PrintBanner(int lookupCount, int storeCount)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║   Famick Plugin Tester                   ║");
        Console.WriteLine($"║   {lookupCount} lookup, {storeCount} store plugin(s) loaded     ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Product Lookup:");
        Console.ResetColor();
        Console.WriteLine("  <barcode>                                    Search by barcode (auto-detected)");
        Console.WriteLine("  <text>                                       Search by product name");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Store Integration:");
        Console.ResetColor();
        Console.WriteLine("  store plugins                                List store plugins & capabilities");
        Console.WriteLine("  store auth <pluginId>                        Show OAuth authorization URL");
        Console.WriteLine("  store token <pluginId> <code>                Exchange auth code for tokens");
        Console.WriteLine("  store refresh <pluginId> <refreshToken>      Refresh an expired token");
        Console.WriteLine("  store locations <pluginId> <zip>             Search stores by ZIP");
        Console.WriteLine("  store locations <pluginId> <lat> <lon>       Search stores by coordinates");
        Console.WriteLine("  store search <pluginId> <locId> <query>      Search products at a store");
        Console.WriteLine("  store product <pluginId> <locId> <prodId>    Get product by ID");
        Console.WriteLine("  store barcode <pluginId> <locId> <barcode>   Lookup product by barcode");
        Console.WriteLine("  store cart <pluginId> <locId>                View shopping cart");
        Console.WriteLine("  store cart add <pluginId> <locId> <prodId> [qty]  Add to cart");
        Console.WriteLine("  store cart update <pluginId> <locId> <prodId> <qty>  Update quantity");
        Console.WriteLine("  store cart remove <pluginId> <locId> <prodId>     Remove from cart");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("General:");
        Console.ResetColor();
        Console.WriteLine("  plugins                                      Show all loaded plugins");
        Console.WriteLine("  help                                         Show this help text");
        Console.WriteLine("  quit / exit                                  Exit the tester");
        Console.WriteLine();
    }

    public static void PrintPlugins(IReadOnlyList<LoadedPlugin> plugins)
    {
        if (plugins.Count == 0)
        {
            PrintWarning("No plugins loaded.");
            return;
        }

        foreach (var p in plugins)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {p.Config.DisplayName}");
            Console.ResetColor();
            Console.Write($" ({p.Config.Id}) v{p.Plugin.Version}");

            if (p.Plugin.IsAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(" [Available]");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write(" [Unavailable]");
            }
            Console.ResetColor();

            var types = new List<string>();
            if (p.IsProductLookup) types.Add("Lookup");
            if (p.IsStoreIntegration) types.Add("Store");
            Console.Write($" [{string.Join(", ", types)}]");
            Console.WriteLine();

            if (p.Plugin.Attribution is { } attr)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    Attribution: {attr.LicenseText} — {attr.Url}");
                Console.ResetColor();
            }
        }
        Console.WriteLine();
    }

    public static void PrintStorePlugins(IReadOnlyList<LoadedPlugin> plugins)
    {
        var storePlugins = plugins.Where(p => p.IsStoreIntegration).ToList();
        if (storePlugins.Count == 0)
        {
            PrintWarning("No store integration plugins loaded.");
            return;
        }

        foreach (var p in storePlugins)
        {
            var store = p.StorePlugin!;
            var caps = store.Capabilities;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  {p.Config.DisplayName} ({p.Config.Id})");
            Console.ResetColor();

            PrintField("    OAuth Required", caps.RequiresOAuth ? "Yes" : "No");
            PrintField("    Product Lookup", caps.HasProductLookup ? "Yes" : "No");
            PrintField("    Store Products", caps.HasStoreProductLookup ? "Yes" : "No");
            PrintField("    Shopping Cart", caps.HasShoppingCart ? "Yes" : "No");
            PrintField("    Read Cart", caps.CanReadShoppingCart ? "Yes" : "No");
            Console.WriteLine();
        }
    }

    public static void PrintLookupResults(ProductLookupPipelineContext context, long lookupMs, long enrichMs)
    {
        if (context.Results.Count == 0)
        {
            PrintWarning("No results found.");
            return;
        }

        for (var i = 0; i < context.Results.Count; i++)
        {
            var r = context.Results[i];
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"── Result {i + 1} {new string('─', Math.Max(0, 35 - $"Result {i + 1}".Length))}");
            Console.ResetColor();

            PrintField("  Name", r.Name);
            if (!string.IsNullOrEmpty(r.BrandName)) PrintField("  Brand", r.BrandName);
            if (!string.IsNullOrEmpty(r.BrandOwner)) PrintField("  Brand Owner", r.BrandOwner);
            foreach (var bc in r.Barcodes)
            {
                PrintField("  Barcode", bc.ToString());
            }
            if (r.Categories is { Count: > 0 }) PrintField("  Categories", string.Join(", ", r.Categories));
            if (!string.IsNullOrEmpty(r.Description)) PrintField("  Description", r.Description);
            if (!string.IsNullOrEmpty(r.Size)) PrintField("  Size", r.Size);

            if (r.Nutrition is { } n)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  Nutrition");
                Console.ResetColor();
                if (!string.IsNullOrEmpty(r.ServingSizeDescription))
                    Console.Write($" ({r.ServingSizeDescription})");
                Console.WriteLine(":");

                var nutritionPairs = new List<(string Label, string Value)>();
                if (n.Calories.HasValue) nutritionPairs.Add(("Calories", $"{n.Calories}"));
                if (n.TotalFat.HasValue) nutritionPairs.Add(("Fat", $"{n.TotalFat}g"));
                if (n.Protein.HasValue) nutritionPairs.Add(("Protein", $"{n.Protein}g"));
                if (n.TotalCarbohydrates.HasValue) nutritionPairs.Add(("Carbs", $"{n.TotalCarbohydrates}g"));
                if (n.DietaryFiber.HasValue) nutritionPairs.Add(("Fiber", $"{n.DietaryFiber}g"));
                if (n.TotalSugars.HasValue) nutritionPairs.Add(("Sugars", $"{n.TotalSugars}g"));
                if (n.Sodium.HasValue) nutritionPairs.Add(("Sodium", $"{n.Sodium}mg"));
                if (n.Cholesterol.HasValue) nutritionPairs.Add(("Cholesterol", $"{n.Cholesterol}mg"));

                for (var j = 0; j < nutritionPairs.Count; j += 3)
                {
                    Console.Write("    ");
                    for (var k = j; k < Math.Min(j + 3, nutritionPairs.Count); k++)
                    {
                        var (label, value) = nutritionPairs[k];
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write($"{label}: ");
                        Console.ResetColor();
                        Console.Write($"{value,-12}");
                    }
                    Console.WriteLine();
                }
            }

            var hasStoreInfo = r.Price.HasValue || r.SalePrice.HasValue ||
                               !string.IsNullOrEmpty(r.Aisle) || !string.IsNullOrEmpty(r.Department) ||
                               r.InStock.HasValue;
            if (hasStoreInfo)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  Store Info:");
                Console.ResetColor();

                if (r.Price.HasValue || r.SalePrice.HasValue)
                {
                    Console.Write("    ");
                    if (r.Price.HasValue) { PrintInline("Price", $"${r.Price:F2}"); Console.Write("  "); }
                    if (r.SalePrice.HasValue) { PrintInline("Sale", $"${r.SalePrice:F2}"); }
                    Console.WriteLine();
                }
                if (!string.IsNullOrEmpty(r.Aisle) || !string.IsNullOrEmpty(r.Department))
                {
                    Console.Write("    ");
                    if (!string.IsNullOrEmpty(r.Aisle)) { PrintInline("Aisle", r.Aisle); Console.Write("  "); }
                    if (!string.IsNullOrEmpty(r.Department)) { PrintInline("Dept", r.Department); }
                    Console.WriteLine();
                }
                if (r.InStock.HasValue) PrintField("    In Stock", r.InStock.Value ? "Yes" : "No");
            }

            if (r.DataSources is { Count: > 0 })
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  Data Sources:");
                Console.ResetColor();
                foreach (var (source, id) in r.DataSources)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    {source} → {id}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Separator);
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Pipeline: {context.Results.Count} result(s) | Lookup: {lookupMs}ms | Enrichment: {enrichMs}ms");
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintStoreLocations(List<StoreLocationResult> locations)
    {
        if (locations.Count == 0)
        {
            PrintWarning("No stores found.");
            return;
        }

        for (var i = 0; i < locations.Count; i++)
        {
            var loc = locations[i];
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"── Store {i + 1} {new string('─', Math.Max(0, 35 - $"Store {i + 1}".Length))}");
            Console.ResetColor();

            PrintField("  Name", loc.Name);
            if (!string.IsNullOrEmpty(loc.ChainName)) PrintField("  Chain", loc.ChainName);
            if (!string.IsNullOrEmpty(loc.FullAddress)) PrintField("  Address", loc.FullAddress);
            if (!string.IsNullOrEmpty(loc.Phone)) PrintField("  Phone", loc.Phone);
            if (loc.DistanceMiles.HasValue) PrintField("  Distance", $"{loc.DistanceMiles:F1} miles");
            PrintField("  Location ID", loc.ExternalLocationId);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Separator);
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    public static void PrintStoreProduct(StoreProductResult product)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"── Product {new string('─', 30)}");
        Console.ResetColor();

        PrintField("  Name", product.Name);
        if (!string.IsNullOrEmpty(product.Brand)) PrintField("  Brand", product.Brand);
        foreach (var bc in product.Barcodes)
        {
            PrintField("  Barcode", bc.ToString());
        }
        if (!string.IsNullOrEmpty(product.Size)) PrintField("  Size", product.Size);
        if (product.Categories is { Count: > 0 }) PrintField("  Categories", string.Join(", ", product.Categories));
        if (!string.IsNullOrEmpty(product.Description)) PrintField("  Description", product.Description);

        if (product.Price.HasValue || product.SalePrice.HasValue)
        {
            Console.Write("  ");
            if (product.Price.HasValue) { PrintInline("Price", $"${product.Price:F2}"); Console.Write("  "); }
            if (product.SalePrice.HasValue) { PrintInline("Sale", $"${product.SalePrice:F2}"); }
            Console.WriteLine();
        }
        if (!string.IsNullOrEmpty(product.Aisle) || !string.IsNullOrEmpty(product.Department))
        {
            Console.Write("  ");
            if (!string.IsNullOrEmpty(product.Aisle)) { PrintInline("Aisle", product.Aisle); Console.Write("  "); }
            if (!string.IsNullOrEmpty(product.Department)) { PrintInline("Dept", product.Department); }
            Console.WriteLine();
        }
        if (product.InStock.HasValue) PrintField("  In Stock", product.InStock.Value ? "Yes" : "No");

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(Separator);
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintStoreProducts(List<StoreProductResult> products)
    {
        if (products.Count == 0)
        {
            PrintWarning("No products found.");
            return;
        }

        foreach (var product in products)
            PrintStoreProduct(product);
    }

    public static void PrintShoppingCart(ShoppingCartResult? cart)
    {
        if (cart == null || cart.Items.Count == 0)
        {
            PrintWarning("Cart is empty.");
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"── Shopping Cart {new string('─', 25)}");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(cart.StoreLocationId))
            PrintField("  Store", cart.StoreLocationId);
        Console.WriteLine();

        for (var i = 0; i < cart.Items.Count; i++)
        {
            var item = cart.Items[i];
            Console.Write($"  {i + 1}. ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{item.Name,-30}");
            Console.ResetColor();
            Console.Write($"  x{item.Quantity}");
            if (item.Price.HasValue) Console.Write($"  ${item.Price:F2}");
            Console.WriteLine();
        }

        if (cart.Subtotal.HasValue)
        {
            Console.WriteLine();
            PrintField("  Subtotal", $"${cart.Subtotal:F2}");
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(Separator);
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintOAuthToken(OAuthTokenResult token)
    {
        if (token.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  Token obtained successfully.");
            Console.ResetColor();
            PrintField("  Access Token", token.AccessToken?[..Math.Min(20, token.AccessToken.Length)] + "...");
            if (token.RefreshToken != null)
                PrintField("  Refresh Token", token.RefreshToken[..Math.Min(20, token.RefreshToken.Length)] + "...");
            if (token.ExpiresAt.HasValue)
                PrintField("  Expires", token.ExpiresAt.Value.ToString("g"));
        }
        else
        {
            PrintError("Token exchange failed", token.ErrorMessage ?? "Unknown error");
        }
        Console.WriteLine();
    }

    public static void PrintSearchType(string query, bool isBarcode, Barcode? barcode)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        if (isBarcode && barcode != null)
            Console.WriteLine($"Barcode search: {barcode}");
        else
            Console.WriteLine($"Name search: \"{query}\"");
        Console.ResetColor();
    }

    public static void PrintError(string context, string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {context}: {message}");
        Console.ResetColor();
    }

    public static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void PrintField(string label, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{label}: ");
        Console.ResetColor();
        Console.WriteLine(value);
    }

    private static void PrintInline(string label, string value)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{label}: ");
        Console.ResetColor();
        Console.Write(value);
    }
}
