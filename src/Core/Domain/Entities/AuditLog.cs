
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Audit log entity representing system activity tracking.
/// Records all significant actions and changes within the platform for compliance and monitoring.
/// </summary>
public class AuditLog : BaseEntity<AuditLogId>
{
    /// <summary>
    /// Type of entity being audited (e.g., "Document", "User", "Template").
    /// </summary>
    public string EntityType { get; private set; }

    /// <summary>
    /// ID of the entity being audited.
    /// </summary>
    public string EntityId { get; private set; }

    /// <summary>
    /// Action performed (e.g., "Created", "Updated", "Deleted", "Approved").
    /// </summary>
    public string Action { get; private set; }

    /// <summary>
    /// Description of the action performed.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// When the action occurred.
    /// </summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>
    /// Additional metadata about the action (JSON serialized).
    /// </summary>
    public Dictionary<string, object> Metadata { get; private set; }

    /// <summary>
    /// IP address from which the action was performed.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User agent string of the client.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Session ID associated with the action.
    /// </summary>
    public string? SessionId { get; private set; }

    // Private constructor for EF Core
    private AuditLog() : base()
    {
        EntityType = string.Empty;
        EntityId = string.Empty;
        Action = string.Empty;
        Description = string.Empty;
        OccurredAt = DateTime.UtcNow;
        Metadata = new Dictionary<string, object>();
    }

    /// <summary>
    /// Creates a new audit log entry.
    /// </summary>
    public AuditLog(
        AuditLogId id,
        string entityType,
        string entityId,
        string action,
        string description,
        UserId performedBy,
        DateTime? occurredAt = null,
        Dictionary<string, object>? metadata = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? sessionId = null) : base(id, performedBy)
    {
        EntityType = !string.IsNullOrWhiteSpace(entityType) 
            ? entityType 
            : throw new ArgumentException("Entity type cannot be empty", nameof(entityType));
        
        EntityId = !string.IsNullOrWhiteSpace(entityId) 
            ? entityId 
            : throw new ArgumentException("Entity ID cannot be empty", nameof(entityId));
        
        Action = !string.IsNullOrWhiteSpace(action) 
            ? action 
            : throw new ArgumentException("Action cannot be empty", nameof(action));
        
        Description = !string.IsNullOrWhiteSpace(description) 
            ? description 
            : throw new ArgumentException("Description cannot be empty", nameof(description));

        OccurredAt = occurredAt ?? DateTime.UtcNow;
        Metadata = metadata ?? new Dictionary<string, object>();
        IpAddress = ipAddress;
        UserAgent = userAgent;
        SessionId = sessionId;

        // Add domain event for audit log creation
        AddDomainEvent(new AuditLogCreatedEvent(id, entityType, entityId, action, performedBy));
    }

    /// <summary>
    /// Adds metadata to the audit log.
    /// </summary>
    public void AddMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be empty", nameof(key));

        Metadata[key] = value;
    }

    /// <summary>
    /// Adds multiple metadata entries.
    /// </summary>
    public void AddMetadata(Dictionary<string, object> metadata)
    {
        if (metadata == null) return;

        foreach (var kvp in metadata)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Key))
            {
                Metadata[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Gets a metadata value by key.
    /// </summary>
    public T? GetMetadata<T>(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !Metadata.ContainsKey(key))
            return default;

        try
        {
            return (T)Metadata[key];
        }
        catch (InvalidCastException)
        {
            return default;
        }
        catch (ArgumentException)
        {
            return default;
        }
    }

    /// <summary>
    /// Checks if the audit log has specific metadata.
    /// </summary>
    public bool HasMetadata(string key) => !string.IsNullOrWhiteSpace(key) && Metadata.ContainsKey(key);

    /// <summary>
    /// Creates a summary string for the audit log.
    /// </summary>
    public string GetSummary()
    {
        return $"{Action} {EntityType} {EntityId} at {OccurredAt:yyyy-MM-dd HH:mm:ss} by {CreatedBy.Value}";
    }
}

// Domain Events
public record AuditLogCreatedEvent(AuditLogId AuditLogId, string EntityType, string EntityId, string Action, UserId PerformedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AuditLogCreatedEvent);
}