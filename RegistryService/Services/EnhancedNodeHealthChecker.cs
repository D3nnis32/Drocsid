using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Enhanced node health check implementation that supports active probing of nodes
/// </summary>
public class EnhancedNodeHealthChecker
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly IFileRegistry _fileRegistry;
    private readonly IUserRegistry _userRegistry;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeHealthMonitorOptions _options;
    private readonly ILogger<EnhancedNodeHealthChecker> _logger;

    // Cache to keep track of node failures 
    private readonly Dictionary<string, int> _nodeFailureCount = new();
    private readonly Dictionary<string, DateTime> _lastNodeCheck = new();
    private readonly object _lockObject = new();

    public EnhancedNodeHealthChecker(
        INodeRegistry nodeRegistry,
        IFileRegistry fileRegistry,
        IUserRegistry userRegistry,
        IHttpClientFactory httpClientFactory,
        IOptions<NodeHealthMonitorOptions> options,
        ILogger<EnhancedNodeHealthChecker> logger)
    {
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _fileRegistry = fileRegistry ?? throw new ArgumentNullException(nameof(fileRegistry));
        _userRegistry = userRegistry ?? throw new ArgumentNullException(nameof(userRegistry));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Perform a comprehensive health check of all nodes
    /// </summary>
    public async Task PerformHealthCheck()
    {
        try
        {
            _logger.LogInformation("Starting comprehensive node health check");

            // Get all nodes including those currently marked as unhealthy
            var allNodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: true);
            var checkTasks = new List<Task<(string NodeId, bool IsHealthy, string Details)>>();

            foreach (var node in allNodes)
            {
                // Only check nodes that haven't been checked recently
                if (!ShouldCheckNode(node.Id))
                {
                    continue;
                }

                // Add to check tasks
                checkTasks.Add(CheckNodeHealth(node));
            }

            // Wait for all checks to complete
            var results = await Task.WhenAll(checkTasks);

            int healthyCount = 0;
            int unhealthyCount = 0;

            foreach (var result in results)
            {
                var node = allNodes.FirstOrDefault(n => n.Id == result.NodeId);
                if (node == null) continue;

                // Update node status based on health check
                bool statusChanged = node.Status.IsHealthy != result.IsHealthy;

                if (statusChanged)
                {
                    // Status changed, update in registry
                    node.Status.IsHealthy = result.IsHealthy;
                    node.Status.LastUpdated = DateTime.UtcNow;
                    await _nodeRegistry.UpdateNodeAsync(node);

                    if (result.IsHealthy)
                    {
                        _logger.LogInformation("Node {NodeId} ({Hostname}) is now healthy: {Details}",
                            node.Id, node.Hostname, result.Details);
                        healthyCount++;

                        // Reset failure count
                        lock (_lockObject)
                        {
                            _nodeFailureCount.Remove(node.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Node {NodeId} ({Hostname}) is unhealthy: {Details}",
                            node.Id, node.Hostname, result.Details);
                        unhealthyCount++;

                        // Handle node failure
                        await HandleNodeFailure(node);
                    }
                }

                // Update last check time
                lock (_lockObject)
                {
                    _lastNodeCheck[node.Id] = DateTime.UtcNow;
                }
            }

            _logger.LogInformation(
                "Health check completed. Changed status for {HealthyCount} nodes to healthy and {UnhealthyCount} nodes to unhealthy",
                healthyCount, unhealthyCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing node health check");
        }
    }

    /// <summary>
    /// Check if a specific node should be checked now
    /// </summary>
    private bool ShouldCheckNode(string nodeId)
    {
        lock (_lockObject)
        {
            // Always check if never checked before
            if (!_lastNodeCheck.TryGetValue(nodeId, out var lastCheck))
            {
                return true;
            }

            // Otherwise, only check if enough time has passed
            var timeSinceLastCheck = DateTime.UtcNow - lastCheck;

            // Check more frequently for nodes that have had failures
            if (_nodeFailureCount.TryGetValue(nodeId, out var failures) && failures > 0)
            {
                // More failures = check more frequently
                var checkInterval = TimeSpan.FromSeconds(
                    Math.Max(5, _options.HealthCheckInterval.TotalSeconds / Math.Min(failures, 10)));

                return timeSinceLastCheck > checkInterval;
            }

            // Normal check interval for healthy nodes
            return timeSinceLastCheck > _options.HealthCheckInterval;
        }
    }

    /// <summary>
    /// Perform a health check for a specific node
    /// </summary>
    private async Task<(string NodeId, bool IsHealthy, string Details)> CheckNodeHealth(StorageNode node)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10); // Short timeout for health checks

            // Try hitting the health endpoint
            var healthEndpoint = $"{node.Endpoint.TrimEnd('/')}/health";
            using var response = await client.GetAsync(healthEndpoint);

            if (!response.IsSuccessStatusCode)
            {
                // Record the failure
                RecordNodeFailure(node.Id);
                return (node.Id, false, $"Health endpoint returned status {response.StatusCode}");
            }

            // Try getting more detailed health info
            var detailedEndpoint = $"{node.Endpoint.TrimEnd('/')}/health/detailed";
            using var detailedResponse = await client.GetAsync(detailedEndpoint);

            string details = "Basic health check passed";

            if (detailedResponse.IsSuccessStatusCode)
            {
                var healthData = await detailedResponse.Content.ReadFromJsonAsync<NodeHealthInfo>();
                if (healthData != null)
                {
                    // Update node metrics from health data
                    await UpdateNodeMetricsFromHealthData(node, healthData);

                    details = $"CPU: {healthData.cpuUsage}%, Memory: {healthData.memoryUsageMB}MB, " +
                              $"Connections: {healthData.activeConnections}";
                }
            }

            // Node is healthy
            return (node.Id, true, details);
        }
        catch (Exception ex)
        {
            // Record the failure
            RecordNodeFailure(node.Id);

            return (node.Id, false, $"Error checking health: {ex.Message}");
        }
    }

    /// <summary>
    /// Record a node failure
    /// </summary>
    private void RecordNodeFailure(string nodeId)
    {
        lock (_lockObject)
        {
            if (!_nodeFailureCount.TryGetValue(nodeId, out var count))
            {
                _nodeFailureCount[nodeId] = 1;
            }
            else
            {
                _nodeFailureCount[nodeId] = count + 1;
            }

            _logger.LogDebug("Node {NodeId} failure count: {Count}", nodeId, _nodeFailureCount[nodeId]);
        }
    }

    /// <summary>
    /// Update a node's metrics from health check data
    /// </summary>
    private async Task UpdateNodeMetricsFromHealthData(StorageNode node, dynamic healthData)
    {
        try
        {
            if (healthData == null) return;

            // Update node status from health data
            if (healthData.cpuUsage != null)
            {
                node.Status.CurrentLoad = healthData.cpuUsage;
            }

            if (healthData.activeConnections != null)
            {
                node.Status.ActiveConnections = healthData.activeConnections;
            }

            if (healthData.availableStorageBytes != null)
            {
                node.Status.AvailableSpace = healthData.availableStorageBytes;
            }

            // Save the changes
            await _nodeRegistry.UpdateNodeAsync(node);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating node metrics from health data for node {NodeId}", node.Id);
        }
    }

    /// <summary>
    /// Handle a node failure by reassigning users and triggering file replication
    /// </summary>
    private async Task HandleNodeFailure(StorageNode failedNode)
    {
        try
        {
            _logger.LogInformation("Handling failure of node {NodeId} ({Hostname})",
                failedNode.Id, failedNode.Hostname);

            // Find all users assigned to this node
            var users = await GetUsersOnNodeAsync(failedNode.Id);

            if (users.Any())
            {
                _logger.LogInformation("Found {UserCount} users on failed node {NodeId}",
                    users.Count, failedNode.Id);

                // Get available healthy nodes for reassignment
                var healthyNodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
                if (!healthyNodes.Any())
                {
                    _logger.LogCritical("No healthy nodes available for reassignment after node {NodeId} failure",
                        failedNode.Id);
                    return;
                }

                // Reassign users to healthy nodes
                foreach (var user in users)
                {
                    try
                    {
                        // Find the best node for this user
                        var bestNode = healthyNodes
                            .OrderBy(n => n.Status.CurrentLoad)
                            .First();

                        // Update user's assigned node
                        user.CurrentNodeId = bestNode.Id;
                        await _userRegistry.UpdateUserAsync(user);

                        _logger.LogInformation(
                            "Reassigned user {UserId} from failed node {FailedNodeId} to node {NewNodeId}",
                            user.Id, failedNode.Id, bestNode.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reassigning user {UserId} from failed node {NodeId}",
                            user.Id, failedNode.Id);
                    }
                }
            }

            // Trigger replication for files on the failed node
            await TriggerFileReplication(failedNode.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling node failure for node {NodeId}", failedNode.Id);
        }
    }

    /// <summary>
    /// Get all users currently assigned to a specific node
    /// </summary>
    private async Task<List<User>> GetUsersOnNodeAsync(string nodeId)
    {
        try
        {
            // In a real implementation, you'd query the database directly
            // Here, we'll use the user registry API to find all users
            var allUsers = await _userRegistry.GetUsersByStatusAsync(UserStatus.Online);
            return allUsers.Where(u => u.CurrentNodeId == nodeId).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users on node {NodeId}", nodeId);
            return new List<User>();
        }
    }

    /// <summary>
    /// Trigger replication for files on a failed node
    /// </summary>
    private async Task TriggerFileReplication(string failedNodeId)
    {
        try
        {
            // Get files on the failed node
            var files = await _fileRegistry.GetFilesByNodeAsync(failedNodeId);
            if (!files.Any())
            {
                _logger.LogInformation("No files to replicate from failed node {NodeId}", failedNodeId);
                return;
            }

            _logger.LogInformation("Found {FileCount} files to replicate from failed node {NodeId}",
                files.Count(), failedNodeId);

            // Get healthy nodes
            var healthyNodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
            if (!healthyNodes.Any())
            {
                _logger.LogCritical("No healthy nodes available for file replication");
                return;
            }

            // For each file, ensure it has enough replicas
            int replicatedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    // Count healthy replicas
                    var healthyReplicas = file.NodeLocations
                        .Where(nodeId => nodeId != failedNodeId &&
                                         healthyNodes.Any(n => n.Id == nodeId))
                        .Count();

                    // If we have enough healthy replicas, skip
                    if (healthyReplicas >= _options.DefaultReplicationFactor)
                    {
                        continue;
                    }

                    // Calculate how many new replicas we need
                    int neededReplicas = _options.DefaultReplicationFactor - healthyReplicas;

                    // Find healthy source nodes for this file
                    var sourceNodes = healthyNodes
                        .Where(n => file.NodeLocations.Contains(n.Id))
                        .ToList();

                    if (!sourceNodes.Any())
                    {
                        _logger.LogCritical("File {FileId} has no healthy replicas and may be lost", file.Id);
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
                        _logger.LogWarning("Not enough target nodes available to fully replicate file {FileId}",
                            file.Id);
                    }

                    if (!targetNodes.Any())
                    {
                        _logger.LogWarning("No target nodes available for file {FileId}", file.Id);
                        continue;
                    }

                    // Replicate the file to each target node
                    var sourceNode = sourceNodes.First();

                    foreach (var targetNode in targetNodes)
                    {
                        // In a real implementation, use your IFileTransferService here
                        _logger.LogInformation(
                            "Triggering replication of file {FileId} from node {SourceNodeId} to node {TargetNodeId}",
                            file.Id, sourceNode.Id, targetNode.Id);

                        // Register the new location in the registry
                        await _fileRegistry.AddFileLocationAsync(file.Id, targetNode.Id);

                        replicatedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error replicating file {FileId} from failed node {NodeId}",
                        file.Id, failedNodeId);
                }
            }

            _logger.LogInformation(
                "Successfully triggered replication for {ReplicatedCount} file instances from failed node {NodeId}",
                replicatedCount, failedNodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering file replication for failed node {NodeId}", failedNodeId);
        }
    }

    /// <summary>
    /// Class to hold health check response data
    /// </summary>
    private class NodeHealthInfo
    {
        public string status { get; set; }
        public string nodeId { get; set; }
        public string uptime { get; set; }
        public double cpuUsage { get; set; }
        public long memoryUsageBytes { get; set; }
        public double memoryUsageMB { get; set; }
        public long totalStorageBytes { get; set; }
        public long availableStorageBytes { get; set; }
        public int activeConnections { get; set; }
        public DateTime timestamp { get; set; }
    }
}