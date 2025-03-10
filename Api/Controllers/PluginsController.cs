using Drocsid.HenrikDennis2025.Api.Services;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

/// <summary>
    /// Controller for managing plugins
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PluginsController : BaseController
    {
        private readonly PluginManagerService _pluginManager;
        private readonly ILogger<PluginsController> _logger;

        public PluginsController(
            PluginManagerService pluginManager,
            ILogger<PluginsController> logger) 
            : base(logger)
        {
            _pluginManager = pluginManager;
            _logger = logger;
        }

        /// <summary>
        /// Get all available plugins
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<PluginInfo>> GetAvailablePlugins()
        {
            try
            {
                var plugins = _pluginManager.GetLoadedPlugins()
                    .Select(p => new PluginInfo
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Version = p.Version.ToString(),
                        Author = p.Author,
                        State = p.State.ToString(),
                        Type = GetPluginType(p)
                    })
                    .ToList();

                return Ok(plugins);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available plugins");
                return StatusCode(500, "Error retrieving plugins");
            }
        }

        /// <summary>
        /// Get details about a specific plugin
        /// </summary>
        [HttpGet("{pluginId}")]
        public ActionResult<PluginInfo> GetPlugin(string pluginId)
        {
            try
            {
                var plugin = _pluginManager.GetPlugin(pluginId);
                if (plugin == null)
                {
                    return NotFound($"Plugin with ID {pluginId} not found");
                }

                var pluginInfo = new PluginInfo
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Description = plugin.Description,
                    Version = plugin.Version.ToString(),
                    Author = plugin.Author,
                    State = plugin.State.ToString(),
                    Type = GetPluginType(plugin)
                };

                return Ok(pluginInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting plugin {pluginId}");
                return StatusCode(500, "Error retrieving plugin details");
            }
        }

        /// <summary>
        /// Load a plugin from the configured plugins directory
        /// </summary>
        [HttpPost("load")]
        public async Task<IActionResult> LoadPlugin([FromQuery] string pluginName)
        {
            try
            {
                if (string.IsNullOrEmpty(pluginName))
                {
                    return BadRequest("Plugin name is required");
                }

                // Get the plugins directory path from configuration
                var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                if (!Directory.Exists(pluginsDir))
                {
                    return BadRequest("Plugins directory not found");
                }

                // Find the plugin DLL
                var pluginPath = Path.Combine(pluginsDir, $"{pluginName}.dll");
                if (!System.IO.File.Exists(pluginPath))
                {
                    return NotFound($"Plugin file not found: {pluginName}.dll");
                }

                // Load the plugin
                var plugin = await _pluginManager.LoadPluginAsync(pluginPath);
                
                var pluginInfo = new PluginInfo
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Description = plugin.Description,
                    Version = plugin.Version.ToString(),
                    Author = plugin.Author,
                    State = plugin.State.ToString(),
                    Type = GetPluginType(plugin)
                };

                return Ok(pluginInfo);
            }
            catch (SecurityException ex)
            {
                _logger.LogError(ex, $"Security error loading plugin {pluginName}");
                return BadRequest($"Security verification failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading plugin {pluginName}");
                return StatusCode(500, $"Error loading plugin: {ex.Message}");
            }
        }

        /// <summary>
        /// Unload a plugin
        /// </summary>
        [HttpPost("{pluginId}/unload")]
        public async Task<IActionResult> UnloadPlugin(string pluginId)
        {
            try
            {
                await _pluginManager.UnloadPluginAsync(pluginId);
                return Ok(new { Message = $"Plugin {pluginId} unloaded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unloading plugin {pluginId}");
                return StatusCode(500, $"Error unloading plugin: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the available communication plugins for a channel
        /// </summary>
        [HttpGet("channel/{channelId}/communication")]
        public ActionResult<IEnumerable<PluginInfo>> GetChannelCommunicationPlugins(Guid channelId)
        {
            try
            {
                // Get communication plugins
                var communicationPlugins = _pluginManager.GetLoadedPlugins()
                    .Where(p => p is ICommunicationPlugin)
                    .Select(p => new PluginInfo
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Version = p.Version.ToString(),
                        Author = p.Author,
                        State = p.State.ToString(),
                        Type = "Communication"
                    })
                    .ToList();

                return Ok(communicationPlugins);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting communication plugins for channel {channelId}");
                return StatusCode(500, "Error retrieving communication plugins");
            }
        }

        /// <summary>
        /// Get the available collaboration plugins for a channel
        /// </summary>
        [HttpGet("channel/{channelId}/collaboration")]
        public ActionResult<IEnumerable<PluginInfo>> GetChannelCollaborationPlugins(Guid channelId)
        {
            try
            {
                // Get collaboration plugins
                var collaborationPlugins = _pluginManager.GetLoadedPlugins()
                    .Where(p => p is ICollaborationPlugin)
                    .Select(p => new PluginInfo
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Version = p.Version.ToString(),
                        Author = p.Author,
                        State = p.State.ToString(),
                        Type = "Collaboration"
                    })
                    .ToList();

                return Ok(collaborationPlugins);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting collaboration plugins for channel {channelId}");
                return StatusCode(500, "Error retrieving collaboration plugins");
            }
        }

        /// <summary>
        /// Determine the type of plugin based on interfaces it implements
        /// </summary>
        private string GetPluginType(IPlugin plugin)
        {
            if (plugin is ICommunicationPlugin)
            {
                return "Communication";
            }
            else if (plugin is ICollaborationPlugin)
            {
                return "Collaboration";
            }
            else
            {
                return "General";
            }
        }
    }

    /// <summary>
    /// Model for plugin information
    /// </summary>
    public class PluginInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string State { get; set; }
        public string Type { get; set; }
    }