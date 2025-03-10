using System.Security.Claims;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.Api.Hub;

[Authorize]
public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly IChannelService _channelService;
    private readonly IUserService _userService;
    private readonly IMessageService _messageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeRegistrationOptions _nodeOptions;
    private readonly ILogger<ChatHub> _logger;
    
    // Add a static dictionary to track user connections across hubs
    private static readonly Dictionary<Guid, HashSet<string>> UserConnections = new();
    private static readonly object ConnectionLock = new();

    public ChatHub(
        IChannelService channelService,
        IUserService userService,
        IMessageService messageService,
        IHttpClientFactory httpClientFactory,
        IOptions<NodeRegistrationOptions> nodeOptions,
        ILogger<ChatHub> logger)
    {
        _channelService = channelService;
        _userService = userService;
        _messageService = messageService;
        _httpClientFactory = httpClientFactory;
        _nodeOptions = nodeOptions.Value;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            // Get user ID from authenticated user
            var userId = GetCurrentUserId();

            // Get user details
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                throw new HubException("User not found");
            }

            // Check if this is a reconnection after node failover
            bool isFailoverReconnection = false;
            string reconnectionToken = Context.GetHttpContext().Request.Query["reconnection"].ToString();
            if (!string.IsNullOrEmpty(reconnectionToken))
            {
                isFailoverReconnection = true;
                _logger.LogInformation("User {UserId} reconnecting after node failover with token {Token}", 
                    userId, reconnectionToken);
            }

            // Add connection ID to tracked connections for this user
            TrackUserConnection(userId, Context.ConnectionId);

            // Update user status to online
            if (!isFailoverReconnection)
            {
                // Only update status if this is a fresh connection, not failover
                await _userService.UpdateUserStatusAsync(userId, UserStatus.Online);
            }

            // Get all user's channels and join those groups
            var channels = await _channelService.GetUserChannelsAsync(userId);
            
            foreach (var channel in channels)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, channel.Id.ToString());
                
                // Only notify other users if this is a new connection, not a failover
                if (!isFailoverReconnection)
                {
                    // Notify all members in the channel about the status change
                    await Clients.OthersInGroup(channel.Id.ToString()).SendAsync("UserStatusChanged", userId, UserStatus.Online);
                }
            }
            
            // Notify registry about the user's online status
            if (!isFailoverReconnection)
            {
                await NotifyRegistryAboutUserStatusAsync(userId, UserStatus.Online);
            }

            await base.OnConnectedAsync();
            
            // If this is a reconnection, send a confirmation to the client
            if (isFailoverReconnection)
            {
                await Clients.Caller.SendAsync("ReconnectionComplete", new { success = true, nodeId = _nodeOptions.NodeId });
            }
            else
            {
                _logger.LogInformation("User {UserId} connected to hub", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync");
            
            // Let the client know there was a connection error
            await Clients.Caller.SendAsync("ConnectionError", ex.Message);
        }
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        try
        {
            // Get user ID from authenticated user
            var userId = GetCurrentUserId();

            // Remove this connection from tracked connections
            bool isLastConnection = RemoveUserConnection(userId, Context.ConnectionId);

            // Only update user status if this is their last connection
            if (isLastConnection)
            {
                // Update user status to offline
                await _userService.UpdateUserStatusAsync(userId, UserStatus.Offline);

                // Get all user's channels
                var channels = await _channelService.GetUserChannelsAsync(userId);

                // Notify all user's channels about the status change
                foreach (var channel in channels)
                {
                    await Clients.Group(channel.Id.ToString()).SendAsync("UserStatusChanged", userId, UserStatus.Offline);
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel.Id.ToString());
                }
                
                // Notify registry about the user's offline status
                await NotifyRegistryAboutUserStatusAsync(userId, UserStatus.Offline);
                
                _logger.LogInformation("User {UserId} disconnected from hub (last connection)", userId);
            }
            else
            {
                // Just remove from groups but don't change status
                var channels = await _channelService.GetUserChannelsAsync(userId);
                foreach (var channel in channels)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel.Id.ToString());
                }
                
                _logger.LogDebug("User {UserId} disconnected a connection, but still has active connections", userId);
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync: {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Handle client signaling that they are being redirected to another node
    /// </summary>
    public async Task NotifyRedirecting(string newNodeId)
    {
        var userId = GetCurrentUserId();
        
        _logger.LogInformation("User {UserId} notifying that they are being redirected to node {NodeId}", 
            userId, newNodeId);
        
        // Don't change user status, as they should reconnect to another node shortly
        // But we do need to clean up local resources
        
        try
        {
            // Get all user's channels
            var channels = await _channelService.GetUserChannelsAsync(userId);
            
            // Remove from groups
            foreach (var channel in channels)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel.Id.ToString());
            }
            
            // Remove connection tracking
            RemoveUserConnection(userId, Context.ConnectionId);
            
            // Notify the client we acknowledged their redirect
            await Clients.Caller.SendAsync("RedirectAcknowledged", new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing redirect notification for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Synchronize a user's state after reconnecting to a different node
    /// </summary>
    public async Task SyncAfterNodeMigration(DateTime lastMessageTimestamp)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} requesting state sync after node migration", userId);
            
            // Get all user's channels
            var channels = await _channelService.GetUserChannelsAsync(userId);
            
            // For each channel, get messages since the last timestamp
            var recentMessages = new Dictionary<Guid, List<Message>>();
            
            foreach (var channel in channels)
            {
                var messages = await _messageService.GetChannelMessagesAsync(
                    channel.Id, 
                    0, 
                    100); // Limit to reasonable number
                    
                // Filter to only messages after the timestamp
                var filteredMessages = messages
                    .Where(m => m.SentAt > lastMessageTimestamp)
                    .ToList();
                    
                if (filteredMessages.Any())
                {
                    recentMessages[channel.Id] = filteredMessages;
                }
            }
            
            // Send the sync data to the client
            await Clients.Caller.SendAsync("StateSyncData", new
            {
                channels = channels,
                messages = recentMessages,
                serverTime = DateTime.UtcNow
            });
            
            _logger.LogInformation("State sync completed for user {UserId} after node migration", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing state after node migration");
            await Clients.Caller.SendAsync("StateSyncError", ex.Message);
        }
    }

    public async Task JoinChannel(Guid channelId)
    {
        var userId = GetCurrentUserId();

        // Verify user is a member of the channel
        var channel = await _channelService.GetChannelByIdAsync(channelId);
        if (channel == null)
        {
            // Check if channel exists in registry but not locally
            var registryChannel = await GetChannelFromRegistryAsync(channelId);
            if (registryChannel == null || !registryChannel.MemberIds.Contains(userId))
            {
                throw new HubException("Not authorized to join this channel");
            }
            
            // Sync the channel from registry to local database
            await _channelService.SyncChannelAsync(registryChannel);
            channel = registryChannel;
        }
        else if (!channel.MemberIds.Contains(userId))
        {
            throw new HubException("Not authorized to join this channel");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());
        
        _logger.LogInformation("User {UserId} joined channel {ChannelId}", userId, channelId);
    }

    public async Task LeaveChannel(Guid channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());
        
        var userId = GetCurrentUserId();
        _logger.LogInformation("User {UserId} left channel {ChannelId}", userId, channelId);
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
        
        // Notify registry about status change
        await NotifyRegistryAboutUserStatusAsync(userId, status);
        
        _logger.LogInformation("User {UserId} updated status to {Status}", userId, status);
    }

    public async Task SendMessage(Guid channelId, string content, List<string> attachmentIds = null)
    {
        var userId = GetCurrentUserId();
        
        // Verify user is a member of the channel
        var channel = await _channelService.GetChannelByIdAsync(channelId);
        if (channel == null || !channel.MemberIds.Contains(userId))
        {
            throw new HubException("Not authorized for this channel");
        }
        
        // Create the message
        var message = new Message
        {
            ChannelId = channelId,
            SenderId = userId,
            Content = content,
            SentAt = DateTime.UtcNow,
            Attachments = attachmentIds?.Select(id => new Attachment 
            { 
                Id = Guid.Parse(id), 
                Filename = "Unknown", // Or fetch from somewhere if needed
                ContentType = "application/octet-stream",
                UploadedAt = DateTime.UtcNow
            }).ToList() ?? new List<Attachment>()
        };
        
        // Save the message to the database
        var savedMessage = await _messageService.CreateMessageAsync(message);
        
        // Broadcast the message to all clients in the channel
        await Clients.Group(channelId.ToString()).SendAsync("ReceiveMessage", savedMessage);
        
        // Notify registry about new message for syncing to other nodes
        await NotifyRegistryAboutNewMessageAsync(savedMessage);
        
        _logger.LogInformation("User {UserId} sent message to channel {ChannelId}", userId, channelId);
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

    /// <summary>
    /// Synchronize messages from other nodes for a specific channel
    /// </summary>
    public async Task SyncChannelMessages(Guid channelId, DateTime lastSyncTime)
    {
        var userId = GetCurrentUserId();
        
        // Verify user is a member of the channel
        var channel = await _channelService.GetChannelByIdAsync(channelId);
        if (channel == null || !channel.MemberIds.Contains(userId))
        {
            throw new HubException("Not authorized for this channel");
        }
        
        // Get new messages from registry for this channel since last sync
        var newMessages = await GetNewMessagesFromRegistryAsync(channelId, lastSyncTime);
        
        if (newMessages != null && newMessages.Any())
        {
            // Save the new messages to local database
            foreach (var message in newMessages)
            {
                // Only sync if we don't already have it
                var existingMessage = await _messageService.GetMessageByIdAsync(message.Id);
                if (existingMessage == null)
                {
                    await _messageService.SyncMessageAsync(message);
                }
            }
            
            // Send the new messages to the client
            await Clients.Client(Context.ConnectionId).SendAsync("ReceiveSyncedMessages", newMessages);
            
            _logger.LogInformation("Synced {Count} messages for channel {ChannelId}", 
                newMessages.Count, channelId);
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        else
        {
            throw new UnauthorizedAccessException("Invalid user ID format");
        }
    }

    /// <summary>
    /// Notify registry about user status changes
    /// </summary>
    private async Task NotifyRegistryAboutUserStatusAsync(Guid userId, UserStatus status)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var statusUpdate = new
            {
                Status = status,
                NodeId = _nodeOptions.NodeId,
                Timestamp = DateTime.UtcNow
            };
            
            var response = await client.PutAsJsonAsync(
                $"{registryUrl}/api/registry/users/{userId}/status", 
                statusUpdate);
                
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to notify registry about user status. Status code: {StatusCode}", 
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying registry about user status");
        }
    }

    /// <summary>
    /// Notify registry about new messages
    /// </summary>
    private async Task NotifyRegistryAboutNewMessageAsync(Message message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var messageSync = new
            {
                Message = message,
                OriginNodeId = _nodeOptions.NodeId,
                Timestamp = DateTime.UtcNow
            };
            
            var response = await client.PostAsJsonAsync(
                $"{registryUrl}/api/registry/messages/sync", 
                messageSync);
                
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to notify registry about new message. Status code: {StatusCode}", 
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying registry about new message: {MessageId}", message.Id);
        }
    }

    /// <summary>
    /// Get a channel from the registry if not found locally
    /// </summary>
    private async Task<Channel> GetChannelFromRegistryAsync(Guid channelId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.GetAsync($"{registryUrl}/api/registry/channels/{channelId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Channel>();
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel from registry: {ChannelId}", channelId);
            return null;
        }
    }

    /// <summary>
    /// Get new messages from registry since last sync
    /// </summary>
    private async Task<List<Message>> GetNewMessagesFromRegistryAsync(Guid channelId, DateTime lastSyncTime)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.GetAsync(
                $"{registryUrl}/api/registry/channels/{channelId}/messages?since={lastSyncTime:o}");
                
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<Message>>();
            }
            
            _logger.LogWarning("Failed to get messages from registry. Status code: {StatusCode}", 
                response.StatusCode);
            return new List<Message>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages from registry");
            return new List<Message>();
        }
    }

    /// <summary>
    /// Track a user's connection in the static dictionary
    /// </summary>
    private void TrackUserConnection(Guid userId, string connectionId)
    {
        lock (ConnectionLock)
        {
            if (!UserConnections.TryGetValue(userId, out var connections))
            {
                connections = new HashSet<string>();
                UserConnections[userId] = connections;
            }
            
            connections.Add(connectionId);
            
            _logger.LogDebug("User {UserId} added connection {ConnectionId}, total connections: {Count}",
                userId, connectionId, connections.Count);
        }
    }
    
    /// <summary>
    /// Remove a user's connection from tracking
    /// </summary>
    /// <returns>True if this was the user's last connection</returns>
    private bool RemoveUserConnection(Guid userId, string connectionId)
    {
        lock (ConnectionLock)
        {
            if (!UserConnections.TryGetValue(userId, out var connections))
            {
                return true; // No connections tracked
            }
            
            connections.Remove(connectionId);
            
            _logger.LogDebug("User {UserId} removed connection {ConnectionId}, remaining connections: {Count}",
                userId, connectionId, connections.Count);
                
            if (connections.Count == 0)
            {
                UserConnections.Remove(userId);
                return true;
            }
            
            return false;
        }
    }
}