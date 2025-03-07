using Drocsid.HenrikDennis2025.Api.Controllers;
using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Drocsid.HenrikDennis2025.Tests.Unit;

public class UsersControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly UsersController _controller;

        public UsersControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _controller = new UsersController(_mockUserService.Object);
        }

        [Fact]
        public async Task GetUsers_ReturnsAllUsers()
        {
            // Arrange
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), Username = "user1", Email = "user1@example.com", Status = UserStatus.Online },
                new User { Id = Guid.NewGuid(), Username = "user2", Email = "user2@example.com", Status = UserStatus.Offline }
            };

            _mockUserService.Setup(x => x.GetAllUsersAsync())
                .ReturnsAsync(users);

            // Act
            var result = await _controller.GetUsers();

            // Assert
            var actionResult = Assert.IsType<ActionResult<IEnumerable<User>>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedUsers = Assert.IsAssignableFrom<IEnumerable<User>>(okResult.Value);
            
            Assert.Equal(2, returnedUsers.Count());
            Assert.Equal(users, returnedUsers);
        }

        [Fact]
        public async Task GetUser_WithExistingId_ReturnsUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "testuser",
                Email = "test@example.com",
                Status = UserStatus.Online
            };

            _mockUserService.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync(user);

            // Act
            var result = await _controller.GetUser(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<User>>(result);
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var returnedUser = Assert.IsType<User>(okResult.Value);
            
            Assert.Equal(userId, returnedUser.Id);
            Assert.Equal("testuser", returnedUser.Username);
            Assert.Equal("test@example.com", returnedUser.Email);
            Assert.Equal(UserStatus.Online, returnedUser.Status);
        }

        [Fact]
        public async Task GetUser_WithNonExistingId_ReturnsNotFound()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockUserService.Setup(x => x.GetUserByIdAsync(userId))
                .ReturnsAsync((User)null);

            // Act
            var result = await _controller.GetUser(userId);

            // Assert
            var actionResult = Assert.IsType<ActionResult<User>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Fact]
        public async Task CreateUser_WithValidData_ReturnsCreatedUser()
        {
            // Arrange
            var request = new CreateUserRequest
            {
                Username = "newuser",
                Email = "new@example.com",
                Password = "password123"
            };

            var createdUser = new User
            {
                Id = Guid.NewGuid(),
                Username = "newuser",
                Email = "new@example.com",
                Status = UserStatus.Offline
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<User>(), request.Password))
                .ReturnsAsync(createdUser);

            // Act
            var result = await _controller.CreateUser(request);

            // Assert
            var actionResult = Assert.IsType<ActionResult<User>>(result);
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            
            Assert.Equal(nameof(UsersController.GetUser), createdResult.ActionName);
            Assert.Equal(createdUser.Id, createdResult.RouteValues["id"]);
            
            var returnedUser = Assert.IsType<User>(createdResult.Value);
            Assert.Equal(createdUser.Id, returnedUser.Id);
            Assert.Equal("newuser", returnedUser.Username);
            Assert.Equal("new@example.com", returnedUser.Email);
            Assert.Equal(UserStatus.Offline, returnedUser.Status);
        }

        [Fact]
        public async Task CreateUser_WhenExceptionThrown_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateUserRequest
            {
                Username = "existinguser",
                Email = "existing@example.com",
                Password = "password123"
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<User>(), request.Password))
                .ThrowsAsync(new InvalidOperationException("Username already exists"));

            // Act
            var result = await _controller.CreateUser(request);

            // Assert
            var actionResult = Assert.IsType<ActionResult<User>>(result);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            
            Assert.Equal("Username already exists", badRequestResult.Value);
        }

        [Fact]
        public async Task UpdateStatus_WithValidStatus_ReturnsNoContent()
        {
            // Arrange
            var request = new UserStatusUpdateRequest
            {
                Status = UserStatus.Online
            };

            var currentUserId = new Guid("11111111-1111-1111-1111-111111111111"); // This matches the hardcoded value in the controller

            _mockUserService.Setup(x => x.UpdateUserStatusAsync(currentUserId, request.Status))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdateStatus(request);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockUserService.Verify(x => x.UpdateUserStatusAsync(currentUserId, request.Status), Times.Once);
        }
    }