namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Event raised when a user's status changes.
/// </summary>
public class UserStatusChangedEvent : EventBase
{
    /// <summary>
    /// Gets the ID of the user whose status changed.
    /// </summary>
    public Guid UserId { get; }
        
    /// <summary>
    /// Gets the new status of the user.
    /// </summary>
    public Core.Models.UserStatus NewStatus { get; }
        
    /// <summary>
    /// Initializes a new instance of the UserStatusChangedEvent class.
    /// </summary>
    /// <param name="userId">The ID of the user whose status changed.</param>
    /// <param name="newStatus">The new status of the user.</param>
    public UserStatusChangedEvent(Guid userId, Core.Models.UserStatus newStatus)
    {
        UserId = userId;
        NewStatus = newStatus;
    }
}