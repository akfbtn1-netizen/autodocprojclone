using System;

namespace Shared.Contracts.Events
{
    /// <summary>
    /// Base class for all domain events.
    /// </summary>
    public abstract record DomainEvent
    {
        /// <summary>
        /// Unique identifier for this event.
        /// </summary>
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// When the event occurred.
        /// </summary>
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
        
        /// <summary>
        /// Correlation ID for tracing.
        /// </summary>
        public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Agent or service that raised the event.
        /// </summary>
        public string Source { get; init; } = string.Empty;
    }
    
    // ============================================================================
    // Common Events
    // ============================================================================
    
    /// <summary>
    /// Published when database schema changes are detected.
    /// </summary>
    public record SchemaChangedEvent : DomainEvent
    {
        public string DatabaseName { get; init; } = string.Empty;
        public string SchemaName { get; init; } = string.Empty;
        public string ObjectName { get; init; } = string.Empty;
        public string ObjectType { get; init; } = string.Empty; // Table, View, Procedure
        public string ChangeType { get; init; } = string.Empty; // Created, Modified, Deleted
        public string ChangedBy { get; init; } = string.Empty;
    }
    
    /// <summary>
    /// Published when a document is successfully generated.
    /// </summary>
    public record DocumentGeneratedEvent : DomainEvent
    {
        public string DocumentId { get; init; } = string.Empty;
        public string DocumentName { get; init; } = string.Empty;
        public string TemplateId { get; init; } = string.Empty;
        public string GeneratedBy { get; init; } = string.Empty;
        public int PageCount { get; init; }
        public long SizeInBytes { get; init; }
    }
    
    /// <summary>
    /// Published when a document requires approval.
    /// </summary>
    public record DocumentApprovalRequiredEvent : DomainEvent
    {
        public string DocumentId { get; init; } = string.Empty;
        public string DocumentName { get; init; } = string.Empty;
        public string SubmittedBy { get; init; } = string.Empty;
        public string AssignedTo { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
    }
    
    /// <summary>
    /// Published when a document is approved or rejected.
    /// </summary>
    public record DocumentApprovedEvent : DomainEvent
    {
        public string DocumentId { get; init; } = string.Empty;
        public bool Approved { get; init; }
        public string ApprovedBy { get; init; } = string.Empty;
        public string Comments { get; init; } = string.Empty;
    }
    
    /// <summary>
    /// Published when an agent fails to execute.
    /// </summary>
    public record AgentExecutionFailedEvent : DomainEvent
    {
        public string AgentId { get; init; } = string.Empty;
        public string AgentName { get; init; } = string.Empty;
        public string ErrorMessage { get; init; } = string.Empty;
        public string? StackTrace { get; init; }
    }
}