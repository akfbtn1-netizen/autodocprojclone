
namespace Enterprise.Documentation.Shared.Contracts.Interfaces;

/// <summary>
/// Generic interface for handling domain events in the Enterprise Documentation Platform.
/// All agent event handlers MUST implement this interface for consistency and governance.
/// </summary>
/// <typeparam name="TEvent">Type of event to handle, must implement IBaseEvent</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IBaseEvent
{
    /// <summary>
    /// Handles the specified event asynchronously.
    /// Implementations MUST be idempotent and include proper error handling.
    /// </summary>
    /// <param name="eventData">The event to process</param>
    /// <param name="cancellationToken">Cancellation token for operation timeout</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleAsync(TEvent eventData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for event publishers in the Enterprise Documentation Platform.
/// Provides standardized event publishing with governance integration.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to the message bus with governance validation.
    /// Events are automatically enriched with governance metadata.
    /// </summary>
    /// <param name="eventData">Event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) 
        where TEvent : IBaseEvent;
    
    /// <summary>
    /// Publishes multiple events as a batch with transactional semantics.
    /// Either all events are published or none are (atomicity).
    /// </summary>
    /// <param name="events">Collection of events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default) 
        where TEvent : IBaseEvent;
}