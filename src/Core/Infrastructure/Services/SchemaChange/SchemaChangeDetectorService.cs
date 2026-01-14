// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector Service
// Core detection logic with snapshot comparison and impact analysis
// ═══════════════════════════════════════════════════════════════════════════

using System.Data;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Dapper;
using Enterprise.Documentation.Core.Application.DTOs.SchemaChange;
using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Enterprise.Documentation.Core.Domain.Entities.SchemaChange;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Services.SchemaChange;

/// <summary>
/// Main service for schema change detection and processing.
/// </summary>
public class SchemaChangeDetectorService : ISchemaChangeDetectorService
{
    private readonly IDbConnection _connection;
    private readonly ISchemaChangeRepository _changeRepository;
    private readonly IDetectionRunRepository _runRepository;
    private readonly ISchemaSnapshotRepository _snapshotRepository;
    private readonly IImpactAnalysisService _impactService;
    private readonly ISchemaChangeNotifier _notifier;
    private readonly ILogger<SchemaChangeDetectorService> _logger;

    public SchemaChangeDetectorService(
        IDbConnection connection,
        ISchemaChangeRepository changeRepository,
        IDetectionRunRepository runRepository,
        ISchemaSnapshotRepository snapshotRepository,
        IImpactAnalysisService impactService,
        ISchemaChangeNotifier notifier,
        ILogger<SchemaChangeDetectorService> logger)
    {
        _connection = connection;
        _changeRepository = changeRepository;
        _runRepository = runRepository;
        _snapshotRepository = snapshotRepository;
        _impactService = impactService;
        _notifier = notifier;
        _logger = logger;
    }

    #region Detection Runs

    public async Task<DetectionRunDto> StartDetectionAsync(StartDetectionRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting detection run: Scope={Scope}, Schema={Schema}",
            request.ScanScope, request.SchemaFilter);

        var run = request.ScanScope switch
        {
            "FULL" => DetectionRun.StartScheduled(request.TriggeredBy),
            "SCHEMA" => DetectionRun.StartManual(request.TriggeredBy, request.ScanScope, request.SchemaFilter),
            _ => DetectionRun.StartManual(request.TriggeredBy, request.ScanScope, request.SchemaFilter)
        };

        await _runRepository.AddAsync(run, ct);

        // Start detection in background
        _ = Task.Run(async () => await ExecuteDetectionAsync(run.RunId, ct), ct);

