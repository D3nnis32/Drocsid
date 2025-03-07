namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Request model for file registration
/// </summary>
public class FileRegistrationRequest
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
    /// IDs of nodes where the file is initially stored
    /// </summary>
    public List<string> NodeIds { get; set; }
    
    /// <summary>
    /// Checksum hash for file integrity verification
    /// </summary>
    public string Checksum { get; set; }
    
    /// <summary>
    /// Optional metadata for the file
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}