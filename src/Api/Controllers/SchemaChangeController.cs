// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector Controller
// REST API endpoints for schema change detection and impact analysis
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Implement service layer and wire DI
// TODO [4]: Add Hangfire job scheduling for periodic detection runs
// TODO [4]: Integrate with Agent #3 Lineage for column-level impact

using Enterprise.Documentation.Core.Application.DTOs.SchemaChange;
using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enterprise.Documentation.Api.Controllers;

/// <summary>
/// Schema Change Detection API - Agent #4
/// Real-time schema change detection with ScriptDom-based impact analysis.
/// </summary>
[ApiController]
[Route("api/schema-changes")]
[Authorize]
[Produces("application/json")]
public class SchemaChangeController : ControllerBase
{
    private readonly ISchemaChangeDetectorService _detectorService;
    private readonly IImpactAnalysisService _impactService;
    private readonly ILogger<SchemaChangeController> _logger;

    public SchemaChangeController(
        ISchemaChangeDetectorService detectorService,
        IImpactAnalysisService impactService,
        ILogger<SchemaChangeController> logger)
    {
        _detectorService = detectorService;
        _impactService = impactService;
        _logger = logger;
    }

    #region Schema Changes

    /// <summary>
    /// Get all schema changes with optional filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SchemaChangeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChanges([FromQuery] SchemaChangeFilterDto filter, CancellationToken ct)
    {
        _logger.LogInformation("Getting schema changes with filter: {@Filter}", filter);
        var changes = await _detectorService.GetChangesAsync(filter, ct);
        return Ok(changes);
    }

    /// <summary>
    /// Get pending schema changes requiring attention.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<SchemaChangeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingChanges([FromQuery] int maxCount = 100, CancellationToken ct = default)
    {
        var changes = await _detectorService.GetPendingChangesAsync(maxCount, ct);
        return Ok(changes);
    }

