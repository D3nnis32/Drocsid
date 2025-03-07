using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
    /// Interface for managing file registry operations
    /// </summary>
    public interface IFileRegistry
    {
        /// <summary>
        /// Registers a file and its storage locations
        /// </summary>
        /// <param name="fileStorage">Information about the file being registered</param>
        Task RegisterFileAsync(FileStorage fileStorage);
        
        /// <summary>
        /// Gets information about a file including its storage locations
        /// </summary>
        /// <param name="fileId">The unique identifier of the file</param>
        Task<FileStorage?> GetFileInfoAsync(string fileId);
        
        /// <summary>
        /// Updates the storage locations for a file
        /// </summary>
        /// <param name="fileId">The unique identifier of the file</param>
        /// <param name="nodeIds">The IDs of nodes where the file is stored</param>
        Task UpdateFileLocationsAsync(string fileId, List<string> nodeIds);
        
        /// <summary>
        /// Adds a new storage location for a file
        /// </summary>
        /// <param name="fileId">The unique identifier of the file</param>
        /// <param name="nodeId">The ID of the node where the file is newly stored</param>
        Task AddFileLocationAsync(string fileId, string nodeId);
        
        /// <summary>
        /// Removes a storage location for a file
        /// </summary>
        /// <param name="fileId">The unique identifier of the file</param>
        /// <param name="nodeId">The ID of the node from which the file is removed</param>
        Task RemoveFileLocationAsync(string fileId, string nodeId);
        
        /// <summary>
        /// Gets all files stored on a specific node
        /// </summary>
        /// <param name="nodeId">The ID of the node</param>
        Task<List<FileStorage>> GetFilesByNodeAsync(string nodeId);
        
        /// <summary>
        /// Removes a file from the registry entirely
        /// </summary>
        /// <param name="fileId">The unique identifier of the file</param>
        Task DeleteFileAsync(string fileId);
        
        /// <summary>
        /// Checks if a file's replication factor meets the minimum requirement
        /// </summary>
        /// <param name="fileId">The unique identifier of the file</param>
        /// <param name="minReplicationFactor">The minimum number of copies required</param>
        Task<bool> CheckReplicationFactorAsync(string fileId, int minReplicationFactor);
        
        /// <summary>
        /// Gets files that need additional replication to meet the minimum factor
        /// </summary>
        /// <param name="minReplicationFactor">The minimum number of copies required</param>
        Task<List<FileStorage>> GetFilesNeedingReplicationAsync(int minReplicationFactor);
    }