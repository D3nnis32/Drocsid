using System.Net.Mail;

namespace Drocsid.HenrikDennis2025.Core.Models;

public class Message
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
}