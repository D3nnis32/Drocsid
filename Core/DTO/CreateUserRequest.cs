namespace Drocsid.HenrikDennis2025.Core.DTO;

public class CreateUserRequest
{
    public string Username { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string PreferredRegion { get; set; }
}