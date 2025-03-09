using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Registry service for managing messages across nodes
/// </summary>
public interface IMessageRegistry
{
    /// <summary>
    /// Register a new message in the registry
    /// </summary>
    Task<bool> RegisterMessageAsync(Message message);
    
    /// <summary>
    /// Get a message by ID
    /// </summary>
    Task<Message> GetMessageAsync(Guid messageId);
    
    /// <summary>
    /// Get all messages for a channel since a specific time
    /// </summary>
    Task<IEnumerable<Message>> GetChannelMessagesAsync(Guid channelId, DateTime since);
    
    /// <summary>
    /// Add a node location for a message
    /// </summary>
    Task<bool> AddMessageLocationAsync(Guid messageId, string nodeId);
    
    /// <summary>
    /// Remove a node location for a message
    /// </summary>
    Task<bool> RemoveMessageLocationAsync(Guid messageId, string nodeId);
    
    /// <summary>
    /// Get all nodes that have a specific message
    /// </summary>
    Task<IEnumerable<string>> GetMessageNodesAsync(Guid messageId);
}