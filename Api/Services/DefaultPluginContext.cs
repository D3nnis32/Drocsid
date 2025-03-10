using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.Api.Services;

/// <summary>
/// Default implementation of IPluginContext provided to plugins
/// </summary>
public class DefaultPluginContext : IPluginContext
{
    private readonly ILogger<DefaultPluginContext> _logger;
    
    public IPluginConfiguration Configuration { get; }
    public IPluginLogger Logger { get; }
    public IUserSessionService UserSession { get; }
    public IUIService UIService { get; }
    public IPluginEventManager EventManager { get; }

    public DefaultPluginContext(
        ILogger<DefaultPluginContext> logger,
        IPluginConfiguration configuration,
        IPluginLogger pluginLogger,
        IUserSessionService userSession,
        IUIService uiService,
        IPluginEventManager eventManager)
    {
        _logger = logger;
        Configuration = configuration;
        Logger = pluginLogger;
        UserSession = userSession;
        UIService = uiService;
        EventManager = eventManager;
    }
}

/// <summary>
/// Simple implementation of plugin configuration
/// </summary>
public class DefaultPluginConfiguration : IPluginConfiguration
{
    private readonly Dictionary<string, object> _configValues = new();
    private readonly ILogger<DefaultPluginConfiguration> _logger;

    public DefaultPluginConfiguration(ILogger<DefaultPluginConfiguration> logger)
    {
        _logger = logger;
    }

    public T GetValue<T>(string key, T defaultValue = default)
    {
        if (_configValues.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    public void SetValue<T>(string key, T value)
    {
        _configValues[key] = value;
    }

    public Task SaveAsync()
    {
        // In a real implementation, this would save to disk or database
        _logger.LogInformation("Plugin configuration saved");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Simple implementation of plugin logger
/// </summary>
public class DefaultPluginLogger : IPluginLogger
{
    private readonly ILogger _logger;
    private readonly string _pluginName;

    public DefaultPluginLogger(ILogger logger, string pluginName)
    {
        _logger = logger;
        _pluginName = pluginName;
    }

    public void Debug(string message)
    {
        _logger.LogDebug($"[Plugin:{_pluginName}] {message}");
    }

    public void Info(string message)
    {
        _logger.LogInformation($"[Plugin:{_pluginName}] {message}");
    }

    public void Warning(string message)
    {
        _logger.LogWarning($"[Plugin:{_pluginName}] {message}");
    }

    public void Error(string message, Exception exception = null)
    {
        if (exception != null)
        {
            _logger.LogError(exception, $"[Plugin:{_pluginName}] {message}");
        }
        else
        {
            _logger.LogError($"[Plugin:{_pluginName}] {message}");
        }
    }
}

/// <summary>
/// Simple implementation of plugin event manager
/// </summary>
public class DefaultPluginEventManager : IPluginEventManager
{
    private readonly Dictionary<string, List<Delegate>> _eventHandlers = new();
    private readonly ILogger<DefaultPluginEventManager> _logger;

    public DefaultPluginEventManager(ILogger<DefaultPluginEventManager> logger)
    {
        _logger = logger;
    }

    public void Subscribe<T>(string eventName, Action<T> handler)
    {
        if (!_eventHandlers.ContainsKey(eventName))
        {
            _eventHandlers[eventName] = new List<Delegate>();
        }
        _eventHandlers[eventName].Add(handler);
        _logger.LogDebug($"Subscribed to event: {eventName}");
    }

    public void Unsubscribe<T>(string eventName, Action<T> handler)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers.Remove(handler);
            _logger.LogDebug($"Unsubscribed from event: {eventName}");
        }
    }

    public void Publish<T>(string eventName, T eventData) where T : class
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    if (handler is Action<T> typedHandler)
                    {
                        typedHandler(eventData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error invoking event handler for event: {eventName}");
                }
            }
            _logger.LogDebug($"Published event: {eventName} to {handlers.Count} subscribers");
        }
    }
}

/// <summary>
/// Simple implementation of user session service for plugins
/// </summary>
public class DefaultUserSessionService : IUserSessionService
{
    private readonly ILogger<DefaultUserSessionService> _logger;
    
    public Guid CurrentUserId { get; private set; }
    public string CurrentUsername { get; private set; }
    public string AuthToken { get; private set; }
    public string ApiBaseUrl { get; private set; }

    public DefaultUserSessionService(
        ILogger<DefaultUserSessionService> logger,
        Guid currentUserId,
        string currentUsername,
        string authToken,
        string apiBaseUrl)
    {
        _logger = logger;
        CurrentUserId = currentUserId;
        CurrentUsername = currentUsername;
        AuthToken = authToken;
        ApiBaseUrl = apiBaseUrl;
    }

    public Task<PluginContracts.Models.ChannelInfo> GetChannelInfoAsync(Guid channelId)
    {
        // In a real implementation, this would fetch channel info from the API
        _logger.LogWarning("GetChannelInfoAsync not fully implemented");
        return Task.FromResult(new PluginContracts.Models.ChannelInfo
        {
            Id = channelId,
            Name = "Channel " + channelId,
            MemberIds = new List<Guid> { CurrentUserId }
        });
    }

    public Task<PluginContracts.Models.UserInfo> GetUserInfoAsync(Guid userId)
    {
        // In a real implementation, this would fetch user info from the API
        _logger.LogWarning("GetUserInfoAsync not fully implemented");
        return Task.FromResult(new PluginContracts.Models.UserInfo
        {
            Id = userId,
            Username = userId == CurrentUserId ? CurrentUsername : "User " + userId,
            Status = "Online"
        });
    }
}

/// <summary>
/// Simple implementation of UI service for plugins
/// </summary>
public class DefaultUIService : IUIService
{
    private readonly ILogger<DefaultUIService> _logger;
    private readonly Dictionary<Guid, List<UiComponent>> _channelComponents = new();
    private readonly List<UiComponent> _sidebarComponents = new();

    public DefaultUIService(ILogger<DefaultUIService> logger)
    {
        _logger = logger;
    }

    public void RegisterChannelHeaderComponent(Guid channelId, UiComponent component)
    {
        if (!_channelComponents.ContainsKey(channelId))
        {
            _channelComponents[channelId] = new List<UiComponent>();
        }
        _channelComponents[channelId].Add(component);
        _logger.LogInformation($"Registered channel header component for channel {channelId}");
    }

    public void RegisterSidebarComponent(UiComponent component)
    {
        _sidebarComponents.Add(component);
        _logger.LogInformation("Registered sidebar component");
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        _logger.LogInformation($"Notification [{type}] {title}: {message}");
    }

    public Task<bool> ShowConfirmationDialogAsync(string title, string message)
    {
        _logger.LogInformation($"Confirmation dialog: {title} - {message}");
        // In a real implementation, this would show a dialog to the user
        return Task.FromResult(true);
    }

    public Task ShowModalAsync(string title, UiComponent content)
    {
        _logger.LogInformation($"Modal dialog: {title}");
        // In a real implementation, this would show a modal dialog to the user
        return Task.CompletedTask;
    }

    // Methods to get components for UI rendering
    public IEnumerable<UiComponent> GetChannelComponents(Guid channelId)
    {
        if (_channelComponents.TryGetValue(channelId, out var components))
        {
            return components;
        }
        return Enumerable.Empty<UiComponent>();
    }

    public IEnumerable<UiComponent> GetSidebarComponents()
    {
        return _sidebarComponents;
    }
}