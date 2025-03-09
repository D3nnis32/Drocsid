using System.Linq.Expressions;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Drocsid.HenrikDennis2025.RegistryService.Services;

/// <summary>
/// Implementation of IUserService for registry service
/// </summary>
public class RegistryUserService : IUserService
{
    private readonly RegistryDbContext _dbContext;
    private readonly ILogger<RegistryUserService> _logger;

    public RegistryUserService(RegistryDbContext dbContext, ILogger<RegistryUserService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<User> CreateUserAsync(User user, string password)
    {
        try
        {
            // Hash the password
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            user.PasswordHash = hashedPassword;
            
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", user.Username);
            throw;
        }
    }

    public async Task<User> GetUserByIdAsync(Guid id)
    {
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            return false;
        }
        
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    public async Task<IEnumerable<User>> FindUsersAsync(Expression<Func<User, bool>> predicate)
    {
        return await _dbContext.Users.Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _dbContext.Users.ToListAsync();
    }
    
    public async Task UpdateUserAsync(User user)
    {
        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (existingUser == null)
        {
            throw new InvalidOperationException($"User with ID {user.Id} not found");
        }
    
        // Update properties
        _dbContext.Entry(existingUser).CurrentValues.SetValues(user);
        user.UpdatedAt = DateTime.UtcNow;
    
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateUserStatusAsync(Guid userId, UserStatus status)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }
    
        user.Status = status;
        user.LastSeen = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> SyncUserAsync(User user)
    {
        var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (existingUser != null)
        {
            // Update existing user
            _dbContext.Entry(existingUser).CurrentValues.SetValues(user);
        }
        else
        {
            // Add new user
            await _dbContext.Users.AddAsync(user);
        }
        
        await _dbContext.SaveChangesAsync();
        return true;
    }
}