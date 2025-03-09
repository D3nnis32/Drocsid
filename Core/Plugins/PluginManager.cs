using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Drocsid.HenrikDennis2025.Core.Plugins.Security;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
    /// Manages plugin discovery, loading, and lifecycle
    /// </summary>
    public class PluginManager : INotifyPropertyChanged
    {
        private readonly IPluginContext _pluginContext;
        private readonly string _pluginsDirectory;
        private readonly Dictionary<string, PluginInfo> _loadedPlugins = new Dictionary<string, PluginInfo>();
        private readonly ObservableCollection<IPlugin> _availablePlugins = new ObservableCollection<IPlugin>();
        private readonly List<Assembly> _loadedAssemblies = new List<Assembly>();
        private readonly PluginVerifier _pluginVerifier;

        // Directory inside application directory where plugins are stored
        private const string DefaultPluginsFolder = "Plugins";
        
        // Directory where downloaded plugins are temporarily stored
        private const string DownloadDirectory = "Downloads";
        
        // Trusted keys file path
        private const string TrustedKeysFile = "trusted_plugins.json";

        /// <summary>
        /// Event raised when a plugin is loaded
        /// </summary>
        public event EventHandler<IPlugin> PluginLoaded;
        
        /// <summary>
        /// Event raised when a plugin is unloaded
        /// </summary>
        public event EventHandler<string> PluginUnloaded;
        
        /// <summary>
        /// Event raised when verification is required for a plugin
        /// </summary>
        public event EventHandler<VerificationRequiredEventArgs> VerificationRequired;

        /// <summary>
        /// Collection of all available plugins
        /// </summary>
        public ReadOnlyObservableCollection<IPlugin> AvailablePlugins { get; }
        
        /// <summary>
        /// Plugin verification level
        /// </summary>
        public VerificationLevel VerificationLevel
        {
            get => _pluginVerifier.VerificationLevel;
            set
            {
                if (_pluginVerifier.VerificationLevel != value)
                {
                    _pluginVerifier.VerificationLevel = value;
                    OnPropertyChanged(nameof(VerificationLevel));
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public PluginManager(IPluginContext pluginContext, string verificationEndpoint = null, VerificationLevel verificationLevel = VerificationLevel.Optional)
        {
            _pluginContext = pluginContext ?? throw new ArgumentNullException(nameof(pluginContext));
            
            // Get the plugins directory (create if it doesn't exist)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _pluginsDirectory = Path.Combine(baseDir, DefaultPluginsFolder);
            
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
            }
            
            // Create downloads directory
            string downloadDir = Path.Combine(_pluginsDirectory, DownloadDirectory);
            if (!Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }
            
            // Create plugin verifier
            string trustedKeysPath = Path.Combine(_pluginsDirectory, TrustedKeysFile);
            _pluginVerifier = new PluginVerifier(
                verificationEndpoint ?? "https://drocsid.example.com/api/verify-plugin", 
                trustedKeysPath,
                verificationLevel,
                pluginContext.Logger);
            
            // Initialize available plugins collection
            AvailablePlugins = new ReadOnlyObservableCollection<IPlugin>(_availablePlugins);
        }

        /// <summary>
        /// Discover and load all available plugins
        /// </summary>
        public async Task DiscoverPluginsAsync()
        {
            _pluginContext.Logger.Info("Discovering plugins...");
            
            // Look for all DLL files in the plugins directory
            var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.AllDirectories);
            
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    // Verify the plugin before loading
                    var verificationResult = await _pluginVerifier.VerifyPluginAsync(pluginFile);
                    
                    if (verificationResult.IsVerified)
                    {
                        // Plugin is verified, load it
                        await LoadPluginFromFileAsync(pluginFile);
                    }
                    else if (verificationResult.Status == VerificationStatus.RequiresUserConfirmation)
                    {
                        // Ask user for confirmation
                        if (VerificationRequired != null)
                        {
                            var args = new VerificationRequiredEventArgs
                            {
                                PluginFilePath = pluginFile,
                                VerificationResult = verificationResult,
                                AllowInstall = false // Default to not allowing
                            };
                            
                            VerificationRequired(this, args);
                            
                            if (args.AllowInstall)
                            {
                                // User approved, load the plugin
                                await LoadPluginFromFileAsync(pluginFile);
                                
                                // Add to trusted plugins
                                _pluginVerifier.ManuallyTrustPlugin(pluginFile, "User Approved");
                            }
                            else
                            {
                                _pluginContext.Logger.Warning($"Plugin verification failed and user denied installation: {pluginFile}");
                            }
                        }
                        else
                        {
                            _pluginContext.Logger.Warning($"Plugin verification required but no handler registered: {pluginFile}");
                        }
                    }
                    else
                    {
                        _pluginContext.Logger.Warning($"Plugin verification failed: {pluginFile}. Status: {verificationResult.Status}");
                    }
                }
                catch (Exception ex)
                {
                    _pluginContext.Logger.Error($"Error loading plugin from {pluginFile}", ex);
                }
            }
            
            _pluginContext.Logger.Info($"Discovered {_availablePlugins.Count} plugins");
        }

        /// <summary>
        /// Load a plugin from a file
        /// </summary>
        private async Task LoadPluginFromFileAsync(string pluginFile)
        {
            try
            {
                // Load the assembly
                var pluginAssembly = Assembly.LoadFrom(pluginFile);
                _loadedAssemblies.Add(pluginAssembly);
                
                // Find all types that implement IPlugin
                var pluginTypes = pluginAssembly.GetTypes()
                    .Where(t => !t.IsAbstract && typeof(IPlugin).IsAssignableFrom(t))
                    .ToList();
                
                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        // Create an instance of the plugin
                        var plugin = (IPlugin)Activator.CreateInstance(pluginType);
                        
                        // Check if this plugin is already loaded
                        if (_loadedPlugins.ContainsKey(plugin.Id))
                        {
                            _pluginContext.Logger.Warning($"Plugin with ID {plugin.Id} is already loaded. Skipping.");
                            continue;
                        }
                        
                        // Initialize the plugin
                        await plugin.InitializeAsync(_pluginContext);
                        
                        // Add to loaded plugins
                        var pluginInfo = new PluginInfo
                        {
                            Plugin = plugin,
                            Assembly = pluginAssembly,
                            FilePath = pluginFile
                        };
                        
                        _loadedPlugins.Add(plugin.Id, pluginInfo);
                        _availablePlugins.Add(plugin);
                        
                        // Raise event
                        PluginLoaded?.Invoke(this, plugin);
                        
                        _pluginContext.Logger.Info($"Loaded plugin: {plugin.Name} (ID: {plugin.Id}, Version: {plugin.Version})");
                    }
                    catch (Exception ex)
                    {
                        _pluginContext.Logger.Error($"Error initializing plugin type {pluginType.FullName}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _pluginContext.Logger.Error($"Error loading plugin assembly from {pluginFile}", ex);
                throw;
            }
        }

        /// <summary>
        /// Unload a plugin by ID
        /// </summary>
        public async Task UnloadPluginAsync(string pluginId)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInfo))
            {
                _pluginContext.Logger.Warning($"Attempted to unload non-existent plugin with ID {pluginId}");
                return;
            }

            try
            {
                // Shutdown the plugin
                await pluginInfo.Plugin.ShutdownAsync();
                
                // Remove from collections
                _availablePlugins.Remove(pluginInfo.Plugin);
                _loadedPlugins.Remove(pluginId);
                
                // Raise event
                PluginUnloaded?.Invoke(this, pluginId);
                
                _pluginContext.Logger.Info($"Unloaded plugin: {pluginInfo.Plugin.Name} (ID: {pluginId})");
            }
            catch (Exception ex)
            {
                _pluginContext.Logger.Error($"Error unloading plugin with ID {pluginId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Install a plugin from a file
        /// </summary>
        public async Task<IPlugin> InstallPluginAsync(string pluginFilePath)
        {
            try
            {
                _pluginContext.Logger.Info($"Installing plugin from {pluginFilePath}");
                
                // Verify the plugin before installation
                var verificationResult = await _pluginVerifier.VerifyPluginAsync(pluginFilePath);
                
                if (!verificationResult.IsVerified && verificationResult.Status != VerificationStatus.NotRequired)
                {
                    if (verificationResult.Status == VerificationStatus.RequiresUserConfirmation)
                    {
                        // Ask user for confirmation
                        if (VerificationRequired != null)
                        {
                            var args = new VerificationRequiredEventArgs
                            {
                                PluginFilePath = pluginFilePath,
                                VerificationResult = verificationResult,
                                AllowInstall = false // Default to not allowing
                            };
                            
                            VerificationRequired(this, args);
                            
                            if (!args.AllowInstall)
                            {
                                throw new SecurityException("Plugin verification failed and user denied installation");
                            }
                            
                            // User approved, continue with installation
                            _pluginVerifier.ManuallyTrustPlugin(pluginFilePath, "User Approved");
                        }
                        else
                        {
                            throw new SecurityException("Plugin verification failed and requires user confirmation");
                        }
                    }
                    else
                    {
                        throw new SecurityException($"Plugin verification failed: {verificationResult.Status}");
                    }
                }
                
                // Generate a unique filename in the plugins directory
                string fileName = Path.GetFileName(pluginFilePath);
                string destPath = Path.Combine(_pluginsDirectory, fileName);
                
                // Copy the file to the plugins directory
                File.Copy(pluginFilePath, destPath, true);
                
                // Load the plugin
                await LoadPluginFromFileAsync(destPath);
                
                // Return the loaded plugin
                var plugin = _availablePlugins.LastOrDefault();
                if (plugin == null)
                {
                    throw new Exception("Failed to load plugin after installation");
                }
                
                return plugin;
            }
            catch (Exception ex)
            {
                _pluginContext.Logger.Error($"Error installing plugin from {pluginFilePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Download and install a plugin from a URL
        /// </summary>
        public async Task<IPlugin> DownloadAndInstallPluginAsync(string downloadUrl, Action<double> progressCallback = null)
        {
            try
            {
                _pluginContext.Logger.Info($"Downloading plugin from {downloadUrl}");
                
                // Create a unique filename for the download
                string fileName = $"plugin_{Guid.NewGuid()}.dll";
                string downloadPath = Path.Combine(_pluginsDirectory, DownloadDirectory, fileName);
                
                // Download the file
                using (var client = new System.Net.WebClient())
                {
                    if (progressCallback != null)
                    {
                        client.DownloadProgressChanged += (s, e) => 
                        {
                            progressCallback(e.ProgressPercentage / 100.0);
                        };
                    }
                    
                    await client.DownloadFileTaskAsync(new Uri(downloadUrl), downloadPath);
                }
                
                // Install the plugin
                var plugin = await InstallPluginAsync(downloadPath);
                
                // Clean up the download file
                try
                {
                    File.Delete(downloadPath);
                }
                catch
                {
                    // Ignore deletion errors
                }
                
                return plugin;
            }
            catch (Exception ex)
            {
                _pluginContext.Logger.Error($"Error downloading plugin from {downloadUrl}", ex);
                throw;
            }
        }

        /// <summary>
        /// Get a plugin by ID
        /// </summary>
        public IPlugin GetPlugin(string pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var pluginInfo))
            {
                return pluginInfo.Plugin;
            }
            
            return null;
        }

        /// <summary>
        /// Get all plugins of a specific type
        /// </summary>
        public IEnumerable<T> GetPlugins<T>() where T : IPlugin
        {
            return _availablePlugins.OfType<T>();
        }
        
        /// <summary>
        /// Check for plugin updates from a central repository
        /// </summary>
        public async Task CheckForUpdatesAsync()
        {
            // TODO: Implement update checking
            await Task.CompletedTask;
        }

        /// <summary>
        /// Update a plugin to a newer version
        /// </summary>
        public async Task UpdatePluginAsync(string pluginId, string downloadUrl)
        {
            try
            {
                if (!_loadedPlugins.TryGetValue(pluginId, out var pluginInfo))
                {
                    throw new ArgumentException($"Plugin with ID {pluginId} is not loaded");
                }
                
                // Unload the existing plugin
                await UnloadPluginAsync(pluginId);
                
                // Remember the old plugin file path
                string oldFilePath = pluginInfo.FilePath;
                
                // Download and install the new version
                await DownloadAndInstallPluginAsync(downloadUrl);
                
                // Remove the old plugin file after successful installation
                try
                {
                    File.Delete(oldFilePath);
                }
                catch (Exception ex)
                {
                    _pluginContext.Logger.Warning($"Could not delete old plugin file: {ex.Message}");
                }
                
                _pluginContext.Logger.Info($"Updated plugin {pluginId}");
            }
            catch (Exception ex)
            {
                _pluginContext.Logger.Error($"Error updating plugin {pluginId}", ex);
                throw;
            }
        }
        
        /// <summary>
        /// Manually trust a plugin file
        /// </summary>
        /// <param name="pluginFilePath">Path to the plugin file</param>
        /// <param name="publisherName">Name of the publisher</param>
        /// <returns>True if successful</returns>
        public bool TrustPlugin(string pluginFilePath, string publisherName)
        {
            return _pluginVerifier.ManuallyTrustPlugin(pluginFilePath, publisherName);
        }
        
        /// <summary>
        /// Verify a plugin file
        /// </summary>
        /// <param name="pluginFilePath">Path to the plugin file</param>
        /// <returns>Verification result</returns>
        public Task<VerificationResult> VerifyPluginAsync(string pluginFilePath)
        {
            return _pluginVerifier.VerifyPluginAsync(pluginFilePath);
        }
        
        /// <summary>
        /// Verify all plugins in the plugins directory
        /// </summary>
        /// <returns>Dictionary of plugin paths and verification results</returns>
        public Task<Dictionary<string, VerificationResult>> VerifyAllPluginsAsync()
        {
            return _pluginVerifier.VerifyAllPluginsAsync(_pluginsDirectory);
        }

        /// <summary>
        /// Information about a loaded plugin
        /// </summary>
        private class PluginInfo
        {
            /// <summary>
            /// The plugin instance
            /// </summary>
            public IPlugin Plugin { get; set; }
            
            /// <summary>
            /// The plugin assembly
            /// </summary>
            public Assembly Assembly { get; set; }
            
            /// <summary>
            /// Path to the plugin file
            /// </summary>
            public string FilePath { get; set; }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
    
    /// <summary>
    /// Exception thrown when there is a security issue with a plugin
    /// </summary>
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
        public SecurityException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// Event arguments for plugin verification required
    /// </summary>
    public class VerificationRequiredEventArgs : EventArgs
    {
        /// <summary>
        /// Path to the plugin file
        /// </summary>
        public string PluginFilePath { get; set; }
        
        /// <summary>
        /// Verification result
        /// </summary>
        public VerificationResult VerificationResult { get; set; }
        
        /// <summary>
        /// Whether to allow installation of the plugin
        /// </summary>
        public bool AllowInstall { get; set; }
    }