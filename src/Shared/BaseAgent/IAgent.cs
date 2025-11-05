using System.Diagnostics;

namespace Enterprise.Documentation.Shared.BaseAgent;

/// <summary>
/// Defines the contract for all agents in the platform.
/// </summary>
public interface IAgent : IDisposable
{
    /// <summary>
    /// Main execution method for the agent.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task ExecuteAsync(CancellationToken ct);

    /// <summary>
    /// Health check to verify agent is operational.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Health check result</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct);

    /// <summary>
    /// Gets the agent's name for identification.
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Gets the agent's version.
    /// </summary>
    string Version { get; }
}

/// <summary>
/// Represents the result of a health check.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Gets or sets whether the agent is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the health check description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception if health check failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets additional health data.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Creates a healthy result.
    /// </summary>
    public static HealthCheckResult Healthy(string description) =>
        new() { IsHealthy = true, Description = description };

    /// <summary>
    /// Creates an unhealthy result.
    /// </summary>
    public static HealthCheckResult Unhealthy(string description, Exception? exception = null) =>
        new() { IsHealthy = false, Description = description, Exception = exception };

    /// <summary>
    /// Creates an unhealthy result with additional data.
    /// </summary>
    public static HealthCheckResult Unhealthy(string description, Dictionary<string, object> data) =>
        new() { IsHealthy = false, Description = description, Data = data };
}