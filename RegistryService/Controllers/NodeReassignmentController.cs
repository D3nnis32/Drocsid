using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Drocsid.HenrikDennis2025.Core.Interfaces;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Drocsid.HenrikDennis2025.RegistryService.Controllers;

/// <summary>
/// Request model for node reassignment
/// </summary>
public class NodeReassignmentRequest
{
    /// <summary>
    /// The ID of the current node that failed (if known)
    /// </summary>
    public string CurrentNodeId { get; set; }
    
    /// <summary>
    /// The reason for requesting reassignment
    /// Valid values: NODE_FAILURE, LOAD_BALANCING, REGION_PREFERENCE, MANUAL
    /// </summary>
    public string Reason { get; set; } = "NODE_FAILURE";
    
    /// <summary>
    /// Optional preferred region for the new node
    /// </summary>
    public string PreferredRegion { get; set; }
    
    /// <summary>
    /// Optional list of file IDs that need to be accessible from the new node
    /// </summary>
    public List<string> RequiredFileAccess { get; set; } = new List<string>();
}

/// <summary>
/// Response model for node reassignment
/// </summary>
public class NodeReassignmentResponse
{
    /// <summary>
    /// The new node endpoint URL
    /// </summary>
    public string NodeEndpoint { get; set; }
    
    /// <summary>
    /// The ID of the new node
    /// </summary>
    public string NodeId { get; set; }
    
    /// <summary>
    /// The region of the new node
    /// </summary>
    public string Region { get; set; }
    
    /// <summary>
    /// Optional updated authentication token
    /// </summary>
    public string Token { get; set; }
    
    /// <summary>
    /// When the token expires (if a new one was issued)
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }
}

