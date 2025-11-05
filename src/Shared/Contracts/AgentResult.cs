using System;
using System.Collections.Generic;

namespace Shared.Contracts
{
    /// <summary>
    /// Standard result type returned by all agents.
    /// </summary>
    public class AgentResult<TOutput>
    {
        /// <summary>
        /// Indicates if the operation was successful.
        /// </summary>
        public bool Success { get; init; }
        
        /// <summary>
        /// The output data (null if failed).
        /// </summary>
        public TOutput? Data { get; init; }
        
        /// <summary>
        /// Error message if operation failed.
        /// </summary>
        public string? ErrorMessage { get; init; }
        
        /// <summary>
        /// Exception details if operation failed.
        /// </summary>
        public Exception? Exception { get; init; }
        
        /// <summary>
        /// Correlation ID for tracing across services.
        /// </summary>
        public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Duration of the operation.
        /// </summary>
        public TimeSpan Duration { get; init; }
        
        /// <summary>
        /// Additional metadata about the operation.
        /// </summary>
        public Dictionary<string, object> Metadata { get; init; } = new();
        
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static AgentResult<TOutput> Ok(TOutput data, TimeSpan duration)
        {
            return new AgentResult<TOutput>
            {
                Success = true,
                Data = data,
                Duration = duration
            };
        }
        
        /// <summary>
        /// Creates a failed result.
        /// </summary>
        public static AgentResult<TOutput> Fail(string errorMessage, Exception? exception = null)
        {
            return new AgentResult<TOutput>
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }
}