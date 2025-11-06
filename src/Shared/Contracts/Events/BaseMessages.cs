using Shared.Contracts.Interfaces;

namespace Shared.Contracts.Events;

/// <summary>
/// Base implementation of IMessage providing common message properties.
/// All messages in the system should inherit from this base class.
/// </summary>
public abstract record BaseMessage : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public string CreatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Version { get; set; } = "1.0";

    /// <inheritdoc />
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Base implementation of IEvent providing common event properties.
/// All domain events should inherit from this base class.
/// </summary>
public abstract record BaseEvent : BaseMessage, IEvent
{
    /// <inheritdoc />
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public string AggregateId { get; set; } = string.Empty;

    /// <inheritdoc />
    public string AggregateType { get; set; } = string.Empty;

    /// <inheritdoc />
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Event type name for routing and handling.
    /// Automatically derived from the class name.
    /// </summary>
    public string EventType => GetType().Name;

    /// <summary>
    /// Creates an event with the specified aggregate information.
    /// </summary>
    /// <param name="aggregateId">Aggregate identifier</param>
    /// <param name="aggregateType">Aggregate type name</param>
    /// <param name="correlationId">Optional correlation ID</param>
    protected BaseEvent(string aggregateId, string aggregateType, string? correlationId = null)
    {
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        if (!string.IsNullOrEmpty(correlationId))
        {
            CorrelationId = correlationId;
        }
    }

    /// <summary>
    /// Parameterless constructor for serialization.
    /// </summary>
    protected BaseEvent() { }
}

/// <summary>
/// Base implementation of ICommand providing common command properties.
/// All commands in the system should inherit from this base class.
/// </summary>
public abstract record BaseCommand : BaseMessage, ICommand
{
    /// <inheritdoc />
    public DateTime? ExecuteAt { get; set; }

    /// <inheritdoc />
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public CommandPriority Priority { get; set; } = CommandPriority.Normal;

    /// <inheritdoc />
    public bool RequiresAcknowledgment { get; set; } = true;

    /// <summary>
    /// Command type name for routing and handling.
    /// Automatically derived from the class name.
    /// </summary>
    public string CommandType => GetType().Name;

    /// <summary>
    /// Target service or handler for this command.
    /// </summary>
    public string? TargetService { get; set; }

    /// <summary>
    /// Creates a command with the specified correlation ID.
    /// </summary>
    /// <param name="correlationId">Optional correlation ID</param>
    protected BaseCommand(string? correlationId = null)
    {
        if (!string.IsNullOrEmpty(correlationId))
        {
            CorrelationId = correlationId;
        }
    }

    /// <summary>
    /// Parameterless constructor for serialization.
    /// </summary>
    protected BaseCommand() { }
}

/// <summary>
/// Base implementation of ICommandResponse providing common response properties.
/// All command responses should inherit from this base class.
/// </summary>
public abstract record BaseCommandResponse : BaseMessage, ICommandResponse
{
    /// <inheritdoc />
    public string CommandId { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsSuccess { get; set; }

    /// <inheritdoc />
    public string? ErrorMessage { get; set; }

    /// <inheritdoc />
    public Dictionary<string, object>? ErrorDetails { get; set; }

    /// <inheritdoc />
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public TimeSpan ProcessingDuration { get; set; }

    /// <summary>
    /// Response type name for routing and handling.
    /// Automatically derived from the class name.
    /// </summary>
    public string ResponseType => GetType().Name;

    /// <summary>
    /// Creates a successful command response.
    /// </summary>
    /// <param name="commandId">Original command identifier</param>
    /// <param name="correlationId">Correlation identifier</param>
    protected BaseCommandResponse(string commandId, string correlationId)
    {
        CommandId = commandId;
        CorrelationId = correlationId;
        IsSuccess = true;
    }

    /// <summary>
    /// Creates a failed command response.
    /// </summary>
    /// <param name="commandId">Original command identifier</param>
    /// <param name="correlationId">Correlation identifier</param>
    /// <param name="errorMessage">Error description</param>
    /// <param name="errorDetails">Detailed error information</param>
    protected BaseCommandResponse(string commandId, string correlationId, string errorMessage, Dictionary<string, object>? errorDetails = null)
    {
        CommandId = commandId;
        CorrelationId = correlationId;
        IsSuccess = false;
        ErrorMessage = errorMessage;
        ErrorDetails = errorDetails;
    }

    /// <summary>
    /// Parameterless constructor for serialization.
    /// </summary>
    protected BaseCommandResponse() { }

    /// <summary>
    /// Creates a successful response for the specified command.
    /// </summary>
    public static T Success<T>(string commandId, string correlationId) where T : BaseCommandResponse, new() =>
        new() { CommandId = commandId, CorrelationId = correlationId, IsSuccess = true };

    /// <summary>
    /// Creates a failed response for the specified command.
    /// </summary>
    public static T Failure<T>(string commandId, string correlationId, string errorMessage, Dictionary<string, object>? errorDetails = null) 
        where T : BaseCommandResponse, new() =>
        new() { CommandId = commandId, CorrelationId = correlationId, IsSuccess = false, ErrorMessage = errorMessage, ErrorDetails = errorDetails };
}

/// <summary>
/// Domain event raised when an entity is created.
/// Generic event that can be used for any entity type.
/// </summary>
/// <typeparam name="T">Entity type that was created</typeparam>
public record EntityCreatedEvent<T> : BaseEvent
{
    /// <summary>The entity that was created</summary>
    public T Entity { get; init; } = default!;

