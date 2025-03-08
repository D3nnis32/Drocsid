using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Request model for message synchronization
/// </summary>
public class MessageSyncRequest
{
    /// <summary>
    /// The message to sync
    /// </summary>
    public Message Message { get; set; }
    
    /// <summary>
    /// The ID of the node that originated the message
    /// </summary>
    public string OriginNodeId { get; set; }
    
    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime Timestamp { get; set; }
}