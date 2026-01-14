using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.Entities.Lineage;

namespace Enterprise.Documentation.Core.Domain.Events;

/// <summary>
/// Domain event raised when a lineage scan is started
/// </summary>
public record LineageScanStartedEvent(
    Guid ScanId,
    ScanType ScanType,
    string? SchemaFilter,
    string StartedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(LineageScanStartedEvent);
}

/// <summary>
/// Domain event raised when a lineage scan progresses
/// </summary>
public record LineageScanProgressEvent(
    Guid ScanId,
    string CurrentObject,
    int ProcessedObjects,
    int TotalObjects,
    decimal ProgressPercent) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(LineageScanProgressEvent);
}

/// <summary>
/// Domain event raised when a lineage scan completes
/// </summary>
public record LineageScanCompletedEvent(
    Guid ScanId,
    int NodesCreated,
    int EdgesCreated,
    int PiiColumnsFound,
    int DynamicSqlCount,
    TimeSpan Duration) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(LineageScanCompletedEvent);
}

/// <summary>
/// Domain event raised when a lineage scan fails
/// </summary>
public record LineageScanFailedEvent(
    Guid ScanId,
    string ErrorMessage,
    int ProcessedObjects) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(LineageScanFailedEvent);
}

/// <summary>
/// Domain event raised when a lineage node is created
/// </summary>
public record LineageNodeCreatedEvent(
    string NodeId,
    string NodeType,
    string SchemaName,
    string ObjectName,
    string? ColumnName,
    bool IsPii) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(LineageNodeCreatedEvent);
}

/// <summary>
/// Domain event raised when a lineage edge is created
/// </summary>
public record LineageEdgeCreatedEvent(
    Guid EdgeId,
    string SourceNodeId,
    string TargetNodeId,
    string EdgeType,
    bool IsPiiFlow) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(LineageEdgeCreatedEvent);
}

/// <summary>
/// Domain event raised when PII column is detected
/// </summary>
public record PiiColumnDetectedEvent(
    string SchemaName,
    string TableName,
    string ColumnName,
    string PiiType,
    string DetectedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(PiiColumnDetectedEvent);
}

/// <summary>
/// Domain event raised when dynamic SQL is detected in a procedure
/// </summary>
public record DynamicSqlDetectedEvent(
    string SchemaName,
    string ProcedureName,
    string DynamicSqlType,
    string RiskLevel,
    int? LineNumber) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(DynamicSqlDetectedEvent);
}

/// <summary>
/// Domain event raised when column risk score is calculated
/// </summary>
public record ColumnRiskScoreCalculatedEvent(
    string SchemaName,
    string TableName,
    string ColumnName,
    int RiskScore,
    string ImpactLevel) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(ColumnRiskScoreCalculatedEvent);
}

/// <summary>
/// Domain event raised when lineage graph is synced to GraphRAG
/// </summary>
public record LineageGraphSyncedEvent(
    Guid ScanId,
    int NodesSynced,
    int EdgesSynced) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(LineageGraphSyncedEvent);
}

/// <summary>
/// Domain event raised when OpenLineage event is published
/// </summary>
public record OpenLineageEventPublishedEvent(
    Guid EventId,
    string JobNamespace,
    string JobName,
    string EventType) : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    string IDomainEvent.EventType { get; } = nameof(OpenLineageEventPublishedEvent);
    Guid IDomainEvent.EventId => Id;
}
