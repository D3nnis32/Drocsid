using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Registry service for managing channels across nodes
/// </summary>
public interface IChannelRegistry
{
    /// <summary>
    /// Register a new channel in the registry
    /// </summary>
    Task<bool> RegisterChannelAsync(Channel channel);
    
    /// <summary>
    /// Update an existing channel in the registry
    /// </summary>
    Task<bool> UpdateChannelAsync(Channel channel);
    
    /// <summary>
    /// Get a channel by ID
    /// </summary>
    Task<Channel> GetChannelAsync(Guid channelId);
    
    /// <summary>
    /// Get all channels for a user
    /// </summary>
    Task<IEnumerable<Channel>> GetUserChannelsAsync(Guid userId);
    
    /// <summary>
    /// Add a node as hosting this channel
    /// </summary>
    Task<bool> AddChannelNodeAsync(Guid channelId, string nodeId);
    
    /// <summary>
    /// Remove a node from hosting this channel
    /// </summary>
    Task<bool> RemoveChannelNodeAsync(Guid channelId, string nodeId);
    
    /// <summary>
    /// Get all nodes that host a channel
    /// </summary>
    Task<IEnumerable<string>> GetChannelNodesAsync(Guid channelId);
    
    /// <summary>
    /// Add a member to a channel
    /// </summary>
    Task<bool> AddChannelMemberAsync(Guid channelId, Guid userId);
    
    /// <summary>
    /// Remove a member from a channel
    /// </summary>
    Task<bool> RemoveChannelMemberAsync(Guid channelId, Guid userId);
}