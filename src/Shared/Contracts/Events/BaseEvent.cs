
using Enterprise.Documentation.Shared.Contracts.Interfaces;

namespace Enterprise.Documentation.Shared.Contracts.Events;

/// <summary>
/// Base implementation of IBaseEvent providing common event functionality.
/// All domain events in the Enterprise Documentation Platform should inherit from this class.
/// </summary>
public abstract class BaseEvent : IBaseEvent
{
    /// <summary>
    /// Initializes a new BaseEvent with required governance and tracing properties.
    /// </summary>
    /// <param name="sourceAgent">Name of the agent publishing this event</param>
    /// <param name="correlationId">Optional correlation ID (generates new GUID if not provided)</param>
    protected BaseEvent(string sourceAgent, string? correlationId = null)
    {
        EventId = Guid.NewGuid();
        CorrelationId = correlationId ?? Guid.NewGuid().ToString();
        Timestamp = DateTimeOffset.UtcNow;
        SourceAgent = sourceAgent ?? throw new ArgumentNullException(nameof(sourceAgent));
        EventVersion = "1.0";
        Metadata = new Dictionary<string, object>();
    }

    /// <inheritdoc />
    public Guid EventId { get; }

    /// <inheritdoc />
    public string CorrelationId { get; }

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc />
    public string SourceAgent { get; }

    /// <inheritdoc />
    public string EventVersion { get; protected set; }

    /// <inheritdoc />
    public Dictionary<string, object>? Metadata { get; protected set; }

    /// <summary>
    /// Adds metadata to the event for additional context.
    /// Useful for agent-specific information, debugging, or governance data.
    /// </summary>
    /// <param name="key">Metadata key</param>
    /// <param name="value">Metadata value</param>
    /// <returns>This event instance for method chaining</returns>
    public BaseEvent AddMetadata(string key, object value)
    {
        Metadata ??= new Dictionary<string, object>();
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the event version for backward compatibility.
    /// Should be updated when event schema changes in a breaking way.
    /// </summary>
    /// <param name="version">Event schema version</param>
    /// <returns>This event instance for method chaining</returns>
    protected BaseEvent SetVersion(string version)
    {
        EventVersion = version ?? throw new ArgumentNullException(nameof(version));
        return this;
    }

    /// <summary>
    /// Returns a string representation of the event for logging and debugging.
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name} [EventId: {EventId}, Source: {SourceAgent}, Timestamp: {Timestamp:O}]";
    }
}