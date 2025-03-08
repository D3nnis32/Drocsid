namespace Drocsid.HenrikDennis2025.Core.Interfaces.Options;

/// <summary>
/// Options for configuring the DrocsidClient
/// </summary>
public class DrocsidClientOptions
{
    /// <summary>
    /// URL of the registry service
    /// </summary>
    public string RegistryUrl { get; set; }
        
    /// <summary>
    /// How often to refresh node information
    /// </summary>
    public TimeSpan NodeRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
        
    /// <summary>
    /// Maximum number of parallel file transfers
    /// </summary>
    public int MaxParallelTransfers { get; set; } = 3;
        
    /// <summary>
    /// Preferred region for node selection
    /// </summary>
    public string PreferredRegion { get; set; }
}