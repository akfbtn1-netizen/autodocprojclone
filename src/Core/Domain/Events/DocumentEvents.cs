using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Domain.Events;

/// <summary>
/// Domain event raised when a document is created
/// </summary>
public record DocumentCreatedEvent(DocumentId DocumentId, string Title, string Category, UserId CreatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentCreatedEvent);
}

/// <summary>
/// Domain event raised when document content is updated
/// </summary>
public record DocumentContentUpdatedEvent(DocumentId DocumentId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentContentUpdatedEvent);
}

/// <summary>
/// Domain event raised when document metadata is updated
/// </summary>
public record DocumentMetadataUpdatedEvent(DocumentId DocumentId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentMetadataUpdatedEvent);
}

/// <summary>
/// Domain event raised when document approval status changes
/// </summary>
public record DocumentApprovalStatusChangedEvent(DocumentId DocumentId, string PreviousStatus, string NewStatus, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentApprovalStatusChangedEvent);
}

/// <summary>
/// Domain event raised when a document is published
/// </summary>
public record DocumentPublishedEvent(DocumentId DocumentId, UserId PublishedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentPublishedEvent);
}

/// <summary>
/// Domain event raised when a document is archived
/// </summary>
public record DocumentArchivedEvent(DocumentId DocumentId, UserId ArchivedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentArchivedEvent);
}

/// <summary>
/// Domain event raised when a related document is added
/// </summary>
public record DocumentRelationAddedEvent(DocumentId DocumentId, DocumentId RelatedDocumentId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentRelationAddedEvent);
}

/// <summary>
/// Domain event raised when a related document is removed
/// </summary>
public record DocumentRelationRemovedEvent(DocumentId DocumentId, DocumentId RelatedDocumentId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentRelationRemovedEvent);
}

/// <summary>
/// Domain event raised when document security classification changes
/// </summary>
public record DocumentSecurityClassificationChangedEvent(DocumentId DocumentId, string PreviousLevel, string NewLevel, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DocumentSecurityClassificationChangedEvent);
}