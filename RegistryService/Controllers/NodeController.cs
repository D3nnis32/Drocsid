using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Drocsid.HenrikDennis2025.RegistryService.Services;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
    [Route("api/nodes")]
    public class NodeController : ControllerBase
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly ILogger<NodeController> _logger;

        public NodeController(INodeRegistry nodeRegistry, ILogger<NodeController> logger)
        {
            _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Registers a new storage node with the registry
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(NodeRegistrationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterNode([FromBody] NodeRegistrationRequest request)
        {
            _logger.LogInformation("Received node registration request: {@Request}", request);
    
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                _logger.LogInformation("Registering node with hostname: {Hostname}, endpoint: {Endpoint}", 
                    request.Hostname, request.Endpoint);

                // Use the helper to create a properly initialized node
                var node = NodeRegistryInitialization.InitializeNode(
                    id: string.IsNullOrEmpty(request.Id) ? Guid.NewGuid().ToString() : request.Id,
                    hostname: request.Hostname,
                    endpoint: request.Endpoint,
                    region: request.Region,
                    totalStorage: request.TotalStorage
                );

                bool success = await _nodeRegistry.RegisterNodeAsync(node);
        
                if (!success)
                {
                    return BadRequest("Failed to register node");
                }

                return Ok(new NodeRegistrationResponse
                {
                    NodeId = node.Id,
                    Status = "Registered"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering node");
                return StatusCode(500, "Internal server error during node registration");
            }
        }

        /// <summary>
        /// Updates a node's heartbeat to indicate it's still alive
        /// </summary>
        [HttpPut("{nodeId}/heartbeat")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateHeartbeat(string nodeId, [FromBody] NodeHeartbeatRequest request)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return BadRequest("Node ID is required");
            }

            try
            {
                _logger.LogDebug("Received heartbeat from node {NodeId}", nodeId);

                var node = await _nodeRegistry.GetNodeAsync(nodeId);
                if (node == null)
                {
                    _logger.LogWarning("Heartbeat received for unknown node {NodeId}", nodeId);
                    return NotFound($"Node with ID {nodeId} not found");
                }

                // Update node status and metrics
                node.LastSeen = DateTime.UtcNow;
                node.Status = request.Status;
                
                // Ensure the status LastUpdated time is set
                if (node.Status.LastUpdated == default)
                {
                    node.Status.LastUpdated = DateTime.UtcNow;
                }

                bool success = await _nodeRegistry.UpdateNodeAsync(node);
                if (!success)
                {
                    return StatusCode(500, "Failed to update node heartbeat");
                }

                return Ok(new { Status = "Heartbeat recorded" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating heartbeat for node {NodeId}", nodeId);
                return StatusCode(500, "Internal server error during heartbeat update");
            }
        }

        /// <summary>
        /// Gets all active nodes in the registry
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<StorageNode>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllNodes([FromQuery] bool includeOffline = false)
        {
            try
            {
                var nodes = await _nodeRegistry.GetAllNodesAsync(includeOffline);
                return Ok(nodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving nodes");
                return StatusCode(500, "Internal server error while retrieving nodes");
            }
        }

        /// <summary>
        /// Gets a specific node by ID
        /// </summary>
        [HttpGet("{nodeId}")]
        [ProducesResponseType(typeof(StorageNode), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetNode(string nodeId)
        {
            try
            {
                var node = await _nodeRegistry.GetNodeAsync(nodeId);
                if (node == null)
                {
                    return NotFound($"Node with ID {nodeId} not found");
                }

                return Ok(node);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving node {NodeId}", nodeId);
                return StatusCode(500, "Internal server error while retrieving node");
            }
        }
    }