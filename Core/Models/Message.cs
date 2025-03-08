using System.Net.Mail;

namespace Drocsid.HenrikDennis2025.Core.Models;

/// <summary>
/// Represents a message in a channel
/// </summary>
public class Message
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public List<Attachment> Attachments { get; set; } = new();
}