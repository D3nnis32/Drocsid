using System.Reflection;
using System.Security.Cryptography;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.Api.Services;

public class PluginManagerService
{
    private readonly ILogger<PluginManagerService> _logger;
    private readonly Dictionary<string, IPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, string> _verifiedPluginHashes = new();
    private readonly IPluginContext _pluginContext;
    private readonly string _pluginsDirectory;

    public PluginManagerService(ILogger<PluginManagerService> logger, IPluginContext pluginContext)
    {
        _logger = logger;
        _pluginContext = pluginContext;
        
        // Set the plugins directory
        _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        
        // Initialize with trusted plugin hashes
        InitializeVerifiedPluginHashes();
    }

    private void InitializeVerifiedPluginHashes()
    {
        // In a real implementation, these hashes would be loaded from a secured configuration
        // These are placeholder hashes that will be replaced with actual hashes when plugins are compiled
        _verifiedPluginHashes.Add("VoiceChatPlugin", "PLACEHOLDER_HASH_FOR_VOICE_CHAT_PLUGIN");
        _verifiedPluginHashes.Add("WhiteboardPlugin", "PLACEHOLDER_HASH_FOR_WHITEBOARD_PLUGIN");
    }

    /// <summary>
    /// Get all available plugin files from the plugins directory
    /// </summary>
    public IEnumerable<PluginFileInfo> GetAvailablePluginFiles()
    {
        List<PluginFileInfo> availablePlugins = new List<PluginFileInfo>();
        
        try
        {
            // Ensure plugins directory exists
            if (!Directory.Exists(_pluginsDirectory))
            {
                _logger.LogWarning($"Plugins directory not found: {_pluginsDirectory}");
                return availablePlugins;
            }

            // Get all DLL files in the plugins directory
            string[] pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll");
            
            foreach (string pluginPath in pluginFiles)
            {
                try
                {
                    string fileName = Path.GetFileName(pluginPath);
                    string pluginName = Path.GetFileNameWithoutExtension(pluginPath);
                    
                    // Check if plugin is already loaded
                    bool isLoaded = _loadedPlugins.ContainsKey(pluginName.ToLowerInvariant());
                    
                    // Determine plugin type (communication or collaboration) based on filename
                    string pluginType = "General";
                    if (pluginName.Contains("Voice") || pluginName.Contains("Chat"))
                    {
                        pluginType = "Communication";
                    }
                    else if (pluginName.Contains("Whiteboard") || pluginName.Contains("Collab"))
                    {
                        pluginType = "Collaboration";
                    }
                    
                    // Calculate hash for security verification
                    string pluginHash = CalculateFileHash(pluginPath);
                    
                    // Create plugin file info
                    var pluginInfo = new PluginFileInfo
                    {
                        FileName = fileName,
                        Name = FormatPluginName(pluginName),
                        FilePath = pluginPath,
                        IsLoaded = isLoaded,
                        Type = pluginType,
                        Hash = pluginHash
                    };
                    
                    availablePlugins.Add(pluginInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing plugin file: {pluginPath}");
                }
            }
            
            return availablePlugins;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error scanning plugins directory: {_pluginsDirectory}");
            return availablePlugins;
        }
    }

    /// <summary>
    /// Get all loaded plugins
    /// </summary>
    public IEnumerable<IPlugin> GetLoadedPlugins()
    {
        return _loadedPlugins.Values;
    }

    /// <summary>
    /// Load a plugin by file path
    /// </summary>
    public async Task<IPlugin> LoadPluginAsync(string pluginPath)
    {
        try
        {
            // Verify the plugin file exists
            if (!File.Exists(pluginPath))
            {
                _logger.LogError($"Plugin file not found: {pluginPath}");
                throw new FileNotFoundException($"Plugin file not found: {pluginPath}");
            }

            // Calculate the hash of the plugin assembly
            string pluginHash = CalculateFileHash(pluginPath);
            _logger.LogInformation($"Plugin hash: {pluginHash}");

            // Get the plugin type name without loading the assembly
            string pluginTypeName = Path.GetFileNameWithoutExtension(pluginPath);
            _logger.LogInformation($"Loading plugin: {pluginTypeName}");
            
            // Check if plugin is already loaded
            string pluginId = pluginTypeName.ToLowerInvariant();
            if (_loadedPlugins.ContainsKey(pluginId))
            {
                _logger.LogInformation($"Plugin already loaded: {pluginTypeName}");
                return _loadedPlugins[pluginId];
            }
            
            // Determine plugin type (communication or collaboration)
            bool isCommunication = pluginTypeName.Contains("Voice") || pluginTypeName.Contains("Chat");
            bool isCollaboration = pluginTypeName.Contains("Whiteboard") || pluginTypeName.Contains("Collab");
            
            IPlugin plugin;
            
            if (isCommunication)
            {
                var commPlugin = new PlaceholderCommunicationPlugin
                {
                    Id = pluginId,
                    Name = FormatPluginName(pluginTypeName),
                    Description = $"Communication plugin for {FormatPluginName(pluginTypeName)}",
                    Version = new Version(1, 0, 0),
                    Author = "Henrik Dennis"
                };
                plugin = commPlugin;
            }
            else if (isCollaboration)
            {
                var collabPlugin = new PlaceholderCollaborationPlugin
                {
                    Id = pluginId,
                    Name = FormatPluginName(pluginTypeName),
                    Description = $"Collaboration plugin for {FormatPluginName(pluginTypeName)}",
                    Version = new Version(1, 0, 0),
                    Author = "Henrik Dennis"
                };
                plugin = collabPlugin;
            }
            else
            {
                // General plugin
                plugin = new PlaceholderGeneralPlugin
                {
                    Id = pluginId,
                    Name = FormatPluginName(pluginTypeName),
                    Description = $"Plugin for {FormatPluginName(pluginTypeName)}",
                    Version = new Version(1, 0, 0),
                    Author = "Henrik Dennis"
                };
            }
            
            // Initialize the placeholder plugin
            await plugin.InitializeAsync(_pluginContext);
            
            // Add to loaded plugins
            _loadedPlugins[plugin.Id] = plugin;
            
            _logger.LogInformation($"Successfully loaded placeholder for plugin: {plugin.Name} ({plugin.Id})");
            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading plugin: {pluginPath}");
            throw;
        }
    }

    /// <summary>
    /// Unload a plugin by ID
    /// </summary>
    public async Task UnloadPluginAsync(string pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            try
            {
                await plugin.ShutdownAsync();
                _loadedPlugins.Remove(pluginId);
                _logger.LogInformation($"Plugin unloaded: {plugin.Name} ({plugin.Id})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unloading plugin: {plugin.Name} ({plugin.Id})");
                throw;
            }
        }
        else
        {
            _logger.LogWarning($"Attempted to unload non-existent plugin: {pluginId}");
        }
    }