    /// <summary>Additional context about the creation</summary>
    public Dictionary<string, object> Context { get; init; } = new();

    /// <summary>Creates an entity created event</summary>
    public EntityCreatedEvent(T entity, string aggregateId, string aggregateType, string? correlationId = null)
        : base(aggregateId, aggregateType, correlationId)
    {
        Entity = entity;
    }

    /// <summary>Parameterless constructor for serialization</summary>
    public EntityCreatedEvent() { }
}

/// <summary>
/// Domain event raised when an entity is updated.
/// Generic event that can be used for any entity type.
/// </summary>
/// <typeparam name="T">Entity type that was updated</typeparam>
public record EntityUpdatedEvent<T> : BaseEvent
{
    /// <summary>The entity before the update</summary>
    public T? PreviousEntity { get; init; }

    /// <summary>The entity after the update</summary>
    public T CurrentEntity { get; init; } = default!;

    /// <summary>Fields that were changed</summary>
    public IReadOnlyList<string> ChangedFields { get; init; } = Array.Empty<string>();

    /// <summary>Additional context about the update</summary>
    public Dictionary<string, object> Context { get; init; } = new();

    /// <summary>Creates an entity updated event</summary>
    public EntityUpdatedEvent(T currentEntity, string aggregateId, string aggregateType, T? previousEntity = default, IReadOnlyList<string>? changedFields = null, string? correlationId = null)
        : base(aggregateId, aggregateType, correlationId)
    {
        CurrentEntity = currentEntity;
        PreviousEntity = previousEntity;
        ChangedFields = changedFields ?? Array.Empty<string>();
    }

    /// <summary>Parameterless constructor for serialization</summary>
    public EntityUpdatedEvent() { }
}

/// <summary>
/// Domain event raised when an entity is deleted.
/// Generic event that can be used for any entity type.
/// </summary>
/// <typeparam name="T">Entity type that was deleted</typeparam>
public record EntityDeletedEvent<T> : BaseEvent
{
    /// <summary>The entity that was deleted</summary>
    public T Entity { get; init; } = default!;

    /// <summary>Whether this was a soft delete (marked as deleted) or hard delete (removed from database)</summary>
    public bool IsSoftDelete { get; init; }

    /// <summary>Additional context about the deletion</summary>
    public Dictionary<string, object> Context { get; init; } = new();

    /// <summary>Creates an entity deleted event</summary>
    public EntityDeletedEvent(T entity, string aggregateId, string aggregateType, bool isSoftDelete = false, string? correlationId = null)
        : base(aggregateId, aggregateType, correlationId)
    {
        Entity = entity;
        IsSoftDelete = isSoftDelete;
    }

    /// <summary>Parameterless constructor for serialization</summary>
    public EntityDeletedEvent() { }
}