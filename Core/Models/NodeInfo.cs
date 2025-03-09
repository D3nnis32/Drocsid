namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Simplified node information for API responses
/// </summary>
public class NodeInfo
{
    /// <summary>
    /// Node ID
    /// </summary>
    public string Id { get; set; }
        
    /// <summary>
    /// Node endpoint URL
    /// </summary>
    public string Endpoint { get; set; }
        
    /// <summary>
    /// Geographic region
    /// </summary>
    public string Region { get; set; }
        
    /// <summary>
    /// Whether the node is healthy
    /// </summary>
    public bool IsHealthy { get; set; }
        
    /// <summary>
    /// Current load percentage
    /// </summary>
    public double CurrentLoad { get; set; }
        
    /// <summary>
    /// Available storage space in bytes
    /// </summary>
    public long AvailableSpace { get; set; }
}