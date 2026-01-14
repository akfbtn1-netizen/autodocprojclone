// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Detection Run Repository
// Dapper-based data access for detection runs
// ═══════════════════════════════════════════════════════════════════════════

using System.Data;
using Dapper;
using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Enterprise.Documentation.Core.Domain.Entities.SchemaChange;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

public class DetectionRunRepository : IDetectionRunRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger<DetectionRunRepository> _logger;

    public DetectionRunRepository(IDbConnection connection, ILogger<DetectionRunRepository> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<DetectionRun?> GetByIdAsync(Guid runId, CancellationToken ct = default)
    {
        var sql = @"
            SELECT
                RunId, RunType, ScanScope, SchemaFilter, ObjectFilter,
                CurrentState, TotalObjects, ProcessedObjects, ChangesDetected, HighRiskChanges,
                StartedAt, SnapshotCompletedAt, ComparisonCompletedAt, AnalysisCompletedAt,
                CompletedAt, DurationMs, ErrorMessage, RetryCount, TriggeredBy,
                SnapshotId, ResultSummary AS ResultSummaryJson
            FROM DaQa.SchemaDetectionRuns
            WHERE RunId = @RunId";

        var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { RunId = runId });
        return row == null ? null : MapFromRow(row);
    }

    public async Task<IEnumerable<DetectionRun>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        var sql = @"
            SELECT TOP (@Count)
                RunId, RunType, ScanScope, SchemaFilter, ObjectFilter,
                CurrentState, TotalObjects, ProcessedObjects, ChangesDetected, HighRiskChanges,
                StartedAt, SnapshotCompletedAt, ComparisonCompletedAt, AnalysisCompletedAt,
                CompletedAt, DurationMs, ErrorMessage, RetryCount, TriggeredBy,
                SnapshotId, ResultSummary AS ResultSummaryJson
            FROM DaQa.SchemaDetectionRuns
            ORDER BY StartedAt DESC";

        var rows = await _connection.QueryAsync<dynamic>(sql, new { Count = count });
        return rows.Select(MapFromRow);
    }

    public async Task<DetectionRun?> GetLatestAsync(CancellationToken ct = default)
    {
        var sql = @"
            SELECT TOP 1
                RunId, RunType, ScanScope, SchemaFilter, ObjectFilter,
                CurrentState, TotalObjects, ProcessedObjects, ChangesDetected, HighRiskChanges,
                StartedAt, SnapshotCompletedAt, ComparisonCompletedAt, AnalysisCompletedAt,
                CompletedAt, DurationMs, ErrorMessage, RetryCount, TriggeredBy,
                SnapshotId, ResultSummary AS ResultSummaryJson
            FROM DaQa.SchemaDetectionRuns
            ORDER BY StartedAt DESC";

        var row = await _connection.QuerySingleOrDefaultAsync<dynamic>(sql);
        return row == null ? null : MapFromRow(row);
    }

    public async Task AddAsync(DetectionRun run, CancellationToken ct = default)
    {
        var sql = @"
            INSERT INTO DaQa.SchemaDetectionRuns (
                RunId, RunType, ScanScope, SchemaFilter, ObjectFilter,
                CurrentState, TotalObjects, ProcessedObjects, ChangesDetected, HighRiskChanges,
                TriggeredBy
            ) VALUES (
                @RunId, @RunType, @ScanScope, @SchemaFilter, @ObjectFilter,
                @CurrentState, @TotalObjects, @ProcessedObjects, @ChangesDetected, @HighRiskChanges,
                @TriggeredBy
            )";

        await _connection.ExecuteAsync(sql, new
        {
            run.RunId,
            run.RunType,
            run.ScanScope,
            run.SchemaFilter,
            run.ObjectFilter,
            CurrentState = run.CurrentState.ToString(),
            run.TotalObjects,
            run.ProcessedObjects,
            run.ChangesDetected,
            run.HighRiskChanges,
            run.TriggeredBy
        });
    }

    public async Task UpdateAsync(DetectionRun run, CancellationToken ct = default)
    {
        var sql = @"
            UPDATE DaQa.SchemaDetectionRuns SET
                CurrentState = @CurrentState,
                TotalObjects = @TotalObjects,
                ProcessedObjects = @ProcessedObjects,
                ChangesDetected = @ChangesDetected,
                HighRiskChanges = @HighRiskChanges,
                StartedAt = @StartedAt,
                SnapshotCompletedAt = @SnapshotCompletedAt,
                ComparisonCompletedAt = @ComparisonCompletedAt,
                AnalysisCompletedAt = @AnalysisCompletedAt,
                CompletedAt = @CompletedAt,
                DurationMs = @DurationMs,
                ErrorMessage = @ErrorMessage,
                RetryCount = @RetryCount,
                SnapshotId = @SnapshotId,
                ResultSummary = @ResultSummaryJson
            WHERE RunId = @RunId";

        await _connection.ExecuteAsync(sql, new
        {
            run.RunId,
            CurrentState = run.CurrentState.ToString(),
            run.TotalObjects,
            run.ProcessedObjects,
            run.ChangesDetected,
            run.HighRiskChanges,
            run.StartedAt,
            run.SnapshotCompletedAt,
            run.ComparisonCompletedAt,
            run.AnalysisCompletedAt,
            run.CompletedAt,
            run.DurationMs,
            run.ErrorMessage,
            run.RetryCount,
            run.SnapshotId,
            run.ResultSummaryJson
        });
    }

    private static DetectionRun MapFromRow(dynamic row)
    {
        // Create run based on type
        var run = (string)row.RunType switch
        {
            "SCHEDULED" => DetectionRun.StartScheduled((string)row.TriggeredBy),
            "DDL_TRIGGER" => DetectionRun.StartFromTrigger(
                (string?)row.SchemaFilter ?? "",
                (string?)row.ObjectFilter ?? ""),
            _ => DetectionRun.StartManual(
                (string)row.TriggeredBy,
                (string)row.ScanScope,
                (string?)row.SchemaFilter)
        };

        // TODO [4]: Proper entity hydration from database row
        return run;
    }
}
