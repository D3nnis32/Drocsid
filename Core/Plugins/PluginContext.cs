using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
/// Implementation of the plugin context
/// </summary>
public class PluginContext : IPluginContext
{
    /// <summary>
    /// Configuration service for plugins
    /// </summary>
    public IPluginConfiguration Configuration { get; }
        
    /// <summary>
    /// Logging service for plugins
    /// </summary>
    public IPluginLogger Logger { get; }
        
    /// <summary>
    /// User session service for plugins
    /// </summary>
    public IUserSessionService UserSession { get; }
        
    /// <summary>
    /// UI service for plugins
    /// </summary>
    public IUIService UIService { get; }
        
    /// <summary>
    /// Event manager for plugins
    /// </summary>
    public IPluginEventManager EventManager { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    public PluginContext(
        IPluginConfiguration configuration,
        IPluginLogger logger,
        IUserSessionService userSession,
        IUIService uiService,
        IPluginEventManager eventManager)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        UserSession = userSession ?? throw new ArgumentNullException(nameof(userSession));
        UIService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        EventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
    }
}