namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents a file attachment in a message
/// </summary>
public class Attachment
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string ContentType { get; set; }
    public long FileSize { get; set; }
    // Adding the missing properties
    public string StoragePath { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

}