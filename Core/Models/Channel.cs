namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents a channel in the chat system
/// </summary>
public class Channel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public ChannelType Type { get; set; }
    public List<Guid> MemberIds { get; set; } = new List<Guid>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ChannelType
{
    Public,
    Private,
    DirectMessage
}