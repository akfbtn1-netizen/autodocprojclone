namespace Enterprise.Documentation.Core.Domain.Entities.Lineage;

/// <summary>
/// Represents a lineage scan operation with its state and progress.
/// Used for saga orchestration and tracking scan history.
/// </summary>
public class LineageScan
{
    public int Id { get; private set; }
    public Guid ScanId { get; private set; }
    public ScanType ScanType { get; private set; }
    public ScanStatus Status { get; private set; }
    public string? SchemaFilter { get; private set; }
    public string? ObjectFilter { get; private set; }
    public int TotalObjects { get; private set; }
    public int ProcessedObjects { get; private set; }
    public string? CurrentObject { get; private set; }
    public int NodesCreated { get; private set; }
    public int EdgesCreated { get; private set; }
    public int PiiColumnsFound { get; private set; }
    public int DynamicSqlCount { get; private set; }
    public int ErrorCount { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string StartedBy { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public string? SagaState { get; private set; }
    public Guid? CorrelationId { get; private set; }
    public Guid? ParentScanId { get; private set; }

    private LineageScan() { } // EF Core

    public static LineageScan Create(
        ScanType scanType,
        string startedBy,
        string? schemaFilter = null,
        string? objectFilter = null,
        Guid? correlationId = null,
        Guid? parentScanId = null)
    {
        return new LineageScan
        {
            ScanId = Guid.NewGuid(),
            ScanType = scanType,
            Status = ScanStatus.Pending,
            SchemaFilter = schemaFilter,
            ObjectFilter = objectFilter,
            StartedBy = startedBy,
            StartedAt = DateTime.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid(),
            ParentScanId = parentScanId
        };
    }

    public void SetTotalObjects(int count)
    {
        TotalObjects = count;
    }

    public void StartParsing()
    {
        Status = ScanStatus.Parsing;
    }

    public void StartBuilding()
    {
        Status = ScanStatus.Building;
    }

    public void StartIndexing()
    {
        Status = ScanStatus.Indexing;
    }

    public void UpdateProgress(string currentObject, int processed)
    {
        CurrentObject = currentObject;
        ProcessedObjects = processed;
    }

    public void IncrementNodesCreated(int count = 1)
    {
        NodesCreated += count;
    }

    public void IncrementEdgesCreated(int count = 1)
    {
        EdgesCreated += count;
    }

    public void IncrementPiiColumnsFound(int count = 1)
    {
        PiiColumnsFound += count;
    }

    public void IncrementDynamicSqlCount(int count = 1)
    {
        DynamicSqlCount += count;
    }

    public void IncrementErrorCount()
    {
        ErrorCount++;
    }

    public void Complete()
    {
        Status = ScanStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        CurrentObject = null;
    }

    public void Fail(string errorMessage)
    {
        Status = ScanStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = ScanStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void UpdateSagaState(string state)
    {
        SagaState = state;
    }

    public decimal ProgressPercent => TotalObjects > 0
        ? Math.Round((decimal)ProcessedObjects / TotalObjects * 100, 2)
        : 0;

    public TimeSpan? Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : DateTime.UtcNow - StartedAt;

    public bool IsRunning => Status is ScanStatus.Pending or ScanStatus.Parsing or ScanStatus.Building or ScanStatus.Indexing;
    public bool IsCompleted => Status == ScanStatus.Completed;
    public bool IsFailed => Status == ScanStatus.Failed;
    public bool IsCancelled => Status == ScanStatus.Cancelled;
}

/// <summary>
/// Types of lineage scans
/// </summary>
public enum ScanType
{
    Full,        // Scan all objects
    Incremental, // Scan only changed objects
    Schema,      // Scan specific schema
    Object       // Scan specific object
}

/// <summary>
/// Scan status for saga state machine
/// </summary>
public enum ScanStatus
{
    Pending,   // Scan queued
    Parsing,   // Parsing SQL definitions
    Building,  // Building lineage graph
    Indexing,  // Updating GraphRAG indexes
    Completed, // Successfully completed
    Failed,    // Failed with error
    Cancelled  // Cancelled by user
}
