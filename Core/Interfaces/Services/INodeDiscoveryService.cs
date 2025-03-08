using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces.Services;

public interface INodeDiscoveryService
{
    Task RegisterNodeAsync(string nodeId, string endpoint);
    Task UnregisterNodeAsync(string nodeId);
    Task<IEnumerable<NodeInfo>> GetActiveNodesAsync();
    Task ReportHealthStatusAsync(string nodeId, bool isHealthy);
}