namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents the current status of a node
/// </summary>
public class NodeStatus
{
    /// <summary>
    /// Indicates if the node is healthy and responsive
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Current CPU/memory load percentage (0-100)
    /// </summary>
    public double CurrentLoad { get; set; } = 0;

    /// <summary>
    /// Available storage space in bytes
    /// </summary>
    public long AvailableSpace { get; set; } = 0;

    /// <summary>
    /// Active connections being handled by the node
    /// </summary>
    public int ActiveConnections { get; set; } = 0;
            
    /// <summary>
    /// Time when the status was updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int ActiveTransfers { get; set; }
    public long NetworkCapacity { get; set; } = 1000;
    /// <summary>
    /// Used storage space in bytes
    /// </summary>
    public long UsedSpace { get; set; }
}