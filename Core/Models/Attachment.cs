namespace Drocsid.HenrikDennis2025.Core.Models;

public class Attachment
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long FileSize { get; set; }
    public string StoragePath { get; set; }
}