using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
    /// Interface for managing node registry operations
    /// </summary>
    public interface INodeRegistry
    {
        /// <summary>
        /// Registers a new node in the system
        /// </summary>
        /// <param name="node">Information about the node being registered</param>
        /// <returns>The assigned node ID</returns>
        Task<string> RegisterNodeAsync(Node node);
        
        /// <summary>
        /// Updates the status of a node
        /// </summary>
        /// <param name="nodeId">The ID of the node</param>
        /// <param name="status">The new status information</param>
        Task UpdateNodeStatusAsync(string nodeId, NodeStatus status);
        
        /// <summary>
        /// Gets information about a specific node
        /// </summary>
        /// <param name="nodeId">The ID of the node</param>
        Task<Node?> GetNodeAsync(string nodeId);
        
        /// <summary>
        /// Gets information about multiple nodes by their IDs
        /// </summary>
        /// <param name="nodeIds">The IDs of the nodes</param>
        Task<List<Node>> GetNodesByIdsAsync(List<string> nodeIds);
        
        /// <summary>
        /// Gets all registered nodes in the system
        /// </summary>
        Task<List<Node>> GetAllNodesAsync();
        
        /// <summary>
        /// Gets nodes that are currently healthy and available
        /// </summary>
        /// <param name="limit">Optional limit on the number of nodes to return</param>
        Task<List<Node>> GetAvailableNodesAsync(int? limit = null);
        
        /// <summary>
        /// Selects nodes for storing new data, prioritizing nodes with more available space
        /// </summary>
        /// <param name="count">The number of nodes to select</param>
        /// <param name="preferredRegion">Optional preferred region for the nodes</param>
        Task<List<Node>> SelectNodesForStorageAsync(int count, string? preferredRegion = null);
        
        /// <summary>
        /// Marks a node as offline or unhealthy
        /// </summary>
        /// <param name="nodeId">The ID of the node</param>
        Task MarkNodeOfflineAsync(string nodeId);
        
        /// <summary>
        /// Removes a node from the registry
        /// </summary>
        /// <param name="nodeId">The ID of the node</param>
        Task RemoveNodeAsync(string nodeId);
    }