using System.Windows.Controls;
using System.Windows.Media;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Base interface for all Drocsid plugins
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique identifier for the plugin
    /// </summary>
    string Id { get; }
        
    /// <summary>
    /// Display name of the plugin
    /// </summary>
    string Name { get; }
        
    /// <summary>
    /// Plugin description
    /// </summary>
    string Description { get; }
        
    /// <summary>
    /// Plugin version
    /// </summary>
    Version Version { get; }
        
    /// <summary>
    /// Plugin author information
    /// </summary>
    string Author { get; }
        
    /// <summary>
    /// URL for more information about the plugin
    /// </summary>
    string InfoUrl { get; }
        
    /// <summary>
    /// Indicates the current state of the plugin
    /// </summary>
    PluginState State { get; }
        
    /// <summary>
    /// Gets the plugin's icon to display in the UI
    /// </summary>
    System.Windows.Media.ImageSource Icon { get; }
        
    /// <summary>
    /// Called when the plugin is loaded
    /// </summary>
    /// <param name="context">The plugin context providing access to application services</param>
    Task InitializeAsync(IPluginContext context);
        
    /// <summary>
    /// Called when the plugin is about to be unloaded
    /// </summary>
    Task ShutdownAsync();
        
    /// <summary>
    /// Get plugin settings UI
    /// </summary>
    /// <returns>A user control for configuring plugin settings</returns>
    UserControl GetSettingsView();
}