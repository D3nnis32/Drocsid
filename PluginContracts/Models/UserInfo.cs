namespace Drocsid.HenrikDennis2025.PluginContracts.Models;

/// <summary>
/// Basic user information for plugins
/// </summary>
public class UserInfo
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Status { get; set; }
}