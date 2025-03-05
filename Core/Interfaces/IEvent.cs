namespace Drocsid.HenrikDennis2025.Core.Interfaces;

/// <summary>
/// Represents an event that can be published through the event bus.
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Gets the timestamp when the event was created.
    /// </summary>
    DateTime Timestamp { get; }
}