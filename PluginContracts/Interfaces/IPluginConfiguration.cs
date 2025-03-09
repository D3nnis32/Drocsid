namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for plugin configuration
/// </summary>
public interface IPluginConfiguration
{
    /// <summary>
    /// Get a configuration value
    /// </summary>
    T GetValue<T>(string key, T defaultValue = default);
        
    /// <summary>
    /// Set a configuration value
    /// </summary>
    void SetValue<T>(string key, T value);
        
    /// <summary>
    /// Save configuration to persistent storage
    /// </summary>
    Task SaveAsync();
}