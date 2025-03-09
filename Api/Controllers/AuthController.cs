using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Interfaces.Options;
using Drocsid.HenrikDennis2025.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Drocsid.HenrikDennis2025.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NodeRegistrationOptions _nodeOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService, 
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IOptions<NodeRegistrationOptions> nodeOptions,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _nodeOptions = nodeOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Direct login to this storage node - should only be used if the client
    /// already knows this is the correct node to connect to
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        // Validate credentials
        var isValid = await _userService.ValidateCredentialsAsync(request.Username, request.Password);
        if (!isValid)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return Unauthorized("Invalid username or password");
        }

        // Get user details
        var users = await _userService.FindUsersAsync(u => u.Username == request.Username);
        var user = users.FirstOrDefault();
        if (user == null)
        {
            return Unauthorized("User not found");
        }

        // Generate JWT token
        var token = GenerateJwtToken(user.Id.ToString(), user.Username);

        _logger.LogInformation("User {Username} logged in directly to storage node", user.Username);
        
        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username
        };
    }

    /// <summary>
    /// Verify a token issued by the registry service
    /// </summary>
    [HttpPost("verify-token")]
    public async Task<ActionResult<TokenVerificationResponse>> VerifyToken(TokenVerificationRequest request)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Validate the token
            var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out var validatedToken);
            
            // Extract claims
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username = principal.FindFirst(ClaimTypes.Name)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("Invalid token claims");
            }

            // Check if user exists in this node's database
            var userGuid = Guid.Parse(userId);
            var user = await _userService.GetUserByIdAsync(userGuid);
            
            if (user == null)
            {
                _logger.LogInformation("User {UserId} not found locally, will request from registry", userId);
                
                // Try to fetch user data from registry
                var registryUser = await FetchUserFromRegistryAsync(userGuid);
                if (registryUser != null)
                {
                    // Create the user locally
                    await _userService.SyncUserAsync(registryUser);
                    _logger.LogInformation("Synced user {Username} from registry", registryUser.Username);
                }
                else
                {
                    return Unauthorized("User not found in system");
                }
            }

            return Ok(new TokenVerificationResponse
            {
                IsValid = true,
                UserId = Guid.Parse(userId),
                Username = username,
                ExpiresAt = validatedToken.ValidTo
            });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed: {Message}", ex.Message);
            return Unauthorized("Invalid token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying token");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Fetch user data from the registry if not found locally
    /// </summary>
    private async Task<Core.Models.User> FetchUserFromRegistryAsync(Guid userId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var registryUrl = _nodeOptions.RegistryUrl.TrimEnd('/');
            
            var response = await client.GetAsync($"{registryUrl}/api/registry/users/{userId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Core.Models.User>();
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

    private string GenerateJwtToken(string userId, string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.Now.AddDays(Convert.ToDouble(_configuration["Jwt:ExpireDays"]));

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