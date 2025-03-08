using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Drocsid.HenrikDennis2025.Api.Controllers;

/// <summary>
/// Base controller with common functionality for all controllers
/// </summary>
public class BaseController : ControllerBase
{
    private readonly ILogger<BaseController> _logger;

    public BaseController(ILogger<BaseController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get the current user's ID from JWT claims
    /// </summary>
    protected Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogWarning("User ID claim not found in token");
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        else
        {
            _logger.LogWarning("Invalid user ID format in token: {UserId}", userIdClaim);
            throw new UnauthorizedAccessException("Invalid user ID format");
        }
    }

    /// <summary>
    /// Get the current username from JWT claims
    /// </summary>
    protected string GetCurrentUsername()
    {
        var usernameClaim = User.FindFirst(ClaimTypes.Name)?.Value;
        
        if (string.IsNullOrEmpty(usernameClaim))
        {
            _logger.LogWarning("Username claim not found in token");
            throw new UnauthorizedAccessException("Username not found in token");
        }

        return usernameClaim;
    }
}