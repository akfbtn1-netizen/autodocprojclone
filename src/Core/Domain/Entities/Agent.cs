
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Agent entity representing AI agents that can generate and process documentation.
/// Manages agent capabilities, configuration, and operational state.
/// </summary>
public class Agent : BaseEntity<AgentId>
{
    /// <summary>
    /// Agent name (unique identifier).
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Agent display name for UI.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Agent description and purpose.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Agent type/category.
    /// </summary>
    public AgentType Type { get; private set; }

    /// <summary>
    /// Agent's current operational status.
    /// </summary>
    public AgentStatus Status { get; private set; }

    /// <summary>
    /// Agent capabilities and what it can do.
    /// </summary>
    public List<AgentCapability> Capabilities { get; private set; }

    /// <summary>
    /// Agent configuration settings.
    /// </summary>
    public AgentConfiguration Configuration { get; private set; }

    /// <summary>
    /// Agent version information.
    /// </summary>
    public string AgentVersion { get; private set; }

    /// <summary>
    /// Maximum security level the agent can handle.
    /// </summary>
    public SecurityClearanceLevel MaxSecurityClearance { get; private set; }

    /// <summary>
    /// Whether the agent is currently available for requests.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Current number of active requests being processed.
    /// </summary>
    public int ActiveRequestCount { get; private set; }

    /// <summary>
    /// Maximum number of concurrent requests the agent can handle.
    /// </summary>
    public int MaxConcurrentRequests { get; private set; }

    /// <summary>
    /// Total number of requests processed by this agent.
    /// </summary>
    public long TotalRequestsProcessed { get; private set; }

    /// <summary>
    /// Number of successful requests.
    /// </summary>
    public long SuccessfulRequests { get; private set; }

    /// <summary>
    /// Number of failed requests.
    /// </summary>
    public long FailedRequests { get; private set; }

    /// <summary>
    /// Average processing time in milliseconds.
    /// </summary>
    public double AverageProcessingTimeMs { get; private set; }

    /// <summary>
    /// Last time the agent processed a request.
    /// </summary>
    public DateTime? LastRequestAt { get; private set; }

    /// <summary>
    /// Last time the agent reported its health status.
    /// </summary>
    public DateTime? LastHealthCheckAt { get; private set; }

    // Private constructor for EF Core
    private Agent() : base()
    {
        Name = string.Empty;
        DisplayName = string.Empty;
        Type = AgentType.General;
        Status = AgentStatus.Offline;
        Capabilities = new List<AgentCapability>();
        Configuration = AgentConfiguration.Default();
        AgentVersion = "1.0.0";
        MaxSecurityClearance = SecurityClearanceLevel.Public;
        IsAvailable = false;
        MaxConcurrentRequests = 5;
        ActiveRequestCount = 0;
        TotalRequestsProcessed = 0;
        SuccessfulRequests = 0;
        FailedRequests = 0;
        AverageProcessingTimeMs = 0;
    }

    /// <summary>
    /// Creates a new agent.
    /// </summary>
    public Agent(
        AgentId id,
        string name,
        string displayName,
        AgentType type,
        List<AgentCapability> capabilities,
        SecurityClearanceLevel maxSecurityClearance,
        UserId createdBy,
        string? description = null,
        AgentConfiguration? configuration = null,
        string version = "1.0.0",
        int maxConcurrentRequests = 5) : base(id, createdBy)
    {
        Name = IsValidAgentName(name) 
            ? name 
            : throw new ArgumentException("Invalid agent name format", nameof(name));
            
        DisplayName = !string.IsNullOrWhiteSpace(displayName) 
            ? displayName 
            : throw new ArgumentException("Display name cannot be empty", nameof(displayName));
            
        Description = description;
        Type = type;
        Status = AgentStatus.Offline;
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        Configuration = configuration ?? AgentConfiguration.Default();
        AgentVersion = version;
        MaxSecurityClearance = maxSecurityClearance;
        IsAvailable = false;
        MaxConcurrentRequests = maxConcurrentRequests > 0 ? maxConcurrentRequests : 5;
        ActiveRequestCount = 0;
        TotalRequestsProcessed = 0;
        SuccessfulRequests = 0;
        FailedRequests = 0;
        AverageProcessingTimeMs = 0;

        AddDomainEvent(new AgentRegisteredEvent(id, name, type, createdBy));
    }

