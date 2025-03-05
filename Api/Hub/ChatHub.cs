using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Drocsid.HenrikDennis2025.Api.Hub;

[Authorize]
    public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly IChannelService _channelService;
        private readonly IUserService _userService;
        
        public ChatHub(IChannelService channelService, IUserService userService)
        {
            _channelService = channelService;
            _userService = userService;
        }
        
        public override async Task OnConnectedAsync()
        {
            // Get user ID from authenticated user
            var userId = GetCurrentUserId();
            
            // Get all user's channels and join those groups
            var channels = await _channelService.GetUserChannelsAsync(userId);
            foreach (var channel in channels)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, channel.Id.ToString());
            }
            
            // Update user status to online
            await _userService.UpdateUserStatusAsync(userId, UserStatus.Online);
            
            // Notify all user's channels about the status change
            foreach (var channel in channels)
            {
                await Clients.Group(channel.Id.ToString()).SendAsync("UserStatusChanged", userId, UserStatus.Online);
            }
            
            await base.OnConnectedAsync();
        }
        
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            // Get user ID from authenticated user
            var userId = GetCurrentUserId();
            
            // Update user status to offline
            await _userService.UpdateUserStatusAsync(userId, UserStatus.Offline);
            
            // Get all user's channels
            var channels = await _channelService.GetUserChannelsAsync(userId);
            
            // Notify all user's channels about the status change and leave groups
            foreach (var channel in channels)
            {
                await Clients.Group(channel.Id.ToString()).SendAsync("UserStatusChanged", userId, UserStatus.Offline);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel.Id.ToString());
            }
            
            await base.OnDisconnectedAsync(exception);
        }
        
        public async Task JoinChannel(Guid channelId)
        {
            var userId = GetCurrentUserId();
            
            // Verify user is a member of the channel
            var channel = await _channelService.GetChannelByIdAsync(channelId);
            if (channel == null || !channel.MemberIds.Contains(userId))
            {
                throw new HubException("Not authorized to join this channel");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());
        }
        
        public async Task LeaveChannel(Guid channelId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());
        }
        
        public async Task UpdateStatus(UserStatus status)
        {
            var userId = GetCurrentUserId();
            
            // Update user status
            await _userService.UpdateUserStatusAsync(userId, status);
            
            // Notify all user's channels about the status change
            var channels = await _channelService.GetUserChannelsAsync(userId);
            foreach (var channel in channels)
            {
                await Clients.Group(channel.Id.ToString()).SendAsync("UserStatusChanged", userId, status);
            }
        }
        
        public async Task SendTypingNotification(Guid channelId)
        {
            var userId = GetCurrentUserId();
            
            // Verify user is a member of the channel
            var channel = await _channelService.GetChannelByIdAsync(channelId);
            if (channel == null || !channel.MemberIds.Contains(userId))
            {
                throw new HubException("Not authorized for this channel");
            }
            
            // Send typing notification to channel (except sender)
            await Clients.OthersInGroup(channelId.ToString()).SendAsync("UserTyping", userId, channelId);
        }
        
        private Guid GetCurrentUserId()
        {
            // In a real application, get this from the authenticated user claims
            // For now, return a dummy user ID
            return new Guid("11111111-1111-1111-1111-111111111111");
        }
    }