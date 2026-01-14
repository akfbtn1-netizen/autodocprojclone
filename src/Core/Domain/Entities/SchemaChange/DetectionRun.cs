// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Detection Run Entity
// Tracks detection job execution state (Saga pattern)
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Wire to MassTransit state machine for distributed saga coordination

namespace Enterprise.Documentation.Core.Domain.Entities.SchemaChange;

/// <summary>
/// Tracks a schema detection run through its lifecycle states.
/// Implements the saga pattern for long-running detection operations.
/// </summary>
public class DetectionRun
{
    public Guid RunId { get; private set; }

    // Configuration
    public string RunType { get; private set; } = string.Empty;  // SCHEDULED, MANUAL, DDL_TRIGGER, STARTUP
    public string ScanScope { get; private set; } = string.Empty;  // FULL, SCHEMA, OBJECT
    public string? SchemaFilter { get; private set; }
    public string? ObjectFilter { get; private set; }

    // Saga state
    public DetectionRunState CurrentState { get; private set; }

    // Progress
    public int TotalObjects { get; private set; }
    public int ProcessedObjects { get; private set; }
    public int ChangesDetected { get; private set; }
    public int HighRiskChanges { get; private set; }

    // Timing
    public DateTime? StartedAt { get; private set; }
    public DateTime? SnapshotCompletedAt { get; private set; }
    public DateTime? ComparisonCompletedAt { get; private set; }
    public DateTime? AnalysisCompletedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long? DurationMs { get; private set; }

    // Error handling
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }

    // Context
    public string TriggeredBy { get; private set; } = string.Empty;
    public Guid? SnapshotId { get; private set; }
    public string? ResultSummaryJson { get; private set; }

    private DetectionRun() { } // EF Core / Dapper

    public static DetectionRun StartManual(string triggeredBy, string scanScope, string? schemaFilter = null)
    {
        return new DetectionRun
        {
            RunId = Guid.NewGuid(),
            RunType = "MANUAL",
            ScanScope = scanScope,
            SchemaFilter = schemaFilter,
            CurrentState = DetectionRunState.Pending,
            TriggeredBy = triggeredBy
        };
    }

    public static DetectionRun StartScheduled(string triggeredBy = "SCHEDULER")
    {
        return new DetectionRun
        {
            RunId = Guid.NewGuid(),
            RunType = "SCHEDULED",
            ScanScope = "FULL",
            CurrentState = DetectionRunState.Pending,
            TriggeredBy = triggeredBy
        };
    }

    public static DetectionRun StartFromTrigger(string schemaName, string objectName)
    {
        return new DetectionRun
        {
            RunId = Guid.NewGuid(),
            RunType = "DDL_TRIGGER",
            ScanScope = "OBJECT",
            SchemaFilter = schemaName,
            ObjectFilter = objectName,
            CurrentState = DetectionRunState.Pending,
            TriggeredBy = "DDL_TRIGGER"
        };
    }

    // State transitions
    public void Begin(int totalObjects)
    {
        if (CurrentState != DetectionRunState.Pending)
            throw new InvalidOperationException($"Cannot begin from state {CurrentState}");

        CurrentState = DetectionRunState.Snapshotting;
        StartedAt = DateTime.UtcNow;
        TotalObjects = totalObjects;
    }

    public void CompleteSnapshot(Guid snapshotId)
    {
        if (CurrentState != DetectionRunState.Snapshotting)
            throw new InvalidOperationException($"Cannot complete snapshot from state {CurrentState}");

        SnapshotId = snapshotId;
        SnapshotCompletedAt = DateTime.UtcNow;
        CurrentState = DetectionRunState.Comparing;
    }

    public void CompleteComparison(int changesFound)
    {
        if (CurrentState != DetectionRunState.Comparing)
            throw new InvalidOperationException($"Cannot complete comparison from state {CurrentState}");

        ChangesDetected = changesFound;
        ComparisonCompletedAt = DateTime.UtcNow;
        CurrentState = DetectionRunState.Analyzing;
    }

    public void CompleteAnalysis(int highRiskCount)
    {
        if (CurrentState != DetectionRunState.Analyzing)
            throw new InvalidOperationException($"Cannot complete analysis from state {CurrentState}");

        HighRiskChanges = highRiskCount;
        AnalysisCompletedAt = DateTime.UtcNow;
        CurrentState = DetectionRunState.Notifying;
    }

    public void Complete(string resultSummary)
    {
        if (CurrentState != DetectionRunState.Notifying)
            throw new InvalidOperationException($"Cannot complete from state {CurrentState}");

        ResultSummaryJson = resultSummary;
        CompletedAt = DateTime.UtcNow;
        DurationMs = (long)(CompletedAt.Value - StartedAt!.Value).TotalMilliseconds;
        CurrentState = DetectionRunState.Complete;
    }

    public void Fail(string errorMessage)
    {
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
        CurrentState = DetectionRunState.Failed;
    }

    public void Cancel()
    {
        CompletedAt = DateTime.UtcNow;
        if (StartedAt.HasValue)
            DurationMs = (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds;
        CurrentState = DetectionRunState.Cancelled;
    }

    public void UpdateProgress(int processed)
    {
        ProcessedObjects = processed;
    }

    public void IncrementRetry()
    {
        RetryCount++;
    }

    public double ProgressPercent =>
        TotalObjects > 0 ? (double)ProcessedObjects / TotalObjects * 100 : 0;
}
