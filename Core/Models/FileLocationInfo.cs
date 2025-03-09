namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Information about a file's location within the system
/// </summary>
public class FileLocationInfo
{
    public string FileId { get; set; }
    public string NodeEndpoint { get; set; }
    public long FileSize { get; set; }
    public string ContentType { get; set; }
}