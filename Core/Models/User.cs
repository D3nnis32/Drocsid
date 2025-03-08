namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents a user in the system
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Offline;
    public DateTime LastSeen { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string PreferredRegion { get; set; }
    public string CurrentNodeId { get; set; }
}

public enum UserStatus
{
    Online,
    Away,
    DoNotDisturb,
    Offline
}

/// <summary>
/// Internal model for status updates between registry and nodes
/// </summary>
public class UserStatusUpdate
{
    /// <summary>
    /// The user's status
    /// </summary>
    public UserStatus Status { get; set; }
    
    /// <summary>
    /// The ID of the node that sent the update
    /// </summary>
    public string NodeId { get; set; }
    
    /// <summary>
    /// When the status was updated
    /// </summary>
    public DateTime Timestamp { get; set; }
}