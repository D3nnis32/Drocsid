namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Context provided to plugins with access to application services
/// </summary>
public interface IPluginContext
{
    // Core methods without WPF dependencies
    ILogger Logger { get; }
    IConfiguration Configuration { get; }
    IEventManager EventManager { get; }
    IUserSession UserSession { get; }
}