using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces.Services;

public interface IMessageService
{
    Task<Message> GetMessageByIdAsync(Guid messageId);
    Task<IEnumerable<Message>> GetChannelMessagesAsync(Guid channelId, int skip = 0, int take = 50);
    Task<Message> CreateMessageAsync(Message message);
    Task DeleteMessageAsync(Guid messageId);
}