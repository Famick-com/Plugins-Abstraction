using System.Text.Json;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

namespace Famick.HomeManagement.Plugin.Tester;

/// <summary>
/// Persists OAuth refresh tokens per plugin to a JSON file so tokens survive between sessions.
/// On startup, attempts to refresh expired access tokens automatically.
/// </summary>
internal class TokenCache
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Famick", "PluginTester", "tokens.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public TokenCache(string? filePath = null)
    {
        _filePath = filePath ?? DefaultPath;
    }

    /// <summary>
    /// Saves the current token dictionary to disk. Only persists tokens that have a refresh token.
    /// </summary>
    public void Save(Dictionary<string, OAuthTokenResult> tokens)
    {
        var entries = new Dictionary<string, TokenEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pluginId, result) in tokens)
        {
            if (result.Success && !string.IsNullOrEmpty(result.RefreshToken))
            {
                entries[pluginId] = new TokenEntry
                {
                    RefreshToken = result.RefreshToken,
                    AccessToken = result.AccessToken,
                    ExpiresAt = result.ExpiresAt
                };
            }
        }

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_filePath, JsonSerializer.Serialize(entries, JsonOptions));
    }

    /// <summary>
    /// Loads cached tokens from disk and refreshes any expired access tokens using the plugin's RefreshTokenAsync.
    /// </summary>
    public async Task<Dictionary<string, OAuthTokenResult>> LoadAndRefreshAsync(
        List<LoadedPlugin> plugins,
        CancellationToken ct)
    {
        var tokens = new Dictionary<string, OAuthTokenResult>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(_filePath))
            return tokens;

        Dictionary<string, TokenEntry>? entries;
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            entries = JsonSerializer.Deserialize<Dictionary<string, TokenEntry>>(json);
        }
        catch
        {
            return tokens;
        }

        if (entries == null)
            return tokens;

        foreach (var (pluginId, entry) in entries)
        {
            if (string.IsNullOrEmpty(entry.RefreshToken))
                continue;

            var plugin = plugins.FirstOrDefault(p =>
                p.IsStoreIntegration &&
                p.Config.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));

            if (plugin?.StorePlugin == null)
                continue;

            // If access token is still valid, reuse it
            if (!string.IsNullOrEmpty(entry.AccessToken) &&
                entry.ExpiresAt.HasValue &&
                entry.ExpiresAt.Value > DateTime.UtcNow.AddMinutes(1))
            {
                tokens[pluginId] = OAuthTokenResult.Ok(
                    entry.AccessToken, entry.RefreshToken, entry.ExpiresAt.Value);

                ConsoleRenderer.PrintSuccess($"  Restored cached token for '{pluginId}' (expires {entry.ExpiresAt.Value:HH:mm:ss UTC})");
                continue;
            }

            // Otherwise, refresh
            try
            {
                var result = await plugin.StorePlugin.RefreshTokenAsync(entry.RefreshToken, ct);
                if (result.Success)
                {
                    tokens[pluginId] = result;
                    ConsoleRenderer.PrintSuccess($"  Refreshed token for '{pluginId}'");
                }
                else
                {
                    ConsoleRenderer.PrintWarning($"  Failed to refresh token for '{pluginId}': {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ConsoleRenderer.PrintWarning($"  Failed to refresh token for '{pluginId}': {ex.Message}");
            }
        }

        return tokens;
    }

    private class TokenEntry
    {
        public string? RefreshToken { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
