using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FishChamp.Services.Events;

/// <summary>
/// In-memory event bus implementation for publishing and subscribing to events
/// </summary>
public class EventBus : IEventBus
{
    private readonly ILogger<EventBus> _logger;
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly object _lock = new();

    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) 
        where TEvent : IEvent
    {
        if (eventData == null)
        {
            _logger.LogWarning("Attempted to publish null event of type {EventType}", typeof(TEvent).Name);
            return;
        }

        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlerList))
        {
            _logger.LogDebug("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }

        List<object> currentHandlers;
        lock (_lock)
        {
            currentHandlers = new List<object>(handlerList);
        }

        _logger.LogDebug("Publishing event {EventType} with ID {EventId} to {HandlerCount} handlers", 
            eventType.Name, eventData.EventId, currentHandlers.Count);

        var tasks = currentHandlers
            .Cast<IEventHandler<TEvent>>()
            .Select(handler => HandleEventSafely(handler, eventData, cancellationToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        if (handler == null)
        {
            _logger.LogWarning("Attempted to subscribe null handler for event type {EventType}", typeof(TEvent).Name);
            return;
        }

        var eventType = typeof(TEvent);
        lock (_lock)
        {
            if (!_handlers.TryGetValue(eventType, out var handlerList))
            {
                handlerList = new List<object>();
                _handlers[eventType] = handlerList;
            }

            if (!handlerList.Contains(handler))
            {
                handlerList.Add(handler);
                _logger.LogDebug("Subscribed handler {HandlerType} to event type {EventType}", 
                    handler.GetType().Name, eventType.Name);
            }
        }
    }

    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        if (handler == null)
        {
            return;
        }

        var eventType = typeof(TEvent);
        lock (_lock)
        {
            if (_handlers.TryGetValue(eventType, out var handlerList))
            {
                if (handlerList.Remove(handler))
                {
                    _logger.LogDebug("Unsubscribed handler {HandlerType} from event type {EventType}", 
                        handler.GetType().Name, eventType.Name);
                }
            }
        }
    }

    private async Task HandleEventSafely<TEvent>(IEventHandler<TEvent> handler, TEvent eventData, 
        CancellationToken cancellationToken) where TEvent : IEvent
    {
        try
        {
            await handler.HandleAsync(eventData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling event {EventType} with ID {EventId} in handler {HandlerType}", 
                typeof(TEvent).Name, eventData.EventId, handler.GetType().Name);
        }
    }
}