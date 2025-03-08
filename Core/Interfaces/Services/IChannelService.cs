using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Core.Interfaces.Services;

public interface IChannelService
{
    Task<Channel> GetChannelByIdAsync(Guid channelId);
    Task<IEnumerable<Channel>> GetUserChannelsAsync(Guid userId);
    Task<Channel> CreateChannelAsync(Channel channel);
    Task UpdateChannelAsync(Channel channel);
    Task DeleteChannelAsync(Guid channelId);
    Task<bool> AddUserToChannelAsync(Guid channelId, Guid userId);
    Task<bool> RemoveUserFromChannelAsync(Guid channelId, Guid userId);
    Task<Channel> SyncChannelAsync(Channel channel);
}