// =============================================================================
// Agent #5: Post-Approval Pipeline - REST Controller
// API endpoints for post-approval pipeline operations
// =============================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Enterprise.Documentation.Core.Application.Services.PostApproval;
using Enterprise.Documentation.Api.Hubs;

namespace Enterprise.Documentation.Api.Controllers;

[ApiController]
[Route("api/post-approval")]
[Produces("application/json")]
public class PostApprovalController : ControllerBase
{
    private readonly IPostApprovalOrchestrator _orchestrator;
    private readonly IColumnLineageService _lineageService;
    private readonly IMetadataStampingService _stampingService;
    private readonly IHubContext<DocumentationHub> _hubContext;
    private readonly ILogger<PostApprovalController> _logger;

    public PostApprovalController(
        IPostApprovalOrchestrator orchestrator,
        IColumnLineageService lineageService,
        IMetadataStampingService stampingService,
        IHubContext<DocumentationHub> hubContext,
        ILogger<PostApprovalController> logger)
    {
        _orchestrator = orchestrator;
        _lineageService = lineageService;
        _stampingService = stampingService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Execute the complete post-approval pipeline for a document
    /// </summary>
    [HttpPost("execute/{approvalId:int}")]
    [ProducesResponseType(typeof(PostApprovalResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PostApprovalResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PostApprovalResult>> ExecutePipeline(
        int approvalId,
        [FromBody] ExecutePipelineRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Executing post-approval pipeline for {ApprovalId}", approvalId);

        var result = await _orchestrator.ExecuteAsync(
            approvalId,
            request.ApprovedBy,
            request.Comments,
            ct);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Retry a failed pipeline step
    /// </summary>
    [HttpPost("retry/{approvalId:int}/{stepName}")]
    [ProducesResponseType(typeof(PostApprovalResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PostApprovalResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PostApprovalResult>> RetryStep(
        int approvalId,
        string stepName,
        CancellationToken ct)
    {
        var result = await _orchestrator.RetryStepAsync(approvalId, stepName, ct);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Extract column lineage from a stored procedure
    /// </summary>
    [HttpPost("lineage/extract")]
    [ProducesResponseType(typeof(LineageExtractionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LineageExtractionResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LineageExtractionResult>> ExtractLineage(
        [FromBody] ExtractLineageRequest request,
        CancellationToken ct)
    {
        var result = await _lineageService.ExtractLineageAsync(
            request.SchemaName,
            request.ProcedureName,
            request.Definition,
            ct);

        if (!result.Success)
            return BadRequest(result);

        // Notify via SignalR
        await _hubContext.NotifyLineageExtracted(
            request.SchemaName,
            request.ProcedureName,
            result.ColumnLineages.Count);

        return Ok(result);
    }

    /// <summary>
    /// Get impact analysis for a column change
    /// </summary>
    [HttpGet("lineage/impact/{schema}/{table}/{column}")]
    [ProducesResponseType(typeof(ImpactAnalysisResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ImpactAnalysisResult>> GetImpactAnalysis(
        string schema,
        string table,
        string column,
        CancellationToken ct)
    {
        var result = await _lineageService.AnalyzeImpactAsync(schema, table, column, ct);

        // Notify via SignalR
        await _hubContext.NotifyImpactAnalysisComplete(result);

        return Ok(result);
    }

    /// <summary>
    /// Get downstream dependencies for an object
    /// </summary>
    [HttpGet("lineage/downstream/{schema}/{objectName}")]
    [ProducesResponseType(typeof(List<LineageDependency>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LineageDependency>>> GetDownstreamDependencies(
        string schema,
        string objectName,
        CancellationToken ct)
    {
        var dependencies = await _lineageService.GetDownstreamDependenciesAsync(schema, objectName, ct);
        return Ok(dependencies);
    }

    /// <summary>
    /// Read Shadow Metadata from a document
    /// </summary>
    [HttpGet("shadow-metadata")]
    [ProducesResponseType(typeof(ShadowMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShadowMetadata>> GetShadowMetadata(
        [FromQuery] string documentPath,
        CancellationToken ct)
    {
        var metadata = await _stampingService.ReadShadowMetadataAsync(documentPath, ct);

        if (metadata == null)
            return NotFound("Document not found or no Shadow Metadata present");

        return Ok(metadata);
    }

    /// <summary>
    /// Validate document sync status
    /// </summary>
    [HttpGet("sync-status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> ValidateSyncStatus(
        [FromQuery] string documentPath,
        CancellationToken ct)
    {
        var status = await _stampingService.ValidateSyncStatusAsync(documentPath, ct);
        return Ok(new { Status = status.ToString(), Path = documentPath });
    }
}

#region Request DTOs

public class ExecutePipelineRequest
{
    public string ApprovedBy { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

public class ExtractLineageRequest
{
    public string SchemaName { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
}

#endregion
