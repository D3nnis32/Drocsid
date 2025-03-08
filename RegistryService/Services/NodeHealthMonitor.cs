using System.Collections.Concurrent;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
    /// Service that monitors node health and ensures file replication requirements are met
    /// </summary>
    public class NodeHealthMonitor : INodeHealthMonitor
    {
        private readonly INodeRegistry _nodeRegistry;
        private readonly IFileRegistry _fileRegistry;
        private readonly IFileTransferService _fileTransferService;
        private readonly ILogger<NodeHealthMonitor> _logger;
        private readonly NodeHealthMonitorOptions _options;
        
        // Dictionary to track ongoing replication tasks by file ID
        private readonly ConcurrentDictionary<string, Task> _ongoingReplications = new();

        public NodeHealthMonitor(
            INodeRegistry nodeRegistry,
            IFileRegistry fileRegistry,
            IFileTransferService fileTransferService,
            IOptions<NodeHealthMonitorOptions> options,
            ILogger<NodeHealthMonitor> logger)
        {
            _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
            _fileRegistry = fileRegistry ?? throw new ArgumentNullException(nameof(fileRegistry));
            _fileTransferService = fileTransferService ?? throw new ArgumentNullException(nameof(fileTransferService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks the health of all registered nodes and marks them as healthy/unhealthy
        /// </summary>
        public async Task CheckNodeHealthAsync()
        {
            try
            {
                _logger.LogInformation("Starting node health check");
                
                // Get all nodes, including those currently marked unhealthy
                var allNodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: true);
                var offlineThreshold = DateTime.UtcNow.Subtract(_options.NodeOfflineThreshold);
                
                int healthyCount = 0;
                int unhealthyCount = 0;
                
                foreach (var node in allNodes)
                {
                    // Check if the node has sent a heartbeat recently
                    if (node.Status.IsHealthy && node.LastSeen < offlineThreshold)
                    {
                        _logger.LogWarning("Node {NodeId} ({Hostname}) has not sent a heartbeat since {LastSeen} and will be marked unhealthy",
                            node.Id, node.Hostname, node.LastSeen);
                            
                        // Mark the node as unhealthy
                        node.Status.IsHealthy = false;
                        node.Status.LastUpdated = DateTime.UtcNow;
                        await _nodeRegistry.UpdateNodeAsync(node);
                        
                        // Trigger file replication for files on this node
                        var affectedFilesCount = await TriggerFileReplication(node.Id);
                        _logger.LogInformation("Found {FileCount} files needing replication from node {NodeId}", 
                            affectedFilesCount, node.Id);
                            
                        unhealthyCount++;
                    }
                    else if (!node.Status.IsHealthy && node.LastSeen >= offlineThreshold)
                    {
                        // Node was unhealthy but has sent a heartbeat recently
                        _logger.LogInformation("Node {NodeId} ({Hostname}) has come back online", 
                            node.Id, node.Hostname);
                            
                        node.Status.IsHealthy = true;
                        node.Status.LastUpdated = DateTime.UtcNow;
                        await _nodeRegistry.UpdateNodeAsync(node);
                        
                        healthyCount++;
                    }
                }
                
                _logger.LogInformation("Node health check completed. Changed status for {HealthyCount} nodes to healthy and {UnhealthyCount} nodes to unhealthy",
                    healthyCount, unhealthyCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during node health check");
            }
        }

        /// <summary>
        /// Ensures all files have the required replication factor across healthy nodes
        /// </summary>
        public async Task EnsureReplicationFactorAsync()
        {
            try
            {
                _logger.LogInformation("Starting replication factor check");
                
                // Clean up completed replication tasks
                CleanupCompletedReplicationTasks();
                
                // Get all healthy nodes
                var healthyNodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
                
                if (healthyNodes.Count() < _options.DefaultReplicationFactor)
                {
                    _logger.LogWarning("Not enough healthy nodes ({HealthyCount}) to maintain desired replication factor ({ReplicationFactor})",
                        healthyNodes.Count(), _options.DefaultReplicationFactor);
                    return;
                }
                
                // Get all files in the system
                var allFiles = await _fileRegistry.GetAllFilesAsync();
                int processedCount = 0;
                int replicatedCount = 0;
                
                // Process files in batches to avoid memory issues with large systems
                var fileBatches = BatchFiles(allFiles, _options.ReplicationBatchSize);
                
                foreach (var batch in fileBatches)
                {
                    foreach (var file in batch)
                    {
                        try
                        {
                            // Skip files that already have enough replicas
                            if (file.NodeLocations.Count >= _options.DefaultReplicationFactor)
                            {
                                processedCount++;
                                continue;
                            }
                            
                            // Skip files that are already being replicated
                            if (_ongoingReplications.ContainsKey(file.Id))
                            {
                                _logger.LogDebug("File {FileId} is already being replicated, skipping", file.Id);
                                processedCount++;
                                continue;
                            }
                            
                            // Check if any of the nodes hosting this file are unhealthy
                            var fileNodes = await GetNodesForFile(file);
                            var healthyFileNodes = fileNodes.Where(n => n.Status.IsHealthy).ToList();
                            
                            // Calculate how many new replicas we need
                            int neededReplicas = _options.DefaultReplicationFactor - healthyFileNodes.Count;
                            
                            if (neededReplicas > 0)
                            {
                                // Find suitable nodes for new replicas - exclude nodes that already have the file
                                var candidateNodes = healthyNodes
                                    .Where(n => !file.NodeLocations.Contains(n.Id))
                                    .OrderByDescending(n => n.Status.AvailableSpace)
                                    .Take(neededReplicas)
                                    .ToList();
                                
                                if (candidateNodes.Count > 0)
                                {
                                    // Start actual file replication
                                    await StartFileReplication(file, healthyFileNodes, candidateNodes);
                                    replicatedCount++;
                                }
                                else
                                {
                                    _logger.LogWarning("Not enough candidate nodes to replicate file {FileId} to meet replication factor",
                                        file.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing file {FileId} during replication check", file.Id);
                        }
                        
                        processedCount++;
                    }
                }
                
                _logger.LogInformation("Replication factor check completed. Processed {ProcessedCount} files and scheduled replication for {ReplicatedCount} files",
                    processedCount, replicatedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during replication factor check");
            }
        }

        /// <summary>
        /// Triggers replication for files on a node that has become unhealthy
        /// </summary>
        private async Task<int> TriggerFileReplication(string offlineNodeId)
        {
            try
            {
                // Get files that were on the offline node
                var filesToReplicate = await _fileRegistry.GetFilesByNodeAsync(offlineNodeId);
                
                if (!filesToReplicate.Any())
                {
                    _logger.LogInformation("No files to replicate from unhealthy node {NodeId}", offlineNodeId);
                    return 0;
                }
                
                _logger.LogInformation("Found {FileCount} files to replicate from unhealthy node {NodeId}",
                    filesToReplicate.Count(), offlineNodeId);
                
                // Get all healthy nodes
                var healthyNodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
                
                int replicationStarted = 0;
                
                foreach (var file in filesToReplicate)
                {
                    try
                    {
                        // Skip if replication is already in progress
                        if (_ongoingReplications.ContainsKey(file.Id))
                        {
                            continue;
                        }
                        
                        // Get all current nodes for this file
                        var existingNodes = await GetNodesForFile(file);
                        var healthyExistingNodes = existingNodes.Where(n => n.Status.IsHealthy).ToList();
                        
                        // If we have no healthy nodes with this file, we're in trouble
                        if (!healthyExistingNodes.Any())
                        {
                            _logger.LogError("Critical: File {FileId} has no healthy nodes and may be lost", file.Id);
                            continue;
                        }
                        
                        // Calculate how many new replicas we need
                        int neededReplicas = _options.DefaultReplicationFactor - healthyExistingNodes.Count;
                        
                        if (neededReplicas > 0)
                        {
                            // Find candidate nodes for new replicas - exclude nodes that already have the file
                            var candidateNodes = healthyNodes
                                .Where(n => !file.NodeLocations.Contains(n.Id))
                                .OrderByDescending(n => n.Status.AvailableSpace)
                                .Take(neededReplicas)
                                .ToList();
                            
                            if (candidateNodes.Any())
                            {
                                // Start actual file replication
                                await StartFileReplication(file, healthyExistingNodes, candidateNodes);
                                replicationStarted++;
                            }
                            else
                            {
                                _logger.LogWarning("Not enough healthy nodes available to replicate file {FileId}", file.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file {FileId} for replication from offline node", file.Id);
                    }
                }
                
                return replicationStarted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering file replication for unhealthy node {NodeId}", offlineNodeId);
                return 0;
            }
        }
        
        /// <summary>
        /// Gets all nodes that have a specific file
        /// </summary>
        private async Task<List<StorageNode>> GetNodesForFile(StoredFile file)
        {
            var result = new List<StorageNode>();
            
            foreach (var nodeId in file.NodeLocations)
            {
                var node = await _nodeRegistry.GetNodeAsync(nodeId);
                if (node != null)
                {
                    result.Add(node);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Starts file replication to multiple target nodes
        /// </summary>
        private async Task StartFileReplication(
            StoredFile file, 
            List<StorageNode> sourceNodes, 
            List<StorageNode> targetNodes)
        {
            try
            {
                if (!sourceNodes.Any())
                {
                    _logger.LogError("No source nodes available for file {FileId}", file.Id);
                    return;
                }
                
                // Select the best source node (lowest load, highest network capacity)
                var bestSourceNode = sourceNodes
                    .OrderBy(n => n.Status.ActiveTransfers)
                    .ThenByDescending(n => n.Status.NetworkCapacity)
                    .First();
                
                _logger.LogInformation("Starting replication of file {FileId} ({FileName}, {Size} bytes) from node {SourceNode} to {TargetCount} nodes",
                    file.Id,
                    file.Filename,
                    file.Size,
                    bestSourceNode.Id,
                    targetNodes.Count);
                
                // Start the actual transfer tasks in parallel, but limit concurrency
                var transferTasks = new List<Task>();
                
                foreach (var targetNode in targetNodes)
                {
                    // Create a replication task
                    var replicationTask = ReplicateToNodeAsync(file.Id, bestSourceNode.Id, targetNode.Id);
                    
                    // Add to tracking dictionary for status monitoring
                    _ongoingReplications.TryAdd(file.Id, replicationTask);
                    
                    transferTasks.Add(replicationTask);
                    
                    // Limit parallel tasks
                    if (transferTasks.Count >= _options.MaxParallelReplications)
                    {
                        // Wait for at least one task to complete before adding more
                        await Task.WhenAny(transferTasks);
                        transferTasks.RemoveAll(t => t.IsCompleted);
                    }
                }
                
                // No need to wait for completion here - the tasks will run in the background
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting replication for file {FileId}", file.Id);
            }
        }
        
        /// <summary>
        /// Replicates a file to a specific target node
        /// </summary>
        private async Task ReplicateToNodeAsync(string fileId, string sourceNodeId, string targetNodeId)
        {
            try
            {
                _logger.LogDebug("Replicating file {FileId} from node {SourceNodeId} to node {TargetNodeId}",
                    fileId, sourceNodeId, targetNodeId);
                
                // Use the file transfer service to perform the actual transfer
                bool success = await _fileTransferService.TransferFileAsync(fileId, sourceNodeId, targetNodeId);
                
                if (success)
                {
                    _logger.LogInformation("Successfully replicated file {FileId} to node {TargetNodeId}",
                        fileId, targetNodeId);
                }
                else
                {
                    _logger.LogWarning("Failed to replicate file {FileId} to node {TargetNodeId}",
                        fileId, targetNodeId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in replication task for file {FileId} to node {TargetNodeId}",
                    fileId, targetNodeId);
            }
            finally
            {
                // Remove from tracking dictionary on completion
                _ongoingReplications.TryRemove(fileId, out _);
            }
        }
        
        /// <summary>
        /// Cleans up completed replication tasks from tracking dictionary
        /// </summary>
        private void CleanupCompletedReplicationTasks()
        {
            // Remove completed tasks from tracking dictionary
            foreach (var key in _ongoingReplications.Keys)
            {
                if (_ongoingReplications.TryGetValue(key, out var task) && task.IsCompleted)
                {
                    _ongoingReplications.TryRemove(key, out _);
                }
            }
        }
        
        /// <summary>
        /// Breaks a large collection of files into smaller batches for processing
        /// </summary>
        private IEnumerable<List<StoredFile>> BatchFiles(IEnumerable<StoredFile> files, int batchSize)
        {
            var currentBatch = new List<StoredFile>();
            int count = 0;
            
            foreach (var file in files)
            {
                currentBatch.Add(file);
                count++;
                
                if (count >= batchSize)
                {
                    yield return currentBatch;
                    currentBatch = new List<StoredFile>();
                    count = 0;
                }
            }
            
            // Return any remaining files
            if (currentBatch.Any())
            {
                yield return currentBatch;
            }
        }
    }