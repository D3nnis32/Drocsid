using System.Linq.Expressions;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Drocsid.HenrikDennis2025.Api.Services;

public class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(IRepository<User> userRepository, IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<User> GetUserByIdAsync(Guid userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    // Einfache Methode ohne Parameter
    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }

    // Separate Methode mit Prädikat
    public async Task<IEnumerable<User>> FindUsersAsync(Expression<Func<User, bool>> predicate)
    {
        return await _userRepository.FindAsync(predicate);
    }

    public async Task<User> CreateUserAsync(User user, string password)
    {
        // Validate user doesn't already exist
        var existingUsers = await _userRepository.FindAsync(u => u.Username == user.Username);
        if (existingUsers.Any())
        {
            throw new InvalidOperationException("Username already exists");
        }

        user.Id = Guid.NewGuid();
    
        // Store password hash directly
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
    
        await _userRepository.AddAsync(user);
        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        await _userRepository.UpdateAsync(user);
    }

    public async Task UpdateUserStatusAsync(Guid userId, UserStatus status)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.Status = status;
            await _userRepository.UpdateAsync(user);
        }
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var users = await _userRepository.FindAsync(u => u.Username == username);
        var user = users.FirstOrDefault();
    
        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return false;
        }
    
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Success;
    }
    
    public async Task<bool> SyncUserAsync(User user)
    {
        // Check if user exists
        var existingUser = await _userRepository.GetByIdAsync(user.Id);
    
        if (existingUser != null)
        {
            // Update existing user without changing password
            user.PasswordHash = existingUser.PasswordHash; // Preserve existing password
            await _userRepository.UpdateAsync(user);
        }
        else
        {
            // Add as new user
            await _userRepository.AddAsync(user);
        }
    
        return true;
    }
}