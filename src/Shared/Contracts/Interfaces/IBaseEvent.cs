
namespace Enterprise.Documentation.Shared.Contracts.Interfaces;

/// <summary>
/// Base interface for all domain events in the Enterprise Documentation Platform.
/// Provides common properties for event correlation, tracking, and governance.
/// </summary>
public interface IBaseEvent
{
    /// <summary>
    /// Unique identifier for this specific event instance.
    /// Used for deduplication and event correlation.
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// Correlation ID that links related events across agent boundaries.
    /// Essential for distributed tracing and governance audit trails.
    /// </summary>
    string CorrelationId { get; }
    
    /// <summary>
    /// UTC timestamp when the event was created.
    /// Used for event ordering and governance compliance.
    /// </summary>
    DateTimeOffset Timestamp { get; }
    
    /// <summary>
    /// Name of the agent or service that published this event.
    /// Required for governance and security audit trails.
    /// </summary>
    string SourceAgent { get; }
    
    /// <summary>
    /// Version of the event schema for backward compatibility.
    /// Enables safe evolution of event contracts.
    /// </summary>
    string EventVersion { get; }
    
    /// <summary>
    /// Optional additional metadata for the event.
    /// Useful for governance, debugging, and agent-specific context.
    /// </summary>
    Dictionary<string, object>? Metadata { get; }
}