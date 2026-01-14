// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Hangfire Job
// Scheduled background job for periodic schema detection
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Configure job schedule in appsettings.json
// TODO [4]: Add job dashboard UI integration

using Enterprise.Documentation.Core.Application.DTOs.SchemaChange;
using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Infrastructure.Jobs;

/// <summary>
/// Hangfire job for scheduled schema detection runs.
/// </summary>
public class SchemaDetectionJob
{
    private readonly ISchemaChangeDetectorService _detectorService;
    private readonly ILogger<SchemaDetectionJob> _logger;

    public SchemaDetectionJob(
        ISchemaChangeDetectorService detectorService,
        ILogger<SchemaDetectionJob> logger)
    {
        _detectorService = detectorService;
        _logger = logger;
    }

    /// <summary>
    /// Execute a full schema detection run.
    /// Scheduled via Hangfire: RecurringJob.AddOrUpdate<SchemaDetectionJob>(
    ///     "schema-detection",
    ///     job => job.ExecuteAsync(CancellationToken.None),
    ///     "0 2 * * *"  // Daily at 2 AM
    /// );
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting scheduled schema detection job");

        try
        {
            var request = new StartDetectionRequest(
                ScanScope: "FULL",
                SchemaFilter: null,
                ObjectFilter: null,
                TriggeredBy: "ScheduledJob"
            );

            var run = await _detectorService.StartDetectionAsync(request, ct);

            _logger.LogInformation(
                "Scheduled schema detection job started: RunId={RunId}",
                run.RunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled schema detection job failed");
            throw;
        }
    }

    /// <summary>
    /// Execute detection for a specific schema.
    /// Can be triggered manually or scheduled per-schema.
    /// </summary>
    public async Task ExecuteForSchemaAsync(string schemaName, CancellationToken ct)
    {
        _logger.LogInformation("Starting schema detection for {Schema}", schemaName);

        try
        {
            var request = new StartDetectionRequest(
                ScanScope: "SCHEMA",
                SchemaFilter: schemaName,
                ObjectFilter: null,
                TriggeredBy: "ScheduledJob"
            );

            var run = await _detectorService.StartDetectionAsync(request, ct);

            _logger.LogInformation(
                "Schema detection for {Schema} started: RunId={RunId}",
                schemaName, run.RunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema detection for {Schema} failed", schemaName);
            throw;
        }
    }

    /// <summary>
    /// Create a baseline snapshot for future comparisons.
    /// Run once initially, then periodically (e.g., weekly).
    /// </summary>
    public async Task CreateBaselineAsync(CancellationToken ct)
    {
        _logger.LogInformation("Creating baseline schema snapshot");

        try
        {
            var request = new CreateBaselineRequest(
                SchemaFilter: null,
                CreatedBy: "ScheduledJob"
            );

            var snapshot = await _detectorService.CreateBaselineAsync(request, ct);

            _logger.LogInformation(
                "Baseline snapshot created: SnapshotId={SnapshotId}, Objects={Count}",
                snapshot.SnapshotId, snapshot.ObjectCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Baseline snapshot creation failed");
            throw;
        }
    }
}

/// <summary>
/// Hangfire job registration helper.
/// Call in Program.cs after app.UseHangfireDashboard().
/// </summary>
public static class SchemaDetectionJobRegistration
{
    /// <summary>
    /// Register recurring schema detection jobs.
    /// </summary>
    public static void RegisterSchemaDetectionJobs()
    {
        // TODO [4]: Uncomment when Hangfire is configured
        /*
        // Daily full detection at 2 AM
        RecurringJob.AddOrUpdate<SchemaDetectionJob>(
            "schema-detection-daily",
            job => job.ExecuteAsync(CancellationToken.None),
            "0 2 * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local }
        );

        // Weekly baseline snapshot on Sundays at 3 AM
        RecurringJob.AddOrUpdate<SchemaDetectionJob>(
            "schema-baseline-weekly",
            job => job.CreateBaselineAsync(CancellationToken.None),
            "0 3 * * 0",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local }
        );
        */
    }
}
