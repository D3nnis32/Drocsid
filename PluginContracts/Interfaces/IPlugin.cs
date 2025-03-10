using System.Windows.Controls;
using System.Windows.Media;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Base interface for all Drocsid plugins
/// </summary>
public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Version Version { get; }
    string Author { get; }
    string InfoUrl { get; }
    PluginState State { get; }
        
    Task InitializeAsync(IPluginContext context);
    Task ShutdownAsync();
    object GetSettingsView();
}