using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Drocsid.HenrikDennis2025.Server.Services;

public class ChannelService : IChannelService
    {
        private readonly IRepository<Channel> _channelRepository;

        public ChannelService(IRepository<Channel> channelRepository)
        {
            _channelRepository = channelRepository;
        }

        public async Task<Channel> GetChannelByIdAsync(Guid channelId)
        {
            return await _channelRepository.GetByIdAsync(channelId);
        }

        public async Task<IEnumerable<Channel>> GetUserChannelsAsync(Guid userId)
        {
            var allChannels = await _channelRepository.GetAllAsync();
            return allChannels.Where(c => c.MemberIds.Contains(userId));
        }

        public async Task<Channel> CreateChannelAsync(Channel channel)
        {
            channel.Id = Guid.NewGuid();
            await _channelRepository.AddAsync(channel);
            return channel;
        }

        public async Task UpdateChannelAsync(Channel channel)
        {
            await _channelRepository.UpdateAsync(channel);
        }

        public async Task DeleteChannelAsync(Guid channelId)
        {
            await _channelRepository.DeleteAsync(channelId);
        }

        public async Task<bool> AddUserToChannelAsync(Guid channelId, Guid userId)
        {
            var channel = await _channelRepository.GetByIdAsync(channelId);
            if (channel == null)
            {
                return false;
            }

            if (channel.MemberIds.Contains(userId))
            {
                return true; // User already in channel
            }

            channel.MemberIds.Add(userId);
            await _channelRepository.UpdateAsync(channel);
            return true;
        }

        public async Task<bool> RemoveUserFromChannelAsync(Guid channelId, Guid userId)
        {
            var channel = await _channelRepository.GetByIdAsync(channelId);
            if (channel == null)
            {
                return false;
            }

            if (!channel.MemberIds.Contains(userId))
            {
                return false; // User not in channel
            }

            channel.MemberIds.Remove(userId);
            await _channelRepository.UpdateAsync(channel);
            return true;
        }
    }