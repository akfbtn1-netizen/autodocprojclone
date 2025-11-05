using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Shared.Contracts
{
    /// <summary>
    /// Provides context and services to agents during execution.
    /// </summary>
    public interface IAgentContext
    {
        /// <summary>
        /// Logger for structured logging.
        /// </summary>
        ILogger Logger { get; }
        
        /// <summary>
        /// Service provider for dependency resolution.
        /// </summary>
        IServiceProvider Services { get; }
        
        /// <summary>
        /// Correlation ID for tracing.
        /// </summary>
        string CorrelationId { get; }
        
        /// <summary>
        /// User ID who initiated the operation (if applicable).
        /// </summary>
        string? UserId { get; }
        
        /// <summary>
        /// Custom properties bag for agent-specific data.
        /// </summary>
        IDictionary<string, object> Properties { get; }
    }
}