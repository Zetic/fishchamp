namespace FishChamp.Services.Events;

/// <summary>
/// Interface for publishing and subscribing to events
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publish an event to all registered handlers
    /// </summary>
    /// <typeparam name="TEvent">The type of event</typeparam>
    /// <param name="eventData">The event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : IEvent;
    
    /// <summary>
    /// Subscribe an event handler to a specific event type
    /// </summary>
    /// <typeparam name="TEvent">The type of event</typeparam>
    /// <param name="handler">The event handler</param>
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    
    /// <summary>
    /// Unsubscribe an event handler from a specific event type
    /// </summary>
    /// <typeparam name="TEvent">The type of event</typeparam>
    /// <param name="handler">The event handler</param>
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
}