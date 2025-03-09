namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents a storage node in the distributed system
/// </summary>
public class StorageNode
{
    public string Id { get; set; }
    public string Hostname { get; set; }
    public string Endpoint { get; set; }
    public NodeStatus Status { get; set; } = new();
    public long TotalStorage { get; set; }
    public string Region { get; set; }
    public DateTime LastSeen { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}