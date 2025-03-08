namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents a file attachment in a message
/// </summary>
public class Attachment
{
    public Guid Id { get; set; }
    public string Filename { get; set; }
    public string ContentType { get; set; }
    public string Path { get; set; }
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}