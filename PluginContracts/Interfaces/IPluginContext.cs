namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Context provided to plugins with access to application services
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Application configuration
    /// </summary>
    IPluginConfiguration Configuration { get; }
        
    /// <summary>
    /// Logging interface for the plugin
    /// </summary>
    IPluginLogger Logger { get; }
        
    /// <summary>
    /// Service for interacting with the current user session
    /// </summary>
    IUserSessionService UserSession { get; }
        
    /// <summary>
    /// UI service for integrating with the application UI
    /// </summary>
    IUIService UIService { get; }
        
    /// <summary>
    /// Event manager for publishing and subscribing to application events
    /// </summary>
    IPluginEventManager EventManager { get; }
}