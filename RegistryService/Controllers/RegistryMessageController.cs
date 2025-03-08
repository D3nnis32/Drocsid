using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
[Route("api/registry/messages")]
public class RegistryMessageController : ControllerBase
{
    private readonly IMessageRegistry _messageRegistry;
    private readonly INodeRegistry _nodeRegistry;
    private readonly IChannelRegistry _channelRegistry;
    private readonly ILogger<RegistryMessageController> _logger;

    public RegistryMessageController(
        IMessageRegistry messageRegistry,
        INodeRegistry nodeRegistry,
        IChannelRegistry channelRegistry,
        ILogger<RegistryMessageController> logger)
    {
        _messageRegistry = messageRegistry ?? throw new ArgumentNullException(nameof(messageRegistry));
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _channelRegistry = channelRegistry ?? throw new ArgumentNullException(nameof(channelRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sync a message from a node to the registry
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncMessage([FromBody] MessageSyncRequest request)
    {
        try
        {
            _logger.LogInformation("Syncing message from node {NodeId}: {MessageId}", 
                request.OriginNodeId, request.Message.Id);
            
            // Register the message in the registry
            var success = await _messageRegistry.RegisterMessageAsync(request.Message);
            if (!success)
            {
                return BadRequest("Failed to register message");
            }
            
            // Record which node has this message
            await _messageRegistry.AddMessageLocationAsync(request.Message.Id, request.OriginNodeId);
            
            // Get all nodes that host this channel
            var channelNodes = await _channelRegistry.GetChannelNodesAsync(request.Message.ChannelId);
            
            // Filter out the origin node
            var nodesToSync = channelNodes
                .Where(nodeId => nodeId != request.OriginNodeId)
                .ToList();
            
            if (nodesToSync.Any())
            {
                // Get node information
                var nodes = await _nodeRegistry.GetNodesByIdsAsync(nodesToSync);
                var healthyNodes = nodes.Where(n => n.IsHealthy).ToList();
                
                // Forward the message to all other nodes that host this channel
                foreach (var node in healthyNodes)
                {
                    await ForwardMessageToNodeAsync(node, request.Message);
                }
                
                _logger.LogInformation("Forwarded message {MessageId} to {NodeCount} nodes", 
                    request.Message.Id, healthyNodes.Count);
            }

            return Ok(new { MessageId = request.Message.Id, SyncedNodes = nodesToSync.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing message");
            return StatusCode(500, "Internal server error during message sync");
        }
    }

    /// <summary>
    /// Get all messages for a channel since a specific time
    /// </summary>
    [HttpGet("channels/{channelId}")]
    public async Task<IActionResult> GetChannelMessages(Guid channelId, [FromQuery] DateTime since)
    {
        try
        {
            var messages = await _messageRegistry.GetChannelMessagesAsync(channelId, since);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages for channel {ChannelId}", channelId);
            return StatusCode(500, "Internal server error while retrieving messages");
        }
    }

    /// <summary>
    /// Forward a message to a specific node
    /// </summary>
    private async Task ForwardMessageToNodeAsync(NodeInfo node, Message message)
    {
        try
        {
            var client = new HttpClient();
            var url = $"{node.Endpoint.TrimEnd('/')}/api/messages/sync";
            
            var response = await client.PostAsJsonAsync(url, message);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to forward message to node {NodeId}. Status: {Status}", 
                    node.Id, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding message to node {NodeId}", node.Id);
        }
    }
}