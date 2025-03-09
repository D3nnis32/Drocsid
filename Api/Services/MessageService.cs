using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Api.Services;

public class MessageService : IMessageService
{
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<User> _userRepository;

    public MessageService(
        IRepository<Message> messageRepository,
        IRepository<User> userRepository)
    {
        _messageRepository = messageRepository;
        _userRepository = userRepository;
    }

    public async Task<Message> GetMessageByIdAsync(Guid messageId)
    {
        var message = await _messageRepository.GetByIdAsync(messageId);
        return message;
    }

    public async Task<IEnumerable<Message>> GetChannelMessagesAsync(Guid channelId, int skip = 0, int take = 50)
    {
        var messages = await _messageRepository.FindAsync(m => m.ChannelId == channelId);
        return messages
            .OrderByDescending(m => m.SentAt)
            .Skip(skip)
            .Take(take)
            .Reverse(); // Return in chronological order
    }

    public async Task<Message> CreateMessageAsync(Message message)
    {
        message.Id = Guid.NewGuid();
        message.SentAt = DateTime.UtcNow;
        
        // Always store the sender name when creating a message
        var sender = await _userRepository.GetByIdAsync(message.SenderId);
        if (sender != null)
        {
            message.SenderName = sender.Username;
        }
        else
        {
            message.SenderName = "Anonymous";
        }
        
        await _messageRepository.AddAsync(message);
        return message;
    }

    public async Task DeleteMessageAsync(Guid messageId)
    {
        await _messageRepository.DeleteAsync(messageId);
    }
    
    public async Task<Message> SyncMessageAsync(Message message)
    {
        // Ensure the sender name is always present when syncing a message
        if (string.IsNullOrEmpty(message.SenderName))
        {
            var sender = await _userRepository.GetByIdAsync(message.SenderId);
            if (sender != null)
            {
                message.SenderName = sender.Username;
            }
            else
            {
                message.SenderName = "Unknown User"; // Fallback name
            }
        }
        
        // Check if message exists
        var existingMessage = await _messageRepository.GetByIdAsync(message.Id);
    
        if (existingMessage != null)
        {
            // Update existing message
            await _messageRepository.UpdateAsync(message);
        }
        else
        {
            // Add as new message
            await _messageRepository.AddAsync(message);
        }
    
        return message;
    }
}