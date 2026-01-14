# ============================================================================
# APPROVAL WORKFLOW BUILD SCRIPT - PART 2
# Services, API, Teams Integration
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$srcPath = Join-Path $projectRoot "src"
$corePath = Join-Path $srcPath "Core"
$apiPath = Join-Path $srcPath "Api"

$utf8NoBom = New-Object System.Text.UTF8Encoding $false

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  BUILDING APPROVAL WORKFLOW - PART 2" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# PART 4: DTOs (Data Transfer Objects)
# ============================================================================

Write-Host "[4/10] Creating DTOs..." -ForegroundColor Yellow

$dtosContent = @'
using System;
using System.Collections.Generic;
using Enterprise.Documentation.Core.Domain.Models.Approval;

namespace Enterprise.Documentation.Core.Application.DTOs.Approval;

// ============================================================================
// Request DTOs
// ============================================================================

/// <summary>
/// Request to create a new document approval entry.
/// </summary>
public record CreateApprovalRequest
{
    public string DocumentId { get; init; } = string.Empty;
    public int? MasterIndexId { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public string SchemaName { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string? TemplateUsed { get; init; }
    public string? CABNumber { get; init; }
    public string GeneratedFilePath { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public ApprovalPriority Priority { get; init; } = ApprovalPriority.Normal;
}

/// <summary>
/// Request to approve a document.
/// </summary>
public record ApproveDocumentRequest
{
    public string ApprovedBy { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public bool MoveToDestination { get; init; } = true;
}

/// <summary>
/// Request to reject a document.
/// </summary>
public record RejectDocumentRequest
{
    public string RejectedBy { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Request to edit and approve a document.
/// </summary>
public record EditAndApproveRequest
{
    public string EditedBy { get; init; } = string.Empty;
    public List<DocumentEditDto> Edits { get; init; } = new();
    public string? ApprovalNotes { get; init; }
}

/// <summary>
/// Single edit to a document section.
/// </summary>
public record DocumentEditDto
{
    public string SectionName { get; init; } = string.Empty;
    public string? OriginalText { get; init; }
    public string EditedText { get; init; } = string.Empty;
    public string? EditReason { get; init; }
    public string Category { get; init; } = "Other";
}

/// <summary>
/// Request to regenerate a document with feedback.
/// </summary>
public record RegenerateDocumentRequest
{
    public string RequestedBy { get; init; } = string.Empty;
    public string FeedbackText { get; init; } = string.Empty;
    public string? FeedbackSection { get; init; }
    public string? AdditionalContext { get; init; }
}

// ============================================================================
// Response DTOs
// ============================================================================

/// <summary>
/// Response with approval details.
/// </summary>
public record ApprovalResponse
{
    public int Id { get; init; }
    public string DocumentId { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string SchemaName { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string? TemplateUsed { get; init; }
    public string? CABNumber { get; init; }
    public string GeneratedFilePath { get; init; } = string.Empty;
    public string? DestinationPath { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public string? AssignedTo { get; init; }
    public DateTime? DueDate { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? ResolutionNotes { get; init; }
    public int Version { get; init; }
    public long? FileSizeBytes { get; init; }
}

/// <summary>
/// Response for pending approvals list.
/// </summary>
public record PendingApprovalsResponse
{
    public List<ApprovalResponse> Approvals { get; init; } = new();
    public int TotalCount { get; init; }
    public int PendingCount { get; init; }
    public int UrgentCount { get; init; }
}

/// <summary>
/// Response with approval history.
/// </summary>
public record ApprovalHistoryResponse
{
    public int ApprovalId { get; init; }
    public string DocumentId { get; init; } = string.Empty;
    public List<HistoryEntryDto> History { get; init; } = new();
}

/// <summary>
/// Single history entry.
/// </summary>
public record HistoryEntryDto
{
    public string Action { get; init; } = string.Empty;
    public string ActionBy { get; init; } = string.Empty;
    public DateTime ActionAt { get; init; }
    public string? PreviousStatus { get; init; }
    public string? NewStatus { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Response with edit history for AI training.
/// </summary>
public record EditHistoryResponse
{
    public string DocumentId { get; init; } = string.Empty;
    public List<EditEntryDto> Edits { get; init; } = new();
    public int TotalEdits { get; init; }
}

/// <summary>
/// Single edit entry.
/// </summary>
public record EditEntryDto
{
    public string SectionName { get; init; } = string.Empty;
    public string? OriginalText { get; init; }
    public string? EditedText { get; init; }
    public string? EditReason { get; init; }
    public string Category { get; init; } = string.Empty;
    public string EditedBy { get; init; } = string.Empty;
    public DateTime EditedAt { get; init; }
}
'@

$dtosPath = Join-Path $corePath "Application\DTOs\Approval"
if (-not (Test-Path $dtosPath)) {
    New-Item -ItemType Directory -Path $dtosPath -Force | Out-Null
}
$dtosFilePath = Join-Path $dtosPath "ApprovalDTOs.cs"
[System.IO.File]::WriteAllText($dtosFilePath, $dtosContent, $utf8NoBom)
Write-Host "  Created: ApprovalDTOs.cs" -ForegroundColor Green

# ============================================================================
# PART 5: Service Interface
# ============================================================================

Write-Host "[5/10] Creating service interface..." -ForegroundColor Yellow

$serviceInterface = @'
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Application.DTOs.Approval;
using Enterprise.Documentation.Core.Domain.Models.Approval;

namespace Enterprise.Documentation.Core.Application.Interfaces.Approval;

/// <summary>
/// Service for managing document approval workflow.
/// </summary>
public interface IApprovalService
{
    // ===== Queue Management =====

    /// <summary>
    /// Creates a new approval entry when a document is generated.
    /// </summary>
    Task<ApprovalResponse> CreateApprovalAsync(
        CreateApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending approvals.
    /// </summary>
    Task<PendingApprovalsResponse> GetPendingApprovalsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific approval by ID.
    /// </summary>
    Task<ApprovalResponse?> GetApprovalByIdAsync(
        int approvalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets approval by document ID.
    /// </summary>
    Task<ApprovalResponse?> GetApprovalByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    // ===== Approval Actions =====

    /// <summary>
    /// Approves a document and moves it to the destination folder.
    /// </summary>
    Task<ApprovalResponse> ApproveDocumentAsync(
        int approvalId,
        ApproveDocumentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a document with a reason.
    /// </summary>
    Task<ApprovalResponse> RejectDocumentAsync(
        int approvalId,
        RejectDocumentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits and approves a document, tracking all changes.
    /// </summary>
    Task<ApprovalResponse> EditAndApproveDocumentAsync(
        int approvalId,
        EditAndApproveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests regeneration of a document with feedback.
    /// </summary>
    Task<ApprovalResponse> RequestRegenerationAsync(
        int approvalId,
        RegenerateDocumentRequest request,
        CancellationToken cancellationToken = default);

    // ===== History & Analytics =====

    /// <summary>
    /// Gets the approval history for a document.
    /// </summary>
    Task<ApprovalHistoryResponse> GetApprovalHistoryAsync(
        int approvalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all edits made to a document (for AI training).
    /// </summary>
    Task<EditHistoryResponse> GetEditHistoryAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    // ===== Document ID Generation =====

    /// <summary>
    /// Generates a new document ID following naming conventions.
    /// Format: {TYPE}-{YYYY}-{NNN}
    /// </summary>
    Task<string> GenerateDocumentIdAsync(
        string documentType,
        CancellationToken cancellationToken = default);
}
'@

$interfacePath = Join-Path $corePath "Application\Interfaces\Approval\IApprovalService.cs"
[System.IO.File]::WriteAllText($interfacePath, $serviceInterface, $utf8NoBom)
Write-Host "  Created: IApprovalService.cs" -ForegroundColor Green

# ============================================================================
# PART 6: Teams Notification Service
# ============================================================================

Write-Host "[6/10] Creating Teams notification service..." -ForegroundColor Yellow

$teamsService = @'
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.Approval;

/// <summary>
/// Service for sending Teams notifications via webhooks.
/// </summary>
public interface ITeamsNotificationService
{
    Task SendApprovalNotificationAsync(
        string documentId,
        string objectName,
        string documentType,
        string requestedBy,
        int approvalId,
        CancellationToken cancellationToken = default);

    Task SendApprovalResultNotificationAsync(
        string documentId,
        string objectName,
        string action, // "Approved", "Rejected", "Regeneration Requested"
        string actionBy,
        string? notes,
        CancellationToken cancellationToken = default);
}

public class TeamsNotificationService : ITeamsNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TeamsNotificationService> _logger;
    private readonly string? _webhookUrl;
    private readonly string _dashboardUrl;

    public TeamsNotificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TeamsNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _webhookUrl = configuration["Teams:WebhookUrl"];
        _dashboardUrl = configuration["Teams:DashboardUrl"] ?? "http://localhost:5000/approval";
    }

    public async Task SendApprovalNotificationAsync(
        string documentId,
        string objectName,
        string documentType,
        string requestedBy,
        int approvalId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
        {
            _logger.LogWarning("Teams webhook URL not configured, skipping notification");
            return;
        }

        var card = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = "📄 New Document Awaiting Approval",
                                weight = "bolder",
                                size = "large"
                            },
                            new
                            {
                                type = "FactSet",
                                facts = new[]
                                {
                                    new { title = "Document ID", value = documentId },
                                    new { title = "Object", value = objectName },
                                    new { title = "Type", value = documentType },
                                    new { title = "Requested By", value = requestedBy },
                                    new { title = "Time", value = DateTime.Now.ToString("g") }
                                }
                            }
                        },
                        actions = new[]
                        {
                            new
                            {
                                type = "Action.OpenUrl",
                                title = "Review Document",
                                url = $"{_dashboardUrl}/{approvalId}"
                            }
                        }
                    }
                }
            }
        };

        await SendTeamsMessageAsync(card, cancellationToken);
        _logger.LogInformation("Sent Teams notification for document {DocumentId}", documentId);
    }

    public async Task SendApprovalResultNotificationAsync(
        string documentId,
        string objectName,
        string action,
        string actionBy,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
        {
            _logger.LogWarning("Teams webhook URL not configured, skipping notification");
            return;
        }

        var emoji = action switch
        {
            "Approved" => "✅",
            "Rejected" => "❌",
            "Regeneration Requested" => "🔄",
            _ => "📄"
        };

        var color = action switch
        {
            "Approved" => "good",
            "Rejected" => "attention",
            _ => "default"
        };

        var card = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = $"{emoji} Document {action}",
                                weight = "bolder",
                                size = "large",
                                color = color
                            },
                            new
                            {
                                type = "FactSet",
                                facts = new[]
                                {
                                    new { title = "Document ID", value = documentId },
                                    new { title = "Object", value = objectName },
                                    new { title = "Action By", value = actionBy },
                                    new { title = "Time", value = DateTime.Now.ToString("g") }
                                }
                            },
                            string.IsNullOrEmpty(notes) ? null : new
                            {
                                type = "TextBlock",
                                text = $"Notes: {notes}",
                                wrap = true
                            }
                        }.Where(x => x != null).ToArray()
                    }
                }
            }
        };

        await SendTeamsMessageAsync(card, cancellationToken);
        _logger.LogInformation("Sent Teams result notification for document {DocumentId}: {Action}", documentId, action);
    }

    private async Task SendTeamsMessageAsync(object card, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(card);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Teams webhook failed: {StatusCode} - {Error}", response.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Teams notification");
        }
    }
}
'@

$teamsServicePath = Join-Path $corePath "Application\Services\Approval\TeamsNotificationService.cs"
[System.IO.File]::WriteAllText($teamsServicePath, $teamsService, $utf8NoBom)
Write-Host "  Created: TeamsNotificationService.cs" -ForegroundColor Green

# ============================================================================
# PART 7: API Controller
# ============================================================================

Write-Host "[7/10] Creating API controller..." -ForegroundColor Yellow

$apiController = @'
using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Application.DTOs.Approval;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Api.Controllers.Approval;

/// <summary>
/// API endpoints for document approval workflow.
/// </summary>
[ApiController]
[Route("api/[controller]")]
// [Authorize] // Uncomment when auth is configured
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly ILogger<ApprovalController> _logger;

    public ApprovalController(
        IApprovalService approvalService,
        ILogger<ApprovalController> logger)
    {
        _approvalService = approvalService;
        _logger = logger;
    }

    /// <summary>
    /// Get all pending approvals.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(PendingApprovalsResponse), 200)]
    public async Task<ActionResult<PendingApprovalsResponse>> GetPendingApprovals(
        CancellationToken cancellationToken)
    {
        var result = await _approvalService.GetPendingApprovalsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific approval by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApprovalResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApprovalResponse>> GetApproval(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _approvalService.GetApprovalByIdAsync(id, cancellationToken);

        if (result == null)
            return NotFound($"Approval {id} not found");

        return Ok(result);
    }

    /// <summary>
    /// Get approval by document ID.
    /// </summary>
    [HttpGet("document/{documentId}")]
    [ProducesResponseType(typeof(ApprovalResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApprovalResponse>> GetApprovalByDocumentId(
        string documentId,
        CancellationToken cancellationToken)
    {
        var result = await _approvalService.GetApprovalByDocumentIdAsync(documentId, cancellationToken);

        if (result == null)
            return NotFound($"Document {documentId} not found");

        return Ok(result);
    }

    /// <summary>
    /// Approve a document.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(typeof(ApprovalResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApprovalResponse>> ApproveDocument(
        int id,
        [FromBody] ApproveDocumentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _approvalService.ApproveDocumentAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "Document {ApprovalId} approved by {ApprovedBy}",
                id,
                request.ApprovedBy);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Approval {id} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Reject a document.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(typeof(ApprovalResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApprovalResponse>> RejectDocument(
        int id,
        [FromBody] RejectDocumentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _approvalService.RejectDocumentAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "Document {ApprovalId} rejected by {RejectedBy}: {Reason}",
                id,
                request.RejectedBy,
                request.Reason);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Approval {id} not found");
        }
    }

    /// <summary>
    /// Edit and approve a document.
    /// </summary>
    [HttpPost("{id:int}/edit")]
    [ProducesResponseType(typeof(ApprovalResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApprovalResponse>> EditAndApprove(
        int id,
        [FromBody] EditAndApproveRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _approvalService.EditAndApproveDocumentAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "Document {ApprovalId} edited ({EditCount} edits) and approved by {EditedBy}",
                id,
                request.Edits.Count,
                request.EditedBy);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Approval {id} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Request document regeneration with feedback.
    /// </summary>
    [HttpPost("{id:int}/regenerate")]
    [ProducesResponseType(typeof(ApprovalResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApprovalResponse>> RequestRegeneration(
        int id,
        [FromBody] RegenerateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _approvalService.RequestRegenerationAsync(id, request, cancellationToken);

            _logger.LogInformation(
                "Regeneration requested for {ApprovalId} by {RequestedBy}",
                id,
                request.RequestedBy);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Approval {id} not found");
        }
    }

    /// <summary>
    /// Get approval history for a document.
    /// </summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(ApprovalHistoryResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ApprovalHistoryResponse>> GetHistory(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _approvalService.GetApprovalHistoryAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get all edits made to a document (for AI training).
    /// </summary>
    [HttpGet("edits/{documentId}")]
    [ProducesResponseType(typeof(EditHistoryResponse), 200)]
    public async Task<ActionResult<EditHistoryResponse>> GetEditHistory(
        string documentId,
        CancellationToken cancellationToken)
    {
        var result = await _approvalService.GetEditHistoryAsync(documentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Generate a new document ID.
    /// </summary>
    [HttpGet("generate-id/{documentType}")]
    [ProducesResponseType(typeof(string), 200)]
    public async Task<ActionResult<string>> GenerateDocumentId(
        string documentType,
        CancellationToken cancellationToken)
    {
        var docId = await _approvalService.GenerateDocumentIdAsync(documentType, cancellationToken);
        return Ok(new { documentId = docId });
    }
}
'@

$controllerPath = Join-Path $apiPath "Controllers\Approval\ApprovalController.cs"
[System.IO.File]::WriteAllText($controllerPath, $apiController, $utf8NoBom)
Write-Host "  Created: ApprovalController.cs" -ForegroundColor Green

# ============================================================================
# PART 8: Configuration Updates
# ============================================================================

Write-Host "[8/10] Creating configuration template..." -ForegroundColor Yellow

$configTemplate = @'
{
  "// NOTE": "Add these settings to your appsettings.json",

  "Teams": {
    "WebhookUrl": "YOUR_TEAMS_WEBHOOK_URL_HERE",
    "DashboardUrl": "http://localhost:5000/approval"
  },

  "Approval": {
    "DestinationBasePath": "C:\\Temp\\Documentation-Catalog\\Database",
    "DefaultDueDays": 3,
    "UrgentDueDays": 1,
    "Approvers": [
      "your.email@company.com",
      "alex@company.com"
    ]
  },

  "DocumentNaming": {
    "TypePrefixes": {
      "StoredProcedure": "SP",
      "DataDictionary": "DD",
      "QATesting": "QA",
      "ChangeControl": "CC",
      "DefectFix": "FIX"
    }
  }
}
'@

$configPath = Join-Path $projectRoot "config\approval-settings.json"
$configDir = Split-Path $configPath -Parent
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}
[System.IO.File]::WriteAllText($configPath, $configTemplate, $utf8NoBom)
Write-Host "  Created: config\approval-settings.json (template)" -ForegroundColor Green

Write-Host ""
Write-Host "[9/10] Creating remaining files..." -ForegroundColor Yellow
Write-Host "  Run Part 3 for ApprovalService implementation and DI setup" -ForegroundColor Cyan
Write-Host ""

Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  PART 2 COMPLETE!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Created files:" -ForegroundColor White
Write-Host "  - ApprovalDTOs.cs (request/response objects)" -ForegroundColor Gray
Write-Host "  - IApprovalService.cs (service interface)" -ForegroundColor Gray
Write-Host "  - TeamsNotificationService.cs (Teams webhooks)" -ForegroundColor Gray
Write-Host "  - ApprovalController.cs (API endpoints)" -ForegroundColor Gray
Write-Host "  - approval-settings.json (configuration template)" -ForegroundColor Gray
Write-Host ""
Write-Host "Next: Run Part 3 for the ApprovalService implementation" -ForegroundColor Yellow
Write-Host ""
