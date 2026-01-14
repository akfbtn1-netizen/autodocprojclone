
namespace Enterprise.Documentation.Shared.Contracts.DTOs;

/// <summary>
/// Base class for all Data Transfer Objects in the Enterprise Documentation Platform.
/// Provides common properties for governance, auditing, and data correlation.
/// </summary>
public abstract class BaseDto
{
    /// <summary>
    /// Unique identifier for this DTO instance.
    /// Used for correlation and debugging across agent boundaries.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when this DTO was created.
    /// Used for data freshness validation and audit trails.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Correlation ID linking this DTO to related operations.
    /// Essential for distributed tracing and governance.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Version of the DTO schema for backward compatibility.
    /// Incremented when DTO structure changes in breaking ways.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Optional metadata for additional context.
    /// Useful for agent-specific data or debugging information.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Adds metadata to the DTO for additional context.
    /// </summary>
    /// <param name="key">Metadata key</param>
    /// <param name="value">Metadata value</param>
    /// <returns>This DTO instance for method chaining</returns>
    public T AddMetadata<T>(string key, object value) where T : BaseDto
    {
        Metadata ??= new Dictionary<string, object>();
        Metadata[key] = value;
        return (T)this;
    }

    /// <summary>
    /// Sets the correlation ID for this DTO.
    /// </summary>
    /// <param name="correlationId">Correlation ID to set</param>
    /// <returns>This DTO instance for method chaining</returns>
    public T WithCorrelationId<T>(string correlationId) where T : BaseDto
    {
        CorrelationId = correlationId;
        return (T)this;
    }
}