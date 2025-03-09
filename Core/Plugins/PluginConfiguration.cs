using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
/// Implementation of plugin configuration
/// </summary>
public class PluginConfiguration : IPluginConfiguration
{
    private readonly Dictionary<string, object> _settings = new Dictionary<string, object>();
    private readonly string _pluginId;
        
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pluginId">The ID of the plugin this configuration belongs to</param>
    public PluginConfiguration(string pluginId)
    {
        _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        LoadSettings();
    }
        
    /// <summary>
    /// Get a configuration value
    /// </summary>
    public T GetValue<T>(string key, T defaultValue = default)
    {
        string fullKey = $"{_pluginId}.{key}";
            
        if (_settings.TryGetValue(fullKey, out var value) && value is T typedValue)
        {
            return typedValue;
        }
            
        return defaultValue;
    }
        
    /// <summary>
    /// Set a configuration value
    /// </summary>
    public void SetValue<T>(string key, T value)
    {
        string fullKey = $"{_pluginId}.{key}";
        _settings[fullKey] = value;
    }
        
    /// <summary>
    /// Save configuration to persistent storage
    /// </summary>
    public async Task SaveAsync()
    {
        // Save settings to isolated storage
        await Task.Run(() => 
        {
            // TODO: Save settings to a file or other persistent storage
        });
    }
        
    /// <summary>
    /// Load settings from persistent storage
    /// </summary>
    private void LoadSettings()
    {
        // TODO: Load settings from a file or other persistent storage
    }
}