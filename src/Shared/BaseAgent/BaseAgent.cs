using Microsoft.Extensions.Logging;
using Shared.Contracts;
using System.Diagnostics;

namespace Enterprise.Documentation.Shared.BaseAgent;

/// <summary>
/// Clean V2 base implementation of IBaseAgent following SOLID principles.
/// Provides enterprise-grade functionality through dependency injection:
/// - Structured logging with correlation tracking
/// - Activity-based observability (OpenTelemetry compatible)
/// - Input validation pipeline using contracts
/// - Health monitoring capabilities
/// - Performance metrics and timing
/// - Graceful error handling and recovery
/// </summary>
/// <typeparam name="TInput">The input type this agent processes</typeparam>
/// <typeparam name="TOutput">The output type this agent produces</typeparam>
public abstract class BaseAgent<TInput, TOutput> : IBaseAgent<TInput, TOutput>, IDisposable
{
    protected readonly IAgentContext Context;
    protected readonly ILogger Logger;
    private readonly ActivitySource _activitySource;

    public abstract string AgentId { get; }
    public abstract string AgentName { get; }
    public abstract string Version { get; }

    /// <summary>
    /// Initializes a new instance of the BaseAgent with dependency injection.
    /// Follows V2 standards with clean separation of concerns.
    /// </summary>
    /// <param name="context">Agent execution context providing services and correlation</param>
    protected BaseAgent(IAgentContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Logger = context.Logger;
        _activitySource = new ActivitySource($"{GetType().Name}-{Version}");
    }

    /// <inheritdoc />
    public async Task<AgentResult<TOutput>> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity($"{AgentName}.Execute");
        activity?.SetTag("agent.name", AgentName);
        activity?.SetTag("agent.version", Version);
        activity?.SetTag("agent.id", AgentId);
        activity?.SetTag("correlation.id", Context.CorrelationId);

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            LogInformation("Starting execution with correlation ID {CorrelationId}", Context.CorrelationId);

            // Input validation using our V2 contracts
            var validationResult = await ValidateInputAsync(input);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                LogWarning("Input validation failed: {ValidationErrors}", errors);
                
                activity?.SetTag("validation.failed", true);
                activity?.SetTag("validation.errors", errors);
                
                return AgentResult<TOutput>.Fail($"Input validation failed: {errors}");
            }

            activity?.SetTag("validation.passed", true);

            // Execute the core agent logic
            var result = await ExecuteInternalAsync(input, cancellationToken);
            
            stopwatch.Stop();
            
            if (result.Success)
            {
                LogInformation("Execution completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                activity?.SetTag("execution.success", true);
            }
            else
            {
                LogWarning("Execution completed with failure in {ElapsedMs}ms: {ErrorMessage}", 
                    stopwatch.ElapsedMilliseconds, result.ErrorMessage ?? "Unknown error");
                activity?.SetTag("execution.success", false);
                activity?.SetTag("execution.error", result.ErrorMessage);
            }
            
            activity?.SetTag("execution.duration_ms", stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            LogWarning("Execution was cancelled after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            activity?.SetTag("execution.cancelled", true);
            activity?.SetTag("execution.duration_ms", stopwatch.ElapsedMilliseconds);
            
            return AgentResult<TOutput>.Fail("Operation was cancelled");
        }
#pragma warning disable CA1031 // Do not catch general exception types - BaseAgent needs to handle any exception gracefully
        catch (Exception ex)
#pragma warning restore CA1031
        {
            stopwatch.Stop();
            LogError(ex, "Execution failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            activity?.SetTag("execution.success", false);
            activity?.SetTag("execution.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("execution.error", ex.Message);
            activity?.SetTag("execution.exception_type", ex.GetType().Name);
            
            return AgentResult<TOutput>.Fail($"Execution failed: {ex.Message}", ex);
        }
    }

    public virtual Task<ValidationResult> ValidateInputAsync(TInput input)
    {
        if (input == null)
        {
            return Task.FromResult(ValidationResult.Fail(new ValidationError("input", "Input cannot be null")));
        }
        return Task.FromResult(ValidationResult.Ok());
    }

    public virtual Task<bool> HealthCheckAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Abstract method that derived agents must implement for their core logic.
    /// This method receives validated input and should focus on business logic.
    /// </summary>
    /// <param name="input">The validated input to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processing result</returns>
    protected abstract Task<AgentResult<TOutput>> ExecuteInternalAsync(TInput input, CancellationToken cancellationToken);

    /// <summary>
    /// Logs structured information with agent context.
    /// Automatically includes agent metadata in log scope.
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="args">Message arguments</param>
    protected void LogInformation(string messageTemplate, params object[] args)
    {
        using var scope = Logger.BeginScope(new Dictionary<string, object>
        {
            ["AgentId"] = AgentId,
            ["AgentName"] = AgentName,
            ["AgentVersion"] = Version,
            ["CorrelationId"] = Context.CorrelationId
        });
        
        Logger.LogInformation(messageTemplate, args);
    }

    /// <summary>
    /// Logs structured warnings with agent context.
    /// Automatically includes agent metadata in log scope.
    /// </summary>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="args">Message arguments</param>
    protected void LogWarning(string messageTemplate, params object[] args)
    {
        using var scope = Logger.BeginScope(new Dictionary<string, object>
        {
            ["AgentId"] = AgentId,
            ["AgentName"] = AgentName,
            ["AgentVersion"] = Version,
            ["CorrelationId"] = Context.CorrelationId
        });
        
        Logger.LogWarning(messageTemplate, args);
    }

    /// <summary>
    /// Logs structured errors with agent context.
    /// Automatically includes agent metadata in log scope.
    /// </summary>
    /// <param name="exception">Exception to log</param>
    /// <param name="messageTemplate">Message template</param>
    /// <param name="args">Message arguments</param>
    protected void LogError(Exception exception, string messageTemplate, params object[] args)
    {
        using var scope = Logger.BeginScope(new Dictionary<string, object>
        {
            ["AgentId"] = AgentId,
            ["AgentName"] = AgentName,
            ["AgentVersion"] = Version,
            ["CorrelationId"] = Context.CorrelationId
        });
        
        Logger.LogError(exception, messageTemplate, args);
    }

    /// <summary>
    /// Creates a child activity for tracking sub-operations.
    /// Follows OpenTelemetry best practices for distributed tracing.
    /// </summary>
    /// <param name="operationName">Name of the operation to track</param>
    /// <returns>Activity that should be disposed when operation completes</returns>
    protected Activity? StartActivity(string operationName)
    {
        var activity = _activitySource.StartActivity($"{AgentName}.{operationName}");
        activity?.SetTag("agent.name", AgentName);
        activity?.SetTag("agent.version", Version);
        activity?.SetTag("correlation.id", Context.CorrelationId);
        return activity;
    }

    /// <summary>
    /// Disposes resources used by the agent.
    /// Follows proper disposal patterns for activity sources.
    /// </summary>
    public virtual void Dispose()
    {
        _activitySource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
