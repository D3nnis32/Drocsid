namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Health and status information for storage nodes
/// </summary>
public class NodeHealthInfo
{
    public string NodeId { get; set; }
    public string Region { get; set; }
    public bool IsHealthy { get; set; }
    public double CurrentLoad { get; set; }
    public long AvailableSpace { get; set; }
}