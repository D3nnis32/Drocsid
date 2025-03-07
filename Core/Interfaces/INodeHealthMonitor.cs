namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Interface for node health monitoring service
/// </summary>
public interface INodeHealthMonitor
{
    /// <summary>
    /// Triggers a node health check
    /// </summary>
    Task CheckNodeHealthAsync();
        
    /// <summary>
    /// Ensures all files have the required replication factor
    /// </summary>
    Task EnsureReplicationFactorAsync();
}