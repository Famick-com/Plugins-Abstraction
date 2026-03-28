using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Plugin.Abstractions;

/// <summary>
/// Implement this interface in a plugin assembly to register services
/// with the host's dependency injection container.
/// The host will scan the assembly for a class implementing IStartup
/// and call <see cref="ConfigureServices"/> before resolving plugin instances.
/// </summary>
public interface IStartup
{
    /// <summary>
    /// Register plugin services (HttpClient configurations, custom services, etc.)
    /// with the host's service collection.
    /// </summary>
    /// <param name="services">The host's service collection</param>
    void ConfigureServices(IServiceCollection services);
}