[ApiController]
[Route("api/gateway")]
[Authorize]
public class NodeReassignmentController : ControllerBase
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly IUserRegistry _userRegistry;
    private readonly IFileRegistry _fileRegistry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NodeReassignmentController> _logger;

    public NodeReassignmentController(
        INodeRegistry nodeRegistry,
        IUserRegistry userRegistry,
        IFileRegistry fileRegistry,
        IConfiguration configuration,
        ILogger<NodeReassignmentController> logger)
    {
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _userRegistry = userRegistry ?? throw new ArgumentNullException(nameof(userRegistry));
        _fileRegistry = fileRegistry ?? throw new ArgumentNullException(nameof(fileRegistry));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reassigns a user to a new node when their current node fails
    /// </summary>
    [HttpPost("reassign")]
    public async Task<ActionResult<NodeReassignmentResponse>> ReassignNode([FromBody] NodeReassignmentRequest request)
    {
        try
        {
            _logger.LogInformation("Node reassignment requested. Reason: {Reason}, Current Node: {CurrentNodeId}",
                request.Reason, request.CurrentNodeId);

            // Get user ID from JWT token
            var userId = GetUserIdFromToken();
            if (userId == null)
            {
                return Unauthorized("Invalid authentication token");
            }

            // Get user details
            var user = await _userRegistry.GetUserAsync(Guid.Parse(userId));
            if (user == null)
            {
                return NotFound("User not found");
            }

            // If the current node is provided, mark it as potentially unhealthy
            if (!string.IsNullOrEmpty(request.CurrentNodeId))
            {
                await CheckNodeHealth(request.CurrentNodeId);
            }

            // Select new node for the user based on various criteria
            var newNode = await SelectOptimalNodeForUserAsync(user, 
                request.PreferredRegion, 
                request.RequiredFileAccess,
                request.CurrentNodeId); // Avoid reassigning to same node
            
            if (newNode == null)
            {
                return StatusCode(503, "No available nodes to handle your request");
            }

            // Update user's current node in registry
            user.CurrentNodeId = newNode.Id;
            await _userRegistry.UpdateUserAsync(user);

            // Create response with new node information
            var response = new NodeReassignmentResponse
            {
                NodeEndpoint = newNode.Endpoint,
                NodeId = newNode.Id,
                Region = newNode.Region
            };

            // Only generate a new token if the node failure was due to authentication issues
            if (request.Reason == "AUTH_FAILURE")
            {
                var token = GenerateJwtToken(user.Id.ToString(), user.Username);
                response.Token = token;
                response.TokenExpiresAt = DateTime.UtcNow.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"]));
            }

            _logger.LogInformation("User {UserId} reassigned from node {OldNodeId} to node {NewNodeId}",
                userId, request.CurrentNodeId, newNode.Id);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during node reassignment");
            return StatusCode(500, "Internal server error during node reassignment");
        }
    }

    /// <summary>
    /// Checks a node's health and potentially marks it as unhealthy
    /// </summary>
    private async Task CheckNodeHealth(string nodeId)
    {
        try
        {
            var node = await _nodeRegistry.GetNodeAsync(nodeId);
            if (node == null)
            {
                _logger.LogWarning("Node {NodeId} not found during health check", nodeId);
                return;
            }

            // If node was already marked as unhealthy, no need to check again
            if (!node.Status.IsHealthy)
            {
                return;
            }

            // Count how many times this node has been reported as potentially failed
            // This could be stored in a distributed cache or database in a real implementation
            // For simplicity, we'll just immediately mark it as potentially unhealthy

            _logger.LogWarning("Node {NodeId} reported as potentially unhealthy by client", nodeId);
            
            // Perform a quick connection test to the node
            bool nodeIsResponding = await PerformNodeConnectivityCheck(node);
            
            if (!nodeIsResponding)
            {
                _logger.LogWarning("Node {NodeId} failed connectivity check, marking as unhealthy", nodeId);
                
                // Mark node as unhealthy
                node.Status.IsHealthy = false;
                node.Status.LastUpdated = DateTime.UtcNow;
                await _nodeRegistry.UpdateNodeAsync(node);
                
                // Trigger replication for files on this node would happen here
                // or be picked up by the node health monitor service
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking node health: {NodeId}", nodeId);
        }
    }

    /// <summary>
    /// Performs a simple connectivity check to a node
    /// </summary>
    private async Task<bool> PerformNodeConnectivityCheck(StorageNode node)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout for quick check
            
            var response = await httpClient.GetAsync($"{node.Endpoint.TrimEnd('/')}/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            // Any exception indicates the node is unreachable
            return false;
        }
    }

    /// <summary>
    /// Select the optimal node for a user based on various factors
    /// </summary>
    private async Task<NodeInfo> SelectOptimalNodeForUserAsync(
        User user, 
        string preferredRegion = null,
        List<string> requiredFileAccess = null,
        string excludeNodeId = null)
    {
        // Get all healthy nodes
        var nodes = await _nodeRegistry.GetAllNodesAsync(includeOffline: false);
        if (!nodes.Any())
        {
            return null;
        }

        // Filter out the excluded node if specified
        if (!string.IsNullOrEmpty(excludeNodeId))
        {
            nodes = nodes.Where(n => n.Id != excludeNodeId).ToList();
            if (!nodes.Any())
            {
                return null;
            }
        }

        // First priority: Required file access
        if (requiredFileAccess != null && requiredFileAccess.Any())
        {
            var fileNodes = new Dictionary<string, HashSet<string>>();
            
            // Get nodes for each required file
            foreach (var fileId in requiredFileAccess)
            {
                var file = await _fileRegistry.GetFileAsync(fileId);
                if (file != null)
                {
                    fileNodes[fileId] = new HashSet<string>(file.NodeLocations);
                }
            }
            
            // Find nodes that have all required files
            var nodesWithAllFiles = nodes
                .Where(n => requiredFileAccess.All(fileId => 
                    fileNodes.ContainsKey(fileId) && fileNodes[fileId].Contains(n.Id)))
                .ToList();
            
            if (nodesWithAllFiles.Any())
            {
                // Use nodes with all files
                nodes = nodesWithAllFiles;
            }
            else
            {
                // Fallback: Find nodes with most of the required files
                var nodeFileCount = nodes
                    .Select(n => new 
                    { 
                        Node = n, 
                        FileCount = fileNodes.Values.Count(files => files.Contains(n.Id)) 
                    })
                    .OrderByDescending(x => x.FileCount)
                    .ToList();
                
                if (nodeFileCount.Any() && nodeFileCount[0].FileCount > 0)
                {
                    // Take nodes with the maximum file count
                    var maxFileCount = nodeFileCount[0].FileCount;
                    nodes = nodeFileCount
                        .Where(x => x.FileCount == maxFileCount)
                        .Select(x => x.Node)
                        .ToList();
                }
            }
        }

        // Second priority: Region preference
        string regionToUse = preferredRegion ?? user.PreferredRegion;
        
        if (!string.IsNullOrEmpty(regionToUse))
        {
            var regionNodes = nodes
                .Where(n => n.Region == regionToUse)
                .ToList();

            if (regionNodes.Any())
            {
                // Use nodes in preferred region
                nodes = regionNodes;
            }
        }

        // Final selection: Pick node with lowest load
        var bestNode = nodes
            .OrderBy(n => n.Status.CurrentLoad)
            .ThenByDescending(n => n.Status.AvailableSpace)
            .First();

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
    /// Gets user ID from the JWT token in the request
    /// </summary>
    private string GetUserIdFromToken()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
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
}