    /// <summary>
    /// Get a plugin by ID
    /// </summary>
    public IPlugin GetPlugin(string pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            return plugin;
        }
        return null;
    }

    /// <summary>
    /// Calculate SHA256 hash of a file
    /// </summary>
    private string CalculateFileHash(string filePath)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    /// <summary>
    /// Verify a plugin's hash against trusted hashes
    /// </summary>
    private bool VerifyPluginHash(string pluginName, string calculatedHash)
    {
        // For development purposes, we'll accept any hash 
        // In production, you'd want to use the commented code instead
        return true;
        
        /*
        if (_verifiedPluginHashes.TryGetValue(pluginName, out string trustedHash))
        {
            return string.Equals(calculatedHash, trustedHash, StringComparison.OrdinalIgnoreCase);
        }
        return false;
        */
    }
    
    // Helper method to format plugin name (e.g., "WhiteboardPlugin" -> "Whiteboard Plugin")
    private string FormatPluginName(string typeName)
    {
        // Remove "Plugin" suffix if present
        string name = typeName.EndsWith("Plugin", StringComparison.OrdinalIgnoreCase)
            ? typeName.Substring(0, typeName.Length - 6)
            : typeName;
        
        // Add spaces before capital letters (e.g., "WhiteBoard" -> "White Board")
        string formatted = string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
        
        return formatted;
    }
}

/// <summary>
/// Information about an available plugin file
/// </summary>
public class PluginFileInfo
{
    public string FileName { get; set; }
    public string Name { get; set; }
    public string FilePath { get; set; }
    public bool IsLoaded { get; set; }
    public string Type { get; set; }
    public string Hash { get; set; }
}

