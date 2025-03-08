namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Simplified file storage model for API operations
/// </summary>
public class FileStorage
{
    /// <summary>
    /// Unique ID for the file
    /// </summary>
    public string FileId { get; set; }
        
    /// <summary>
    /// Original filename
    /// </summary>
    public string FileName { get; set; }
        
    /// <summary>
    /// MIME type of the file
    /// </summary>
    public string ContentType { get; set; }
        
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long Size { get; set; }
        
    /// <summary>
    /// List of node IDs where this file is stored
    /// </summary>
    public List<string> NodeIds { get; set; } = new();
        
    /// <summary>
    /// Checksum for file integrity
    /// </summary>
    public string Checksum { get; set; }
        
    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}