using Drocsid.HenrikDennis2025.Api.Services;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;
using Drocsid.HenrikDennis2025.PluginContracts.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

/// <summary>
/// Controller for channel communication sessions (voice chat, etc.)
/// </summary>
[ApiController]
[Route("api/channels/{channelId}/communication")]
[Authorize]
public class ChannelCommunicationController : BaseController
{
    private readonly PluginManagerService _pluginManager;
    private readonly ILogger<ChannelCommunicationController> _logger;
    private readonly Dictionary<string, string> _activeSessionPlugins = new();

    public ChannelCommunicationController(
        PluginManagerService pluginManager,
        ILogger<ChannelCommunicationController> logger)
        : base(logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;
    }

    /// <summary>
    /// Start a communication session using the specified plugin
    /// </summary>
    [HttpPost("{pluginId}/start")]
    public async Task<IActionResult> StartCommunicationSession(Guid channelId, string pluginId, [FromQuery] string mode = "Audio")
    {
        try
        {
            _logger.LogInformation($"Starting communication session for channel {channelId} with plugin {pluginId}, mode: {mode}");

            // Get the plugin
            var plugin = _pluginManager.GetPlugin(pluginId) as ICommunicationPlugin;
            if (plugin == null)
            {
                _logger.LogWarning($"Plugin {pluginId} not found or is not a communication plugin");
                return NotFound($"Communication plugin {pluginId} not found");
            }

            // Convert mode string to enum
            if (!Enum.TryParse<CommunicationMode>(mode, out var communicationMode))
            {
                _logger.LogWarning($"Invalid communication mode: {mode}");
                return BadRequest($"Invalid communication mode: {mode}");
            }

            // Start the session
            var uiComponent = await plugin.StartSessionAsync(channelId, communicationMode);
            
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
            _logger.LogError(ex, $"Error starting communication session for channel {channelId} with plugin {pluginId}");
            return StatusCode(500, $"Error starting communication session: {ex.Message}");
        }
    }

    /// <summary>
    /// Join an existing communication session
    /// </summary>
    [HttpPost("session/{sessionId}/join")]
    public async Task<IActionResult> JoinCommunicationSession(Guid channelId, string sessionId)
    {
        try
        {
            _logger.LogInformation($"Joining communication session {sessionId} for channel {channelId}");

            // Find the plugin for this session
            if (!_activeSessionPlugins.TryGetValue(sessionId, out var pluginId))
            {
                _logger.LogWarning($"No plugin found for session {sessionId}");
                return NotFound($"Session {sessionId} not found");
            }

            // Get the plugin
            var plugin = _pluginManager.GetPlugin(pluginId) as ICommunicationPlugin;
            if (plugin == null)
            {
                _logger.LogWarning($"Plugin {pluginId} not found or is not a communication plugin");
                return NotFound($"Communication plugin {pluginId} not found");
            }

            // Join the session
            var uiComponent = await plugin.JoinSessionAsync(sessionId);

            // Return session info
            return Ok(new PluginSessionInfo
            {
                SessionId = sessionId,
                UiComponent = uiComponent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error joining communication session {sessionId} for channel {channelId}");
            return StatusCode(500, $"Error joining communication session: {ex.Message}");
        }
    }

    /// <summary>
    /// End a communication session
    /// </summary>
    [HttpPost("session/{sessionId}/end")]
    public async Task<IActionResult> EndCommunicationSession(Guid channelId, string sessionId)
    {
        try
        {
            _logger.LogInformation($"Ending communication session {sessionId} for channel {channelId}");

            // Find the plugin for this session
            if (!_activeSessionPlugins.TryGetValue(sessionId, out var pluginId))
            {
                _logger.LogWarning($"No plugin found for session {sessionId}");
                return NotFound($"Session {sessionId} not found");
            }

            // Get the plugin
            var plugin = _pluginManager.GetPlugin(pluginId) as ICommunicationPlugin;
            if (plugin == null)
            {
                _logger.LogWarning($"Plugin {pluginId} not found or is not a communication plugin");
                return NotFound($"Communication plugin {pluginId} not found");
            }

            // End the session
            await plugin.EndSessionAsync(sessionId);
            
            // Remove the session mapping
            _activeSessionPlugins.Remove(sessionId);

            return Ok(new { Message = $"Session {sessionId} ended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error ending communication session {sessionId} for channel {channelId}");
            return StatusCode(500, $"Error ending communication session: {ex.Message}");
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

/// <summary>
/// Model for plugin session information
/// </summary>
public class PluginSessionInfo
{
    public string SessionId { get; set; }
    public UiComponent UiComponent { get; set; }
}