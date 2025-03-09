namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Information about a node hosting a file
/// </summary>
public class FileNodeLocation
{
    public string NodeId { get; set; }
    public string Endpoint { get; set; }
    public string Region { get; set; }
}