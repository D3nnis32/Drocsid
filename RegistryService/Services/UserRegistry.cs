using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Registry service for managing users across nodes
/// </summary>
public class UserRegistry : IUserRegistry
{
    private readonly RegistryDbContext _dbContext;
    private readonly ILogger<UserRegistry> _logger;

    public UserRegistry(RegistryDbContext dbContext, ILogger<UserRegistry> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> RegisterUserAsync(User user)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existingUser != null)
            {
                _logger.LogWarning("User with ID {UserId} already exists", user.Id);
                return false;
            }

            // Add user to database
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("User registered: {Username}, ID: {UserId}", user.Username, user.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user: {Username}", user.Username);
            return false;
        }
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        try
        {
            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existingUser == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for update", user.Id);
                return false;
            }

            // Update user properties
            _dbContext.Entry(existingUser).CurrentValues.SetValues(user);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("User updated: {Username}, ID: {UserId}", user.Username, user.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
            return false;
        }
    }

    public async Task<User> GetUserAsync(Guid userId)
    {
        try
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user: {UserId}", userId);
            return null;
        }
    }

    public async Task<IEnumerable<User>> SearchUsersAsync(string searchTerm)
    {
        try
        {
            return await _dbContext.Users
                .Where(u => u.Username.Contains(searchTerm) || u.Email.Contains(searchTerm))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with term: {SearchTerm}", searchTerm);
            return Enumerable.Empty<User>();
        }
    }

    public async Task<IEnumerable<User>> GetUsersByStatusAsync(UserStatus status)
    {
        try
        {
            return await _dbContext.Users
                .Where(u => u.Status == status)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users by status: {Status}", status);
            return Enumerable.Empty<User>();
        }
    }

    public async Task<IEnumerable<Guid>> GetUserChannelsAsync(Guid userId)
    {
        try
        {
            return await _dbContext.UserChannels
                .Where(uc => uc.UserId == userId)
                .Select(uc => uc.ChannelId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channels for user: {UserId}", userId);
            return Enumerable.Empty<Guid>();
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

    public async Task<bool> AddUserToChannelAsync(Guid userId, Guid channelId)
    {
        try
        {
            // Check if user and channel exist
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
            
            if (user == null || channel == null)
            {
                _logger.LogWarning("User or channel not found: User {UserId}, Channel {ChannelId}", userId, channelId);
                return false;
            }

            // Check if user is already a member
            var existingMembership = await _dbContext.UserChannels
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ChannelId == channelId);
                
            if (existingMembership != null)
            {
                // Already a member
                return true;
            }

            // Add user to channel
            await _dbContext.UserChannels.AddAsync(new UserChannel
            {
                UserId = userId,
                ChannelId = channelId,
                JoinedAt = DateTime.UtcNow
            });
            
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("User {UserId} added to channel {ChannelId}", userId, channelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user to channel: User {UserId}, Channel {ChannelId}", userId, channelId);
            return false;
        }
    }

    public async Task<bool> RemoveUserFromChannelAsync(Guid userId, Guid channelId)
    {
        try
        {
            var membership = await _dbContext.UserChannels
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ChannelId == channelId);
                
            if (membership == null)
            {
                // Not a member
                return true;
            }

            // Remove user from channel
            _dbContext.UserChannels.Remove(membership);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("User {UserId} removed from channel {ChannelId}", userId, channelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user from channel: User {UserId}, Channel {ChannelId}", userId, channelId);
            return false;
        }
    }
}