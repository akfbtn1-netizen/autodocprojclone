using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Contracts
{
    /// <summary>
    /// Core contract that all agents must implement.
    /// Defines the standard execution pattern for autonomous agents.
    /// </summary>
    /// <typeparam name="TInput">The input type the agent accepts</typeparam>
    /// <typeparam name="TOutput">The output type the agent produces</typeparam>
    public interface IBaseAgent<in TInput, TOutput>
    {
        /// <summary>
        /// Unique identifier for this agent instance.
        /// </summary>
        string AgentId { get; }
        
        /// <summary>
        /// Human-readable name of the agent (e.g., "DocumentGenerator").
        /// </summary>
        string AgentName { get; }
        
        /// <summary>
        /// Version of the agent implementation.
        /// </summary>
        string Version { get; }
        
        /// <summary>
        /// Executes the agent's primary function with the given input.
        /// </summary>
        /// <param name="input">The input data to process</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>Result containing output or error information</returns>
        Task<AgentResult<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Validates input before execution.
        /// </summary>
        /// <param name="input">The input to validate</param>
        /// <returns>Validation result</returns>
        Task<ValidationResult> ValidateInputAsync(TInput input);
        
        /// <summary>
        /// Health check to verify agent is operational.
        /// </summary>
        /// <returns>True if healthy, false otherwise</returns>
        Task<bool> HealthCheckAsync();
    }
}