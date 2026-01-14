using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Domain.Events;

/// <summary>
/// Domain event raised when a template is created
/// </summary>
public record TemplateCreatedEvent(TemplateId TemplateId, string Name, string Category, UserId CreatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(TemplateCreatedEvent);
}

/// <summary>
/// Domain event raised when template content is updated
/// </summary>
public record TemplateContentUpdatedEvent(TemplateId TemplateId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(TemplateContentUpdatedEvent);
}

/// <summary>
/// Domain event raised when template metadata is updated
/// </summary>
public record TemplateMetadataUpdatedEvent(TemplateId TemplateId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(TemplateMetadataUpdatedEvent);
}

/// <summary>
/// Domain event raised when a template is activated
/// </summary>
public record TemplateActivatedEvent(TemplateId TemplateId, UserId ActivatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(TemplateActivatedEvent);
}

/// <summary>
/// Domain event raised when a template is deactivated
/// </summary>
public record TemplateDeactivatedEvent(TemplateId TemplateId, UserId DeactivatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(TemplateDeactivatedEvent);
}

/// <summary>
/// Domain event raised when a template is used to generate a document
/// </summary>
public record TemplateUsedEvent(TemplateId TemplateId, UserId UsedBy, int TotalUsageCount) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(TemplateUsedEvent);
}