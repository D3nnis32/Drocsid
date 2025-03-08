namespace Drocsid.HenrikDennis2025.Core.Interfaces.Options;

/// <summary>
/// Configuration options for the registry service
/// </summary>
public class RegistryOptions
{
    /// <summary>
    /// The public endpoint where clients can reach the registry
    /// </summary>
    public string PublicEndpoint { get; set; }
    
    /// <summary>
    /// Default replication factor for files
    /// </summary>
    public int DefaultReplicationFactor { get; set; } = 3;
    
    /// <summary>
    /// Whether to require user accounts
    /// </summary>
    public bool RequireAuthentication { get; set; } = true;
    
    /// <summary>
    /// How long to keep inactive nodes in the registry
    /// </summary>
    public TimeSpan NodeExpirationTime { get; set; } = TimeSpan.FromDays(7);
}