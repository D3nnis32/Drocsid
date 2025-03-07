using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
[Route("api/registry/nodes")]
public class NodeController : ControllerBase
{
    private readonly INodeRegistry _nodeRegistry;

    public NodeController(INodeRegistry nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterNode(NodeRegistrationRequest request)
    {
        var nodeId = await _nodeRegistry.RegisterNodeAsync(new Node
        {
            Endpoint = request.Endpoint,
            Region = request.Region,
            Capacity = request.CapacityBytes,
            IsHealthy = true,
            LastHeartbeat = DateTime.UtcNow
        });
        
        return Ok(new { NodeId = nodeId });
    }
    
    [HttpPost("{nodeId}/heartbeat")]
    public async Task<IActionResult> Heartbeat(string nodeId, NodeHeartbeatRequest request)
    {
        await _nodeRegistry.UpdateNodeStatusAsync(nodeId, new NodeStatus
        {
            IsHealthy = true,
            CurrentLoad = request.CurrentLoad,
            AvailableSpace = request.AvailableSpace,
            LastUpdated = DateTime.UtcNow
        });
        
        return Ok();
    }
    
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableNodes([FromQuery] int count = 2)
    {
        var nodes = await _nodeRegistry.GetAvailableNodesAsync(count);
        return Ok(nodes);
    }
}