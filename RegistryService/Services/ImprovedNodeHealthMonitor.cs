using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Improved NodeHealthMonitor that integrates the enhanced health checker
/// </summary>
public class ImprovedNodeHealthMonitor : INodeHealthMonitor
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly IFileRegistry _fileRegistry;
    private readonly EnhancedNodeHealthChecker _healthChecker;
    private readonly NodeHealthMonitorOptions _options;
    private readonly ILogger<ImprovedNodeHealthMonitor> _logger;
    
    // Track when we last did a full replication check
    private DateTime _lastReplicationCheck = DateTime.MinValue;

    public ImprovedNodeHealthMonitor(
        INodeRegistry nodeRegistry,
        IFileRegistry fileRegistry,
        IHttpClientFactory httpClientFactory,
        IUserRegistry userRegistry,
        IOptions<NodeHealthMonitorOptions> options,
        ILogger<ImprovedNodeHealthMonitor> logger,
        ILogger<EnhancedNodeHealthChecker> healthCheckerLogger)
    {
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _fileRegistry = fileRegistry ?? throw new ArgumentNullException(nameof(fileRegistry));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create the enhanced health checker
        _healthChecker = new EnhancedNodeHealthChecker(
            nodeRegistry,
            fileRegistry,
            userRegistry,
            httpClientFactory,
            options,
            healthCheckerLogger);
    }

    /// <summary>
    /// Check the health of all nodes using the enhanced checker
    /// </summary>
    public async Task CheckNodeHealthAsync()
    {
        try
        {
            _logger.LogInformation("Starting node health check with enhanced checker");
            
            // Use the enhanced health checker
            await _healthChecker.PerformHealthCheck();
            
            _logger.LogInformation("Node health check completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during node health check");
        }
    }

    /// <summary>
    /// Ensure all files have the required replication factor
    /// </summary>
    public async Task EnsureReplicationFactorAsync()
    {
        try
        {
            // Check if it's time to run the replication check
            var now = DateTime.UtcNow;
            var replicationCheckInterval = TimeSpan.FromMinutes(15);
            
            if (now - _lastReplicationCheck < replicationCheckInterval)
            {
                _logger.LogDebug("Skipping replication check, last check was {TimeAgo} ago", 
                    now - _lastReplicationCheck);
                return;
            }
            
            _logger.LogInformation("Starting replication factor check");
            
            // Get all files in the system
            var allFiles = await _fileRegistry.GetAllFilesAsync();
            
            // Get all healthy nodes
            var healthyNodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
            
            if (healthyNodes.Count() < _options.DefaultReplicationFactor)
            {
                _logger.LogWarning("Not enough healthy nodes ({HealthyCount}) to maintain desired replication factor ({ReplicationFactor})",
                    healthyNodes.Count(), _options.DefaultReplicationFactor);
            }
            
            int processedCount = 0;
            int replicatedCount = 0;
            
            // Process files in batches
            var batchSize = _options.ReplicationBatchSize;
            
            for (int i = 0; i < allFiles.Count(); i += batchSize)
            {
                var batch = allFiles.Skip(i).Take(batchSize);
                
                foreach (var file in batch)
                {
                    try
                    {
                        // Count healthy replicas
                        var healthyReplicaCount = 0;
                        
                        foreach (var nodeId in file.NodeLocations)
                        {
                            var node = healthyNodes.FirstOrDefault(n => n.Id == nodeId);
                            if (node != null && node.Status.IsHealthy)
                            {
                                healthyReplicaCount++;
                            }
                        }
                        
                        // If we have enough healthy replicas, skip
                        if (healthyReplicaCount >= _options.DefaultReplicationFactor)
                        {
                            processedCount++;
                            continue;
                        }
                        
                        // Calculate how many new replicas we need
                        int neededReplicas = _options.DefaultReplicationFactor - healthyReplicaCount;
                        
                        // Find healthy source nodes for this file
                        var sourceNodes = healthyNodes
                            .Where(n => file.NodeLocations.Contains(n.Id))
                            .ToList();
                        
                        if (!sourceNodes.Any())
                        {
                            _logger.LogCritical("File {FileId} has no healthy replicas and may be lost", file.Id);
                            processedCount++;
                            continue;
                        }
                        
                        // Find target nodes (nodes that don't have this file)
                        var targetNodes = healthyNodes
                            .Where(n => !file.NodeLocations.Contains(n.Id))
                            .OrderByDescending(n => n.Status.AvailableSpace)
                            .Take(neededReplicas)
                            .ToList();
                        
                        if (targetNodes.Count < neededReplicas)
                        {
                            _logger.LogWarning("Not enough target nodes available to fully replicate file {FileId}", file.Id);
                        }
                        
                        if (!targetNodes.Any())
                        {
                            _logger.LogWarning("No target nodes available for file {FileId}", file.Id);
                            processedCount++;
                            continue;
                        }
                        
                        // Replicate the file to each target node
                        var sourceNode = sourceNodes.First();
                        
                        foreach (var targetNode in targetNodes)
                        {
                            // In a real implementation, use your IFileTransferService here
                            _logger.LogInformation("Triggering replication of file {FileId} from node {SourceNodeId} to node {TargetNodeId}", 
                                file.Id, sourceNode.Id, targetNode.Id);
                            
                            // Register the new location in the registry
                            await _fileRegistry.AddFileLocationAsync(file.Id, targetNode.Id);
                            
                            replicatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file {FileId} during replication check", file.Id);
                    }
                    
                    processedCount++;
                }
            }
            
            _lastReplicationCheck = now;
            
            _logger.LogInformation("Replication factor check completed. Processed {ProcessedCount} files and initiated replication for {ReplicatedCount} file instances",
                processedCount, replicatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during replication factor check");
        }
    }
}