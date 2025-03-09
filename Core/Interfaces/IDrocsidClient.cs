namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Client interface for interacting with the Drocsid distributed file system
/// </summary>
public interface IDrocsidClient
{
    /// <summary>
    /// Uploads a file to the distributed storage system
    /// </summary>
    Task<string> UploadFileAsync(string filePath, IEnumerable<string> tags = null, int replicationFactor = 3);
        
    /// <summary>
    /// Downloads a file from the distributed storage system
    /// </summary>
    Task<bool> DownloadFileAsync(string fileId, string destinationPath);
        
    /// <summary>
    /// Deletes a file from the distributed storage system
    /// </summary>
    Task<bool> DeleteFileAsync(string fileId);
        
    /// <summary>
    /// Lists files matching certain criteria
    /// </summary>
    Task<IEnumerable<Models.FileInfo>> ListFilesAsync(string filenamePattern = null, IEnumerable<string> tags = null);
        
    /// <summary>
    /// Gets information about a specific file
    /// </summary>
    Task<Models.FileInfo> GetFileInfoAsync(string fileId);
}