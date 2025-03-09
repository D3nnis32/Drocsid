using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Api.Services;

public class MessageService : IMessageService
{
    private readonly IRepository<Message> _messageRepository;

    public MessageService(IRepository<Message> messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task<Message> GetMessageByIdAsync(Guid messageId)
    {
        return await _messageRepository.GetByIdAsync(messageId);
    }

    public async Task<IEnumerable<Message>> GetChannelMessagesAsync(Guid channelId, int skip = 0, int take = 50)
    {
        var allMessages = await _messageRepository.FindAsync(m => m.ChannelId == channelId);
        return allMessages
            .OrderByDescending(m => m.SentAt)
            .Skip(skip)
            .Take(take)
            .Reverse(); // Return in chronological order
    }

    public async Task<Message> CreateMessageAsync(Message message)
    {
        message.Id = Guid.NewGuid();
        message.SentAt = DateTime.UtcNow;
        await _messageRepository.AddAsync(message);
        return message;
    }

    public async Task DeleteMessageAsync(Guid messageId)
    {
        await _messageRepository.DeleteAsync(messageId);
    }
    
    public async Task<Message> SyncMessageAsync(Message message)
    {
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