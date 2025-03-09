using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
    [Route("api/registry/files")]
    public class FileController : ControllerBase
    {
        private readonly IFileRegistry _fileRegistry;
        private readonly INodeRegistry _nodeRegistry;
        private readonly ILogger<FileController> _logger;

        public FileController(
            IFileRegistry fileRegistry, 
            INodeRegistry nodeRegistry,
            ILogger<FileController> logger)
        {
            _fileRegistry = fileRegistry ?? throw new ArgumentNullException(nameof(fileRegistry));
            _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterFile(FileRegistrationRequest request)
        {
            try
            {
                _logger.LogInformation("Registering file: {FileName}, Size: {Size} bytes", 
                    request.FileName, request.Size);
                
                var fileStorage = new FileStorage
                {
                    FileId = request.FileId,
                    FileName = request.FileName,
                    ContentType = request.ContentType,
                    Size = request.Size,
                    NodeIds = request.NodeIds,
                    Checksum = request.Checksum,
                    Metadata = request.Metadata
                };
                
                // Use the new method that handles FileStorage objects
                bool success = await _fileRegistry.RegisterFileStorageAsync(fileStorage);
                
                if (!success)
                {
                    return BadRequest("Failed to register file");
                }
                
                return Ok(new { FileId = fileStorage.FileId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering file: {FileName}", request.FileName);
                return StatusCode(500, "An error occurred while registering the file");
            }
        }
        
        [HttpGet("{fileId}/locations")]
        public async Task<IActionResult> GetFileLocations(string fileId)
        {
            try
            {
                var fileStorage = await _fileRegistry.GetFileInfoAsync(fileId);
                if (fileStorage == null)
                {
                    return NotFound();
                }
                
                // Get healthy nodes that have this file
                var nodes = await _nodeRegistry.GetNodesByIdsAsync(fileStorage.NodeIds);
                var healthyNodes = nodes.Where(n => n.IsHealthy).ToList();
                
                return Ok(new
                {
                    FileId = fileStorage.FileId,
                    FileName = fileStorage.FileName,
                    Locations = healthyNodes.Select(n => new
                    {
                        NodeId = n.Id,
                        Endpoint = n.Endpoint,
                        Region = n.Region
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file locations: {FileId}", fileId);
                return StatusCode(500, "An error occurred while retrieving file locations");
            }
        }
        
        [HttpGet("locate")]
        public async Task<IActionResult> LocateBestNodeForFile(string fileId, string clientRegion = null)
        {
            try
            {
                var fileStorage = await _fileRegistry.GetFileInfoAsync(fileId);
                if (fileStorage == null)
                {
                    return NotFound();
                }
                
                var nodes = await _nodeRegistry.GetNodesByIdsAsync(fileStorage.NodeIds);
                var healthyNodes = nodes.Where(n => n.IsHealthy).ToList();
                
                if (!healthyNodes.Any())
                {
                    return StatusCode(503, "File exists but no healthy nodes are available");
                }
                
                // Select best node based on region and load
                var bestNode = healthyNodes
                    .OrderBy(n => clientRegion != null ? (n.Region == clientRegion ? 0 : 1) : 0)
                    .ThenBy(n => n.CurrentLoad)
                    .First();
                    
                return Ok(new
                {
                    NodeId = bestNode.Id,
                    Endpoint = bestNode.Endpoint
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error locating best node for file: {FileId}", fileId);
                return StatusCode(500, "An error occurred while locating the best node");
            }
        }
        
        [HttpPost("{fileId}/replicate")]
        public async Task<IActionResult> TriggerReplication(string fileId, [FromQuery] int targetReplicationFactor = 2)
        {
            try
            {
                var fileStorage = await _fileRegistry.GetFileInfoAsync(fileId);
                if (fileStorage == null)
                {
                    return NotFound();
                }
                
                // Check if we need to replicate at all
                if (fileStorage.NodeIds.Count >= targetReplicationFactor)
                {
                    return Ok(new { Message = "File already meets target replication factor" });
                }
                
                // Get healthy nodes that have this file
                var existingNodes = await _nodeRegistry.GetNodesByIdsAsync(fileStorage.NodeIds);
                var healthyExistingNodes = existingNodes.Where(n => n.IsHealthy).ToList();
                
                if (!healthyExistingNodes.Any())
                {
                    return StatusCode(503, "Cannot replicate as no healthy source nodes available");
                }
                
                // Select additional nodes for replication
                var additionalNodesNeeded = targetReplicationFactor - fileStorage.NodeIds.Count;
                var availableNodes = await _nodeRegistry.GetAvailableNodesAsync();
                var candidateNodes = availableNodes
                    .Where(n => !fileStorage.NodeIds.Contains(n.Id))
                    .OrderByDescending(n => n.AvailableSpace)
                    .Take(additionalNodesNeeded)
                    .ToList();
                
                if (candidateNodes.Count < additionalNodesNeeded)
                {
                    return StatusCode(503, "Insufficient available nodes for target replication factor");
                }
                
                // Add the new node locations to the file registry
                foreach (var node in candidateNodes)
                {
                    await _fileRegistry.AddFileLocationAsync(fileId, node.Id);
                }
                
                // Return replication plan
                return Ok(new 
                {
                    FileId = fileId,
                    SourceNode = healthyExistingNodes.First().Id,
                    TargetNodes = candidateNodes.Select(n => n.Id).ToList(),
                    Message = "Replication task has been queued"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering replication for file: {FileId}", fileId);
                return StatusCode(500, "An error occurred while triggering replication");
            }
        }
    }