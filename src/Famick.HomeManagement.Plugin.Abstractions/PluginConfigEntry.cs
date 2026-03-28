namespace Famick.HomeManagement.Plugin.Abstractions;

/// <summary>
/// Plugin configuration entry from plugins/config.json
/// </summary>
public class PluginConfigEntry
{
    /// <summary>
    /// Plugin identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Whether this plugin is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this is a built-in plugin (compiled into the application)
    /// </summary>
    public bool Builtin { get; set; }

    /// <summary>
    /// Path to the DLL file for external plugins (relative to plugins folder)
    /// </summary>
    public string? Assembly { get; set; }

    /// <summary>
    /// Assembly-qualified type name for the plugin class (e.g. "MyCompany.Plugins.KrogerPlugin, MyCompany.Plugins.Kroger").
    /// When specified, the loader resolves this exact type instead of scanning the assembly for an IPlugin implementation.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Display name for the plugin
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Plugin-specific configuration (as a JsonElement)
    /// </summary>
    public System.Text.Json.JsonElement? Config { get; set; }
}
