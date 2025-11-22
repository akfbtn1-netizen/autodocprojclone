using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Application.Services.Batch;
using Enterprise.Documentation.Core.Domain.Entities;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Api.Controllers;

/// <summary>
/// API controller for batch processing operations
/// Supports multi-source batch documentation generation with confidence tracking
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BatchProcessingController : ControllerBase
{
    private readonly ILogger<BatchProcessingController> _logger;
    private readonly IBatchProcessingOrchestrator _orchestrator;

    public BatchProcessingController(
        ILogger<BatchProcessingController> logger,
        IBatchProcessingOrchestrator orchestrator)
    {
        _logger = logger;
        _orchestrator = orchestrator;
    }

    #region Start Batch Operations

    /// <summary>
    /// Start batch processing for a database schema
    /// </summary>
    /// <remarks>
    /// Enumerates all stored procedures, tables, views, and functions in the specified schema.
    /// Extracts metadata with confidence scoring and optionally generates documentation.
    ///
    /// Example:
    /// ```json
    /// {
    ///   "database": "IRFS1",
    ///   "schema": "gwpc",
    ///   "userId": "00000000-0000-0000-0000-000000000000",
    ///   "options": {
    ///     "confidenceThreshold": 0.85,
    ///     "requireHumanReviewBelowThreshold": true,
    ///     "generateDocuments": true,
    ///     "populateMasterIndex": true,
    ///     "generateEmbeddings": true
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <param name="request">Schema processing request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Batch job ID</returns>
    /// <response code="200">Returns the batch job ID</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("schema")]
    [ProducesResponseType(typeof(StartBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StartBatchResponse>> StartSchemaProcessing(
        [FromBody] StartSchemaProcessingRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting schema processing: Database={Database}, Schema={Schema}, UserId={UserId}",
            request.Database, request.Schema, request.UserId);

        try
        {
            var batchId = await _orchestrator.StartSchemaProcessingAsync(
                request.Database,
                request.Schema,
                request.UserId,
                request.Options,
                ct);

            // Queue background processing via Hangfire
            BackgroundJob.Enqueue(() => _orchestrator.ProcessBatchJobAsync(batchId, CancellationToken.None));

            return Ok(new StartBatchResponse
            {
                BatchId = batchId,
                Message = $"Schema processing started for {request.Database}.{request.Schema}",
                StatusUrl = Url.Action(nameof(GetBatchStatus), new { batchId })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start schema processing");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Start batch processing for a folder of .docx files
    /// </summary>
    /// <remarks>
    /// Scans a folder for existing .docx files and reverse-engineers metadata from documents.
    /// Useful for importing existing documentation into the system.
    ///
    /// Example:
    /// ```json
    /// {
    ///   "folderPath": "C:\\Documentation\\IRFS1\\gwpc",
    ///   "userId": "00000000-0000-0000-0000-000000000000",
    ///   "options": {
    ///     "confidenceThreshold": 0.70,
    ///     "populateMasterIndex": true,
    ///     "generateEmbeddings": true
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <param name="request">Folder processing request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Batch job ID</returns>
    [HttpPost("folder")]
    [ProducesResponseType(typeof(StartBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StartBatchResponse>> StartFolderProcessing(
        [FromBody] StartFolderProcessingRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting folder processing: FolderPath={FolderPath}, UserId={UserId}",
            request.FolderPath, request.UserId);

        try
        {
            var batchId = await _orchestrator.StartFolderProcessingAsync(
                request.FolderPath,
                request.UserId,
                request.Options,
                ct);

            // Queue background processing via Hangfire
            BackgroundJob.Enqueue(() => _orchestrator.ProcessBatchJobAsync(batchId, CancellationToken.None));

            return Ok(new StartBatchResponse
            {
                BatchId = batchId,
                Message = $"Folder processing started for {request.FolderPath}",
                StatusUrl = Url.Action(nameof(GetBatchStatus), new { batchId })
            });
        }
        catch (System.IO.DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex, "Folder not found: {FolderPath}", request.FolderPath);
            return NotFound(new { error = $"Folder not found: {request.FolderPath}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start folder processing");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Start batch processing from Excel spreadsheet
    /// </summary>
    /// <remarks>
    /// Imports completed Excel entries and generates documentation in bulk.
    /// Reads rows with Status="Completed" and DocId=NULL from the DocumentChanges table.
    ///
    /// Example:
    /// ```json
    /// {
    ///   "excelFilePath": "C:\\Users\\User\\Desktop\\Change Spreadsheet\\BI Analytics Change Spreadsheet.xlsx",
    ///   "userId": "00000000-0000-0000-0000-000000000000",
    ///   "options": {
    ///     "confidenceThreshold": 0.85,
    ///     "generateDocuments": true,
    ///     "populateMasterIndex": true
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <param name="request">Excel import request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Batch job ID</returns>
    [HttpPost("excel")]
    [ProducesResponseType(typeof(StartBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StartBatchResponse>> StartExcelImport(
        [FromBody] StartExcelImportRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting Excel import: FilePath={FilePath}, UserId={UserId}",
            request.ExcelFilePath, request.UserId);

        try
        {
            var batchId = await _orchestrator.StartExcelImportAsync(
                request.ExcelFilePath,
                request.UserId,
                request.Options,
                ct);

            // Queue background processing via Hangfire
            BackgroundJob.Enqueue(() => _orchestrator.ProcessBatchJobAsync(batchId, CancellationToken.None));

            return Ok(new StartBatchResponse
            {
                BatchId = batchId,
                Message = $"Excel import started from {request.ExcelFilePath}",
                StatusUrl = Url.Action(nameof(GetBatchStatus), new { batchId })
            });
        }
        catch (System.IO.FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Excel file not found: {FilePath}", request.ExcelFilePath);
            return NotFound(new { error = $"Excel file not found: {request.ExcelFilePath}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Excel import");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Batch Status and Management

    /// <summary>
    /// Get batch job status with detailed progress
    /// </summary>
    /// <param name="batchId">Batch job ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Batch job details</returns>
    [HttpGet("{batchId}")]
    [ProducesResponseType(typeof(BatchJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchJobDto>> GetBatchStatus(
        Guid batchId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting batch status: {BatchId}", batchId);

        try
        {
            var status = await _orchestrator.GetBatchStatusAsync(batchId, ct);
            return Ok(status);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Batch job not found: {BatchId}", batchId);
            return NotFound(new { error = $"Batch job {batchId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get batch status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all batch jobs with pagination and filtering
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="status">Filter by status (optional)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of batch jobs</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<BatchJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<BatchJobDto>>> GetAllBatches(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting all batches: Page={Page}, PageSize={PageSize}, Status={Status}",
            page, pageSize, status);

        try
        {
            BatchJobStatus? statusFilter = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BatchJobStatus>(status, out var parsedStatus))
            {
                statusFilter = parsedStatus;
            }

            var result = await _orchestrator.GetAllBatchesAsync(page, pageSize, statusFilter, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all batches");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a running batch job
    /// </summary>
    /// <param name="batchId">Batch job ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("{batchId}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelBatch(
        Guid batchId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Cancelling batch: {BatchId}", batchId);

        try
        {
            await _orchestrator.CancelBatchAsync(batchId, ct);
            return Ok(new { message = $"Batch {batchId} cancelled successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Batch job not found: {BatchId}", batchId);
            return NotFound(new { error = $"Batch job {batchId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel batch");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Retry failed items in a batch
    /// </summary>
    /// <param name="batchId">Batch job ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("{batchId}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryFailedItems(
        Guid batchId,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Retrying failed items: {BatchId}", batchId);

        try
        {
            await _orchestrator.RetryFailedItemsAsync(batchId, ct);

            // Queue background processing via Hangfire
            BackgroundJob.Enqueue(() => _orchestrator.ProcessBatchJobAsync(batchId, CancellationToken.None));

            return Ok(new { message = $"Failed items in batch {batchId} queued for retry" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Batch job not found: {BatchId}", batchId);
            return NotFound(new { error = $"Batch job {batchId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry items");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Human-in-Loop Workflow

    /// <summary>
    /// Get items requiring human review
    /// </summary>
    /// <param name="batchId">Batch job ID (optional, null for all batches)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of items requiring review</returns>
    [HttpGet("review")]
    [ProducesResponseType(typeof(List<BatchJobItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BatchJobItemDto>>> GetItemsRequiringReview(
        [FromQuery] Guid? batchId = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Getting items requiring review: BatchId={BatchId}", batchId);

        try
        {
            var items = batchId.HasValue
                ? await _orchestrator.GetItemsRequiringReviewAsync(batchId.Value, ct)
                : await GetAllItemsRequiringReviewAsync(ct);

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get items requiring review");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Approve items for processing
    /// </summary>
    /// <param name="request">Approval request with item IDs</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("review/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveItems(
        [FromBody] ApproveItemsRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Approving {Count} items by user {UserId}",
            request.ItemIds.Count, request.ReviewedBy);

        try
        {
            await _orchestrator.ApproveItemsAsync(request.ItemIds, request.ReviewedBy, ct);
            return Ok(new { message = $"Approved {request.ItemIds.Count} items successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve items");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reject items with feedback
    /// </summary>
    /// <param name="request">Rejection request with item IDs and reason</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    [HttpPost("review/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectItems(
        [FromBody] RejectItemsRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Rejecting {Count} items by user {UserId}: {Reason}",
            request.ItemIds.Count, request.ReviewedBy, request.Reason);

        try
        {
            await _orchestrator.RejectItemsAsync(request.ItemIds, request.Reason, request.ReviewedBy, ct);
            return Ok(new { message = $"Rejected {request.ItemIds.Count} items successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject items");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Helper Methods

    private async Task<List<BatchJobItemDto>> GetAllItemsRequiringReviewAsync(CancellationToken ct)
    {
        // Get all batches and aggregate items requiring review
        var batches = await _orchestrator.GetAllBatchesAsync(1, 1000, null, ct);
        var allItems = new List<BatchJobItemDto>();

        foreach (var batch in batches.Items)
        {
            var items = await _orchestrator.GetItemsRequiringReviewAsync(batch.BatchId, ct);
            allItems.AddRange(items);
        }

        return allItems;
    }

    #endregion
}

#region Request/Response Models

/// <summary>
/// Request to start schema processing
/// </summary>
public class StartSchemaProcessingRequest
{
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public BatchProcessingOptions? Options { get; set; }
}

/// <summary>
/// Request to start folder processing
/// </summary>
public class StartFolderProcessingRequest
{
    public string FolderPath { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public BatchProcessingOptions? Options { get; set; }
}

/// <summary>
/// Request to start Excel import
/// </summary>
public class StartExcelImportRequest
{
    public string ExcelFilePath { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public BatchProcessingOptions? Options { get; set; }
}

/// <summary>
/// Response when starting a batch
/// </summary>
public class StartBatchResponse
{
    public Guid BatchId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StatusUrl { get; set; }
}

/// <summary>
/// Request to approve items
/// </summary>
public class ApproveItemsRequest
{
    public List<Guid> ItemIds { get; set; } = new();
    public Guid ReviewedBy { get; set; }
}

/// <summary>
/// Request to reject items
/// </summary>
public class RejectItemsRequest
{
    public List<Guid> ItemIds { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public Guid ReviewedBy { get; set; }
}

#endregion
