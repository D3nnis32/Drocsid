namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Request model for requesting reassignment to a new storage node
/// </summary>
public class NodeReassignmentRequest
{
    /// <summary>
    /// The ID of the current node that failed (if known)
    /// </summary>
    public string CurrentNodeId { get; set; }
        
    /// <summary>
    /// The reason for requesting reassignment
    /// Valid values: NODE_FAILURE, LOAD_BALANCING, REGION_PREFERENCE, MANUAL, AUTH_FAILURE
    /// </summary>
    public string Reason { get; set; } = "NODE_FAILURE";
        
    /// <summary>
    /// Optional preferred region for the new node
    /// </summary>
    public string PreferredRegion { get; set; }
        
    /// <summary>
    /// Optional list of file IDs that need to be accessible from the new node
    /// </summary>
    public List<string> RequiredFileAccess { get; set; } = new List<string>();
}