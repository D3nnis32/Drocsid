// using System.IdentityModel.Tokens.Jwt;
// using System.Linq.Expressions;
// using Drocsid.HenrikDennis2025.Api.Controllers;
// using Drocsid.HenrikDennis2025.Core.DTO;
// using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
// using Drocsid.HenrikDennis2025.Core.Models;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Configuration;
// using Moq;
//
// namespace Drocsid.HenrikDennis2025.Tests.Unit;
//
// public class AuthControllerTests
//     {
//         private readonly Mock<IUserService> _mockUserService;
//         private readonly Mock<IConfiguration> _mockConfiguration;
//         private readonly Mock<IConfigurationSection> _mockConfigSection;
//         private readonly AuthController _controller;
//
//         public AuthControllerTests()
//         {
//             _mockUserService = new Mock<IUserService>();
//             _mockConfiguration = new Mock<IConfiguration>();
//             _mockConfigSection = new Mock<IConfigurationSection>();
//
//             // Setup configuration
//             _mockConfigSection.Setup(x => x.Value).Returns("a-very-long-secret-key-for-testing-at-least-32-bytes");
//             _mockConfiguration.Setup(x => x["Jwt:Key"]).Returns("a-very-long-secret-key-for-testing-at-least-32-bytes");
//             _mockConfiguration.Setup(x => x["Jwt:ExpireDays"]).Returns("1");
//             _mockConfiguration.Setup(x => x["Jwt:Issuer"]).Returns("test-issuer");
//             _mockConfiguration.Setup(x => x["Jwt:Audience"]).Returns("test-audience");
//
//             _controller = new AuthController(_mockUserService.Object, _mockConfiguration.Object);
//         }
//
//         [Fact]
//         public async Task Login_WithValidCredentials_ReturnsOkWithToken()
//         {
//             // Arrange
//             var loginRequest = new LoginRequest
//             {
//                 Username = "testuser",
//                 Password = "password123"
//             };
//
//             var userId = Guid.NewGuid();
//             var user = new User
//             {
//                 Id = userId,
//                 Username = "testuser",
//                 Email = "test@example.com",
//                 Status = UserStatus.Online
//             };
//
//             _mockUserService.Setup(x => x.ValidateCredentialsAsync(loginRequest.Username, loginRequest.Password))
//                 .ReturnsAsync(true);
//             _mockUserService.Setup(x => x.FindUsersAsync(It.IsAny<Expression<Func<User, bool>>>()))
//                 .ReturnsAsync(new List<User> { user });
//
//             // Act
//             var result = await _controller.Login(loginRequest);
//
//             // Assert
//             var actionResult = Assert.IsType<ActionResult<LoginResponse>>(result);
//             var okResult = Assert.IsType<LoginResponse>(actionResult.Value);
//             
//             Assert.Equal(userId, okResult.UserId);
//             Assert.Equal("testuser", okResult.Username);
//             Assert.NotNull(okResult.Token);
//             Assert.NotEmpty(okResult.Token);
//
//             // Validate token structure
//             var handler = new JwtSecurityTokenHandler();
//             var token = handler.ReadJwtToken(okResult.Token);
//             
//             Assert.Equal("test-issuer", token.Issuer);
//             Assert.Equal("test-audience", token.Audiences.First());
//             Assert.Equal(userId.ToString(), token.Subject);
//             Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == "testuser");
//         }
//
//         [Fact]
//         public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
//         {
//             // Arrange
//             var loginRequest = new LoginRequest
//             {
//                 Username = "testuser",
//                 Password = "wrongpassword"
//             };
//
//             _mockUserService.Setup(x => x.ValidateCredentialsAsync(loginRequest.Username, loginRequest.Password))
//                 .ReturnsAsync(false);
//
//             // Act
//             var result = await _controller.Login(loginRequest);
//
//             // Assert
//             var actionResult = Assert.IsType<ActionResult<LoginResponse>>(result);
//             var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
//             Assert.Equal("Invalid username or password", unauthorizedResult.Value);
//         }
//
//         [Fact]
//         public async Task Login_WhenUserNotFound_ReturnsUnauthorized()
//         {
//             // Arrange
//             var loginRequest = new LoginRequest
//             {
//                 Username = "testuser",
//                 Password = "password123"
//             };
//
//             _mockUserService.Setup(x => x.ValidateCredentialsAsync(loginRequest.Username, loginRequest.Password))
//                 .ReturnsAsync(true);
//             _mockUserService.Setup(x => x.FindUsersAsync(It.IsAny<Expression<Func<User, bool>>>()))
//                 .ReturnsAsync(new List<User>());
//
//             // Act
//             var result = await _controller.Login(loginRequest);
//
//             // Assert
//             var actionResult = Assert.IsType<ActionResult<LoginResponse>>(result);
//             var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
//             Assert.Equal("User not found", unauthorizedResult.Value);
//         }
//     }