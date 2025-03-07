namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents information about a file in the distributed system
/// </summary>
public class FileStorage
{
    /// <summary>
    /// Unique identifier for the file
    /// </summary>
    public string FileId { get; set; }
        
    /// <summary>
    /// Original name of the file
    /// </summary>
    public string FileName { get; set; }
        
    /// <summary>
    /// MIME content type of the file
    /// </summary>
    public string ContentType { get; set; }
        
    /// <summary>
    /// Size of the file in bytes
    /// </summary>
    public long Size { get; set; }
        
    /// <summary>
    /// IDs of nodes where the file is stored
    /// </summary>
    public List<string> NodeIds { get; set; } = new List<string>();
        
    /// <summary>
    /// Checksum hash for file integrity verification
    /// </summary>
    public string Checksum { get; set; }
        
    /// <summary>
    /// When the file was created in the system
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    /// <summary>
    /// When the file was last accessed
    /// </summary>
    public DateTime? LastAccessed { get; set; }
        
    /// <summary>
    /// Optional metadata for the file
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}