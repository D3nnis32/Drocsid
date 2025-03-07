namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Request model for node registration
/// </summary>
public class NodeRegistrationRequest
{
    /// <summary>
    /// API endpoint URL for the node
    /// </summary>
    public string Endpoint { get; set; }
        
    /// <summary>
    /// Geographic region where the node is located
    /// </summary>
    public string Region { get; set; }
        
    /// <summary>
    /// Total storage capacity in bytes
    /// </summary>
    public long CapacityBytes { get; set; }
        
    /// <summary>
    /// IP address of the node
    /// </summary>
    public string IpAddress { get; set; }
        
    /// <summary>
    /// Optional metadata for the node
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}