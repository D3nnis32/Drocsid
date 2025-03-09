using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
/// Implementation of plugin event manager
/// </summary>
public class PluginEventManager : IPluginEventManager
{
    private readonly Dictionary<string, List<Delegate>> _eventHandlers = new Dictionary<string, List<Delegate>>();
    private readonly IPluginLogger _logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public PluginEventManager(IPluginLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Subscribe to an event
    /// </summary>
    public void Subscribe<T>(string eventName, Action<T> handler) where T : class
    {
        if (string.IsNullOrEmpty(eventName))
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (!_eventHandlers.ContainsKey(eventName))
        {
            _eventHandlers[eventName] = new List<Delegate>();
        }

        _eventHandlers[eventName].Add(handler);
    }

    /// <summary>
    /// Unsubscribe from an event
    /// </summary>
    public void Unsubscribe<T>(string eventName, Action<T> handler) where T : class
    {
        if (string.IsNullOrEmpty(eventName))
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers.Remove(handler);
        }
    }

    /// <summary>
    /// Publish an event
    /// </summary>
    public void Publish<T>(string eventName, T eventData) where T : class
    {
        if (string.IsNullOrEmpty(eventName))
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        if (eventData == null)
        {
            throw new ArgumentNullException(nameof(eventData));
        }

        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            foreach (var handler in handlers.ToList())
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
                    _logger.Error($"Error in event handler for {eventName}", ex);
                }
            }
        }
    }
}