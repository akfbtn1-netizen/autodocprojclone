using Shared.Contracts.Interfaces;

namespace Core.Domain.Entities;

/// <summary>
/// Base entity implementation providing common properties and functionality.
/// All domain entities should inherit from this base class to ensure consistency.
/// Implements IEntity interface with Guid primary keys and audit tracking.
/// </summary>
public abstract class BaseEntity : IEntity
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public string CreatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Whether this entity has been soft deleted.
    /// Soft deleted entities are not returned in normal queries.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// When this entity was soft deleted (if applicable).
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Who soft deleted this entity (if applicable).
    /// </summary>
    public string? DeletedBy { get; set; }

    /// <summary>
    /// Updates the entity's audit information.
    /// Call this method whenever the entity is modified.
    /// </summary>
    /// <param name="updatedBy">Who is making the update</param>
    public virtual void UpdateAuditInfo(string updatedBy)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    /// <summary>
    /// Marks the entity as soft deleted.
    /// The entity will remain in the database but won't appear in normal queries.
    /// </summary>
    /// <param name="deletedBy">Who is deleting the entity</param>
    public virtual void SoftDelete(string deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = deletedBy;
        UpdateAuditInfo(deletedBy);
    }

    /// <summary>
    /// Restores a soft deleted entity.
    /// </summary>
    /// <param name="restoredBy">Who is restoring the entity</param>
    public virtual void Restore(string restoredBy)
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        UpdateAuditInfo(restoredBy);
    }

    /// <summary>
    /// Determines equality based on the entity ID.
    /// Two entities are equal if they have the same ID and are not transient.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (IsTransient() || other.IsTransient())
            return false;

        return Id == other.Id;
    }

    /// <summary>
    /// Gets the hash code based on the entity ID.
    /// </summary>
    public override int GetHashCode()
    {
        return IsTransient() ? base.GetHashCode() : Id.GetHashCode();
    }

    /// <summary>
    /// Returns a string representation of the entity.
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name} [Id={Id}]";
    }

    /// <summary>
    /// Checks if this entity is transient (not yet persisted).
    /// A transient entity has a default Guid value.
    /// </summary>
    /// <returns>True if the entity is transient, false otherwise</returns>
    public bool IsTransient()
    {
        return Id == Guid.Empty;
    }

    /// <summary>
    /// Equality operator overload.
    /// </summary>
    public static bool operator ==(BaseEntity? left, BaseEntity? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    /// <summary>
    /// Inequality operator overload.
    /// </summary>
    public static bool operator !=(BaseEntity? left, BaseEntity? right)
    {
        return !(left == right);
    }
}