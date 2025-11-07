namespace Enterprise.Documentation.Shared.BaseAgent;

/// <summary>
/// Base interface for all agents in the system
/// </summary>
/// <typeparam name="TInput">The input type this agent processes</typeparam>
/// <typeparam name="TOutput">The output type this agent produces</typeparam>
public interface IBaseAgent<TInput, TOutput>
{
    /// <summary>
    /// Unique identifier for this agent instance
    /// </summary>
    string AgentId { get; }

    /// <summary>
    /// Execute the agent's primary task
    /// </summary>
    /// <param name="input">The input data to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed output</returns>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check for the agent
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the agent is healthy</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Context interface for agent execution
/// </summary>
public interface IAgentContext
{
    /// <summary>
    /// Unique identifier for the execution context
    /// </summary>
    string ContextId { get; }

    /// <summary>
    /// Additional properties for the context
    /// </summary>
    Dictionary<string, object> Properties { get; }
}