using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Registry service for managing channels across nodes
/// </summary>
public class ChannelRegistry : IChannelRegistry
{
    private readonly RegistryDbContext _dbContext;
    private readonly ILogger<ChannelRegistry> _logger;

    public ChannelRegistry(RegistryDbContext dbContext, ILogger<ChannelRegistry> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> RegisterChannelAsync(Channel channel)
    {
        try
        {
            // Check if channel already exists
            var existingChannel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            if (existingChannel != null)
            {
                _logger.LogWarning("Channel with ID {ChannelId} already exists", channel.Id);
                return false;
            }

            // Add channel to database
            await _dbContext.Channels.AddAsync(channel);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Channel registered: {ChannelName}, ID: {ChannelId}", channel.Name, channel.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering channel: {ChannelName}", channel.Name);
            return false;
        }
    }

    public async Task<bool> UpdateChannelAsync(Channel channel)
    {
        try
        {
            var existingChannel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channel.Id);
            if (existingChannel == null)
            {
                _logger.LogWarning("Channel with ID {ChannelId} not found for update", channel.Id);
                return false;
            }

            // Update channel properties
            _dbContext.Entry(existingChannel).CurrentValues.SetValues(channel);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Channel updated: {ChannelName}, ID: {ChannelId}", channel.Name, channel.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating channel: {ChannelId}", channel.Id);
            return false;
        }
    }

    public async Task<Channel> GetChannelAsync(Guid channelId)
    {
        try
        {
            return await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel: {ChannelId}", channelId);
            return null;
        }
    }

    public async Task<IEnumerable<Channel>> GetUserChannelsAsync(Guid userId)
    {
        try
        {
            return await _dbContext.UserChannels
                .Where(uc => uc.UserId == userId)
                .Join(_dbContext.Channels,
                    uc => uc.ChannelId,
                    c => c.Id,
                    (uc, c) => c)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channels for user: {UserId}", userId);
            return Enumerable.Empty<Channel>();
        }
    }

    public async Task<bool> AddChannelNodeAsync(Guid channelId, string nodeId)
    {
        try
        {
            // Check if channel exists
            var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
            if (channel == null)
            {
                _logger.LogWarning("Channel with ID {ChannelId} not found", channelId);
                return false;
            }

            // Check if node exists
            var node = await _dbContext.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId);
            if (node == null)
            {
                _logger.LogWarning("Node with ID {NodeId} not found", nodeId);
                return false;
            }

            // Check if mapping already exists
            var existingMapping = await _dbContext.ChannelNodes
                .FirstOrDefaultAsync(cn => cn.ChannelId == channelId && cn.NodeId == nodeId);
                
            if (existingMapping != null)
            {
                // Already exists
                return true;
            }

            // Add channel node mapping
            await _dbContext.ChannelNodes.AddAsync(new ChannelNode
            {
                ChannelId = channelId,
                NodeId = nodeId,
                CreatedAt = DateTime.UtcNow
            });
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Channel {ChannelId} node added: {NodeId}", channelId, nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding channel node: Channel {ChannelId}, Node {NodeId}", channelId, nodeId);
            return false;
        }
    }

    public async Task<bool> RemoveChannelNodeAsync(Guid channelId, string nodeId)
    {
        try
        {
            var mapping = await _dbContext.ChannelNodes
                .FirstOrDefaultAsync(cn => cn.ChannelId == channelId && cn.NodeId == nodeId);
                
            if (mapping == null)
            {
                // Doesn't exist
                return true;
            }

            // Remove channel node mapping
            _dbContext.ChannelNodes.Remove(mapping);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Channel {ChannelId} node removed: {NodeId}", channelId, nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing channel node: Channel {ChannelId}, Node {NodeId}", channelId, nodeId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetChannelNodesAsync(Guid channelId)
    {
        try
        {
            return await _dbContext.ChannelNodes
                .Where(cn => cn.ChannelId == channelId)
                .Select(cn => cn.NodeId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nodes for channel: {ChannelId}", channelId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<bool> AddChannelMemberAsync(Guid channelId, Guid userId)
    {
        try
        {
            // Check if channel exists
            var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
            if (channel == null)
            {
                _logger.LogWarning("Channel with ID {ChannelId} not found", channelId);
                return false;
            }

            // Check if user exists
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return false;
            }

            // Check if membership already exists
            var existingMembership = await _dbContext.UserChannels
                .FirstOrDefaultAsync(uc => uc.ChannelId == channelId && uc.UserId == userId);
                
            if (existingMembership != null)
            {
                // Already exists
                return true;
            }

            // Add user channel membership
            await _dbContext.UserChannels.AddAsync(new UserChannel
            {
                ChannelId = channelId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Channel {ChannelId} member added: {UserId}", channelId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding channel member: Channel {ChannelId}, User {UserId}", channelId, userId);
            return false;
        }
    }

    public async Task<bool> RemoveChannelMemberAsync(Guid channelId, Guid userId)
    {
        try
        {
            var membership = await _dbContext.UserChannels
                .FirstOrDefaultAsync(uc => uc.ChannelId == channelId && uc.UserId == userId);
                
            if (membership == null)
            {
                // Doesn't exist
                return true;
            }

            // Remove user channel membership
            _dbContext.UserChannels.Remove(membership);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Channel {ChannelId} member removed: {UserId}", channelId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing channel member: Channel {ChannelId}, User {UserId}", channelId, userId);
            return false;
        }
    }
}