    /// <summary>
    /// Get detailed information about a specific schema change.
    /// </summary>
    [HttpGet("{changeId:guid}")]
    [ProducesResponseType(typeof(SchemaChangeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChangeDetail(Guid changeId, CancellationToken ct)
    {
        var change = await _detectorService.GetChangeDetailAsync(changeId, ct);
        if (change == null)
            return NotFound(new { message = $"Schema change {changeId} not found" });

        return Ok(change);
    }

    /// <summary>
    /// Acknowledge a schema change as reviewed.
    /// </summary>
    [HttpPost("{changeId:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeChange(Guid changeId, [FromBody] AcknowledgeChangeRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Acknowledging schema change {ChangeId} by {User}", changeId, request.AcknowledgedBy);
        await _detectorService.AcknowledgeChangeAsync(changeId, request, ct);
        return Ok(new { message = "Change acknowledged successfully" });
    }

    /// <summary>
    /// Trigger documentation regeneration for affected objects.
    /// </summary>
    [HttpPost("{changeId:guid}/trigger-documentation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TriggerDocumentation(Guid changeId, CancellationToken ct)
    {
        _logger.LogInformation("Triggering documentation for schema change {ChangeId}", changeId);
        await _detectorService.TriggerDocumentationAsync(changeId, ct);
        return Ok(new { message = "Documentation regeneration triggered" });
    }

    /// <summary>
    /// Trigger approval workflow for high-risk changes.
    /// </summary>
    [HttpPost("{changeId:guid}/trigger-approval")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TriggerApproval(Guid changeId, CancellationToken ct)
    {
        _logger.LogInformation("Triggering approval workflow for schema change {ChangeId}", changeId);
        var approvalId = await _detectorService.TriggerApprovalWorkflowAsync(changeId, ct);
        return Ok(new { message = "Approval workflow triggered", approvalWorkflowId = approvalId });
    }

    #endregion

    #region Impact Analysis

    /// <summary>
    /// Get impact analysis for a schema change.
    /// </summary>
    [HttpGet("{changeId:guid}/impacts")]
    [ProducesResponseType(typeof(IEnumerable<ChangeImpactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImpacts(Guid changeId, CancellationToken ct)
    {
        var impacts = await _impactService.AnalyzeImpactAsync(changeId, ct);
        return Ok(impacts);
    }

    /// <summary>
    /// Find objects that depend on a specific table/column.
    /// </summary>
    [HttpGet("dependencies")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindDependencies(
        [FromQuery] string schemaName,
        [FromQuery] string objectName,
        [FromQuery] string? columnName = null,
        CancellationToken ct = default)
    {
        var dependents = await _impactService.FindDependentObjectsAsync(schemaName, objectName, columnName, ct);
        return Ok(dependents);
    }

    #endregion

    #region Detection Runs

    /// <summary>
    /// Start a new schema detection run.
    /// </summary>
    [HttpPost("runs")]
    [ProducesResponseType(typeof(DetectionRunDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> StartDetection([FromBody] StartDetectionRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Starting detection run: {@Request}", request);
        var run = await _detectorService.StartDetectionAsync(request, ct);
        return Accepted(run);
    }

    /// <summary>
    /// Get status of a detection run.
    /// </summary>
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(DetectionRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetectionRun(Guid runId, CancellationToken ct)
    {
        var run = await _detectorService.GetDetectionRunAsync(runId, ct);
        if (run == null)
            return NotFound(new { message = $"Detection run {runId} not found" });

        return Ok(run);
    }

    /// <summary>
    /// Get recent detection runs.
    /// </summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(IEnumerable<DetectionRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentRuns([FromQuery] int count = 10, CancellationToken ct = default)
    {
        var runs = await _detectorService.GetRecentRunsAsync(count, ct);
        return Ok(runs);
    }

    /// <summary>
    /// Cancel a running detection.
    /// </summary>
    [HttpPost("runs/{runId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelDetection(Guid runId, CancellationToken ct)
    {
        _logger.LogInformation("Cancelling detection run {RunId}", runId);
        await _detectorService.CancelDetectionAsync(runId, ct);
        return Ok(new { message = "Detection cancelled" });
    }

    #endregion

    #region Snapshots

    /// <summary>
    /// Create a new schema snapshot.
    /// </summary>
    [HttpPost("snapshots")]
    [ProducesResponseType(typeof(SchemaSnapshotDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSnapshot(
        [FromQuery] string snapshotType = "FULL",
        [FromQuery] string? schemaFilter = null,
        CancellationToken ct = default)
    {
        var userId = User.Identity?.Name ?? "System";
        _logger.LogInformation("Creating {Type} snapshot by {User}", snapshotType, userId);

        var snapshot = await _detectorService.CreateSnapshotAsync(snapshotType, schemaFilter, userId, ct);
        return CreatedAtAction(nameof(GetSnapshot), new { snapshotId = snapshot.SnapshotId }, snapshot);
    }

    /// <summary>
    /// Create a baseline snapshot for future comparisons.
    /// </summary>
    [HttpPost("snapshots/baseline")]
    [ProducesResponseType(typeof(SchemaSnapshotDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBaseline([FromBody] CreateBaselineRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Creating baseline snapshot by {User}", request.CreatedBy);
        var snapshot = await _detectorService.CreateBaselineAsync(request, ct);
        return CreatedAtAction(nameof(GetSnapshot), new { snapshotId = snapshot.SnapshotId }, snapshot);
    }

    /// <summary>
    /// Get recent snapshots.
    /// </summary>
    [HttpGet("snapshots")]
    [ProducesResponseType(typeof(IEnumerable<SchemaSnapshotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshots([FromQuery] int count = 20, CancellationToken ct = default)
    {
        var snapshots = await _detectorService.GetSnapshotsAsync(count, ct);
        return Ok(snapshots);
    }

    /// <summary>
    /// Get a specific snapshot.
    /// </summary>
    [HttpGet("snapshots/{snapshotId:guid}")]
    [ProducesResponseType(typeof(SchemaSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSnapshot(Guid snapshotId, CancellationToken ct)
    {
        // TODO: Implement GetSnapshotAsync
        return Ok(new { snapshotId });
    }

    /// <summary>
    /// Get the latest baseline snapshot.
    /// </summary>
    [HttpGet("snapshots/baseline")]
    [ProducesResponseType(typeof(SchemaSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestBaseline(CancellationToken ct)
    {
        var baseline = await _detectorService.GetLatestBaselineAsync(ct);
        if (baseline == null)
            return NotFound(new { message = "No baseline snapshot found" });

        return Ok(baseline);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Get schema change statistics for dashboard.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(SchemaChangeStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(CancellationToken ct)
    {
        var stats = await _detectorService.GetStatisticsAsync(ct);
        return Ok(stats);
    }

    #endregion
}
