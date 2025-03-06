using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Drocsid.HenrikDennis2025.Server.Services;

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

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _userRepository.GetAllAsync();
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
            
            // Store password hash
            var passwordHash = _passwordHasher.HashPassword(user, password);
            
            // We need a place to store the password hash
            // For simplicity, you could add a PasswordHash property to the User model
            // But in a real app, you'd want to separate this for security
            
            // For now, let's assume we've added this property
            typeof(User).GetProperty("PasswordHash")?.SetValue(user, passwordHash);
            
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
            
            if (user == null)
            {
                return false;
            }
            
            // Get the stored password hash
            var passwordHash = typeof(User).GetProperty("PasswordHash")?.GetValue(user) as string;
            
            if (string.IsNullOrEmpty(passwordHash))
            {
                return false;
            }
            
            var result = _passwordHasher.VerifyHashedPassword(user, passwordHash, password);
            return result == PasswordVerificationResult.Success;
        }
    }