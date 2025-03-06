namespace Drocsid.HenrikDennis2025.Core.Interfaces;

public interface INodeDiscoveryService
{
    Task RegisterNodeAsync(string nodeId, string endpoint);
    Task UnregisterNodeAsync(string nodeId);
    Task<IEnumerable<NodeInfo>> GetActiveNodesAsync();
    Task ReportHealthStatusAsync(string nodeId, bool isHealthy);
}