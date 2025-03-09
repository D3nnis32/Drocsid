namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for plugin logging
/// </summary>
public interface IPluginLogger
{
    /// <summary>
    /// Log a debug message
    /// </summary>
    void Debug(string message);
        
    /// <summary>
    /// Log an informational message
    /// </summary>
    void Info(string message);
        
    /// <summary>
    /// Log a warning message
    /// </summary>
    void Warning(string message);
        
    /// <summary>
    /// Log an error message
    /// </summary>
    void Error(string message, Exception exception = null);
}