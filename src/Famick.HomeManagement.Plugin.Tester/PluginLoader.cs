using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using Famick.HomeManagement.Plugin.Abstractions;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Plugin.Tester;

/// <summary>
/// Tester-specific config entry. Uses "Type" to specify the fully-qualified
/// type name in standard .NET nomenclature: "Namespace.ClassName, AssemblyReference".
/// The assembly reference can be an assembly name, relative path, or absolute path.
/// </summary>
internal class TesterPluginConfig
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Fully-qualified type name: "Namespace.ClassName, AssemblyReference"
    /// Examples:
    ///   "MyCompany.Plugins.KrogerPlugin, MyCompany.Plugins.Kroger"
    ///   "MyCompany.Plugins.KrogerPlugin, ../bin/MyCompany.Plugins.Kroger.dll"
    ///   "MyCompany.Plugins.KrogerPlugin, /opt/plugins/MyCompany.Plugins.Kroger.dll"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("Config")]
    public JsonElement? PluginConfig { get; set; }
}

internal class LoadedPlugin
{
    public required TesterPluginConfig Config { get; init; }
    public required IPlugin Plugin { get; init; }
    public bool IsProductLookup => Plugin is IProductLookupPlugin;
    public bool IsStoreIntegration => Plugin is IStoreIntegrationPlugin;
    public IProductLookupPlugin? LookupPlugin => Plugin as IProductLookupPlugin;
    public IStoreIntegrationPlugin? StorePlugin => Plugin as IStoreIntegrationPlugin;
}

internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }
}

internal static class PluginLoader
{
    public static async Task<List<LoadedPlugin>> LoadAsync(string configPath, CancellationToken ct)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Config file not found: {configPath}");

        var json = await File.ReadAllTextAsync(configPath, ct);
        var jsonOptions = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        // Support both a top-level array and an object with a "plugins" array
        List<TesterPluginConfig> configs;
        var doc = JsonDocument.Parse(json, jsonOptions);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            configs = JsonSerializer.Deserialize<List<TesterPluginConfig>>(json, options)
                ?? throw new InvalidOperationException("Config file is empty or invalid.");
        }
        else if (doc.RootElement.TryGetProperty("plugins", out var pluginsElement) ||
                 doc.RootElement.TryGetProperty("Plugins", out pluginsElement))
        {
            configs = JsonSerializer.Deserialize<List<TesterPluginConfig>>(pluginsElement.GetRawText(), options)
                ?? throw new InvalidOperationException("Config file 'plugins' array is empty or invalid.");
        }
        else
        {
            throw new InvalidOperationException("Config file must be a JSON array or an object with a 'plugins' array.");
        }

        var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";

        // Phase 1: Load assemblies and resolve types
        var resolvedPlugins = new List<(TesterPluginConfig Config, Type PluginType, Assembly Assembly, string AssemblyPath)>();

        foreach (var config in configs.Where(c => c.Enabled))
        {
            if (string.IsNullOrWhiteSpace(config.Type))
            {
                ConsoleRenderer.PrintWarning($"  Skipping '{config.Id}': no Type specified.");
                continue;
            }

            try
            {
                var (typeName, assemblyPath) = ResolveType(config.Type, configDir);

                var loadContext = new PluginLoadContext(assemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                var pluginType = assembly.GetType(typeName);
                if (pluginType == null)
                {
                    ConsoleRenderer.PrintError(config.Id, $"Type '{typeName}' not found in {Path.GetFileName(assemblyPath)}.");
                    continue;
                }

                resolvedPlugins.Add((config, pluginType, assembly, assemblyPath));
            }
            catch (Exception ex)
            {
                ConsoleRenderer.PrintError(config.Id, $"Load failed: {ex.Message}");
            }
        }

        // Phase 2: Build DI container with base services and plugin IStartup registrations
        var services = new ServiceCollection();

        // Register base services that plugins typically need
        services.AddHttpClient();
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Debug)
            .AddFilter("Microsoft.Extensions.Http", LogLevel.Warning));

        // Register each plugin type so DI can construct it
        foreach (var (config, pluginType, assembly, _) in resolvedPlugins)
        {
            // Scan the assembly for IStartup implementations and invoke them
            try
            {
                var startupTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface &&
                                typeof(IStartup).IsAssignableFrom(t));

                foreach (var startupType in startupTypes)
                {
                    if (Activator.CreateInstance(startupType) is IStartup startup)
                    {
                        startup.ConfigureServices(services);
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  [Startup] {startupType.Name} registered services for '{config.Id}'");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleRenderer.PrintWarning($"  '{config.Id}': IStartup scan failed: {ex.Message}");
            }

            // Only register the plugin type if IStartup didn't already register it
            if (!services.Any(sd => sd.ServiceType == pluginType))
            {
                services.AddTransient(pluginType);
            }
        }

        var serviceProvider = services.BuildServiceProvider();

        // Phase 3: Resolve plugin instances from DI, initialize, and return
        var loaded = new List<LoadedPlugin>();

        foreach (var (config, pluginType, _, _) in resolvedPlugins)
        {
            try
            {
                var instance = serviceProvider.GetRequiredService(pluginType);
                if (instance is not IPlugin plugin)
                {
                    ConsoleRenderer.PrintError(config.Id, $"Type '{pluginType.Name}' does not implement IPlugin.");
                    continue;
                }

                await plugin.InitAsync(config.PluginConfig, ct);

                loaded.Add(new LoadedPlugin { Config = config, Plugin = plugin });

                var status = plugin.IsAvailable ? "Available" : "Unavailable";
                var color = plugin.IsAvailable ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.ForegroundColor = color;
                Console.Write($"  [{status}]");
                Console.ResetColor();
                Console.WriteLine($" {config.DisplayName} ({plugin.PluginId}) — {pluginType.Name}");
            }
            catch (Exception ex)
            {
                ConsoleRenderer.PrintError(config.Id, $"Init failed: {ex.Message}");
            }
        }

        return loaded;
    }

    private static (string TypeName, string AssemblyPath) ResolveType(string typeSpec, string configDir)
    {
        var commaIndex = typeSpec.IndexOf(',');
        if (commaIndex < 0)
            throw new FormatException($"Invalid Type format: '{typeSpec}'. Expected 'Namespace.Class, AssemblyReference'.");

        var typeName = typeSpec[..commaIndex].Trim();
        var assemblyRef = typeSpec[(commaIndex + 1)..].Trim();

        // Resolve the assembly path:
        // - Contains path separator or ends with .dll → treat as a path
        // - Otherwise → assembly name, look for AssemblyName.dll in config dir
        string assemblyPath;
        if (assemblyRef.Contains(Path.DirectorySeparatorChar) ||
            assemblyRef.Contains(Path.AltDirectorySeparatorChar) ||
            assemblyRef.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            assemblyPath = Path.IsPathRooted(assemblyRef)
                ? assemblyRef
                : Path.GetFullPath(Path.Combine(configDir, assemblyRef));
        }
        else
        {
            assemblyPath = Path.GetFullPath(Path.Combine(configDir, assemblyRef + ".dll"));
        }

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

        return (typeName, assemblyPath);
    }
}
