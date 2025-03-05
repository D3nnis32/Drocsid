namespace Drocsid.HenrikDennis2025.Core.Interfaces;

public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : IEvent;
        
    /// <summary>
    /// Subscribes a handler to events of the specified type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <typeparam name="THandler">The type of handler to register.</typeparam>
    void Subscribe<TEvent, THandler>() 
        where TEvent : IEvent 
        where THandler : IEventHandler<TEvent>;
}