// ==================== Placeholder Plugin Classes ====================

/// <summary>
/// Base placeholder plugin class that implements IPlugin
/// </summary>
public abstract class PlaceholderPluginBase : IPlugin
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Version Version { get; set; }
    public string Author { get; set; }
    public string InfoUrl => $"https://henrikdennis.github.io/drocsid/plugins/{Id}";
    public PluginState State { get; protected set; } = PluginState.Uninitialized;

    public virtual Task InitializeAsync(IPluginContext context)
    {
        State = PluginState.Running;
        return Task.CompletedTask;
    }

    public virtual Task ShutdownAsync()
    {
        State = PluginState.Disabled;
        return Task.CompletedTask;
    }

    public virtual UiComponent GetSettingsView()
    {
        // Return a platform-agnostic UI component instead of a WPF UserControl
        return new UiComponent
        {
            Id = $"{Id}-settings",
            ComponentType = "SettingsView",
            Configuration = "{}",
            Properties = new Dictionary<string, string>
            {
                ["title"] = $"{Name} Settings"
            }
        };
    }
}

/// <summary>
/// Placeholder implementation for general plugins
/// </summary>
public class PlaceholderGeneralPlugin : PlaceholderPluginBase
{
}

/// <summary>
/// Placeholder implementation for communication plugins
/// </summary>
public class PlaceholderCommunicationPlugin : PlaceholderPluginBase, ICommunicationPlugin
{
    public IEnumerable<CommunicationMode> SupportedModes => new[] { CommunicationMode.Audio };

    public Task<UiComponent> JoinSessionAsync(string sessionId)
    {
        // Return a platform-agnostic UI component instead of a WPF UserControl
        var component = new UiComponent
        {
            Id = $"{Id}-session-{sessionId}",
            ComponentType = "CommunicationView",
            Configuration = $"{{ \"sessionId\": \"{sessionId}\" }}",
            Properties = new Dictionary<string, string>
            {
                ["mode"] = "join",
                ["plugin"] = Id
            }
        };
        
        return Task.FromResult(component);
    }

    public Task<UiComponent> StartSessionAsync(Guid channelId, CommunicationMode mode)
    {
        // Return a platform-agnostic UI component instead of a WPF UserControl
        var sessionId = Guid.NewGuid().ToString();
        var component = new UiComponent
        {
            Id = $"{Id}-channel-{channelId}",
            ComponentType = "CommunicationView",
            Configuration = $"{{ \"channelId\": \"{channelId}\", \"mode\": \"{mode}\", \"sessionId\": \"{sessionId}\" }}",
            Properties = new Dictionary<string, string>
            {
                ["mode"] = "start",
                ["plugin"] = Id
            }
        };
        
        return Task.FromResult(component);
    }

    public Task EndSessionAsync(string sessionId)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Placeholder implementation for collaboration plugins
/// </summary>
public class PlaceholderCollaborationPlugin : PlaceholderPluginBase, ICollaborationPlugin
{
    public Task<UiComponent> JoinCollaborationAsync(string sessionId)
    {
        // Return a platform-agnostic UI component instead of a WPF UserControl
        var component = new UiComponent
        {
            Id = $"{Id}-collaboration-{sessionId}",
            ComponentType = "CollaborationView",
            Configuration = $"{{ \"sessionId\": \"{sessionId}\" }}",
            Properties = new Dictionary<string, string>
            {
                ["mode"] = "join",
                ["plugin"] = Id
            }
        };
        
        return Task.FromResult(component);
    }

    public Task<UiComponent> StartCollaborationAsync(Guid channelId)
    {
        // Return a platform-agnostic UI component instead of a WPF UserControl
        var sessionId = Guid.NewGuid().ToString();
        var component = new UiComponent
        {
            Id = $"{Id}-channel-{channelId}",
            ComponentType = "CollaborationView",
            Configuration = $"{{ \"channelId\": \"{channelId}\", \"sessionId\": \"{sessionId}\" }}",
            Properties = new Dictionary<string, string>
            {
                ["mode"] = "start",
                ["plugin"] = Id
            }
        };
        
        return Task.FromResult(component);
    }

    public Task EndCollaborationAsync(string sessionId)
    {
        return Task.CompletedTask;
    }
}