namespace Drocsid.HenrikDennis2025.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public UserStatus Status { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string PasswordHash { get; set; }
}

public enum UserStatus
{
    Online,
    Away,
    DoNotDisturb,
    Offline
}