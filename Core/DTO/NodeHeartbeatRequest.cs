namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Request model for node heartbeat updates
/// </summary>
public class NodeHeartbeatRequest
{
    /// <summary>
    /// Current CPU/memory load percentage (0-100)
    /// </summary>
    public double CurrentLoad { get; set; }
        
    /// <summary>
    /// Available storage space in bytes
    /// </summary>
    public long AvailableSpace { get; set; }
        
    /// <summary>
    /// Active connections being handled by the node
    /// </summary>
    public int ActiveConnections { get; set; }
}