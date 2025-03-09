using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
    /// Interface for file registry operations
    /// </summary>
    public interface IFileRegistry
    {
        /// <summary>
        /// Registers a new file in the system
        /// </summary>
        /// <param name="file">The file to register</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RegisterFileAsync(StoredFile file);
        
        /// <summary>
        /// Registers a new file from FileStorage DTO
        /// </summary>
        /// <param name="fileStorage">The FileStorage DTO to register</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RegisterFileStorageAsync(FileStorage fileStorage);

        /// <summary>
        /// Updates an existing file's metadata
        /// </summary>
        /// <param name="file">The file with updated information</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateFileAsync(StoredFile file);

        /// <summary>
        /// Gets a file by its ID
        /// </summary>
        /// <param name="fileId">The ID of the file to retrieve</param>
        /// <returns>The file if found, null otherwise</returns>
        Task<StoredFile> GetFileAsync(string fileId);

        /// <summary>
        /// Gets files by filename or partial match
        /// </summary>
        /// <param name="filename">The filename or pattern to search for</param>
        /// <returns>A list of matching files</returns>
        Task<IEnumerable<StoredFile>> FindFilesByNameAsync(string filename);

        /// <summary>
        /// Gets files stored on a specific node
        /// </summary>
        /// <param name="nodeId">The ID of the node</param>
        /// <returns>A list of files stored on the node</returns>
        Task<IEnumerable<StoredFile>> GetFilesByNodeAsync(string nodeId);

        /// <summary>
        /// Adds a file location (node where the file is stored)
        /// </summary>
        /// <param name="fileId">The ID of the file</param>
        /// <param name="nodeId">The ID of the node</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> AddFileLocationAsync(string fileId, string nodeId);

        /// <summary>
        /// Removes a file location (when a file is removed from a node)
        /// </summary>
        /// <param name="fileId">The ID of the file</param>
        /// <param name="nodeId">The ID of the node</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> RemoveFileLocationAsync(string fileId, string nodeId);

        /// <summary>
        /// Deletes a file from the system (all copies)
        /// </summary>
        /// <param name="fileId">The ID of the file to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteFileAsync(string fileId);
        
        /// <summary>
        /// Gets all files in the system
        /// </summary>
        /// <returns>All files in the system</returns>
        Task<IEnumerable<StoredFile>> GetAllFilesAsync();
        
        /// <summary>
        /// Gets file information for UI display or operations
        /// </summary>
        /// <param name="fileId">The ID of the file</param>
        /// <returns>File information or null if not found</returns>
        Task<FileStorage> GetFileInfoAsync(string fileId);
        Task UpdateFileAccessTimeAsync(string fileId);
    }