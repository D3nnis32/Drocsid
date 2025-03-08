namespace Drocsid.HenrikDennis2025.Core.Models;

public class UserChannel
{
    public Guid UserId { get; set; }
    public Guid ChannelId { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class ChannelNode
{
    public Guid ChannelId { get; set; }
    public string NodeId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MessageLocation
{
    public Guid MessageId { get; set; }
    public string NodeId { get; set; }
    public DateTime CreatedAt { get; set; }
}