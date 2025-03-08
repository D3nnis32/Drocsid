namespace Drocsid.HenrikDennis2025.Core.DTO;


/// <summary>
/// Request model for client-side node assignment
/// </summary>
public class NodeAssignmentRequest
{
    public Guid UserId { get; set; }
    public string PreferredRegion { get; set; }
    public bool RequireFileAccess { get; set; }
    public List<string> FileIds { get; set; } = new List<string>();
}