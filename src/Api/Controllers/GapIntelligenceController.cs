// =============================================================================
// Agent #7: Gap Intelligence Agent - REST Controller
// API endpoints for gap detection, analysis, and RLHF feedback
// =============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Enterprise.Documentation.Core.Application.Services.GapIntelligence;
using Enterprise.Documentation.Api.Hubs;

namespace Enterprise.Documentation.Api.Controllers;

/// <summary>
/// REST API controller for Gap Intelligence Agent.
/// Provides endpoints for detection, analysis, clustering, and RLHF feedback.
/// </summary>
[ApiController]
[Route("api/gap-intelligence")]
[Produces("application/json")]
public class GapIntelligenceController : ControllerBase
{
    private readonly IGapIntelligenceAgent _agent;
    private readonly IQueryPatternMiner _queryMiner;
    private readonly IHubContext<GapIntelligenceHub> _hubContext;
    private readonly ILogger<GapIntelligenceController> _logger;

    public GapIntelligenceController(
        IGapIntelligenceAgent agent,
        IQueryPatternMiner queryMiner,
        IHubContext<GapIntelligenceHub> hubContext,
        ILogger<GapIntelligenceController> logger)
    {
        _agent = agent;
        _queryMiner = queryMiner;
        _hubContext = hubContext;
        _logger = logger;
    }

    #region Detection Endpoints

