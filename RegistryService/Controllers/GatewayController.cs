using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ConnectionInfo = Drocsid.HenrikDennis2025.Core.Models.ConnectionInfo;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

[ApiController]
[Route("api/gateway")]
public class GatewayController : ControllerBase
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GatewayController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GatewayController(
        INodeRegistry nodeRegistry,
        IUserService userService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<GatewayController> logger)
    {
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Entry point for client connection - authenticates user and assigns to optimal node
    /// </summary>
    [HttpPost("connect")]
    public async Task<ActionResult<ConnectionInfo>> Connect(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Connection request received for user: {Username}", request.Username);
            
            // Validate credentials
            var isValid = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
            if (!isValid)
            {
                _logger.LogWarning("Invalid credentials for user: {Username}", request.Username);
                return Unauthorized("Invalid username or password");
            }

            // Get user details
            var users = await _userService.FindUsersAsync(u => u.Username == request.Username);
            var user = users.FirstOrDefault();
            if (user == null)
            {
                _logger.LogWarning("User not found after successful validation: {Username}", request.Username);
                return Unauthorized("User not found");
            }

            // Find best node for user based on region/load
            var bestNode = await SelectOptimalNodeForUserAsync(user);
            if (bestNode == null)
            {
                _logger.LogError("No available nodes for user connection: {Username}", request.Username);
                return StatusCode(503, "No available nodes to handle your request");
            }

            // Generate JWT token that will be valid for both registry and storage nodes
            var token = GenerateJwtToken(user.Id.ToString(), user.Username);

            _logger.LogInformation("User {Username} connected and assigned to node {NodeId} at {Endpoint}", 
                request.Username, bestNode.Id, bestNode.Endpoint);

            return new ConnectionInfo
            {
                NodeEndpoint = bestNode.Endpoint,
                Token = token,
                UserId = user.Id,
                Username = user.Username,
                RegistryEndpoint = GetRegistryEndpoint(),
                ExpiresAt = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"]))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing connection request for user {Username}", request.Username);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Allows clients to query node health and status
    /// </summary>
    [HttpGet("nodes/health")]
    public async Task<ActionResult<IEnumerable<NodeHealthInfo>>> GetNodesHealth()
    {
        try
        {
            var nodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
            
            var healthInfo = nodes.Select(n => new NodeHealthInfo
            {
                NodeId = n.Id,
                Region = n.Region,
                IsHealthy = n.Status.IsHealthy,
                CurrentLoad = n.Status.CurrentLoad,
                AvailableSpace = n.Status.AvailableSpace
            });
            
            return Ok(healthInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving nodes health information");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Handles file location discovery for clients
    /// </summary>
    [HttpGet("files/{fileId}/location")]
    public async Task<ActionResult<FileLocationInfo>> GetFileLocation(string fileId)
    {
        try
        {
            // Get file registry information
            var fileRegistry = await GetFileRegistryAsync();
            if (fileRegistry == null)
            {
                return StatusCode(500, "File registry service unavailable");
            }

            var fileInfo = await fileRegistry.GetFileInfoAsync(fileId);
            if (fileInfo == null)
            {
                return NotFound($"File with ID {fileId} not found");
            }

            // Get nodes that have this file
            var nodes = await _nodeRegistry.GetNodesByIdsAsync(fileInfo.NodeIds);
            var healthyNodes = nodes.Where(n => n.IsHealthy).ToList();

            if (!healthyNodes.Any())
            {
                return StatusCode(503, "File exists but no healthy nodes are currently available");
            }

            // Determine client region for optimal node selection
            string clientRegion = GetClientRegion();

            // Select best node based on region and load
            var bestNode = healthyNodes
                .OrderBy(n => clientRegion != null ? (n.Region == clientRegion ? 0 : 1) : 0)
                .ThenBy(n => n.CurrentLoad)
                .First();

            return new FileLocationInfo
            {
                FileId = fileId,
                NodeEndpoint = bestNode.Endpoint,
                FileSize = fileInfo.Size,
                ContentType = fileInfo.ContentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file location for {FileId}", fileId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Determines the best region for a client based on IP or headers
    /// </summary>
    private string GetClientRegion()
    {
        // Look for region information in headers
        if (Request.Headers.TryGetValue("X-Client-Region", out var regionHeader))
        {
            return regionHeader.ToString();
        }

        // Could implement IP-based region detection here
        // For now, return null (no preference)
        return null;
    }

    /// <summary>
    /// Get the registry endpoint for client communication
    /// </summary>
    private string GetRegistryEndpoint()
    {
        var endpoint = _configuration["Registry:PublicEndpoint"];
        if (string.IsNullOrEmpty(endpoint))
        {
            // Fallback to server URL from request
            var request = HttpContext.Request;
            endpoint = $"{request.Scheme}://{request.Host}";
        }
        return endpoint;
    }

    /// <summary>
    /// Select the optimal node for a user based on various factors
    /// </summary>
    private async Task<NodeInfo> SelectOptimalNodeForUserAsync(User user)
    {
        // Get all healthy nodes
        var nodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
        if (!nodes.Any())
        {
            return null;
        }

        // Get user's preferred region (if any)
        string userRegion = user.PreferredRegion;

        // First try to find a node in the user's region
        if (!string.IsNullOrEmpty(userRegion))
        {
            var regionNodes = nodes
                .Where(n => n.Region == userRegion)
                .OrderBy(n => n.Status.CurrentLoad)
                .ToList();

            if (regionNodes.Any())
            {
                var bestRegionNode = regionNodes.First();
                return new NodeInfo
                {
                    Id = bestRegionNode.Id,
                    Endpoint = bestRegionNode.Endpoint,
                    Region = bestRegionNode.Region,
                    IsHealthy = bestRegionNode.Status.IsHealthy,
                    AvailableSpace = bestRegionNode.Status.AvailableSpace,
                    CurrentLoad = bestRegionNode.Status.CurrentLoad
                };
            }
        }

        // If no region match, select node with lowest load
        var bestNode = nodes.OrderBy(n => n.Status.CurrentLoad).First();
        return new NodeInfo
        {
            Id = bestNode.Id,
            Endpoint = bestNode.Endpoint,
            Region = bestNode.Region,
            IsHealthy = bestNode.Status.IsHealthy,
            AvailableSpace = bestNode.Status.AvailableSpace,
            CurrentLoad = bestNode.Status.CurrentLoad
        };
    }

    /// <summary>
    /// Generate a JWT token for client authentication
    /// </summary>
    private string GenerateJwtToken(string userId, string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"]));

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Get the file registry service
    /// </summary>
    private async Task<IFileRegistry> GetFileRegistryAsync()
    {
        // In a real-world implementation, you might want to inject this service directly
        using var scope = HttpContext.RequestServices.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IFileRegistry>();
    }
}