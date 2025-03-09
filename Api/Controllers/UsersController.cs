using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : BaseController
{
    private readonly IUserService _userService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeRegistrationOptions _nodeOptions;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserService userService,
        IHttpClientFactory httpClientFactory,
        IOptions<NodeRegistrationOptions> nodeOptions,
        ILogger<UsersController> logger) 
        : base(logger)
    {
        _userService = userService;
        _httpClientFactory = httpClientFactory;
        _nodeOptions = nodeOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            // Try to fetch from registry if not found locally
            user = await FetchUserFromRegistryAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            
            // Cache the user locally
            await _userService.SyncUserAsync(user);
        }

        return Ok(user);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
    {
        try
        {
            // Check if user already exists (locally or in registry)
            var existingUsers = await _userService.FindUsersAsync(u => 
                u.Username == request.Username || u.Email == request.Email);
                
            if (existingUsers.Any())
            {
                return BadRequest("Username or email already exists");
            }

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                Status = UserStatus.Offline
            };

            var createdUser = await _userService.CreateUserAsync(user, request.Password);
            
            // Register the user with the registry
            await RegisterUserWithRegistryAsync(createdUser);
            
            return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, createdUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", request.Username);
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("status")]
    public async Task<IActionResult> UpdateStatus(UserStatusUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        await _userService.UpdateUserStatusAsync(userId, request.Status);
        
        // Propagate status update to registry
        await UpdateUserStatusInRegistryAsync(userId, request.Status);
        
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<User>> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetUserByIdAsync(userId);
            
            if (user == null)
            {
                return NotFound("Current user not found in database");
            }
            
            return Ok(user);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Fetch user data from the registry
    /// </summary>
    private async Task<User> FetchUserFromRegistryAsync(Guid userId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.GetAsync($"{registryUrl}/api/registry/users/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<User>();
            }
            
            _logger.LogWarning("Failed to fetch user {UserId} from registry. Status: {Status}", 
                userId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {UserId} from registry", userId);
            return null;
        }
    }

    /// <summary>
    /// Register a new user with the registry
    /// </summary>
    private async Task<bool> RegisterUserWithRegistryAsync(User user)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.PostAsJsonAsync($"{registryUrl}/api/registry/users/register", user);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("User {Username} registered with registry", user.Username);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to register user {Username} with registry. Status: {Status}", 
                    user.Username, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user {Username} with registry", user.Username);
            return false;
        }
    }

    /// <summary>
    /// Update user status in the registry
    /// </summary>
    private async Task<bool> UpdateUserStatusInRegistryAsync(Guid userId, UserStatus status)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.PutAsJsonAsync(
                $"{registryUrl}/api/registry/users/{userId}/status", 
                new { Status = status });
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("User {UserId} status updated in registry to {Status}", userId, status);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to update user {UserId} status in registry. Status: {Status}", 
                    userId, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId} status in registry", userId);
            return false;
        }
    }
}