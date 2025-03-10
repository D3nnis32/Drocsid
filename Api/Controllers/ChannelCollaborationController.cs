using Drocsid.HenrikDennis2025.Api.Services;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

/// <summary>
/// Controller for channel collaboration sessions (whiteboard, etc.)
/// </summary>
[ApiController]
[Route("api/channels/{channelId}/collaboration")]
[Authorize]
public class ChannelCollaborationController : BaseController
{
    private readonly PluginManagerService _pluginManager;
    private readonly ILogger<ChannelCollaborationController> _logger;
    private readonly Dictionary<string, string> _activeSessionPlugins = new();

    public ChannelCollaborationController(
        PluginManagerService pluginManager,
        ILogger<ChannelCollaborationController> logger)
        : base(logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;
    }

    /// <summary>
    /// Start a collaboration session using the specified plugin
    /// </summary>
    [HttpPost("{pluginId}/start")]
    public async Task<IActionResult> StartCollaborationSession(Guid channelId, string pluginId)
    {
        try
        {
            _logger.LogInformation($"Starting collaboration session for channel {channelId} with plugin {pluginId}");

            // Get the plugin
            var plugin = _pluginManager.GetPlugin(pluginId) as ICollaborationPlugin;
            if (plugin == null)
            {
                _logger.LogWarning($"Plugin {pluginId} not found or is not a collaboration plugin");
                return NotFound($"Collaboration plugin {pluginId} not found");
            }

            // Start the session
            var uiComponent = await plugin.StartCollaborationAsync(channelId);
            
            // Get the session ID from the component configuration
            var sessionId = ExtractSessionId(uiComponent.Configuration);
            
            // Store the session mapping
            if (!string.IsNullOrEmpty(sessionId))
            {
                _activeSessionPlugins[sessionId] = pluginId;
            }

            // Return session info
            return Ok(new PluginSessionInfo
            {
                SessionId = sessionId,
                UiComponent = uiComponent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error starting collaboration session for channel {channelId} with plugin {pluginId}");
            return StatusCode(500, $"Error starting collaboration session: {ex.Message}");
        }
    }

    /// <summary>
    /// Join an existing collaboration session
    /// </summary>
    [HttpPost("session/{sessionId}/join")]
    public async Task<IActionResult> JoinCollaborationSession(Guid channelId, string sessionId)
    {
        try
        {
            _logger.LogInformation($"Joining collaboration session {sessionId} for channel {channelId}");

            // Find the plugin for this session
            if (!_activeSessionPlugins.TryGetValue(sessionId, out var pluginId))
            {
                _logger.LogWarning($"No plugin found for session {sessionId}");
                return NotFound($"Session {sessionId} not found");
            }

            // Get the plugin
            var plugin = _pluginManager.GetPlugin(pluginId) as ICollaborationPlugin;
            if (plugin == null)
            {
                _logger.LogWarning($"Plugin {pluginId} not found or is not a collaboration plugin");
                return NotFound($"Collaboration plugin {pluginId} not found");
            }

            // Join the session
            var uiComponent = await plugin.JoinCollaborationAsync(sessionId);

            // Return session info
            return Ok(new PluginSessionInfo
            {
                SessionId = sessionId,
                UiComponent = uiComponent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error joining collaboration session {sessionId} for channel {channelId}");
            return StatusCode(500, $"Error joining collaboration session: {ex.Message}");
        }
    }

    /// <summary>
    /// End a collaboration session
    /// </summary>
    [HttpPost("session/{sessionId}/end")]
    public async Task<IActionResult> EndCollaborationSession(Guid channelId, string sessionId)
    {
        try
        {
            _logger.LogInformation($"Ending collaboration session {sessionId} for channel {channelId}");

            // Find the plugin for this session
            if (!_activeSessionPlugins.TryGetValue(sessionId, out var pluginId))
            {
                _logger.LogWarning($"No plugin found for session {sessionId}");
                return NotFound($"Session {sessionId} not found");
            }

            // Get the plugin
            var plugin = _pluginManager.GetPlugin(pluginId) as ICollaborationPlugin;
            if (plugin == null)
            {
                _logger.LogWarning($"Plugin {pluginId} not found or is not a collaboration plugin");
                return NotFound($"Collaboration plugin {pluginId} not found");
            }

            // End the session
            await plugin.EndCollaborationAsync(sessionId);
            
            // Remove the session mapping
            _activeSessionPlugins.Remove(sessionId);

            return Ok(new { Message = $"Session {sessionId} ended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error ending collaboration session {sessionId} for channel {channelId}");
            return StatusCode(500, $"Error ending collaboration session: {ex.Message}");
        }
    }

    /// <summary>
    /// Extract session ID from component configuration
    /// </summary>
    private string ExtractSessionId(string configuration)
    {
        try
        {
            // This is a simple implementation that assumes the configuration is JSON
            // and contains a sessionId property
            var configObj = System.Text.Json.JsonDocument.Parse(configuration);
            if (configObj.RootElement.TryGetProperty("sessionId", out var sessionIdElement))
            {
                return sessionIdElement.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting session ID from configuration");
        }
        
        // Generate a fallback session ID if we couldn't extract one
        return $"session-{Guid.NewGuid()}";
    }
}