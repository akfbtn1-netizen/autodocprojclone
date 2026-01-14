// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Service Interface
// Main orchestration interface for schema change detection
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Add ScriptDom-based impact analysis methods
// TODO [4]: Add integration with Agent #3 Lineage service

using Enterprise.Documentation.Core.Application.DTOs.SchemaChange;

namespace Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;

/// <summary>
/// Core service for schema change detection and impact analysis.
/// </summary>
public interface ISchemaChangeDetectorService
{
    // Detection runs
    Task<DetectionRunDto> StartDetectionAsync(StartDetectionRequest request, CancellationToken ct = default);
    Task<DetectionRunDto?> GetDetectionRunAsync(Guid runId, CancellationToken ct = default);
    Task<IEnumerable<DetectionRunDto>> GetRecentRunsAsync(int count = 10, CancellationToken ct = default);
    Task CancelDetectionAsync(Guid runId, CancellationToken ct = default);

    // Schema changes
    Task<IEnumerable<SchemaChangeDto>> GetChangesAsync(SchemaChangeFilterDto filter, CancellationToken ct = default);
    Task<SchemaChangeDetailDto?> GetChangeDetailAsync(Guid changeId, CancellationToken ct = default);
    Task<IEnumerable<SchemaChangeDto>> GetPendingChangesAsync(int maxCount = 100, CancellationToken ct = default);

    // Change processing
    Task AcknowledgeChangeAsync(Guid changeId, AcknowledgeChangeRequest request, CancellationToken ct = default);
    Task TriggerDocumentationAsync(Guid changeId, CancellationToken ct = default);
    Task<Guid> TriggerApprovalWorkflowAsync(Guid changeId, CancellationToken ct = default);

    // Snapshots
    Task<SchemaSnapshotDto> CreateSnapshotAsync(string snapshotType, string? schemaFilter, string takenBy, CancellationToken ct = default);
    Task<SchemaSnapshotDto> CreateBaselineAsync(CreateBaselineRequest request, CancellationToken ct = default);
    Task<IEnumerable<SchemaSnapshotDto>> GetSnapshotsAsync(int count = 20, CancellationToken ct = default);
    Task<SchemaSnapshotDto?> GetLatestBaselineAsync(CancellationToken ct = default);

    // Statistics
    Task<SchemaChangeStatsDto> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>
/// Impact analysis service using ScriptDom for SQL parsing.
/// </summary>
public interface IImpactAnalysisService
{
    /// <summary>
    /// Analyze impact of a schema change on dependent objects.
    /// </summary>
    Task<IEnumerable<ChangeImpactDto>> AnalyzeImpactAsync(Guid changeId, CancellationToken ct = default);

    /// <summary>
    /// Find all procedures/views that reference a table/column.
    /// </summary>
    Task<IEnumerable<string>> FindDependentObjectsAsync(
        string schemaName,
        string objectName,
        string? columnName = null,
        CancellationToken ct = default);

    /// <summary>
    /// Calculate risk score based on impact analysis.
    /// </summary>
    Task<(int Score, string RiskLevel)> CalculateRiskScoreAsync(
        Guid changeId,
        IEnumerable<ChangeImpactDto> impacts,
        CancellationToken ct = default);
}

/// <summary>
/// Repository interface for schema changes.
/// </summary>
public interface ISchemaChangeRepository
{
    Task<Domain.Entities.SchemaChange.SchemaChange?> GetByIdAsync(Guid changeId, CancellationToken ct = default);
    Task<IEnumerable<Domain.Entities.SchemaChange.SchemaChange>> GetPendingAsync(int maxCount, CancellationToken ct = default);
    Task<IEnumerable<Domain.Entities.SchemaChange.SchemaChange>> GetFilteredAsync(SchemaChangeFilterDto filter, CancellationToken ct = default);
    Task AddAsync(Domain.Entities.SchemaChange.SchemaChange change, CancellationToken ct = default);
    Task UpdateAsync(Domain.Entities.SchemaChange.SchemaChange change, CancellationToken ct = default);
    Task<int> GetCountAsync(SchemaChangeFilterDto? filter = null, CancellationToken ct = default);
}

/// <summary>
/// Repository interface for detection runs.
/// </summary>
public interface IDetectionRunRepository
{
    Task<Domain.Entities.SchemaChange.DetectionRun?> GetByIdAsync(Guid runId, CancellationToken ct = default);
    Task<IEnumerable<Domain.Entities.SchemaChange.DetectionRun>> GetRecentAsync(int count, CancellationToken ct = default);
    Task<Domain.Entities.SchemaChange.DetectionRun?> GetLatestAsync(CancellationToken ct = default);
    Task AddAsync(Domain.Entities.SchemaChange.DetectionRun run, CancellationToken ct = default);
    Task UpdateAsync(Domain.Entities.SchemaChange.DetectionRun run, CancellationToken ct = default);
}

/// <summary>
/// Repository interface for schema snapshots.
/// </summary>
public interface ISchemaSnapshotRepository
{
    Task<Domain.Entities.SchemaChange.SchemaSnapshot?> GetByIdAsync(Guid snapshotId, CancellationToken ct = default);
    Task<Domain.Entities.SchemaChange.SchemaSnapshot?> GetLatestBaselineAsync(CancellationToken ct = default);
    Task<IEnumerable<Domain.Entities.SchemaChange.SchemaSnapshot>> GetRecentAsync(int count, CancellationToken ct = default);
    Task AddAsync(Domain.Entities.SchemaChange.SchemaSnapshot snapshot, CancellationToken ct = default);
}
