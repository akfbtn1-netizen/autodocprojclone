using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Application.Services;
using Enterprise.Documentation.Api.Models;
using Enterprise.Documentation.Api.Hubs;
using Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.DTOs;
using Enterprise.Documentation.Core.Application.DTOs.Approval;
using CoreEditDecision = Enterprise.Documentation.Core.Application.DTOs.Approval.CoreEditDecision;
using CoreUpdateDocumentRequest = Enterprise.Documentation.Core.Application.DTOs.Approval.CoreUpdateDocumentRequest;
using CoreSuggestion = Enterprise.Documentation.Core.Application.DTOs.Approval.CoreSuggestion;
using Microsoft.AspNetCore.Http;

namespace Enterprise.Documentation.Api.Controllers;

[ApiController]
[Route("api/approval-workflow")]
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly IDocumentGenerationPipeline _pipeline;
    private readonly IMasterIndexRepository _masterIndex;
    private readonly IApprovalNotifier _notifier;
    private readonly ILogger<ApprovalController> _logger;

    public ApprovalController(
        IApprovalService approvalService,
        IDocumentGenerationPipeline pipeline,
        IMasterIndexRepository masterIndex,
        IApprovalNotifier notifier,
        ILogger<ApprovalController> logger)
    {
        _approvalService = approvalService;
        _pipeline = pipeline;
        _masterIndex = masterIndex;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// Get all pending approvals with pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ApprovalSummary>), 200)]
    public async Task<IActionResult> GetApprovals(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var approvals = await _approvalService.GetApprovalsAsync(page, pageSize, status, cancellationToken);
        return Ok(approvals);
    }

    /// <summary>
    /// Approve document and update MasterIndex.
    /// </summary>
    [HttpPut("{id}/approve")]
    [ProducesResponseType(typeof(Enterprise.Documentation.Core.Application.DTOs.ApprovalResult), 200)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken cancellationToken)
    {
        // Temporarily disabled for compilation
        return BadRequest("Method temporarily disabled");
        /*
        try
        {
            var result = await _approvalService.ApproveAsync(id, new ApprovalDecision
            {
                Comments = request.Comments,
                ApprovedBy = request.ApprovedBy ?? "Unknown"
            }, cancellationToken);

            if (result.Success)
            {
                // Update MasterIndex status
                var approval = await _approvalService.GetByIdAsync(id, cancellationToken);
                if (approval?.MetadataId != null)
                {
                    await _masterIndex.UpdateFieldsAsync(approval.MetadataId.Value, new Dictionary<string, object?>
                    {
                        ["ApprovalStatus"] = "Approved",
                        ["ApprovedBy"] = request.ApprovedBy,
                        ["ApprovedDate"] = DateTime.UtcNow,
                        ["ApprovalComments"] = request.Comments,
                        ["ModifiedDate"] = DateTime.UtcNow,
                        ["ModifiedBy"] = request.ApprovedBy
                    }, cancellationToken);
                }

                // Notify real-time
                await _notifier.NotifyApprovalDecision(id, "Approved", request.ApprovedBy ?? "Unknown");

                _logger.LogInformation("Document {ApprovalId} approved by {ApprovedBy}", id, request.ApprovedBy);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving document {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to approve document", details = ex.Message });
        }
        */
    }

    /// <summary>
    /// Reject document with reason.
    /// </summary>
    [HttpPut("{id}/reject")]
    [ProducesResponseType(typeof(Enterprise.Documentation.Core.Application.DTOs.ApprovalResult), 200)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new { error = "Rejection reason is required" });
            }

            var result = await _approvalService.RejectAsync(id, new RejectionDecision
            {
                Reason = request.Reason,
                RejectedBy = request.RejectedBy ?? "Unknown"
            }, cancellationToken);

            if (result.Success)
            {
                // Update MasterIndex status
                var approval = await _approvalService.GetByIdAsync(id, cancellationToken);
                if (approval?.MetadataId != null)
                {
                    await _masterIndex.UpdateFieldsAsync(approval.MetadataId.Value, new Dictionary<string, object?>
                    {
                        ["ApprovalStatus"] = "Rejected",
                        ["RejectionReason"] = request.Reason,
                        ["ModifiedDate"] = DateTime.UtcNow,
                        ["ModifiedBy"] = request.RejectedBy
                    }, cancellationToken);
                }

                // Notify real-time
                await _notifier.NotifyApprovalDecision(id, "Rejected", request.RejectedBy ?? "Unknown");

                _logger.LogInformation("Document {ApprovalId} rejected by {RejectedBy}: {Reason}", 
                    id, request.RejectedBy, request.Reason);
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting document {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to reject document", details = ex.Message });
        }
    }

    /// <summary>
    /// Edit document content and track changes.
    /// </summary>
    [HttpPut("{id}/edit")]
    [ProducesResponseType(typeof(EditResult), 200)]
    public async Task<IActionResult> Edit(
        Guid id,
        [FromBody] EditRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Store original for diff tracking (AI training)
            var approval = await _approvalService.GetByIdAsync(id, cancellationToken);
            if (approval == null)
            {
                return NotFound(new { error = "Approval not found" });
            }

            var editResult = await _approvalService.EditAsync(id, new Enterprise.Documentation.Core.Application.DTOs.EditDecision
            {
                EditedBy = request.EditedBy ?? "Unknown",
                Changes = request.Reason ?? "No reason provided"
            }, cancellationToken);

            if (editResult.Success)
            {
                // Update MasterIndex with edit tracking
                if (approval.MetadataId != null)
                {
                    await _masterIndex.UpdateFieldsAsync(approval.MetadataId.Value, new Dictionary<string, object?>
                    {
                        ["ApprovalStatus"] = "Edited",
                        ["ModifiedDate"] = DateTime.UtcNow,
                        ["ModifiedBy"] = request.EditedBy,
                        ["LastRegenerationFeedback"] = request.Reason
                    }, cancellationToken);
                }

                _logger.LogInformation("Document {ApprovalId} edited by {EditedBy}", id, request.EditedBy);
            }

            return editResult.Success ? Ok(editResult) : BadRequest(editResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing document {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to edit document", details = ex.Message });
        }
    }

    /// <summary>
    /// Regenerate document with feedback/guidance.
    /// </summary>
    [HttpPost("{id}/reprompt")]
    [ProducesResponseType(typeof(RepromptResult), 200)]
    public async Task<IActionResult> Reprompt(
        Guid id,
        [FromBody] RepromptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var approval = await _approvalService.GetByIdAsync(id, cancellationToken);
            if (approval == null)
            {
                return NotFound(new { error = "Approval not found" });
            }

            // Get original Excel entry for reprompt
            var excelEntry = await _approvalService.GetOriginalEntryAsync(id, cancellationToken);
            if (excelEntry == null)
            {
                return BadRequest(new { error = "Original Excel entry not found for reprompt" });
            }

            // Enhance the Excel entry with feedback
            excelEntry.Description += $"\n\nREVISION FEEDBACK: {request.Guidance}";

            // Regenerate document
            var generationResult = await _pipeline.GenerateDocumentAsync(excelEntry, cancellationToken);

            if (generationResult.Success)
            {
                // Update approval with new document
                await _approvalService.UpdateDocumentAsync(id, new Enterprise.Documentation.Core.Application.DTOs.UpdateDocumentRequest
                {
                    NewDocumentPath = generationResult.DocumentPath ?? "",
                    UpdatedBy = request.RequestedBy ?? "Unknown",
                    Comments = request.Guidance
                }, cancellationToken);

                // Update MasterIndex regeneration count
                if (approval.MetadataId != null)
                {
                    await _masterIndex.UpdateFieldsAsync(approval.MetadataId.Value, new Dictionary<string, object?>
                    {
                        ["RegenerationCount"] = "COALESCE(RegenerationCount, 0) + 1",
                        ["LastRegenerationFeedback"] = request.Guidance,
                        ["GeneratedDocPath"] = generationResult.DocumentPath,
                        ["ConfidenceScore"] = generationResult.ConfidenceScore,
                        ["ModifiedDate"] = DateTime.UtcNow,
                        ["ModifiedBy"] = request.RequestedBy
                    }, cancellationToken);
                }

                return Ok(new RepromptResult 
                { 
                    Success = true, 
                    NewDocumentPath = generationResult.DocumentPath,
                    ConfidenceScore = generationResult.ConfidenceScore,
                    TokensUsed = generationResult.TokensUsed
                });
            }

            return BadRequest(new RepromptResult 
            { 
                Success = false, 
                ErrorMessage = generationResult.ErrorMessage 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprompting document {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to reprompt document", details = ex.Message });
        }
    }

    /// <summary>
    /// Download or preview document.
    /// </summary>
    [HttpGet("{id}/document")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    public async Task<IActionResult> GetDocument(
        Guid id,
        [FromQuery] bool preview = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await _approvalService.GetByIdAsync(id, cancellationToken);
            if (approval == null)
            {
                return NotFound(new { error = "Approval not found" });
            }

            if (string.IsNullOrEmpty(approval.DocumentPath) || !System.IO.File.Exists(approval.DocumentPath))
            {
                return NotFound(new { error = "Document file not found" });
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(approval.DocumentPath, cancellationToken);
            var fileName = Path.GetFileName(approval.DocumentPath);

            var contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            var contentDisposition = preview ? "inline" : "attachment";

            Response.Headers["Content-Disposition"] = $"{contentDisposition}; filename=\"{fileName}\"";
            
            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document for approval {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to retrieve document", details = ex.Message });
        }
    }

    /// <summary>
    /// Add suggestion/comment without approving or rejecting.
    /// </summary>
    [HttpPost("{id}/suggestions")]
    [ProducesResponseType(typeof(SuggestionResult), 200)]
    public async Task<IActionResult> AddSuggestion(
        Guid id,
        [FromBody] SuggestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _approvalService.AddSuggestionAsync(id, new Enterprise.Documentation.Core.Application.DTOs.Suggestion
            {
                Content = request.Content ?? "",
                SuggestedBy = request.SuggestedBy ?? "Unknown"
            }, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Suggestion added to approval {ApprovalId} by {SuggestedBy}", 
                    id, request.SuggestedBy);
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding suggestion to approval {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to add suggestion", details = ex.Message });
        }
    }

    /// <summary>
    /// Get approval with full history and suggestions.
    /// </summary>
    [HttpGet("{id}/details")]
    [ProducesResponseType(typeof(ApprovalDetails), 200)]
    public async Task<IActionResult> GetDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var details = await _approvalService.GetDetailsAsync(id, cancellationToken);
            if (details == null)
            {
                return NotFound(new { error = "Approval not found" });
            }

            return Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval details {ApprovalId}", id);
            return StatusCode(500, new { error = "Failed to retrieve approval details", details = ex.Message });
        }
    }

    /// <summary>
    /// Get approval statistics and dashboard data.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApprovalStats), 200)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _approvalService.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics", details = ex.Message });
        }
    }
}

