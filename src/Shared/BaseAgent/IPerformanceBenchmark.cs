namespace Enterprise.Documentation.Shared.BaseAgent;

/// <summary>
/// Interface for agent performance benchmarking.
/// Provides standardized performance testing capabilities for all agents.
/// </summary>
public interface IPerformanceBenchmark<TInput, TOutput>
{
    /// <summary>
    /// Executes performance benchmark test for the agent
    /// </summary>
    /// <returns>Benchmark execution task</returns>
    Task<TOutput> BenchmarkExecuteAsync();
    
    /// <summary>
    /// Sets up benchmark test data
    /// </summary>
    /// <param name="input">Test input data</param>
    void SetupBenchmark(TInput input);
    
    /// <summary>
    /// Gets performance metrics from the last benchmark run
    /// </summary>
    /// <returns>Performance metrics</returns>
    PerformanceMetrics GetMetrics();
}

/// <summary>
/// Performance metrics for agent execution
/// </summary>
public record PerformanceMetrics
{
    public TimeSpan ExecutionTime { get; init; }
    public long MemoryAllocated { get; init; }
    public int Operations { get; init; }
    public double OperationsPerSecond => Operations / ExecutionTime.TotalSeconds;
    public string AgentName { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}