    /// <summary>
    /// Updates agent information.
    /// </summary>
    public void UpdateInfo(
        string? displayName = null,
        string? description = null,
        List<AgentCapability>? capabilities = null,
        UserId? updatedBy = null)
    {
        if (updatedBy == null)
            throw new ArgumentNullException(nameof(updatedBy));

        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName;

        Description = description;

        if (capabilities != null)
            Capabilities = new List<AgentCapability>(capabilities);

        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new AgentInfoUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates agent configuration.
    /// </summary>
    public void UpdateConfiguration(AgentConfiguration configuration, UserId updatedBy)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new AgentConfigurationUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Brings the agent online and makes it available.
    /// </summary>
    public void BringOnline(UserId activatedBy)
    {
        if (Status == AgentStatus.Online)
            throw new InvalidOperationException("Agent is already online");

        Status = AgentStatus.Online;
        IsAvailable = true;
        LastHealthCheckAt = DateTime.UtcNow;
        
        UpdateModificationTracking(activatedBy);
        AddDomainEvent(new AgentOnlineEvent(Id, activatedBy));
    }

    /// <summary>
    /// Takes the agent offline.
    /// </summary>
    public void TakeOffline(UserId deactivatedBy, string? reason = null)
    {
        if (Status == AgentStatus.Offline)
            throw new InvalidOperationException("Agent is already offline");

        Status = AgentStatus.Offline;
        IsAvailable = false;
        
        UpdateModificationTracking(deactivatedBy);
        AddDomainEvent(new AgentOfflineEvent(Id, deactivatedBy, reason));
    }

    /// <summary>
    /// Puts the agent in maintenance mode.
    /// </summary>
    public void EnterMaintenanceMode(UserId initiatedBy, string? reason = null)
    {
        Status = AgentStatus.Maintenance;
        IsAvailable = false;
        
        UpdateModificationTracking(initiatedBy);
        AddDomainEvent(new AgentMaintenanceModeEvent(Id, initiatedBy, reason));
    }

    /// <summary>
    /// Records that a request has started processing.
    /// </summary>
    public void StartProcessingRequest(Guid requestId)
    {
        if (ActiveRequestCount >= MaxConcurrentRequests)
            throw new InvalidOperationException("Agent has reached maximum concurrent request limit");

        if (!IsAvailable)
            throw new InvalidOperationException("Agent is not available for processing requests");

        ActiveRequestCount++;
        LastRequestAt = DateTime.UtcNow;
        
        AddDomainEvent(new AgentRequestStartedEvent(Id, requestId));
    }

    /// <summary>
    /// Records that a request has completed successfully.
    /// </summary>
    public void CompleteRequest(Guid requestId, TimeSpan processingTime)
    {
        if (ActiveRequestCount <= 0)
            throw new InvalidOperationException("No active requests to complete");

        ActiveRequestCount--;
        TotalRequestsProcessed++;
        SuccessfulRequests++;
        
        // Update average processing time
        UpdateAverageProcessingTime(processingTime.TotalMilliseconds);
        
        AddDomainEvent(new AgentRequestCompletedEvent(Id, requestId, processingTime));
    }

    /// <summary>
    /// Records that a request has failed.
    /// </summary>
    public void FailRequest(Guid requestId, string error, TimeSpan processingTime)
    {
        if (ActiveRequestCount <= 0)
            throw new InvalidOperationException("No active requests to fail");

        ActiveRequestCount--;
        TotalRequestsProcessed++;
        FailedRequests++;
        
        // Update average processing time
        UpdateAverageProcessingTime(processingTime.TotalMilliseconds);
        
        AddDomainEvent(new AgentRequestFailedEvent(Id, requestId, error, processingTime));
    }

    /// <summary>
    /// Records a health check.
    /// </summary>
    public void RecordHealthCheck(AgentHealthStatus healthStatus)
    {
        LastHealthCheckAt = DateTime.UtcNow;
        
        if (healthStatus == AgentHealthStatus.Unhealthy && Status == AgentStatus.Online)
        {
            Status = AgentStatus.Unhealthy;
            IsAvailable = false;
        }
        else if (healthStatus == AgentHealthStatus.Healthy && Status == AgentStatus.Unhealthy)
        {
            Status = AgentStatus.Online;
            IsAvailable = true;
        }
        
        AddDomainEvent(new AgentHealthCheckEvent(Id, healthStatus));
    }

    /// <summary>
    /// Checks if the agent has a specific capability.
    /// </summary>
    public bool HasCapability(AgentCapability capability) => Capabilities.Contains(capability);

    /// <summary>
    /// Checks if the agent can handle the specified security level.
    /// </summary>
    public bool CanHandleSecurityLevel(SecurityClearanceLevel requiredLevel)
    {
        return MaxSecurityClearance >= requiredLevel;
    }

    /// <summary>
    /// Gets the agent's success rate as a percentage.
    /// </summary>
    public double GetSuccessRate()
    {
        if (TotalRequestsProcessed == 0)
            return 0;
            
        return (double)SuccessfulRequests / TotalRequestsProcessed * 100;
    }

    private static bool IsValidAgentName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
            
        // Agent names should be alphanumeric with hyphens/underscores
        return name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }

