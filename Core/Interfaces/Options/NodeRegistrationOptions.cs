namespace Drocsid.HenrikDennis2025.Core.Interfaces.Options;

/// <summary>
/// Configuration options for node registration
/// </summary>
public class NodeRegistrationOptions
{
    /// <summary>
    /// The URL of the registry service
    /// </summary>
    public string RegistryUrl { get; set; } = "http://localhost:5000";
    
    /// <summary>
    /// The endpoint where this node can be reached (e.g., http://localhost:6001)
    /// </summary>
    public string NodeEndpoint { get; set; } = "http://localhost:6001";
    
    /// <summary>
    /// The unique ID of this node (if not specified, one will be generated)
    /// </summary>
    public string NodeId { get; set; }
    
    /// <summary>
    /// The region where this node is located
    /// </summary>
    public string NodeRegion { get; set; } = "default";
    
    /// <summary>
    /// Tags for this node (for filtering and grouping)
    /// </summary>
    public List<string> NodeTags { get; set; } = new List<string>();
    
    /// <summary>
    /// The directory where files are stored
    /// </summary>
    public string DataDirectory { get; set; } = "./data";
    
    /// <summary>
    /// How often to send heartbeats to the registry
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Default total storage capacity for this node in bytes (50GB default)
    /// </summary>
    public long DefaultTotalStorage { get; set; } = 50L * 1024 * 1024 * 1024;
    
    /// <summary>
    /// Default available storage if actual size cannot be calculated (25GB default)
    /// </summary>
    public long DefaultAvailableStorage { get; set; } = 25L * 1024 * 1024 * 1024;
}