
namespace Enterprise.Documentation.Shared.Contracts.Interfaces;

/// <summary>
/// Base contract interface that all Enterprise Documentation Platform agents MUST implement.
/// Provides standardized agent lifecycle, health monitoring, and governance integration.
/// </summary>
public interface IAgentContract
{
    /// <summary>
    /// Unique identifier for this agent instance.
    /// Used for distributed tracing and governance audit trails.
    /// </summary>
    string AgentId { get; }
    
    /// <summary>
    /// Human-readable name of the agent (e.g., "Document Generator", "Schema Validator").
    /// Used for logging, monitoring, and administrative interfaces.
    /// </summary>
    string AgentName { get; }
    
    /// <summary>
    /// Current version of the agent implementation.
    /// Used for compatibility checking and deployment tracking.
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Current operational status of the agent.
    /// Used by health monitoring and load balancing systems.
    /// </summary>
    AgentStatus Status { get; }
    
    /// <summary>
    /// Performs agent health check including dependencies and resources.
    /// MUST complete within 30 seconds or will be considered unhealthy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token with 30-second timeout</param>
    /// <returns>Detailed health check result</returns>
    Task<AgentHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Initializes the agent with configuration and establishes dependencies.
    /// Called once during agent startup before processing begins.
    /// </summary>
    /// <param name="configuration">Agent-specific configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async initialization</returns>
    Task InitializeAsync(IAgentConfiguration configuration, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gracefully shuts down the agent, completing current work and releasing resources.
    /// MUST complete within 30 seconds or will be forcibly terminated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token with 30-second timeout</param>
    /// <returns>Task representing the async shutdown</returns>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Enumeration of possible agent operational states.
/// Used for health monitoring and administrative decisions.
/// </summary>
public enum AgentStatus
{
    /// <summary>Agent is initializing and not ready to process requests</summary>
    Initializing,
    
    /// <summary>Agent is healthy and processing requests normally</summary>
    Healthy,
    
    /// <summary>Agent is experiencing issues but still processing requests</summary>
    Degraded,
    
    /// <summary>Agent is unhealthy and cannot process requests</summary>
    Unhealthy,
    
    /// <summary>Agent is shutting down gracefully</summary>
    Shutting_Down,
    
    /// <summary>Agent has been stopped</summary>
    Stopped
}

/// <summary>
/// Result of an agent health check operation.
/// Provides detailed status information for monitoring and diagnostics.
/// </summary>
public class AgentHealthResult
{
    /// <summary>Overall health status of the agent</summary>
    public AgentStatus Status { get; set; }
    
    /// <summary>Human-readable description of the health status</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Detailed health check results for individual dependencies</summary>
    public Dictionary<string, object> Details { get; set; } = new();
    
    /// <summary>Timestamp when the health check was performed</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>Duration of the health check operation</summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>Any warnings or issues that don't affect overall health</summary>
    public List<string> Warnings { get; set; } = new();
}