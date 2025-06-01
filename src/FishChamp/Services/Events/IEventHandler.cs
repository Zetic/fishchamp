namespace FishChamp.Services.Events;

/// <summary>
/// Interface for handling specific types of events
/// </summary>
/// <typeparam name="TEvent">The type of event to handle</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    /// <summary>
    /// Handle the specified event
    /// </summary>
    /// <param name="eventData">The event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleAsync(TEvent eventData, CancellationToken cancellationToken = default);
}