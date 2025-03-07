namespace Drocsid.HenrikDennis2025.Core.DTO;

public class LoginResponse
{
    public string Token { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; }
}