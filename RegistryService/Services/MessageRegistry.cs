using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Registry service for managing messages across nodes
/// </summary>
public class MessageRegistry : IMessageRegistry
{
    private readonly RegistryDbContext _dbContext;
    private readonly ILogger<MessageRegistry> _logger;

    public MessageRegistry(RegistryDbContext dbContext, ILogger<MessageRegistry> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> RegisterMessageAsync(Message message)
    {
        try
        {
            // Check if message already exists
            var existingMessage = await _dbContext.Messages.FirstOrDefaultAsync(m => m.Id == message.Id);
            if (existingMessage != null)
            {
                _logger.LogWarning("Message with ID {MessageId} already exists", message.Id);
                return false;
            }

            // Add message to database
            await _dbContext.Messages.AddAsync(message);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Message registered: {MessageId}", message.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering message: {MessageId}", message.Id);
            return false;
        }
    }

    public async Task<Message> GetMessageAsync(Guid messageId)
    {
        try
        {
            return await _dbContext.Messages
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.Id == messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message: {MessageId}", messageId);
            return null;
        }
    }

    public async Task<IEnumerable<Message>> GetChannelMessagesAsync(Guid channelId, DateTime since)
    {
        try
        {
            return await _dbContext.Messages
                .Include(m => m.Attachments)
                .Where(m => m.ChannelId == channelId && m.SentAt >= since)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for channel: {ChannelId} since {Since}", channelId, since);
            return Enumerable.Empty<Message>();
        }
    }

    public async Task<bool> AddMessageLocationAsync(Guid messageId, string nodeId)
    {
        try
        {
            // Check if message exists
            var message = await _dbContext.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
            if (message == null)
            {
                _logger.LogWarning("Message with ID {MessageId} not found", messageId);
                return false;
            }

            // Check if location already exists
            var existingLocation = await _dbContext.MessageLocations
                .FirstOrDefaultAsync(ml => ml.MessageId == messageId && ml.NodeId == nodeId);
                
            if (existingLocation != null)
            {
                // Already exists
                return true;
            }

            // Add message location
            await _dbContext.MessageLocations.AddAsync(new MessageLocation
            {
                MessageId = messageId,
                NodeId = nodeId,
                CreatedAt = DateTime.UtcNow
            });
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Message {MessageId} location added: {NodeId}", messageId, nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message location: Message {MessageId}, Node {NodeId}", messageId, nodeId);
            return false;
        }
    }

    public async Task<bool> RemoveMessageLocationAsync(Guid messageId, string nodeId)
    {
        try
        {
            var location = await _dbContext.MessageLocations
                .FirstOrDefaultAsync(ml => ml.MessageId == messageId && ml.NodeId == nodeId);
                
            if (location == null)
            {
                // Doesn't exist
                return true;
            }

            // Remove message location
            _dbContext.MessageLocations.Remove(location);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Message {MessageId} location removed: {NodeId}", messageId, nodeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing message location: Message {MessageId}, Node {NodeId}", messageId, nodeId);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetMessageNodesAsync(Guid messageId)
    {
        try
        {
            return await _dbContext.MessageLocations
                .Where(ml => ml.MessageId == messageId)
                .Select(ml => ml.NodeId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting nodes for message: {MessageId}", messageId);
            return Enumerable.Empty<string>();
        }
    }
}