using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
[Route("api/registry/users")]
public class RegistryUserController : ControllerBase
{
    private readonly IUserRegistry _userRegistry;
    private readonly INodeRegistry _nodeRegistry;
    private readonly ILogger<RegistryUserController> _logger;

    public RegistryUserController(
        IUserRegistry userRegistry,
        INodeRegistry nodeRegistry,
        ILogger<RegistryUserController> logger)
    {
        _userRegistry = userRegistry ?? throw new ArgumentNullException(nameof(userRegistry));
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register a new user in the registry
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterUser([FromBody] CreateUserRequest request)
    {
        try
        {
            _logger.LogInformation("Registering user: {Username}", request.Username);
            
            // Create a valid User object from the request
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                // Hash the password - in a real-world scenario you'd use a proper password hasher
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Status = UserStatus.Offline,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PreferredRegion = request.PreferredRegion,
                CurrentNodeId = "registry" // Default to registry as the current node
            };

            bool success = await _userRegistry.RegisterUserAsync(user);
            
            if (!success)
            {
                return BadRequest("Failed to register user");
            }

            return Ok(new { UserId = user.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user: {Username}", request.Username);
            return StatusCode(500, "Internal server error during user registration");
        }
    }

    /// <summary>
    /// Get a user by ID
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUser(Guid userId)
    {
        try
        {
            var user = await _userRegistry.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound($"User with ID {userId} not found");
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", userId);
            return StatusCode(500, "Internal server error while retrieving user");
        }
    }

    /// <summary>
    /// Update a user's status
    /// </summary>
    [HttpPut("{userId}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UserStatusUpdate update)
    {
        try
        {
            _logger.LogInformation("Updating status for user {UserId} to {Status}", userId, update.Status);
            
            var user = await _userRegistry.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound($"User with ID {userId} not found");
            }
            
            // Update user status
            user.Status = update.Status;
            user.LastSeen = update.Timestamp;
            user.CurrentNodeId = update.NodeId;
            
            bool success = await _userRegistry.UpdateUserAsync(user);
            if (!success)
            {
                return StatusCode(500, "Failed to update user status");
            }
            
            // Notify all nodes that care about this user (e.g., nodes hosting channels the user is in)
            await NotifyRelevantNodesAboutUserStatusAsync(userId, update);

            return Ok(new { Status = "Status updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for user {UserId}", userId);
            return StatusCode(500, "Internal server error during status update");
        }
    }

    /// <summary>
    /// Search for users by username
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string username)
    {
        try
        {
            if (string.IsNullOrEmpty(username) || username.Length < 3)
            {
                return BadRequest("Search term must be at least 3 characters");
            }
            
            var users = await _userRegistry.SearchUsersAsync(username);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with term: {Username}", username);
            return StatusCode(500, "Internal server error during user search");
        }
    }
    
    /// <summary>
    /// Get users by their online status
    /// </summary>
    [HttpGet("online")]
    public async Task<IActionResult> GetOnlineUsers()
    {
        try
        {
            var users = await _userRegistry.GetUsersByStatusAsync(UserStatus.Online);
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving online users");
            return StatusCode(500, "Internal server error while retrieving online users");
        }
    }
    
    /// <summary>
    /// Get a list of nodes that need to be notified about this user's status changes
    /// </summary>
    private async Task NotifyRelevantNodesAboutUserStatusAsync(Guid userId, UserStatusUpdate update)
    {
        try
        {
            // Get all channels this user is a member of
            var userChannels = await _userRegistry.GetUserChannelsAsync(userId);
            
            // Get all nodes that host these channels
            var nodeIds = new HashSet<string>();
            foreach (var channelId in userChannels)
            {
                var channelNodes = await _userRegistry.GetChannelNodesAsync(channelId);
                foreach (var nodeId in channelNodes)
                {
                    if (nodeId != update.NodeId) // Skip the node that sent the update
                    {
                        nodeIds.Add(nodeId);
                    }
                }
            }
            
            // Get node info for all relevant nodes
            var nodes = await _nodeRegistry.GetNodesByIdsAsync(nodeIds.ToList());
            var healthyNodes = nodes.Where(n => n.IsHealthy).ToList();
            
            // Send status update to each node
            foreach (var node in healthyNodes)
            {
                await NotifyNodeAboutUserStatusAsync(node, userId, update);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying nodes about user status");
        }
    }
    
    /// <summary>
    /// Notify a specific node about a user's status change
    /// </summary>
    private async Task NotifyNodeAboutUserStatusAsync(NodeInfo node, Guid userId, UserStatusUpdate update)
    {
        try
        {
            var client = new HttpClient();
            var url = $"{node.Endpoint.TrimEnd('/')}/api/users/{userId}/status/sync";
            
            var response = await client.PostAsJsonAsync(url, update);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to notify node {NodeId} about user status. Status: {Status}", 
                    node.Id, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying node {NodeId} about user status", node.Id);
        }
    }
}