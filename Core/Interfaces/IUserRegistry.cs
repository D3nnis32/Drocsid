using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Registry service for managing users across nodes
/// </summary>
public interface IUserRegistry
{
    /// <summary>
    /// Register a new user in the registry
    /// </summary>
    Task<bool> RegisterUserAsync(User user);
    
    /// <summary>
    /// Update an existing user in the registry
    /// </summary>
    Task<bool> UpdateUserAsync(User user);
    
    /// <summary>
    /// Get a user by ID
    /// </summary>
    Task<User> GetUserAsync(Guid userId);
    
    /// <summary>
    /// Search for users by username
    /// </summary>
    Task<IEnumerable<User>> SearchUsersAsync(string searchTerm);
    
    /// <summary>
    /// Get users by status
    /// </summary>
    Task<IEnumerable<User>> GetUsersByStatusAsync(UserStatus status);
    
    /// <summary>
    /// Get all channels a user is a member of
    /// </summary>
    Task<IEnumerable<Guid>> GetUserChannelsAsync(Guid userId);
    
    /// <summary>
    /// Get the nodes that host a particular channel
    /// </summary>
    Task<IEnumerable<string>> GetChannelNodesAsync(Guid channelId);
    
    /// <summary>
    /// Add a user to a channel
    /// </summary>
    Task<bool> AddUserToChannelAsync(Guid userId, Guid channelId);
    
    /// <summary>
    /// Remove a user from a channel
    /// </summary>
    Task<bool> RemoveUserFromChannelAsync(Guid userId, Guid channelId);
}