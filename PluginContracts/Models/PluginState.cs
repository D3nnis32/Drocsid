namespace Drocsid.HenrikDennis2025.PluginContracts.Models;

/// <summary>
/// Represents the current state of a plugin
/// </summary>
public enum PluginState
{
    Uninitialized,
    Running,
    Paused,
    Disabled
}