        return MapToDto(run);
    }

    private async Task ExecuteDetectionAsync(Guid runId, CancellationToken ct)
    {
        var run = await _runRepository.GetByIdAsync(runId, ct);
        if (run == null) return;

        try
        {
            // Step 1: Count objects
            var objectCount = await CountDatabaseObjectsAsync(run.SchemaFilter, ct);
            run.Begin(objectCount);
            await _runRepository.UpdateAsync(run, ct);

            await _notifier.NotifyDetectionProgress(new DetectionProgressNotification(
                run.RunId, run.CurrentState.ToString(), 0, objectCount, 0, 0));

            // Step 2: Take snapshot
            var snapshotData = await CaptureSchemaSnapshotAsync(run.SchemaFilter, ct);
            var snapshot = SchemaSnapshot.CreateFull(run.TriggeredBy, snapshotData, 0, 0, 0, 0);
            await _snapshotRepository.AddAsync(snapshot, ct);
            run.CompleteSnapshot(snapshot.SnapshotId);
            await _runRepository.UpdateAsync(run, ct);

            // Step 3: Compare with baseline
            var baseline = await _snapshotRepository.GetLatestBaselineAsync(ct);
            var changes = await CompareSnapshotsAsync(baseline, snapshot, ct);
            run.CompleteComparison(changes.Count);
            await _runRepository.UpdateAsync(run, ct);

            // Step 4: Analyze each change
            var highRiskCount = 0;
            foreach (var change in changes)
            {
                await _changeRepository.AddAsync(change, ct);

                // Analyze impact
                var impacts = await _impactService.AnalyzeImpactAsync(change.ChangeId, ct);
                var (score, riskLevel) = await _impactService.CalculateRiskScoreAsync(change.ChangeId, impacts, ct);

                change.AssessImpact(
                    score,
                    Enum.Parse<RiskLevel>(riskLevel),
                    impacts.Count(i => i.AffectedObjectType == "PROCEDURE"),
                    impacts.Count(i => i.AffectedObjectType == "VIEW"),
                    impacts.Count(i => i.AffectedObjectType == "FUNCTION"),
                    false, // TODO [4]: Check PII from lineage
                    false  // TODO [4]: Check downstream lineage
                );

                await _changeRepository.UpdateAsync(change, ct);

                if (change.RiskLevel is RiskLevel.High or RiskLevel.Critical)
                    highRiskCount++;

                // Notify
                await _notifier.NotifyChangeDetected(new SchemaChangeDetectedNotification(
                    change.ChangeId,
                    change.SchemaName,
                    change.ObjectName,
                    change.ObjectType.ToString(),
                    change.ChangeType.ToString(),
                    change.RiskLevel.ToString(),
                    change.DetectedAt));

                run.UpdateProgress(run.ProcessedObjects + 1);
            }

            run.CompleteAnalysis(highRiskCount);
            await _runRepository.UpdateAsync(run, ct);

            // Step 5: Complete
            var summary = JsonSerializer.Serialize(new
            {
                TotalChanges = changes.Count,
                HighRiskChanges = highRiskCount,
                CompletedAt = DateTime.UtcNow
            });

            run.Complete(summary);
            await _runRepository.UpdateAsync(run, ct);

            await _notifier.NotifyDetectionComplete(run.RunId, changes.Count, highRiskCount);

            _logger.LogInformation("Detection run {RunId} completed: {Changes} changes, {HighRisk} high-risk",
                runId, changes.Count, highRiskCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Detection run {RunId} failed", runId);
            run.Fail(ex.Message);
            await _runRepository.UpdateAsync(run, ct);
            await _notifier.NotifyDetectionFailed(run.RunId, ex.Message);
        }
    }

    public async Task<DetectionRunDto?> GetDetectionRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _runRepository.GetByIdAsync(runId, ct);
        return run == null ? null : MapToDto(run);
    }

    public async Task<IEnumerable<DetectionRunDto>> GetRecentRunsAsync(int count = 10, CancellationToken ct = default)
    {
        var runs = await _runRepository.GetRecentAsync(count, ct);
        return runs.Select(MapToDto);
    }

    public async Task CancelDetectionAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _runRepository.GetByIdAsync(runId, ct);
        if (run != null)
        {
            run.Cancel();
            await _runRepository.UpdateAsync(run, ct);
        }
    }

    #endregion

    #region Schema Changes

    public async Task<IEnumerable<SchemaChangeDto>> GetChangesAsync(SchemaChangeFilterDto filter, CancellationToken ct = default)
    {
        var changes = await _changeRepository.GetFilteredAsync(filter, ct);
        return changes.Select(MapToDto);
    }

    public async Task<SchemaChangeDetailDto?> GetChangeDetailAsync(Guid changeId, CancellationToken ct = default)
    {
        var change = await _changeRepository.GetByIdAsync(changeId, ct);
        return change == null ? null : MapToDetailDto(change);
    }

    public async Task<IEnumerable<SchemaChangeDto>> GetPendingChangesAsync(int maxCount = 100, CancellationToken ct = default)
    {
        var changes = await _changeRepository.GetPendingAsync(maxCount, ct);
        return changes.Select(MapToDto);
    }

    public async Task AcknowledgeChangeAsync(Guid changeId, AcknowledgeChangeRequest request, CancellationToken ct = default)
    {
        var change = await _changeRepository.GetByIdAsync(changeId, ct);
        if (change == null)
            throw new KeyNotFoundException($"Change {changeId} not found");

        change.Acknowledge(request.AcknowledgedBy, request.Notes);
        await _changeRepository.UpdateAsync(change, ct);

        _logger.LogInformation("Change {ChangeId} acknowledged by {User}", changeId, request.AcknowledgedBy);
    }

    public async Task TriggerDocumentationAsync(Guid changeId, CancellationToken ct = default)
    {
        var change = await _changeRepository.GetByIdAsync(changeId, ct);
        if (change == null)
            throw new KeyNotFoundException($"Change {changeId} not found");

        change.TriggerDocumentation();
        await _changeRepository.UpdateAsync(change, ct);

        // TODO [4]: Queue documentation regeneration job via Agent #5
        _logger.LogInformation("Documentation triggered for change {ChangeId}", changeId);
    }

    public async Task<Guid> TriggerApprovalWorkflowAsync(Guid changeId, CancellationToken ct = default)
    {
        var change = await _changeRepository.GetByIdAsync(changeId, ct);
        if (change == null)
            throw new KeyNotFoundException($"Change {changeId} not found");

        // TODO [4]: Create actual approval workflow via Agent #2A
        var approvalId = Guid.NewGuid();
        change.LinkApprovalWorkflow(approvalId);
        await _changeRepository.UpdateAsync(change, ct);

        _logger.LogInformation("Approval workflow {ApprovalId} triggered for change {ChangeId}", approvalId, changeId);
        return approvalId;
    }

    #endregion

    #region Snapshots

    public async Task<SchemaSnapshotDto> CreateSnapshotAsync(string snapshotType, string? schemaFilter, string takenBy, CancellationToken ct = default)
    {
        var data = await CaptureSchemaSnapshotAsync(schemaFilter, ct);
        var snapshot = snapshotType == "BASELINE"
            ? SchemaSnapshot.CreateBaseline(takenBy, data)
            : SchemaSnapshot.CreateFull(takenBy, data, 0, 0, 0, 0);

        await _snapshotRepository.AddAsync(snapshot, ct);
        return MapToDto(snapshot);
    }

    public async Task<SchemaSnapshotDto> CreateBaselineAsync(CreateBaselineRequest request, CancellationToken ct = default)
    {
        return await CreateSnapshotAsync("BASELINE", request.SchemaFilter, request.CreatedBy, ct);
    }

    public async Task<IEnumerable<SchemaSnapshotDto>> GetSnapshotsAsync(int count = 20, CancellationToken ct = default)
    {
        var snapshots = await _snapshotRepository.GetRecentAsync(count, ct);
        return snapshots.Select(MapToDto);
    }

    public async Task<SchemaSnapshotDto?> GetLatestBaselineAsync(CancellationToken ct = default)
    {
        var baseline = await _snapshotRepository.GetLatestBaselineAsync(ct);
        return baseline == null ? null : MapToDto(baseline);
    }

    #endregion

    #region Statistics

    public async Task<SchemaChangeStatsDto> GetStatisticsAsync(CancellationToken ct = default)
    {
        var sql = @"
            SELECT
                COUNT(*) AS TotalChanges,
                SUM(CASE WHEN ProcessingStatus = 'Pending' THEN 1 ELSE 0 END) AS PendingChanges,
                SUM(CASE WHEN RiskLevel IN ('HIGH', 'CRITICAL') THEN 1 ELSE 0 END) AS HighRiskChanges,
                SUM(CASE WHEN RiskLevel = 'CRITICAL' THEN 1 ELSE 0 END) AS CriticalChanges,
                SUM(CASE WHEN CAST(DetectedAt AS DATE) = CAST(GETUTCDATE() AS DATE) THEN 1 ELSE 0 END) AS ChangesToday,
                SUM(CASE WHEN DetectedAt >= DATEADD(DAY, -7, GETUTCDATE()) THEN 1 ELSE 0 END) AS ChangesThisWeek,
                AVG(ImpactScore) AS AverageImpactScore,
                SUM(CASE WHEN HasPiiColumns = 1 THEN 1 ELSE 0 END) AS PiiRelatedChanges,
                SUM(CASE WHEN ApprovalRequired = 1 AND ProcessingStatus NOT IN ('Acknowledged', 'AutoProcessed') THEN 1 ELSE 0 END) AS AwaitingApproval
            FROM DaQa.SchemaChanges";

        var stats = await _connection.QuerySingleOrDefaultAsync<SchemaChangeStatsDto>(sql);

        // Get last run info
        var lastRun = await _runRepository.GetLatestAsync(ct);

        return stats with
        {
            LastDetectionRun = lastRun?.CompletedAt?.ToString("o"),
            LastDetectionStatus = lastRun?.CurrentState.ToString()
        };
    }

    #endregion

    #region Private Helpers

    private async Task<int> CountDatabaseObjectsAsync(string? schemaFilter, CancellationToken ct)
    {
        var sql = @"
            SELECT COUNT(*)
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')
              AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
              AND (@SchemaFilter IS NULL OR s.name = @SchemaFilter)";

        return await _connection.ExecuteScalarAsync<int>(sql, new { SchemaFilter = schemaFilter });
    }

    private async Task<byte[]> CaptureSchemaSnapshotAsync(string? schemaFilter, CancellationToken ct)
    {
        // Query all objects and their definitions
        var sql = @"
            SELECT
                s.name AS SchemaName,
                o.name AS ObjectName,
                o.type_desc AS ObjectType,
                OBJECT_DEFINITION(o.object_id) AS Definition,
                o.create_date AS CreatedAt,
                o.modify_date AS ModifiedAt
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF')
              AND s.name NOT IN ('sys', 'INFORMATION_SCHEMA')
              AND (@SchemaFilter IS NULL OR s.name = @SchemaFilter)
            ORDER BY s.name, o.name";

        var objects = await _connection.QueryAsync<dynamic>(sql, new { SchemaFilter = schemaFilter });
        var json = JsonSerializer.Serialize(objects);

        // Compress
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await gzip.WriteAsync(bytes, ct);
        }

        return output.ToArray();
    }

    private async Task<List<Domain.Entities.SchemaChange.SchemaChange>> CompareSnapshotsAsync(
        SchemaSnapshot? baseline,
        SchemaSnapshot current,
        CancellationToken ct)
    {
        var changes = new List<Domain.Entities.SchemaChange.SchemaChange>();

        if (baseline == null)
        {
            _logger.LogWarning("No baseline snapshot found, skipping comparison");
            return changes;
        }

        // Decompress and compare
        var baselineObjects = DecompressSnapshot(baseline.SnapshotData);
        var currentObjects = DecompressSnapshot(current.SnapshotData);

        var baselineDict = baselineObjects.ToDictionary(o => $"{o.SchemaName}.{o.ObjectName}");
        var currentDict = currentObjects.ToDictionary(o => $"{o.SchemaName}.{o.ObjectName}");

        // Find new objects (CREATE)
        foreach (var key in currentDict.Keys.Except(baselineDict.Keys))
        {
            var obj = currentDict[key];
            var change = Domain.Entities.SchemaChange.SchemaChange.Create(
                "IRFS1",
                obj.SchemaName,
                obj.ObjectName,
                ParseObjectType(obj.ObjectType),
                ChangeType.Create,
                null,
                DetectionMethod.Polling
            );
            change.SetDefinitions(null, obj.Definition);
            changes.Add(change);
        }

        // Find removed objects (DROP)
        foreach (var key in baselineDict.Keys.Except(currentDict.Keys))
        {
            var obj = baselineDict[key];
            var change = Domain.Entities.SchemaChange.SchemaChange.Create(
                "IRFS1",
                obj.SchemaName,
                obj.ObjectName,
                ParseObjectType(obj.ObjectType),
                ChangeType.Drop,
                null,
                DetectionMethod.Polling
            );
            change.SetDefinitions(obj.Definition, null);
            changes.Add(change);
        }

        // Find modified objects (ALTER)
        foreach (var key in baselineDict.Keys.Intersect(currentDict.Keys))
        {
            var oldObj = baselineDict[key];
            var newObj = currentDict[key];

            if (oldObj.Definition != newObj.Definition)
            {
                var change = Domain.Entities.SchemaChange.SchemaChange.Create(
                    "IRFS1",
                    newObj.SchemaName,
                    newObj.ObjectName,
                    ParseObjectType(newObj.ObjectType),
                    ChangeType.Alter,
                    null,
                    DetectionMethod.Polling
                );
                change.SetDefinitions(oldObj.Definition, newObj.Definition);
                changes.Add(change);
            }
        }

        return changes;
    }

    private List<dynamic> DecompressSnapshot(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<List<dynamic>>(json) ?? new List<dynamic>();
    }

    private static ObjectType ParseObjectType(string typeDesc)
    {
        return typeDesc.ToUpperInvariant() switch
        {
            "USER_TABLE" => ObjectType.Table,
            "VIEW" => ObjectType.View,
            "SQL_STORED_PROCEDURE" => ObjectType.Procedure,
            "SQL_SCALAR_FUNCTION" => ObjectType.Function,
            "SQL_INLINE_TABLE_VALUED_FUNCTION" => ObjectType.Function,
            "SQL_TABLE_VALUED_FUNCTION" => ObjectType.Function,
            _ => ObjectType.Unknown
        };
    }

    #endregion

    #region Mapping

    private static DetectionRunDto MapToDto(DetectionRun run) => new(
        run.RunId,
        run.RunType,
        run.ScanScope,
        run.SchemaFilter,
        run.CurrentState.ToString(),
        run.TotalObjects,
        run.ProcessedObjects,
        run.ProgressPercent,
        run.ChangesDetected,
        run.HighRiskChanges,
        run.StartedAt,
        run.CompletedAt,
        run.DurationMs,
        run.ErrorMessage,
        run.TriggeredBy
    );

    private static SchemaChangeDto MapToDto(Domain.Entities.SchemaChange.SchemaChange change) => new(
        change.ChangeId,
        change.DatabaseName,
        change.SchemaName,
        change.ObjectName,
        change.ObjectType.ToString(),
        change.ChangeType.ToString(),
        change.ChangeDescription,
        change.DetectedAt,
        change.DetectedBy.ToString(),
        change.LoginName,
        change.ImpactScore,
        change.RiskLevel.ToString(),
        change.Status.ToString(),
        change.AffectedProcedures,
        change.AffectedViews,
        change.AffectedFunctions,
        change.HasPiiColumns,
        change.HasLineageDownstream,
        change.ApprovalRequired,
        change.DocumentationTriggered
    );

    private static SchemaChangeDetailDto MapToDetailDto(Domain.Entities.SchemaChange.SchemaChange change) => new(
        change.ChangeId,
        change.DatabaseName,
        change.SchemaName,
        change.ObjectName,
        change.ObjectType.ToString(),
        change.ChangeType.ToString(),
        change.ChangeDescription,
        change.OldDefinition,
        change.NewDefinition,
        change.DdlStatement,
        change.DetectedAt,
        change.DetectedBy.ToString(),
        change.LoginName,
        change.HostName,
        change.ApplicationName,
        change.ImpactScore,
        change.RiskLevel.ToString(),
        change.Status.ToString(),
        change.AcknowledgedBy,
        change.AcknowledgedAt,
        change.AcknowledgementNotes,
        change.ApprovalRequired,
        change.ApprovalWorkflowId,
        change.DocumentationTriggered,
        change.DocumentationTriggeredAt,
        change.Impacts.Select(i => new ChangeImpactDto(
            i.ImpactId, i.AffectedSchema, i.AffectedObject, i.AffectedObjectType.ToString(),
            i.ImpactType.ToString(), i.ImpactSeverity, i.ImpactDescription, i.OperationType,
            i.AffectedColumn, i.LineNumber, i.SqlFragment, i.SuggestedAction, i.RequiresManualReview
        )).ToList(),
        change.ColumnChanges.Select(c => new ColumnChangeDto(
            c.ColumnChangeId, c.SchemaName, c.TableName, c.ColumnName, c.ChangeType.ToString(),
            c.OldDataType, c.NewDataType, c.OldIsNullable, c.NewIsNullable, c.OldIsPii, c.NewIsPii,
            c.TotalUsageCount
        )).ToList()
    );

    private static SchemaSnapshotDto MapToDto(SchemaSnapshot snapshot) => new(
        snapshot.SnapshotId,
        snapshot.SnapshotName,
        snapshot.SnapshotType,
        snapshot.SchemaFilter,
        snapshot.ObjectCount,
        snapshot.TableCount,
        snapshot.ViewCount,
        snapshot.ProcedureCount,
        snapshot.FunctionCount,
        snapshot.TakenAt,
        snapshot.TakenBy,
        snapshot.IsBaseline
    );

    #endregion
}
