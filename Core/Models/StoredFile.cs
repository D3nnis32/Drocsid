namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Model for a stored file
/// </summary>
public class StoredFile
{
    /// <summary>
    /// Unique ID for the file
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Original filename
    /// </summary>
    public string Filename { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// MIME type of the file
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// MD5 checksum of the file
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// When the file was created in the system
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the file was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// User who owns the file
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// List of tags for categorizing the file
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// When the file was last accessed
    /// </summary>
    public DateTime? LastAccessed { get; set; }

    /// <summary>
    /// Metadata as a dictionary
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// List of nodes where this file is stored
    /// </summary>
    public List<string> NodeLocations { get; set; } = new();
}