namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Response model returned when a node reassignment request is successful
/// </summary>
public class NodeReassignmentResponse
{
    /// <summary>
    /// The new node endpoint URL
    /// </summary>
    public string NodeEndpoint { get; set; }
        
    /// <summary>
    /// The ID of the new node
    /// </summary>
    public string NodeId { get; set; }
        
    /// <summary>
    /// The region of the new node
    /// </summary>
    public string Region { get; set; }
        
    /// <summary>
    /// Optional updated authentication token
    /// </summary>
    public string Token { get; set; }
        
    /// <summary>
    /// When the token expires (if a new one was issued)
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }
}