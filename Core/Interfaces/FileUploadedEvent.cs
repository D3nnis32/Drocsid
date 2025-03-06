namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Event raised when a file is uploaded.
/// </summary>
public class FileUploadedEvent : EventBase
{
    /// <summary>
    /// Gets the attachment representing the uploaded file.
    /// </summary>
    public Core.Models.Attachment Attachment { get; }
        
    /// <summary>
    /// Gets the ID of the user who uploaded the file.
    /// </summary>
    public Guid UploadedByUserId { get; }
        
    /// <summary>
    /// Initializes a new instance of the FileUploadedEvent class.
    /// </summary>
    /// <param name="attachment">The attachment representing the uploaded file.</param>
    /// <param name="uploadedByUserId">The ID of the user who uploaded the file.</param>
    public FileUploadedEvent(Core.Models.Attachment attachment, Guid uploadedByUserId)
    {
        Attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
        UploadedByUserId = uploadedByUserId;
    }
}