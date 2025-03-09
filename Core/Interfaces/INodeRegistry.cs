using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
    /// Interface for node registry operations
    /// </summary>
    public interface INodeRegistry
    {
        /// <summary>
        /// Registers a new node in the registry
        /// </summary>
        /// <param name="node">The node to register</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RegisterNodeAsync(StorageNode node);

        /// <summary>
        /// Updates an existing node in the registry
        /// </summary>
        /// <param name="node">The node with updated information</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateNodeAsync(StorageNode node);

        /// <summary>
        /// Gets a node by its ID
        /// </summary>
        /// <param name="nodeId">The ID of the node to retrieve</param>
        /// <returns>The node if found, null otherwise</returns>
        Task<StorageNode> GetNodeAsync(string nodeId);

        /// <summary>
        /// Gets all nodes in the registry
        /// </summary>
        /// <param name="includeOffline">Whether to include offline/unhealthy nodes</param>
        /// <returns>A list of all nodes</returns>
        Task<IEnumerable<StorageNode>> GetAllNodesAsync(bool includeOffline = false);

        /// <summary>
        /// Marks a node as unhealthy
        /// </summary>
        /// <param name="nodeId">The ID of the node to mark unhealthy</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> MarkNodeUnhealthyAsync(string nodeId);

        /// <summary>
        /// Removes a node from the registry
        /// </summary>
        /// <param name="nodeId">The ID of the node to remove</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RemoveNodeAsync(string nodeId);
        
        /// <summary>
        /// Finds nodes that match certain criteria
        /// </summary>
        /// <param name="region">Optional region filter</param>
        /// <param name="tags">Optional tags to filter on</param>
        /// <param name="minAvailableStorage">Minimum available storage</param>
        /// <param name="healthyOnly">Whether to only include healthy nodes</param>
        /// <returns>A list of nodes matching the criteria</returns>
        Task<IEnumerable<StorageNode>> FindNodesAsync(
            string region = null, 
            IEnumerable<string> tags = null, 
            long minAvailableStorage = 0,
            bool healthyOnly = true);
            
        /// <summary>
        /// Gets nodes with the most available storage space
        /// </summary>
        /// <param name="count">Maximum number of nodes to return</param>
        /// <param name="region">Optional region filter</param>
        /// <returns>List of nodes with the most available storage</returns>
        Task<IEnumerable<StorageNode>> GetNodesWithMostStorageAsync(int count, string region = null);
        
        /// <summary>
        /// Gets nodes with the lowest load
        /// </summary>
        /// <param name="count">Maximum number of nodes to return</param>
        /// <param name="region">Optional region filter</param>
        /// <returns>List of nodes with the lowest load</returns>
        Task<IEnumerable<StorageNode>> GetNodesWithLowestLoadAsync(int count, string region = null);
        
        /// <summary>
        /// Gets nodes by their IDs
        /// </summary>
        /// <param name="nodeIds">List of node IDs to retrieve</param>
        /// <returns>List of nodes matching the provided IDs</returns>
        Task<IEnumerable<NodeInfo>> GetNodesByIdsAsync(List<string> nodeIds);
        
        /// <summary>
        /// Gets nodes available for file storage (healthy and with sufficient space)
        /// </summary>
        /// <param name="minAvailableSpace">Minimum space required</param>
        /// <param name="region">Optional region filter</param>
        /// <returns>List of available nodes</returns>
        Task<IEnumerable<NodeInfo>> GetAvailableNodesAsync(long minAvailableSpace = 0, string region = null);
    }