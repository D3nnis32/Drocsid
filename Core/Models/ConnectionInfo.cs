namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Connection information returned to clients after successful authentication
/// </summary>
public class ConnectionInfo
{
    /// <summary>
    /// The JWT token for authentication with both registry and storage nodes
    /// </summary>
    public string Token { get; set; }
    
    /// <summary>
    /// The endpoint URL of the assigned storage node
    /// </summary>
    public string NodeEndpoint { get; set; }
    
    /// <summary>
    /// The endpoint URL of the registry service
    /// </summary>
    public string RegistryEndpoint { get; set; }
    
    /// <summary>
    /// The user's unique identifier
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// The user's username
    /// </summary>
    public string Username { get; set; }
    
    /// <summary>
    /// When the token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}