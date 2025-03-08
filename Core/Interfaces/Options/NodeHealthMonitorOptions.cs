namespace Drocsid.HenrikDennis2025.Core.Interfaces.Options;

/// <summary>
/// Configuration options for node health monitoring
/// </summary>
public class NodeHealthMonitorOptions
{
    /// <summary>
    /// Threshold for marking a node as unhealthy due to missed heartbeats
    /// </summary>
    public TimeSpan NodeOfflineThreshold { get; set; } = TimeSpan.FromMinutes(2);
        
    /// <summary>
    /// Default replication factor for files in the system
    /// </summary>
    public int DefaultReplicationFactor { get; set; } = 3;
        
    /// <summary>
    /// Batch size for processing files during replication checks
    /// </summary>
    public int ReplicationBatchSize { get; set; } = 100;
    
    /// <summary>
    /// Maximum number of parallel replication tasks to run
    /// </summary>
    public int MaxParallelReplications { get; set; } = 10;
        
    /// <summary>
    /// Interval between node health checks
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
}