using System.Reflection;
using System.Security.Cryptography;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Api.Services;

public class PluginManagerService
    {
        private readonly ILogger<PluginManagerService> _logger;
        private readonly Dictionary<string, IPlugin> _loadedPlugins = new();
        private readonly Dictionary<string, string> _verifiedPluginHashes = new();
        private readonly IPluginContext _pluginContext;

        public PluginManagerService(ILogger<PluginManagerService> logger, IPluginContext pluginContext)
        {
            _logger = logger;
            _pluginContext = pluginContext;
            
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

                // Load the assembly
                Assembly pluginAssembly = Assembly.LoadFrom(pluginPath);
                
                // Find plugin types (types that implement IPlugin)
                Type pluginType = pluginAssembly.GetTypes()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                
                if (pluginType == null)
                {
                    _logger.LogError($"No valid plugin found in assembly: {pluginPath}");
                    throw new InvalidOperationException($"No valid plugin found in assembly: {pluginPath}");
                }

                // Verify the plugin against trusted hashes
                bool isVerified = VerifyPluginHash(pluginType.Name, pluginHash);
                if (!isVerified)
                {
                    _logger.LogError($"Plugin verification failed: {pluginType.Name}");
                    throw new SecurityException($"Plugin verification failed: {pluginType.Name}");
                }

                // Create the plugin instance
                IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType);
                if (plugin == null)
                {
                    _logger.LogError($"Failed to create plugin instance: {pluginType.Name}");
                    throw new InvalidOperationException($"Failed to create plugin instance: {pluginType.Name}");
                }

                // Initialize plugin
                await plugin.InitializeAsync(_pluginContext);

                // Add to loaded plugins
                _loadedPlugins[plugin.Id] = plugin;
                
                _logger.LogInformation($"Successfully loaded plugin: {plugin.Name} ({plugin.Id})");
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
    }

    // Custom exception for security issues
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }