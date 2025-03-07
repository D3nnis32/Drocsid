using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Drocsid.HenrikDennis2025.Api.Controllers;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Drocsid.HenrikDennis2025.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Drocsid.HenrikDennis2025.Tests.Unit;

public class UserServiceTests
    {
        private readonly Mock<IRepository<User>> _mockRepository;
        private readonly Mock<IPasswordHasher<User>> _mockPasswordHasher;
        private readonly UserService _service;

        public UserServiceTests()
        {
            _mockRepository = new Mock<IRepository<User>>();
            _mockPasswordHasher = new Mock<IPasswordHasher<User>>();
            _service = new UserService(_mockRepository.Object, _mockPasswordHasher.Object);
            
            // Add a PasswordHash property to the User class for testing purposes
            // This is a hack for testing since the actual code uses reflection to access this property
            if (!typeof(User).GetProperties().Any(p => p.Name == "PasswordHash"))
            {
                var backingField = new Dictionary<Guid, string>();
                
                // Add the property using a mock implementation
                Mock.Get(typeof(User))
                    .Setup(t => t.GetProperty("PasswordHash"))
                    .Returns(new Mock<System.Reflection.PropertyInfo>().Object);
            }
        }
        
        [Fact]
        public async Task GetUserByIdAsync_ReturnsUserFromRepository()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Username = "testuser" };
            
            _mockRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);
                
            // Act
            var result = await _service.GetUserByIdAsync(userId);
            
            // Assert
            Assert.Same(user, result);
            _mockRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
        }
        
        [Fact]
        public async Task GetAllUsersAsync_ReturnsAllUsersFromRepository()
        {
            // Arrange
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), Username = "user1" },
                new User { Id = Guid.NewGuid(), Username = "user2" }
            };
            
            _mockRepository.Setup(r => r.GetAllAsync())
                .ReturnsAsync(users);
                
            // Act
            var result = await _service.GetAllUsersAsync();
            
            // Assert
            Assert.Same(users, result);
            _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
        }
        
        [Fact]
        public async Task FindUsersAsync_ReturnsFilteredUsersFromRepository()
        {
            // Arrange
            Expression<Func<User, bool>> predicate = u => u.Username == "user1";
            var users = new List<User> { new User { Id = Guid.NewGuid(), Username = "user1" } };
            
            _mockRepository.Setup(r => r.FindAsync(predicate))
                .ReturnsAsync(users);
                
            // Act
            var result = await _service.FindUsersAsync(predicate);
            
            // Assert
            Assert.Same(users, result);
            _mockRepository.Verify(r => r.FindAsync(predicate), Times.Once);
        }
        
        [Fact]
        public async Task CreateUserAsync_WhenUsernameDoesNotExist_CreatesAndReturnsUser()
        {
            // Arrange
            var newUser = new User
            {
                Username = "newuser",
                Email = "new@example.com",
                Status = UserStatus.Offline
            };
            
            _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
                
            _mockPasswordHasher.Setup(p => p.HashPassword(newUser, "password123"))
                .Returns("hashedpassword123");
                
            // Act
            var result = await _service.CreateUserAsync(newUser, "password123");
            
            // Assert
            Assert.Equal(newUser.Username, result.Username);
            Assert.Equal(newUser.Email, result.Email);
            Assert.Equal(UserStatus.Offline, result.Status);
            Assert.NotEqual(Guid.Empty, result.Id);
            
            _mockRepository.Verify(r => r.AddAsync(newUser), Times.Once);
        }
        
        [Fact]
        public async Task CreateUserAsync_WhenUsernameExists_ThrowsInvalidOperationException()
        {
            // Arrange
            var newUser = new User
            {
                Username = "existinguser",
                Email = "existing@example.com"
            };
            
            var existingUsers = new List<User>
            {
                new User { Id = Guid.NewGuid(), Username = "existinguser" }
            };
            
            _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(existingUsers);
                
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.CreateUserAsync(newUser, "password123"));
                
            _mockRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }
        
        [Fact]
        public async Task UpdateUserAsync_CallsRepositoryUpdate()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com"
            };
            
            // Act
            await _service.UpdateUserAsync(user);
            
            // Assert
            _mockRepository.Verify(r => r.UpdateAsync(user), Times.Once);
        }
        
        [Fact]
        public async Task UpdateUserStatusAsync_WhenUserExists_UpdatesUserStatus()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "testuser",
                Status = UserStatus.Offline
            };
            
            _mockRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);
                
            // Act
            await _service.UpdateUserStatusAsync(userId, UserStatus.Online);
            
            // Assert
            Assert.Equal(UserStatus.Online, user.Status);
            _mockRepository.Verify(r => r.UpdateAsync(user), Times.Once);
        }
        
        [Fact]
        public async Task UpdateUserStatusAsync_WhenUserDoesNotExist_DoesNothing()
        {
            // Arrange
            var userId = Guid.NewGuid();
            
            _mockRepository.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync((User)null);
                
            // Act
            await _service.UpdateUserStatusAsync(userId, UserStatus.Online);
            
            // Assert
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }
        
        [Fact]
        public async Task ValidateCredentialsAsync_WithValidCredentials_ReturnsTrue()
        {
            // Arrange
            var username = "testuser";
            var password = "password123";
            var user = new User { Id = Guid.NewGuid(), Username = username };
    
            // Add a property to store the password hash
            typeof(User).GetProperty("PasswordHash")?.SetValue(user, "hashedpassword123");
    
            // Setup mock to return the user when looking up by username
            _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });
    
            // Setup password hasher to verify the password successfully
            _mockPasswordHasher.Setup(p => p.VerifyHashedPassword(
                    It.IsAny<User>(),
                    It.IsAny<string>(),
                    It.Is<string>(s => s == password)))
                .Returns(PasswordVerificationResult.Success);
    
            // Act
            var result = await _service.ValidateCredentialsAsync(username, password);
    
            // Assert
            Assert.True(result);
            _mockRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
            _mockPasswordHasher.Verify(
                p => p.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), password),
                Times.Once);
        }
    }