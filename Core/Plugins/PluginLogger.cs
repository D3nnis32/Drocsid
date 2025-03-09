using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
/// Implementation of plugin logger
/// </summary>
public class PluginLogger : IPluginLogger
{
    private readonly string _pluginId;
        
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pluginId">The ID of the plugin this logger belongs to</param>
    public PluginLogger(string pluginId)
    {
        _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
    }
        
    /// <summary>
    /// Log a debug message
    /// </summary>
    public void Debug(string message)
    {
        Log("DEBUG", message);
    }
        
    /// <summary>
    /// Log an informational message
    /// </summary>
    public void Info(string message)
    {
        Log("INFO", message);
    }
        
    /// <summary>
    /// Log a warning message
    /// </summary>
    public void Warning(string message)
    {
        Log("WARNING", message);
    }
        
    /// <summary>
    /// Log an error message
    /// </summary>
    public void Error(string message, Exception exception = null)
    {
        Log("ERROR", message);
            
        if (exception != null)
        {
            Log("ERROR", $"Exception: {exception.Message}");
            Log("ERROR", $"Stack trace: {exception.StackTrace}");
        }
    }
        
    /// <summary>
    /// Internal log method
    /// </summary>
    private void Log(string level, string message)
    {
        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [Plugin: {_pluginId}] {message}";
        Console.WriteLine(logMessage);
            
        // TODO: Log to a file or other logging system
    }
}