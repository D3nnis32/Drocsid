namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Metadata about a file
/// </summary>
public class FileMetadata
{
    public string Id { get; set; }
    public string Filename { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public bool IsLocal { get; set; }
    public string Path { get; set; }
}