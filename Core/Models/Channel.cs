namespace Drocsid.HenrikDennis2025.Core.Models;

public class Channel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public ChannelType Type { get; set; }
    public List<Guid> MemberIds { get; set; } = new();
}

public enum ChannelType
{
    Public,
    Private,
    DirectMessage
}