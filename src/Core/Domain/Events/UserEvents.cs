using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Domain.Events;

/// <summary>
/// Domain event raised when a user is created
/// </summary>
public record UserCreatedEvent(UserId UserId, string Email, string DisplayName, UserId CreatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserCreatedEvent);
}

/// <summary>
/// Domain event raised when user profile is updated
/// </summary>
public record UserProfileUpdatedEvent(UserId UserId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserProfileUpdatedEvent);
}

/// <summary>
/// Domain event raised when a role is assigned to a user
/// </summary>
public record UserRoleAssignedEvent(UserId UserId, UserRole Role, UserId AssignedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserRoleAssignedEvent);
}

/// <summary>
/// Domain event raised when a role is removed from a user
/// </summary>
public record UserRoleRemovedEvent(UserId UserId, UserRole Role, UserId RemovedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserRoleRemovedEvent);
}

/// <summary>
/// Domain event raised when user security clearance is updated
/// </summary>
public record UserSecurityClearanceUpdatedEvent(UserId UserId, SecurityClearanceLevel OldClearance, SecurityClearanceLevel NewClearance, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserSecurityClearanceUpdatedEvent);
}

/// <summary>
/// Domain event raised when a user is activated
/// </summary>
public record UserActivatedEvent(UserId UserId, UserId ActivatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserActivatedEvent);
}

/// <summary>
/// Domain event raised when a user is deactivated
/// </summary>
public record UserDeactivatedEvent(UserId UserId, UserId DeactivatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserDeactivatedEvent);
}

/// <summary>
/// Domain event raised when user access is recorded
/// </summary>
public record UserAccessRecordedEvent(UserId UserId, DateTime AccessTime) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserAccessRecordedEvent);
}

/// <summary>
/// Domain event raised when user preferences are updated
/// </summary>
public record UserPreferencesUpdatedEvent(UserId UserId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(UserPreferencesUpdatedEvent);
}