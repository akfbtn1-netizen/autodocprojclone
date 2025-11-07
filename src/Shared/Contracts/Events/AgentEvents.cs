
namespace Enterprise.Documentation.Shared.Contracts.Events;

/// <summary>
/// Event published when an agent starts up successfully.
/// Used for system monitoring and agent discovery.
/// </summary>
public class AgentStartedEvent : BaseEvent
{
    public AgentStartedEvent(string sourceAgent, string correlationId)
        : base(sourceAgent, correlationId)
    {
    }

    /// <summary>Version of the agent that started</summary>
    public string? AgentVersion { get; init; }

    /// <summary>Environment the agent is running in</summary>
    public string? Environment { get; init; }

    /// <summary>Host information where the agent is running</summary>
    public string? HostInfo { get; init; }

    /// <summary>Configuration snapshot (non-sensitive data only)</summary>
    public Dictionary<string, object>? ConfigurationSnapshot { get; init; }

    /// <summary>Startup duration in milliseconds</summary>
    public long? StartupDurationMs { get; init; }
}

/// <summary>
/// Event published when an agent shuts down gracefully.
/// Used for system monitoring and resource cleanup.
/// </summary>
public class AgentStoppedEvent : BaseEvent
{
    public AgentStoppedEvent(string sourceAgent, string reason, string correlationId)
        : base(sourceAgent, correlationId)
    {
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>Reason for the agent shutdown</summary>
    public string Reason { get; }

    /// <summary>Whether the shutdown was planned or unexpected</summary>
    public bool IsPlannedShutdown { get; init; } = true;

    /// <summary>Shutdown duration in milliseconds</summary>
    public long? ShutdownDurationMs { get; init; }

    /// <summary>Final status of the agent before shutdown</summary>
    public string? FinalStatus { get; init; }

    /// <summary>Any cleanup operations performed during shutdown</summary>
    public List<string>? CleanupOperations { get; init; }
}

/// <summary>
/// Event published when an agent encounters a health issue.
/// Triggers monitoring alerts and potential remediation actions.
/// </summary>
public class AgentHealthChangedEvent : BaseEvent
{
    public AgentHealthChangedEvent(string sourceAgent, string previousStatus, string currentStatus, string correlationId)
        : base(sourceAgent, correlationId)
    {
        PreviousStatus = previousStatus ?? throw new ArgumentNullException(nameof(previousStatus));
        CurrentStatus = currentStatus ?? throw new ArgumentNullException(nameof(currentStatus));
    }

    /// <summary>Previous health status of the agent</summary>
    public string PreviousStatus { get; }

    /// <summary>Current health status of the agent</summary>
    public string CurrentStatus { get; }

    /// <summary>Description of the health change</summary>
    public string? Description { get; init; }

    /// <summary>Health check details</summary>
    public Dictionary<string, object>? HealthDetails { get; init; }

    /// <summary>Whether this represents a degradation or improvement</summary>
    public bool IsImprovement { get; init; }

    /// <summary>Recommended actions to address health issues</summary>
    public List<string>? RecommendedActions { get; init; }
}

/// <summary>
/// Event published when an agent processes a work item.
/// Used for performance monitoring and workload analysis.
/// </summary>
public class AgentWorkCompletedEvent : BaseEvent
{
    public AgentWorkCompletedEvent(string sourceAgent, string workItemId, bool isSuccessful, string correlationId)
        : base(sourceAgent, correlationId)
    {
        WorkItemId = workItemId ?? throw new ArgumentNullException(nameof(workItemId));
        IsSuccessful = isSuccessful;
    }

    /// <summary>Unique identifier of the work item that was processed</summary>
    public string WorkItemId { get; }

    /// <summary>Whether the work was completed successfully</summary>
    public bool IsSuccessful { get; }

    /// <summary>Type of work that was performed</summary>
    public string? WorkType { get; init; }

    /// <summary>Duration of the work processing in milliseconds</summary>
    public long? ProcessingDurationMs { get; init; }

    /// <summary>Error message if work failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Number of retry attempts made</summary>
    public int? RetryCount { get; init; }

    /// <summary>Performance metrics for the work processing</summary>
    public Dictionary<string, object>? PerformanceMetrics { get; init; }

    /// <summary>Resource utilization during work processing</summary>
    public Dictionary<string, object>? ResourceUtilization { get; init; }
}