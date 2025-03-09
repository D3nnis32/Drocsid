namespace Drocsid.HenrikDennis2025.Core.Models;

public class Attachment
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    // Adding the missing properties
    public string StoragePath { get; set; }
    public long FileSize { get; set; }
}