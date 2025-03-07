namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents a node in the distributed system
/// </summary>
public class Node
{
    /// <summary>
    /// Unique identifier for the node
    /// </summary>
    public string Id { get; set; }
        
    /// <summary>
    /// API endpoint URL for the node
    /// </summary>
    public string Endpoint { get; set; }
        
    /// <summary>
    /// Geographic region where the node is located
    /// </summary>
    public string Region { get; set; }
        
    /// <summary>
    /// Total storage capacity of the node in bytes
    /// </summary>
    public long Capacity { get; set; }
        
    /// <summary>
    /// Current available storage space in bytes
    /// </summary>
    public long AvailableSpace { get; set; }
        
    /// <summary>
    /// Current CPU/memory load percentage (0-100)
    /// </summary>
    public double CurrentLoad { get; set; }
        
    /// <summary>
    /// Indicates if the node is healthy and responsive
    /// </summary>
    public bool IsHealthy { get; set; }
        
    /// <summary>
    /// Time of the last successful heartbeat
    /// </summary>
    public DateTime LastHeartbeat { get; set; }
        
    /// <summary>
    /// IP address of the node
    /// </summary>
    public string IpAddress { get; set; }
        
    /// <summary>
    /// Optional metadata for the node
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}