// Supporting interfaces and models for the approval service extensions
public interface IApprovalNotifier
{
    Task NotifyDocumentGenerated(string documentId, string title, decimal confidence);
    Task NotifyApprovalRequested(Guid approvalId, string title, string priority, string requester);
    Task NotifyApprovalDecision(Guid approvalId, string decision, string decidedBy);
}

public class EditDecision
{
    public object? NewContent { get; set; }
    public string EditedBy { get; set; } = string.Empty;
    public string? EditReason { get; set; }
}

public class Suggestion
{
    public string Content { get; set; } = string.Empty;
    public string SuggestedBy { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Priority { get; set; } = "Low";
}

public class UpdateDocumentRequest
{
    public string? NewDocumentPath { get; set; }
    public string? RegenerationFeedback { get; set; }
    public string? RegeneratedBy { get; set; }
}

public class EditResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ApprovalId { get; set; }
}

public class RepromptResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NewDocumentPath { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public int? TokensUsed { get; set; }
}

public class SuggestionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? SuggestionId { get; set; }
}

public class ApprovalDetails
{
    public Guid Id { get; set; }
    public string JiraNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? DocumentPath { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public List<ApprovalAction> History { get; set; } = new();
    public List<Suggestion> Suggestions { get; set; } = new();
}

public class ApprovalAction
{
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Comments { get; set; }
}

public class ApprovalStats
{
    public int TotalPending { get; set; }
    public int TotalApproved { get; set; }
    public int TotalRejected { get; set; }
    public decimal AverageConfidenceScore { get; set; }
    public int AverageSLAHours { get; set; }
    public Dictionary<string, int> ApprovalsByTier { get; set; } = new();
    public Dictionary<string, int> ApprovalsByDocumentType { get; set; } = new();
}

public class ApprovalSummary
{
    public Guid Id { get; set; }
    public string JiraNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public int? Tier { get; set; }
    public bool IsOverdue => DueDate.HasValue && DueDate < DateTime.UtcNow && Status == "Pending";
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}