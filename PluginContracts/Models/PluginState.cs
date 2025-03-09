namespace Drocsid.HenrikDennis2025.PluginContracts.Models;

/// <summary>
/// Represents the current state of a plugin
/// </summary>
public enum PluginState
{
    /// <summary>
    /// Plugin is not yet initialized
    /// </summary>
    Uninitialized,
        
    /// <summary>
    /// Plugin is loaded and running
    /// </summary>
    Running,
        
    /// <summary>
    /// Plugin is in an error state
    /// </summary>
    Error,
        
    /// <summary>
    /// Plugin is disabled
    /// </summary>
    Disabled,
        
    /// <summary>
    /// Plugin is in the process of being updated
    /// </summary>
    Updating
}