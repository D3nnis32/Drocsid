using Drocsid.HenrikDennis2025.PluginContracts.Models;

namespace Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

/// <summary>
/// Interface for interacting with the current user session
/// </summary>
public interface IUserSessionService
{
    /// <summary>
    /// Current user ID
    /// </summary>
    Guid CurrentUserId { get; }
        
    /// <summary>
    /// Current user's username
    /// </summary>
    string CurrentUsername { get; }
        
    /// <summary>
    /// JWT token for authentication
    /// </summary>
    string AuthToken { get; }
        
    /// <summary>
    /// Base API URL for the current user session
    /// </summary>
    string ApiBaseUrl { get; }
        
    /// <summary>
    /// Get information about a channel
    /// </summary>
    Task<ChannelInfo> GetChannelInfoAsync(Guid channelId);
        
    /// <summary>
    /// Get information about a user
    /// </summary>
    Task<UserInfo> GetUserInfoAsync(Guid userId);
}