    /// <summary>
    /// Run full gap detection across all database objects
    /// </summary>
    [HttpPost("detection/full")]
    [ProducesResponseType(typeof(GapDetectionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GapDetectionResult>> RunFullDetection(CancellationToken ct)
    {
        _logger.LogInformation("Starting full gap detection via API");
        await _hubContext.NotifyDetectionStarted("FULL");

        var result = await _agent.RunFullDetectionAsync(ct);

        await _hubContext.NotifyDetectionCompleted(result);
        return Ok(result);
    }

    /// <summary>
    /// Run incremental detection for recently modified objects
    /// </summary>
    [HttpPost("detection/incremental")]
    [ProducesResponseType(typeof(GapDetectionResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GapDetectionResult>> RunIncrementalDetection([FromQuery] DateTime? since, CancellationToken ct)
    {
        var sinceDate = since ?? DateTime.UtcNow.AddHours(-4);
        _logger.LogInformation("Starting incremental detection since {Since}", sinceDate);

        await _hubContext.NotifyDetectionStarted("INCREMENTAL");
        var result = await _agent.RunIncrementalDetectionAsync(sinceDate, ct);
        await _hubContext.NotifyDetectionCompleted(result);

        return Ok(result);
    }

    /// <summary>
    /// Detect gaps for a specific database object
    /// </summary>
    [HttpGet("detection/object/{schema}/{objectName}")]
    [ProducesResponseType(typeof(List<DetectedGap>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DetectedGap>>> DetectObjectGaps(string schema, string objectName, CancellationToken ct)
    {
        var gaps = await _agent.DetectGapsForObjectAsync(schema, objectName, ct);
        return Ok(gaps);
    }

    #endregion

    #region Dashboard Endpoints

    /// <summary>
    /// Get dashboard summary data
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(GapDashboardData), StatusCodes.Status200OK)]
    public async Task<ActionResult<GapDashboardData>> GetDashboard(CancellationToken ct)
    {
        var data = await _agent.GetDashboardDataAsync(ct);
        return Ok(data);
    }

    /// <summary>
    /// Get all active detection patterns
    /// </summary>
    [HttpGet("patterns")]
    [ProducesResponseType(typeof(List<GapPattern>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GapPattern>>> GetPatterns(CancellationToken ct)
    {
        var patterns = await _agent.GetActivePatternsAsync(ct);
        return Ok(patterns);
    }

    /// <summary>
    /// Get documentation velocity metrics
    /// </summary>
    [HttpGet("velocity/{groupType}/{groupName}")]
    [ProducesResponseType(typeof(DocumentationVelocity), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentationVelocity>> GetVelocity(string groupType, string groupName, CancellationToken ct)
    {
        var velocity = await _agent.GetVelocityMetricsAsync(groupType, groupName, ct);
        return Ok(velocity);
    }

    #endregion

    #region Analysis Endpoints

    /// <summary>
    /// Get future gap predictions
    /// </summary>
    [HttpGet("predictions")]
    [ProducesResponseType(typeof(List<PredictedGap>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PredictedGap>>> GetPredictions([FromQuery] int daysAhead = 30, CancellationToken ct = default)
    {
        var predictions = await _agent.PredictFutureGapsAsync(daysAhead, ct);
        return Ok(predictions);
    }

    /// <summary>
    /// Calculate importance score for an object
    /// </summary>
    [HttpGet("analysis/importance/{schema}/{objectName}")]
    [ProducesResponseType(typeof(ObjectImportanceScore), StatusCodes.Status200OK)]
    public async Task<ActionResult<ObjectImportanceScore>> GetImportance(string schema, string objectName, CancellationToken ct)
    {
        var score = await _agent.CalculateImportanceScoreAsync(schema, objectName, ct);
        return Ok(score);
    }

    /// <summary>
    /// Refresh usage heatmap from DMVs
    /// </summary>
    [HttpPost("analysis/refresh-heatmap")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RefreshHeatmap(CancellationToken ct)
    {
        await _agent.RefreshUsageHeatmapAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Find undocumented hotspots (high usage, no docs)
    /// </summary>
    [HttpGet("analysis/hotspots")]
    [ProducesResponseType(typeof(List<UndocumentedHotspot>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UndocumentedHotspot>>> GetHotspots(CancellationToken ct)
    {
        var hotspots = await _queryMiner.FindUndocumentedHotspotsAsync(ct);
        return Ok(hotspots);
    }

    #endregion

    #region Clustering Endpoints

    /// <summary>
    /// Run semantic clustering
    /// </summary>
    [HttpPost("clustering/run")]
    [ProducesResponseType(typeof(ClusteringResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClusteringResult>> RunClustering(CancellationToken ct)
    {
        var result = await _agent.RunSemanticClusteringAsync(ct);
        await _hubContext.NotifyClusteringCompleted(result);
        return Ok(result);
    }

    /// <summary>
    /// Get cluster outliers (undocumented objects in documented clusters)
    /// </summary>
    [HttpGet("clustering/outliers")]
    [ProducesResponseType(typeof(List<ClusterGap>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ClusterGap>>> GetClusterOutliers(CancellationToken ct)
    {
        var outliers = await _agent.FindClusterOutliersAsync(ct);
        return Ok(outliers);
    }

    #endregion

    #region Feedback Endpoints (RLHF)

    /// <summary>
    /// Record human feedback on a detected gap
    /// </summary>
    [HttpPost("gaps/feedback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordFeedback([FromBody] GapFeedbackRequest request, CancellationToken ct)
    {
        var feedback = new GapFeedback
        {
            SchemaName = request.SchemaName,
            ObjectName = request.ObjectName,
            PatternId = request.PatternId,
            DetectedGapType = request.DetectedGapType,
            DetectedConfidence = request.DetectedConfidence,
            FeedbackType = request.FeedbackType,
            FeedbackBy = request.FeedbackBy ?? "api-user",
            FeedbackReason = request.FeedbackReason
        };

        await _agent.RecordFeedbackAsync(feedback, ct);
        await _hubContext.NotifyFeedbackRecorded(request.SchemaName, request.ObjectName, request.FeedbackType);

        return NoContent();
    }

    #endregion
}

#region Request DTOs

/// <summary>
/// Request to record gap feedback
/// </summary>
public class GapFeedbackRequest
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public int? PatternId { get; set; }
    public string DetectedGapType { get; set; } = string.Empty;
    public decimal DetectedConfidence { get; set; }
    public string FeedbackType { get; set; } = string.Empty;
    public string? FeedbackBy { get; set; }
    public string? FeedbackReason { get; set; }
}

#endregion
