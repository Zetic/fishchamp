namespace FishChamp.Events;

/// <summary>
/// Base interface for all events in the system
/// </summary>
public interface IEvent
{
    /// <summary>
    /// When the event was created
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Unique identifier for this event instance
    /// </summary>
    string EventId { get; }
}