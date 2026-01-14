// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - DTOs
// Request/Response models for API and service layer
// ═══════════════════════════════════════════════════════════════════════════

using Enterprise.Documentation.Core.Domain.Entities.SchemaChange;

namespace Enterprise.Documentation.Core.Application.DTOs.SchemaChange;

#region Response DTOs

/// <summary>
/// Schema change summary for list views.
/// </summary>
public record SchemaChangeDto(
    Guid ChangeId,
    string DatabaseName,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    string ChangeType,
    string? ChangeDescription,
    DateTime DetectedAt,
    string DetectedBy,
    string? LoginName,
    int ImpactScore,
    string RiskLevel,
    string ProcessingStatus,
    int AffectedProcedures,
    int AffectedViews,
    int AffectedFunctions,
    bool HasPiiColumns,
    bool HasLineageDownstream,
    bool ApprovalRequired,
    bool DocumentationTriggered
);

/// <summary>
/// Full schema change details including definitions and impacts.
/// </summary>
public record SchemaChangeDetailDto(
    Guid ChangeId,
    string DatabaseName,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    string ChangeType,
    string? ChangeDescription,
    string? OldDefinition,
    string? NewDefinition,
    string? DdlStatement,
    DateTime DetectedAt,
    string DetectedBy,
    string? LoginName,
    string? HostName,
    string? ApplicationName,
    int ImpactScore,
    string RiskLevel,
    string ProcessingStatus,
    string? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    string? AcknowledgementNotes,
    bool ApprovalRequired,
    Guid? ApprovalWorkflowId,
    bool DocumentationTriggered,
    DateTime? DocumentationTriggeredAt,
    List<ChangeImpactDto> Impacts,
    List<ColumnChangeDto> ColumnChanges
);

/// <summary>
/// Impact on a dependent object.
/// </summary>
public record ChangeImpactDto(
    Guid ImpactId,
    string AffectedSchema,
    string AffectedObject,
    string AffectedObjectType,
    string ImpactType,
    int ImpactSeverity,
    string? ImpactDescription,
    string? OperationType,
    string? AffectedColumn,
    int? LineNumber,
    string? SqlFragment,
    string? SuggestedAction,
    bool RequiresManualReview
);

/// <summary>
/// Column-level change details.
/// </summary>
public record ColumnChangeDto(
    Guid ColumnChangeId,
    string SchemaName,
    string TableName,
    string ColumnName,
    string ChangeType,
    string? OldDataType,
    string? NewDataType,
    bool? OldIsNullable,
    bool? NewIsNullable,
    bool? OldIsPii,
    bool? NewIsPii,
    int TotalUsageCount
);

/// <summary>
/// Detection run status and progress.
/// </summary>
public record DetectionRunDto(
    Guid RunId,
    string RunType,
    string ScanScope,
    string? SchemaFilter,
    string CurrentState,
    int TotalObjects,
    int ProcessedObjects,
    double ProgressPercent,
    int ChangesDetected,
    int HighRiskChanges,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? DurationMs,
    string? ErrorMessage,
    string TriggeredBy
);

/// <summary>
/// Schema snapshot summary.
/// </summary>
public record SchemaSnapshotDto(
    Guid SnapshotId,
    string SnapshotName,
    string SnapshotType,
    string? SchemaFilter,
    int ObjectCount,
    int TableCount,
    int ViewCount,
    int ProcedureCount,
    int FunctionCount,
    DateTime TakenAt,
    string TakenBy,
    bool IsBaseline
);

/// <summary>
/// Dashboard statistics.
/// </summary>
public record SchemaChangeStatsDto(
    int TotalChanges,
    int PendingChanges,
    int HighRiskChanges,
    int CriticalChanges,
    int ChangesToday,
    int ChangesThisWeek,
    int AverageImpactScore,
    int PiiRelatedChanges,
    int AwaitingApproval,
    DateTime? LastDetectionRun,
    string? LastDetectionStatus
);

#endregion

#region Request DTOs

/// <summary>
/// Request to start a manual detection run.
/// </summary>
public record StartDetectionRequest(
    string ScanScope,         // FULL, SCHEMA, OBJECT
    string? SchemaFilter,
    string? ObjectFilter,
    string TriggeredBy
);

/// <summary>
/// Request to acknowledge a schema change.
/// </summary>
public record AcknowledgeChangeRequest(
    string AcknowledgedBy,
    string? Notes
);

/// <summary>
/// Request to create a baseline snapshot.
/// </summary>
public record CreateBaselineRequest(
    string? SchemaFilter,
    string CreatedBy
);

/// <summary>
/// Filter parameters for listing schema changes.
/// </summary>
public record SchemaChangeFilterDto(
    string? SchemaName,
    string? ObjectName,
    string? ObjectType,
    string? ChangeType,
    string? RiskLevel,
    string? ProcessingStatus,
    DateTime? FromDate,
    DateTime? ToDate,
    bool? HasPiiColumns,
    bool? ApprovalRequired,
    int Page = 1,
    int PageSize = 20
);

#endregion

#region SignalR DTOs

/// <summary>
/// Real-time change detection notification.
/// </summary>
public record SchemaChangeDetectedNotification(
    Guid ChangeId,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    string ChangeType,
    string RiskLevel,
    DateTime DetectedAt
);

/// <summary>
/// Detection run progress update.
/// </summary>
public record DetectionProgressNotification(
    Guid RunId,
    string CurrentState,
    int ProcessedObjects,
    int TotalObjects,
    double ProgressPercent,
    int ChangesDetected
);

/// <summary>
/// Impact analysis complete notification.
/// </summary>
public record ImpactAnalysisCompleteNotification(
    Guid ChangeId,
    int ImpactScore,
    string RiskLevel,
    int AffectedObjects,
    bool ApprovalRequired
);

#endregion
