namespace Drocsid.HenrikDennis2025.Core.Interfaces;

public class NodeInfo
{
    public string NodeId { get; set; }
    public string Endpoint { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public Dictionary<string, string> Capabilities { get; set; } = new();
}