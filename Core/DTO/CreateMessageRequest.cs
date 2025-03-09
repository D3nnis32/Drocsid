namespace Drocsid.HenrikDennis2025.Core.DTO;

public class CreateMessageRequest
{
    public string Content { get; set; }
    public List<Guid> AttachmentIds { get; set; } = new();
}