    private void UpdateAverageProcessingTime(double processingTimeMs)
    {
        if (TotalRequestsProcessed == 1)
        {
            AverageProcessingTimeMs = processingTimeMs;
        }
        else
        {
            // Calculate rolling average
            AverageProcessingTimeMs = ((AverageProcessingTimeMs * (TotalRequestsProcessed - 1)) + processingTimeMs) / TotalRequestsProcessed;
        }
    }
}

/// <summary>
/// Agent types/categories.
/// </summary>
public enum AgentType
{
    General,
    Documentation,
    CodeAnalysis,
    Security,
    Translation,
    Approval,
    Notification
}

/// <summary>
/// Agent operational status.
/// </summary>
public enum AgentStatus
{
    Offline,
    Online,
    Maintenance,
    Unhealthy
}

/// <summary>
/// Agent capabilities.
/// </summary>
public enum AgentCapability
{
    GenerateDocumentation,
    AnalyzeCode,
    ProcessApprovals,
    TranslateContent,
    SecurityScanning,
    SendNotifications,
    ValidateTemplates,
    ProcessFeedback
}

/// <summary>
/// Agent health status.
/// </summary>
public enum AgentHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Agent configuration settings.
/// </summary>
public class AgentConfiguration : BaseValueObject
{
    public Dictionary<string, object> Settings { get; private set; } = new();
    public TimeSpan RequestTimeout { get; private set; }
    public int RetryAttempts { get; private set; }
    public TimeSpan RetryDelay { get; private set; }

    // Parameterless constructor for EF Core
    private AgentConfiguration()
    {
        Settings = new Dictionary<string, object>();
        RequestTimeout = TimeSpan.FromMinutes(5);
        RetryAttempts = 3;
        RetryDelay = TimeSpan.FromSeconds(1);
    }

    public AgentConfiguration(
        Dictionary<string, object>? settings = null,
        TimeSpan? requestTimeout = null,
        int retryAttempts = 3,
        TimeSpan? retryDelay = null)
    {
        Settings = settings ?? new Dictionary<string, object>();
        RequestTimeout = requestTimeout ?? TimeSpan.FromMinutes(5);
        RetryAttempts = retryAttempts >= 0 ? retryAttempts : 3;
        RetryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
    }

    public static AgentConfiguration Default() => new AgentConfiguration(null, null, 3, null);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return string.Join(",", Settings.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        yield return RequestTimeout;
        yield return RetryAttempts;
        yield return RetryDelay;
    }
}

// Domain Events
public record AgentRegisteredEvent(AgentId AgentId, string Name, AgentType Type, UserId RegisteredBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentRegisteredEvent);
}

public record AgentInfoUpdatedEvent(AgentId AgentId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentInfoUpdatedEvent);
}

public record AgentConfigurationUpdatedEvent(AgentId AgentId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentConfigurationUpdatedEvent);
}

public record AgentOnlineEvent(AgentId AgentId, UserId ActivatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentOnlineEvent);
}

public record AgentOfflineEvent(AgentId AgentId, UserId DeactivatedBy, string? Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentOfflineEvent);
}

public record AgentMaintenanceModeEvent(AgentId AgentId, UserId InitiatedBy, string? Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentMaintenanceModeEvent);
}

public record AgentRequestStartedEvent(AgentId AgentId, Guid RequestId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentRequestStartedEvent);
}

public record AgentRequestCompletedEvent(AgentId AgentId, Guid RequestId, TimeSpan ProcessingTime) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentRequestCompletedEvent);
}

public record AgentRequestFailedEvent(AgentId AgentId, Guid RequestId, string Error, TimeSpan ProcessingTime) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentRequestFailedEvent);
}

public record AgentHealthCheckEvent(AgentId AgentId, AgentHealthStatus HealthStatus) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(AgentHealthCheckEvent);
}