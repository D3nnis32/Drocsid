using System.Windows;
using Drocsid.HenrikDennis2025.Core.Plugins.Security;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Core.Plugins;

/// <summary>
    /// Factory class for creating and initializing the plugin system
    /// </summary>
    public static class PluginFactory
    {
        private static PluginManager _pluginManager;
        private static bool _isInitialized;
        
        /// <summary>
        /// Initialize the plugin system
        /// </summary>
        public static async Task InitializeAsync(string apiBaseUrl, string verificationEndpoint = null, VerificationLevel verificationLevel = VerificationLevel.Optional)
        {
            if (_isInitialized)
            {
                return;
            }
            
            // Create system-wide plugin logger
            var systemLogger = new PluginLogger("SYSTEM");
            
            // Create event manager
            var eventManager = new PluginEventManager(systemLogger);
            
            // Create UI service
            var uiService = new UIService(systemLogger);
            
            // Create user session service
            var userSession = new UserSessionService(apiBaseUrl, systemLogger);
            
            // Create plugin context for plugin initialization
            var pluginContext = new PluginContext(
                new PluginConfiguration("SYSTEM"),
                systemLogger,
                userSession,
                uiService,
                eventManager);
            
            // Create plugin manager
            _pluginManager = new PluginManager(pluginContext, verificationEndpoint, verificationLevel);
            
            // Set up verification required handler
            _pluginManager.VerificationRequired += OnVerificationRequired;
            
            // Discover and load plugins
            await _pluginManager.DiscoverPluginsAsync();
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// Handle verification required event
        /// </summary>
        private static void OnVerificationRequired(object sender, VerificationRequiredEventArgs e)
        {
            // Show a warning dialog to the user
            var result = MessageBox.Show(
                $"The plugin at {e.PluginFilePath} could not be verified.\n\n" +
                $"Plugin ID: {e.VerificationResult.PluginId}\n" +
                $"Hash: {e.VerificationResult.Hash}\n\n" +
                "This plugin may be unsafe. Do you want to install it anyway?",
                "Plugin Verification Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            // Set the result based on user response
            e.AllowInstall = result == MessageBoxResult.Yes;
        }
        
        /// <summary>
        /// Get the plugin manager instance
        /// </summary>
        public static PluginManager GetPluginManager()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Plugin system not initialized. Call InitializeAsync first.");
            }
            
            return _pluginManager;
        }
        
        /// <summary>
        /// Create a plugin context for a specific plugin
        /// </summary>
        public static IPluginContext CreatePluginContext(string pluginId)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Plugin system not initialized. Call InitializeAsync first.");
            }
            
            // Get existing services from the plugin manager's context
            var managerContext = _pluginManager as IPluginContext;
            
            // Create plugin-specific logger and configuration
            var logger = new PluginLogger(pluginId);
            var configuration = new PluginConfiguration(pluginId);
            
            // Create plugin context with shared services
            return new PluginContext(
                configuration,
                logger,
                managerContext.UserSession,
                managerContext.UIService,
                managerContext.EventManager);
        }
        
        /// <summary>
        /// Shutdown the plugin system
        /// </summary>
        public static async Task ShutdownAsync()
        {
            if (!_isInitialized)
            {
                return;
            }
            
            var manager = GetPluginManager();
            
            // Unload all plugins
            foreach (var plugin in manager.AvailablePlugins)
            {
                try
                {
                    await plugin.ShutdownAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error shutting down plugin {plugin.Id}: {ex.Message}");
                }
            }
            
            _isInitialized = false;
            _pluginManager = null;
        }
        
        /// <summary>
        /// Set the plugin verification level
        /// </summary>
        public static void SetVerificationLevel(VerificationLevel level)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Plugin system not initialized. Call InitializeAsync first.");
            }
            
            _pluginManager.VerificationLevel = level;
        }
        
        /// <summary>
        /// Verify all installed plugins
        /// </summary>
        public static async Task<Dictionary<string, VerificationResult>> VerifyAllPluginsAsync()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Plugin system not initialized. Call InitializeAsync first.");
            }
            
            return await _pluginManager.VerifyAllPluginsAsync();
        }
    }