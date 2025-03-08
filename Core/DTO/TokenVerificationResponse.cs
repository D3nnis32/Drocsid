namespace Drocsid.HenrikDennis2025.Core.DTO;

/// <summary>
/// Response for token verification
/// </summary>
public class TokenVerificationResponse
{
    public bool IsValid { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public DateTime ExpiresAt { get; set; }
}