namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Common base class for events.
/// </summary>
public abstract class EventBase : IEvent
{
    /// <summary>
    /// Gets the timestamp when the event was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}