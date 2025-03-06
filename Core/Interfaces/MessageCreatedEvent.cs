namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Event raised when a new message is created.
/// </summary>
public class MessageCreatedEvent : EventBase
{
    /// <summary>
    /// Gets the message that was created.
    /// </summary>
    public Core.Models.Message Message { get; }
        
    /// <summary>
    /// Initializes a new instance of the MessageCreatedEvent class.
    /// </summary>
    /// <param name="message">The message that was created.</param>
    public MessageCreatedEvent(Core.Models.Message message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }
}