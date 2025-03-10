namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for the plugin event manager
/// </summary>
public interface IPluginEventManager
{
    /// <summary>
    /// Subscribe to an event
    /// </summary>
    void Subscribe<T>(string eventName, Action<T> handler);
        
    /// <summary>
    /// Unsubscribe from an event
    /// </summary>
    void Unsubscribe<T>(string eventName, Action<T> handler);
        
    /// <summary>
    /// Publish an event
    /// </summary>
    void Publish<T>(string eventName, T eventData) where T : class;
}