using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Response model for file location requests
/// </summary>
public class FileLocationResponse
{
    public string FileId { get; set; }
    public List<FileNodeLocation> Locations { get; set; } = new();
}