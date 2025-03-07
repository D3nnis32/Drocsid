using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
    /// Implementation of node health monitoring service
    /// </summary>
    public class NodeHealthMonitor : INodeHealthMonitor
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly IFileRegistry _fileRegistry;
        private readonly ILogger<NodeHealthMonitor> _logger;
        private readonly TimeSpan _unhealthyThreshold = TimeSpan.FromMinutes(1);
        private readonly int _minReplicationFactor = 2;

        public NodeHealthMonitor(
            INodeRegistry nodeRegistry, 
            IFileRegistry fileRegistry, 
            ILogger<NodeHealthMonitor> logger)
        {
            _nodeRegistry = nodeRegistry;
            _fileRegistry = fileRegistry;
            _logger = logger;
        }

        public async Task CheckNodeHealthAsync()
        {
            var nodes = await _nodeRegistry.GetAllNodesAsync();
            var now = DateTime.UtcNow;
            
            foreach (var node in nodes)
            {
                var previousState = node.IsHealthy;
                
                // Mark as unhealthy if we haven't received a heartbeat recently
                if (node.LastHeartbeat < now.Subtract(_unhealthyThreshold))
                {
                    node.IsHealthy = false;
                    await _nodeRegistry.UpdateNodeStatusAsync(node.Id, new NodeStatus
                    {
                        IsHealthy = false,
                        LastUpdated = now
                    });
                    
                    if (previousState)
                    {
                        _logger.LogWarning("Node {NodeId} at {Endpoint} marked as unhealthy due to missing heartbeat", 
                            node.Id, node.Endpoint);
                    }
                }
            }
        }

        public async Task EnsureReplicationFactorAsync()
        {
            var underReplicatedFiles = await _fileRegistry.GetFilesNeedingReplicationAsync(_minReplicationFactor);
            
            if (!underReplicatedFiles.Any())
            {
                return;
            }
            
            _logger.LogInformation("Found {Count} files needing replication", underReplicatedFiles.Count);
            
            foreach (var file in underReplicatedFiles)
            {
                // Get available nodes that don't already have this file
                var availableNodes = (await _nodeRegistry.GetAvailableNodesAsync())
                    .Where(n => !file.NodeIds.Contains(n.Id))
                    .OrderByDescending(n => n.AvailableSpace)
                    .ToList();
                
                var neededReplicas = _minReplicationFactor - file.NodeIds.Count;
                
                if (availableNodes.Count < neededReplicas)
                {
                    _logger.LogWarning("Not enough available nodes to replicate file {FileId}", file.FileId);
                    continue;
                }
                
                var sourceNodes = await _nodeRegistry.GetNodesByIdsAsync(file.NodeIds);
                var healthySourceNode = sourceNodes.FirstOrDefault(n => n.IsHealthy);
                
                if (healthySourceNode == null)
                {
                    _logger.LogWarning("No healthy source node available for file {FileId}", file.FileId);
                    continue;
                }
                
                // Select target nodes
                var targetNodes = availableNodes.Take(neededReplicas).ToList();
                
                _logger.LogInformation("Scheduling replication of file {FileId} from node {SourceNodeId} to {TargetCount} nodes",
                    file.FileId, healthySourceNode.Id, targetNodes.Count);
                
                // In a real implementation, you would trigger actual file transfer here
                // For demonstration, we'll just update the registry
                foreach (var targetNode in targetNodes)
                {
                    await _fileRegistry.AddFileLocationAsync(file.FileId, targetNode.Id);
                    _logger.LogInformation("Added node {NodeId} as location for file {FileId}", targetNode.Id, file.FileId);
                }
            }
        }
    }