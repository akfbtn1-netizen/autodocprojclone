
using Enterprise.Documentation.Core.Domain.ValueObjects;


namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Base class for all domain entities in the Enterprise Documentation Platform.
/// Provides common functionality for entity identification, auditing, and domain events.
/// </summary>
/// <typeparam name="TId">The type of the entity's strongly-typed identifier</typeparam>
public abstract class BaseEntity<TId> where TId : StronglyTypedId<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// The unique identifier for this entity.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// When this entity was created.
    /// </summary>
    public DateTime CreatedAt { get; protected set; }

    /// <summary>
    /// When this entity was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; protected set; }

    /// <summary>
    /// Who created this entity.
    /// </summary>
    public UserId CreatedBy { get; protected set; } = default!;

    /// <summary>
    /// Who last modified this entity.
    /// </summary>
    public UserId ModifiedBy { get; protected set; } = default!;

    /// <summary>
    /// Version number for optimistic concurrency control.
    /// </summary>
    public int Version { get; protected set; }

    /// <summary>
    /// Whether this entity has been deleted (soft delete).
    /// </summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// When this entity was deleted (if applicable).
    /// </summary>
    public DateTime? DeletedAt { get; protected set; }

    /// <summary>
    /// Who deleted this entity (if applicable).
    /// </summary>
    public UserId? DeletedBy { get; protected set; }

    protected BaseEntity(TId id, UserId createdBy)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
        ModifiedBy = createdBy;
        Version = 1;
        IsDeleted = false;
    }

    // Required for EF Core
    protected BaseEntity() { }

    /// <summary>
    /// Domain events that have been raised by this entity.
    /// Used for publishing events after successful persistence.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to be published after the entity is persisted.
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all domain events. Called after events have been published.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Updates the modification tracking fields.
    /// Should be called whenever the entity is modified.
    /// </summary>
    protected void UpdateModificationTracking(UserId modifiedBy)
    {
        ModifiedBy = modifiedBy ?? throw new ArgumentNullException(nameof(modifiedBy));
        ModifiedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>
    /// Marks this entity as deleted (soft delete).
    /// The entity remains in the database but is marked as deleted.
    /// </summary>
    public virtual void Delete(UserId deletedBy)
    {
        if (IsDeleted)
            throw new InvalidOperationException("Entity is already deleted");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy ?? throw new ArgumentNullException(nameof(deletedBy));
        UpdateModificationTracking(deletedBy);
        
        AddDomainEvent(new EntityDeletedEvent(Id.ToString(), GetType().Name, deletedBy.ToString()));
    }

    /// <summary>
    /// Restores a soft-deleted entity.
    /// </summary>
    public virtual void Restore(UserId restoredBy)
    {
        if (!IsDeleted)
            throw new InvalidOperationException("Entity is not deleted");

        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        UpdateModificationTracking(restoredBy);
        
        AddDomainEvent(new EntityRestoredEvent(Id.ToString(), GetType().Name, restoredBy.ToString()));
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity<TId> other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return Id.Equals(other.Id);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(BaseEntity<TId>? left, BaseEntity<TId>? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(BaseEntity<TId>? left, BaseEntity<TId>? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Interface for domain events that can be raised by entities.
/// </summary>
public interface IDomainEvent : MediatR.INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
}

/// <summary>
/// Domain event raised when an entity is deleted.
/// </summary>
public record EntityDeletedEvent(string EntityId, string EntityType, string DeletedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(EntityDeletedEvent);
}

/// <summary>
/// Domain event raised when an entity is restored from deletion.
/// </summary>
public record EntityRestoredEvent(string EntityId, string EntityType, string RestoredBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(EntityRestoredEvent);
}