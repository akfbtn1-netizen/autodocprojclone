namespace Shared.Contracts.Interfaces;

/// <summary>
/// Enterprise message bus interface for Service Bus integration.
/// Provides reliable messaging capabilities for distributed architecture.
/// Supports both direct messaging and event publishing patterns.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to the specified topic asynchronously.
    /// Messages are delivered to all active subscribers of the topic.
    /// </summary>
    /// <typeparam name="T">Message type that implements IMessage</typeparam>
    /// <param name="message">Message to publish</param>
    /// <param name="topicName">Target topic name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<T>(T message, string topicName, CancellationToken cancellationToken = default) 
        where T : class, IMessage;

    /// <summary>
    /// Publishes an event to the event bus asynchronously.
    /// Events are broadcast to all interested event handlers.
    /// </summary>
    /// <typeparam name="T">Event type that implements IEvent</typeparam>
    /// <param name="eventMessage">Event to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishEventAsync<T>(T eventMessage, CancellationToken cancellationToken = default) 
        where T : class, IEvent;

    /// <summary>
    /// Sends a command to a specific queue asynchronously.
    /// Commands are delivered to a single consumer for processing.
    /// </summary>
    /// <typeparam name="T">Command type that implements ICommand</typeparam>
    /// <param name="command">Command to send</param>
    /// <param name="queueName">Target queue name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendCommandAsync<T>(T command, string queueName, CancellationToken cancellationToken = default) 
        where T : class, ICommand;

    /// <summary>
    /// Sends a command and waits for a response asynchronously.
    /// Implements request-response messaging pattern with timeout support.
    /// </summary>
    /// <typeparam name="TCommand">Command type that implements ICommand</typeparam>
    /// <typeparam name="TResponse">Response type that implements ICommandResponse</typeparam>
    /// <param name="command">Command to send</param>
    /// <param name="queueName">Target queue name</param>
    /// <param name="timeout">Maximum time to wait for response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command response</returns>
    Task<TResponse> SendCommandAsync<TCommand, TResponse>(
        TCommand command, 
        string queueName, 
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) 
        where TCommand : class, ICommand
        where TResponse : class, ICommandResponse;

    /// <summary>
    /// Subscribes to messages from a specific topic with a message handler.
    /// Handler will be called for each message received on the topic.
    /// </summary>
    /// <typeparam name="T">Message type that implements IMessage</typeparam>
    /// <param name="topicName">Topic to subscribe to</param>
    /// <param name="subscriptionName">Unique subscription name</param>
    /// <param name="handler">Message handler function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeAsync<T>(
        string topicName, 
        string subscriptionName, 
        Func<T, MessageContext, Task> handler,
        CancellationToken cancellationToken = default) 
        where T : class, IMessage;

    /// <summary>
    /// Subscribes to commands from a specific queue with a command handler.
    /// Handler will be called for each command received on the queue.
    /// </summary>
    /// <typeparam name="T">Command type that implements ICommand</typeparam>
    /// <param name="queueName">Queue to subscribe to</param>
    /// <param name="handler">Command handler function</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeToCommandsAsync<T>(
        string queueName, 
        Func<T, MessageContext, Task> handler,
        CancellationToken cancellationToken = default) 
        where T : class, ICommand;

    /// <summary>
    /// Schedules a message to be delivered at a specific time.
    /// Useful for delayed processing and business workflow scenarios.
    /// </summary>
    /// <typeparam name="T">Message type that implements IMessage</typeparam>
    /// <param name="message">Message to schedule</param>
    /// <param name="topicName">Target topic name</param>
    /// <param name="scheduledTime">When to deliver the message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ScheduleMessageAsync<T>(
        T message, 
        string topicName, 
        DateTimeOffset scheduledTime,
        CancellationToken cancellationToken = default) 
        where T : class, IMessage;
}

/// <summary>
/// Base interface for all messages in the system.
/// Provides common properties for correlation, tracking, and metadata.
/// </summary>
public interface IMessage
{
    /// <summary>Unique message identifier</summary>
    string MessageId { get; set; }

    /// <summary>Correlation identifier for request tracking</summary>
    string CorrelationId { get; set; }

    /// <summary>When the message was created</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>Who or what created the message</summary>
    string CreatedBy { get; set; }

    /// <summary>Message version for schema evolution</summary>
    string Version { get; set; }

    /// <summary>Additional metadata for the message</summary>
    Dictionary<string, object> Metadata { get; set; }
}

/// <summary>
/// Interface for domain events that represent something that happened.
/// Events are immutable and describe past occurrences.
/// </summary>
public interface IEvent : IMessage
{
    /// <summary>When the event occurred</summary>
    DateTime OccurredAt { get; set; }

    /// <summary>The aggregate or entity that raised the event</summary>
    string AggregateId { get; set; }

    /// <summary>Type of the aggregate that raised the event</summary>
    string AggregateType { get; set; }

    /// <summary>Sequence number for event ordering</summary>
    long SequenceNumber { get; set; }
}

/// <summary>
/// Interface for commands that represent an intent to do something.
/// Commands are mutable until processed and may be rejected.
/// </summary>
public interface ICommand : IMessage
{
    /// <summary>Expected execution time for the command</summary>
    DateTime? ExecuteAt { get; set; }

    /// <summary>Command timeout for processing</summary>
    TimeSpan? Timeout { get; set; }

    /// <summary>Priority level for command processing</summary>
    CommandPriority Priority { get; set; }

    /// <summary>Whether the command requires acknowledgment</summary>
    bool RequiresAcknowledgment { get; set; }
}

/// <summary>
/// Interface for command responses.
/// Provides result information for processed commands.
/// </summary>
public interface ICommandResponse : IMessage
{
    /// <summary>Original command identifier</summary>
    string CommandId { get; set; }

    /// <summary>Whether the command was processed successfully</summary>
    bool IsSuccess { get; set; }

    /// <summary>Error message if processing failed</summary>
    string? ErrorMessage { get; set; }

    /// <summary>Detailed error information</summary>
    Dictionary<string, object>? ErrorDetails { get; set; }

    /// <summary>When the command was processed</summary>
    DateTime ProcessedAt { get; set; }

    /// <summary>How long the command took to process</summary>
    TimeSpan ProcessingDuration { get; set; }
}

/// <summary>
/// Message context provides additional information about message processing.
/// Contains delivery information, retry counts, and processing metadata.
/// </summary>
public record MessageContext
{
    /// <summary>How many times this message has been delivered</summary>
    public int DeliveryCount { get; init; }

    /// <summary>When the message was enqueued</summary>
    public DateTime EnqueuedAt { get; init; }

    /// <summary>Message properties from the service bus</summary>
    public Dictionary<string, object> Properties { get; init; } = new();

    /// <summary>Session identifier if using sessions</summary>
    public string? SessionId { get; init; }

    /// <summary>Message lock token for completing/abandoning</summary>
    public string? LockToken { get; init; }

    /// <summary>Time until the message lock expires</summary>
    public DateTime? LockedUntil { get; init; }
}

/// <summary>
/// Command priority levels for processing order.
/// </summary>
public enum CommandPriority
{
    /// <summary>Low priority, process when resources are available</summary>
    Low = 0,
    /// <summary>Normal priority, default processing</summary>
    Normal = 1,
    /// <summary>High priority, process before normal commands</summary>
    High = 2,
    /// <summary>Critical priority, process immediately</summary>
    Critical = 3
}