namespace Enterprise.Documentation.Shared.BaseAgent;

/// <summary>
/// Configuration options for agents.
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// Gets or sets the Service Bus connection string.
    /// </summary>
    public string? ServiceBusConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the SQL Server connection string.
    /// </summary>
    public string? SqlServerConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the circuit breaker failure threshold.
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the circuit breaker duration in seconds.
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum concurrent calls for Service Bus processor.
    /// </summary>
    public int MaxConcurrentCalls { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the agent is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the poll interval in minutes (for polling agents).
    /// </summary>
    public int PollIntervalMinutes { get; set; } = 5;
}