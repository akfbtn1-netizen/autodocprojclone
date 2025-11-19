# ============================================================================
# IMPLEMENT APPROVAL WORKFLOW - Complete Implementation
# ============================================================================
# This script creates all remaining approval workflow components
# Run from: C:\Projects\EnterpriseDocumentationPlatform.V2
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$srcPath = Join-Path $projectRoot "src"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  IMPLEMENTING APPROVAL WORKFLOW SYSTEM" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# TASK 1: Update UserRole Enum
# ============================================================================
Write-Host "[1/7] Updating UserRole enum..." -ForegroundColor Yellow

$userRolePath = Join-Path $srcPath "Core\Domain\Enums\UserRole.cs"
$userRoleDir = Split-Path $userRolePath -Parent

if (!(Test-Path $userRoleDir)) {
    New-Item -ItemType Directory -Path $userRoleDir -Force | Out-Null
}

$userRoleContent = @'
namespace Enterprise.Documentation.Core.Domain.Enums;

/// <summary>
/// Defines user roles in the documentation platform.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Read-only access to documentation
    /// </summary>
    Reader = 0,

    /// <summary>
    /// Can create and edit documents
    /// </summary>
    Contributor = 1,

    /// <summary>
    /// Can approve/reject documents
    /// </summary>
    Approver = 2,

    /// <summary>
    /// Full system administration
    /// </summary>
    Admin = 3
}
'@

[System.IO.File]::WriteAllText($userRolePath, $userRoleContent, $utf8NoBom)
Write-Host "  Created: $userRolePath" -ForegroundColor Green

# ============================================================================
# TASK 2: Create ApprovalRepository
# ============================================================================
Write-Host "[2/7] Creating ApprovalRepository..." -ForegroundColor Yellow

$repoPath = Join-Path $srcPath "Core\Infrastructure\Persistence\Repositories\ApprovalRepository.cs"

$repoContent = @'
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Domain.Models.Approval;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for approval workflow data access.
/// Uses Dapper for lightweight ORM with full SQL control.
/// </summary>
public class ApprovalRepository : IApprovalRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ApprovalRepository> _logger;

    public ApprovalRepository(
        IConfiguration configuration,
        ILogger<ApprovalRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentApproval?> GetByIdAsync(int approvalId)
    {
        const string sql = @"
            SELECT
                ApprovalID, MasterIndexID, DocumentId, DocumentPath,
                DocumentType, GeneratedBy, GeneratedDate, Status,
                AssignedTo, AssignedDate, ReviewedBy, ReviewedDate,
                Comments, CABNumber, OriginalContent, ModifiedContent
            FROM DaQa.DocumentApprovals
            WHERE ApprovalID = @ApprovalId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<DocumentApproval>(
                sql,
                new { ApprovalId = approvalId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve approval {ApprovalId}", approvalId);
            throw;
        }
    }

    public async Task<DocumentApproval?> GetByDocumentIdAsync(string documentId)
    {
        const string sql = @"
            SELECT
                ApprovalID, MasterIndexID, DocumentId, DocumentPath,
                DocumentType, GeneratedBy, GeneratedDate, Status,
                AssignedTo, AssignedDate, ReviewedBy, ReviewedDate,
                Comments, CABNumber, OriginalContent, ModifiedContent
            FROM DaQa.DocumentApprovals
            WHERE DocumentId = @DocumentId
            ORDER BY ApprovalID DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<DocumentApproval>(
                sql,
                new { DocumentId = documentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve approval for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<DocumentApproval>> GetPendingApprovalsAsync()
    {
        const string sql = @"
            SELECT
                ApprovalID, MasterIndexID, DocumentId, DocumentPath,
                DocumentType, GeneratedBy, GeneratedDate, Status,
                AssignedTo, AssignedDate, ReviewedBy, ReviewedDate,
                Comments, CABNumber, OriginalContent, ModifiedContent
            FROM DaQa.DocumentApprovals
            WHERE Status = 'Pending'
            ORDER BY GeneratedDate ASC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DocumentApproval>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve pending approvals");
            throw;
        }
    }

    public async Task<IEnumerable<DocumentApproval>> GetApprovalsByStatusAsync(string status)
    {
        const string sql = @"
            SELECT
                ApprovalID, MasterIndexID, DocumentId, DocumentPath,
                DocumentType, GeneratedBy, GeneratedDate, Status,
                AssignedTo, AssignedDate, ReviewedBy, ReviewedDate,
                Comments, CABNumber, OriginalContent, ModifiedContent
            FROM DaQa.DocumentApprovals
            WHERE Status = @Status
            ORDER BY GeneratedDate DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DocumentApproval>(
                sql,
                new { Status = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve approvals with status {Status}", status);
            throw;
        }
    }

    public async Task<int> CreateAsync(DocumentApproval approval)
    {
        const string sql = @"
            INSERT INTO DaQa.DocumentApprovals (
                MasterIndexID, DocumentId, DocumentPath, DocumentType,
                GeneratedBy, GeneratedDate, Status, AssignedTo, AssignedDate,
                CABNumber, OriginalContent
            )
            OUTPUT INSERTED.ApprovalID
            VALUES (
                @MasterIndexID, @DocumentId, @DocumentPath, @DocumentType,
                @GeneratedBy, @GeneratedDate, @Status, @AssignedTo, @AssignedDate,
                @CABNumber, @OriginalContent
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var approvalId = await connection.ExecuteScalarAsync<int>(sql, approval);

            _logger.LogInformation(
                "Created approval {ApprovalId} for document {DocumentId}",
                approvalId,
                approval.DocumentId);

            return approvalId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create approval for document {DocumentId}", approval.DocumentId);
            throw;
        }
    }

    public async Task UpdateAsync(DocumentApproval approval)
    {
        const string sql = @"
            UPDATE DaQa.DocumentApprovals
            SET
                Status = @Status,
                ReviewedBy = @ReviewedBy,
                ReviewedDate = @ReviewedDate,
                Comments = @Comments,
                ModifiedContent = @ModifiedContent
            WHERE ApprovalID = @ApprovalID";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, approval);

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Approval {approval.ApprovalID} not found");
            }

            _logger.LogInformation(
                "Updated approval {ApprovalId} status to {Status}",
                approval.ApprovalID,
                approval.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update approval {ApprovalId}", approval.ApprovalID);
            throw;
        }
    }

    public async Task<IEnumerable<ApprovalHistory>> GetHistoryByApprovalIdAsync(int approvalId)
    {
        const string sql = @"
            SELECT
                Id, ApprovalId, DocumentId, Action, ActionBy, ActionAt,
                PreviousStatus, NewStatus, Notes, SourcePath, DestinationPath
            FROM DaQa.ApprovalHistory
            WHERE ApprovalId = @ApprovalId
            ORDER BY ActionAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ApprovalHistory>(
                sql,
                new { ApprovalId = approvalId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve history for approval {ApprovalId}", approvalId);
            throw;
        }
    }

    public async Task<IEnumerable<ApprovalHistory>> GetHistoryByDocumentIdAsync(string documentId)
    {
        const string sql = @"
            SELECT
                Id, ApprovalId, DocumentId, Action, ActionBy, ActionAt,
                PreviousStatus, NewStatus, Notes, SourcePath, DestinationPath
            FROM DaQa.ApprovalHistory
            WHERE DocumentId = @DocumentId
            ORDER BY ActionAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ApprovalHistory>(
                sql,
                new { DocumentId = documentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve history for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task AddHistoryAsync(ApprovalHistory history)
    {
        const string sql = @"
            INSERT INTO DaQa.ApprovalHistory (
                ApprovalId, DocumentId, Action, ActionBy, ActionAt,
                PreviousStatus, NewStatus, Notes, SourcePath, DestinationPath
            )
            VALUES (
                @ApprovalId, @DocumentId, @Action, @ActionBy, @ActionAt,
                @PreviousStatus, @NewStatus, @Notes, @SourcePath, @DestinationPath
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, history);

            _logger.LogDebug(
                "Added history entry for document {DocumentId}: {Action}",
                history.DocumentId,
                history.Action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add history entry for document {DocumentId}", history.DocumentId);
            throw;
        }
    }

    public async Task<IEnumerable<DocumentEdit>> GetEditsByApprovalIdAsync(int approvalId)
    {
        const string sql = @"
            SELECT
                Id, ApprovalId, DocumentId, SectionName, OriginalText,
                EditedText, EditReason, EditCategory, EditedBy, EditedAt,
                ShouldTrainAI, AIFeedbackProcessed
            FROM DaQa.DocumentEdits
            WHERE ApprovalId = @ApprovalId
            ORDER BY EditedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DocumentEdit>(
                sql,
                new { ApprovalId = approvalId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve edits for approval {ApprovalId}", approvalId);
            throw;
        }
    }

    public async Task<IEnumerable<DocumentEdit>> GetEditsByDocumentIdAsync(string documentId)
    {
        const string sql = @"
            SELECT
                Id, ApprovalId, DocumentId, SectionName, OriginalText,
                EditedText, EditReason, EditCategory, EditedBy, EditedAt,
                ShouldTrainAI, AIFeedbackProcessed
            FROM DaQa.DocumentEdits
            WHERE DocumentId = @DocumentId
            ORDER BY EditedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DocumentEdit>(
                sql,
                new { DocumentId = documentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve edits for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task AddEditAsync(DocumentEdit edit)
    {
        const string sql = @"
            INSERT INTO DaQa.DocumentEdits (
                ApprovalId, DocumentId, SectionName, OriginalText, EditedText,
                EditReason, EditCategory, EditedBy, EditedAt, ShouldTrainAI
            )
            VALUES (
                @ApprovalId, @DocumentId, @SectionName, @OriginalText, @EditedText,
                @EditReason, @EditCategory, @EditedBy, @EditedAt, @ShouldTrainAI
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, edit);

            _logger.LogInformation(
                "Recorded edit for document {DocumentId}, section: {Section}",
                edit.DocumentId,
                edit.SectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record edit for document {DocumentId}", edit.DocumentId);
            throw;
        }
    }

    public async Task<int> GetNextSequenceNumberAsync(string typeCode, int year)
    {
        const string sql = @"
            SELECT ISNULL(MAX(
                CAST(SUBSTRING(DocumentId, LEN(@TypeCode) + 7, 3) AS INT)
            ), 0) + 1
            FROM DaQa.DocumentApprovals
            WHERE DocumentId LIKE @Pattern";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var pattern = $"{typeCode}-{year}-%";
            return await connection.ExecuteScalarAsync<int>(
                sql,
                new { TypeCode = typeCode, Pattern = pattern });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next sequence number for {TypeCode}-{Year}", typeCode, year);
            throw;
        }
    }

    public async Task<RegenerationRequest?> GetRegenerationRequestAsync(int requestId)
    {
        const string sql = @"
            SELECT
                Id, ApprovalId, DocumentId, OriginalVersion, FeedbackText,
                FeedbackSection, AdditionalContext, RequestedBy, RequestedAt,
                Status, NewVersion, NewApprovalId, CompletedAt, ErrorMessage
            FROM DaQa.RegenerationRequests
            WHERE Id = @RequestId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<RegenerationRequest>(
                sql,
                new { RequestId = requestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve regeneration request {RequestId}", requestId);
            throw;
        }
    }

    public async Task<int> CreateRegenerationRequestAsync(RegenerationRequest request)
    {
        const string sql = @"
            INSERT INTO DaQa.RegenerationRequests (
                ApprovalId, DocumentId, OriginalVersion, FeedbackText,
                FeedbackSection, AdditionalContext, RequestedBy, RequestedAt, Status
            )
            OUTPUT INSERTED.Id
            VALUES (
                @ApprovalId, @DocumentId, @OriginalVersion, @FeedbackText,
                @FeedbackSection, @AdditionalContext, @RequestedBy, @RequestedAt, @Status
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(sql, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create regeneration request for document {DocumentId}", request.DocumentId);
            throw;
        }
    }

    public async Task UpdateRegenerationRequestAsync(RegenerationRequest request)
    {
        const string sql = @"
            UPDATE DaQa.RegenerationRequests
            SET
                Status = @Status,
                NewVersion = @NewVersion,
                NewApprovalId = @NewApprovalId,
                CompletedAt = @CompletedAt,
                ErrorMessage = @ErrorMessage
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update regeneration request {RequestId}", request.Id);
            throw;
        }
    }
}
'@

[System.IO.File]::WriteAllText($repoPath, $repoContent, $utf8NoBom)
Write-Host "  Created: $repoPath" -ForegroundColor Green

# ============================================================================
# TASK 3: Update DI Registration
# ============================================================================
Write-Host "[3/7] Updating DI registration..." -ForegroundColor Yellow

$diPath = Join-Path $srcPath "Api\Extensions\ApprovalServiceExtensions.cs"

$diContent = @'
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Application.Services.Approval;
using Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

namespace Enterprise.Documentation.Api.Extensions;

/// <summary>
/// Extension methods for registering approval workflow services.
/// </summary>
public static class ApprovalServiceExtensions
{
    /// <summary>
    /// Adds approval workflow services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddApprovalWorkflow(this IServiceCollection services)
    {
        // Repository
        services.AddScoped<IApprovalRepository, ApprovalRepository>();

        // Services
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<ITeamsNotificationService, TeamsNotificationService>();

        // HttpClient for Teams webhooks
        services.AddHttpClient<ITeamsNotificationService, TeamsNotificationService>();

        return services;
    }
}
'@

[System.IO.File]::WriteAllText($diPath, $diContent, $utf8NoBom)
Write-Host "  Created: $diPath" -ForegroundColor Green

# ============================================================================
# TASK 4: Create Razor Pages Directory Structure
# ============================================================================
Write-Host "[4/7] Creating Razor Pages structure..." -ForegroundColor Yellow

$pagesDir = Join-Path $srcPath "Api\Pages\Approval"
if (!(Test-Path $pagesDir)) {
    New-Item -ItemType Directory -Path $pagesDir -Force | Out-Null
}

# Index.cshtml.cs
$indexModelPath = Join-Path $pagesDir "Index.cshtml.cs"
$indexModelContent = @'
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Application.DTOs.Approval;

namespace Enterprise.Documentation.Api.Pages.Approval;

/// <summary>
/// Page model for approval queue dashboard.
/// </summary>
[Authorize(Roles = "Admin,Approver")]
public class IndexModel : PageModel
{
    private readonly IApprovalService _approvalService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IApprovalService approvalService,
        ILogger<IndexModel> logger)
    {
        _approvalService = approvalService;
        _logger = logger;
    }

    public List<PendingApprovalDto> PendingApprovals { get; set; } = new();
    public int TotalCount { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        try
        {
            PendingApprovals = await _approvalService.GetPendingApprovalsAsync();
            TotalCount = PendingApprovals.Count;
            _logger.LogInformation("Loaded {Count} pending approvals", TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending approvals");
            ErrorMessage = "Failed to load approval queue. Please try again.";
            PendingApprovals = new List<PendingApprovalDto>();
            TotalCount = 0;
        }
    }
}
'@
[System.IO.File]::WriteAllText($indexModelPath, $indexModelContent, $utf8NoBom)
Write-Host "  Created: $indexModelPath" -ForegroundColor Green

# Index.cshtml
$indexViewPath = Join-Path $pagesDir "Index.cshtml"
$indexViewContent = @'
@page
@model Enterprise.Documentation.Api.Pages.Approval.IndexModel
@{
    ViewData["Title"] = "Approval Queue";
}

<div class="container-fluid py-4">
    <div class="row mb-4">
        <div class="col">
            <h1 class="display-4">Document Approval Queue</h1>
            <p class="lead">Review and approve generated documentation</p>
        </div>
    </div>

    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <strong>Error!</strong> @Model.ErrorMessage
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    }

    <div class="row mb-3">
        <div class="col-md-4">
            <div class="card">
                <div class="card-body">
                    <h5 class="card-title">Pending Approvals</h5>
                    <h2 class="display-6">@Model.TotalCount</h2>
                </div>
            </div>
        </div>
    </div>

    @if (Model.TotalCount == 0)
    {
        <div class="alert alert-info">
            No documents pending approval.
        </div>
    }
    else
    {
        <div class="table-responsive">
            <table class="table table-hover">
                <thead class="table-light">
                    <tr>
                        <th>Document ID</th>
                        <th>Type</th>
                        <th>Generated By</th>
                        <th>Generated Date</th>
                        <th>CAB #</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var approval in Model.PendingApprovals)
                    {
                        <tr>
                            <td><strong>@approval.DocumentId</strong></td>
                            <td><span class="badge bg-primary">@approval.DocumentType</span></td>
                            <td>@approval.GeneratedBy</td>
                            <td>@approval.GeneratedDate.ToString("yyyy-MM-dd HH:mm")</td>
                            <td>@(approval.CABNumber ?? "N/A")</td>
                            <td>
                                <a href="/Approval/Details?id=@approval.ApprovalId" class="btn btn-sm btn-outline-primary">
                                    Review
                                </a>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</div>

@section Scripts {
    <script>
        // Auto-refresh every 30 seconds
        setTimeout(function() {
            location.reload();
        }, 30000);
    </script>
}
'@
[System.IO.File]::WriteAllText($indexViewPath, $indexViewContent, $utf8NoBom)
Write-Host "  Created: $indexViewPath" -ForegroundColor Green

# Details.cshtml.cs
$detailsModelPath = Join-Path $pagesDir "Details.cshtml.cs"
$detailsModelContent = @'
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Application.DTOs.Approval;

namespace Enterprise.Documentation.Api.Pages.Approval;

/// <summary>
/// Page model for approval details and actions.
/// </summary>
[Authorize(Roles = "Admin,Approver")]
public class DetailsModel : PageModel
{
    private readonly IApprovalService _approvalService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IApprovalService approvalService,
        ILogger<DetailsModel> logger)
    {
        _approvalService = approvalService;
        _logger = logger;
    }

    public ApprovalDetailDto? Approval { get; set; }
    public List<ApprovalHistoryDto> History { get; set; } = new();

    [BindProperty]
    public string Comments { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
    public string SuccessMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Approval = await _approvalService.GetApprovalByIdAsync(id);
            if (Approval == null)
            {
                ErrorMessage = "Approval not found.";
                return RedirectToPage("/Approval/Index");
            }

            History = await _approvalService.GetApprovalHistoryAsync(id);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load approval {ApprovalId}", id);
            ErrorMessage = "Failed to load approval details.";
            return RedirectToPage("/Approval/Index");
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        try
        {
            var currentUser = User.Identity?.Name ?? "Unknown";

            var request = new ApproveDocumentRequest
            {
                ApprovedBy = currentUser,
                Notes = Comments
            };

            await _approvalService.ApproveDocumentAsync(id, request);

            _logger.LogInformation("Document {ApprovalId} approved by {User}", id, currentUser);

            TempData["SuccessMessage"] = "Document approved successfully!";
            return RedirectToPage("/Approval/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve document {ApprovalId}", id);
            ErrorMessage = $"Failed to approve document: {ex.Message}";
            return await OnGetAsync(id);
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        try
        {
            var currentUser = User.Identity?.Name ?? "Unknown";

            var request = new RejectDocumentRequest
            {
                RejectedBy = currentUser,
                Reason = Comments
            };

            await _approvalService.RejectDocumentAsync(id, request);

            _logger.LogInformation("Document {ApprovalId} rejected by {User}", id, currentUser);

            TempData["SuccessMessage"] = "Document rejected.";
            return RedirectToPage("/Approval/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject document {ApprovalId}", id);
            ErrorMessage = $"Failed to reject document: {ex.Message}";
            return await OnGetAsync(id);
        }
    }
}
'@
[System.IO.File]::WriteAllText($detailsModelPath, $detailsModelContent, $utf8NoBom)
Write-Host "  Created: $detailsModelPath" -ForegroundColor Green

# Details.cshtml
$detailsViewPath = Join-Path $pagesDir "Details.cshtml"
$detailsViewContent = @'
@page
@model Enterprise.Documentation.Api.Pages.Approval.DetailsModel
@{
    ViewData["Title"] = "Approval Details";
}

<div class="container-fluid py-4">
    <div class="row mb-4">
        <div class="col">
            <h1 class="display-6">Document Review</h1>
            <nav aria-label="breadcrumb">
                <ol class="breadcrumb">
                    <li class="breadcrumb-item"><a href="/Approval">Approval Queue</a></li>
                    <li class="breadcrumb-item active">@Model.Approval?.DocumentId</li>
                </ol>
            </nav>
        </div>
    </div>

    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <strong>Error!</strong> @Model.ErrorMessage
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    }

    @if (Model.Approval != null)
    {
        <div class="row">
            <div class="col-md-8">
                <!-- Document Preview Card -->
                <div class="card mb-4">
                    <div class="card-header bg-primary text-white">
                        <h5 class="mb-0">Document Information</h5>
                    </div>
                    <div class="card-body">
                        <div class="row mb-3">
                            <div class="col-md-6">
                                <strong>Document ID:</strong><br />
                                @Model.Approval.DocumentId
                            </div>
                            <div class="col-md-6">
                                <strong>Type:</strong><br />
                                <span class="badge bg-primary">@Model.Approval.DocumentType</span>
                            </div>
                        </div>

                        <div class="row mb-3">
                            <div class="col-md-6">
                                <strong>Generated By:</strong><br />
                                @Model.Approval.GeneratedBy
                            </div>
                            <div class="col-md-6">
                                <strong>Generated Date:</strong><br />
                                @Model.Approval.GeneratedDate.ToString("yyyy-MM-dd HH:mm")
                            </div>
                        </div>

                        <div class="row mb-3">
                            <div class="col-md-6">
                                <strong>CAB Number:</strong><br />
                                @(Model.Approval.CABNumber ?? "N/A")
                            </div>
                            <div class="col-md-6">
                                <strong>Status:</strong><br />
                                <span class="badge bg-warning">@Model.Approval.Status</span>
                            </div>
                        </div>

                        <div class="row mb-3">
                            <div class="col-md-12">
                                <strong>Document Path:</strong><br />
                                <code>@Model.Approval.DocumentPath</code>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Approval Action Card -->
                <div class="card">
                    <div class="card-header bg-success text-white">
                        <h5 class="mb-0">Review Actions</h5>
                    </div>
                    <div class="card-body">
                        <form method="post">
                            <div class="mb-3">
                                <label for="Comments" class="form-label">Comments (optional)</label>
                                <textarea class="form-control" id="Comments" name="Comments" rows="3"
                                          placeholder="Add any comments or feedback...">@Model.Comments</textarea>
                            </div>

                            <div class="d-grid gap-2 d-md-flex">
                                <button type="submit" asp-page-handler="Approve" asp-route-id="@Model.Approval.ApprovalId"
                                        class="btn btn-success btn-lg flex-fill">
                                    Approve Document
                                </button>
                                <button type="submit" asp-page-handler="Reject" asp-route-id="@Model.Approval.ApprovalId"
                                        class="btn btn-danger btn-lg flex-fill">
                                    Reject Document
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            </div>

            <div class="col-md-4">
                <!-- History Timeline -->
                <div class="card">
                    <div class="card-header bg-info text-white">
                        <h5 class="mb-0">Approval History</h5>
                    </div>
                    <div class="card-body">
                        @if (Model.History.Count == 0)
                        {
                            <p class="text-muted">No history available.</p>
                        }
                        else
                        {
                            @foreach (var entry in Model.History)
                            {
                                <div class="mb-3 pb-3 border-bottom">
                                    <h6 class="mb-1">@entry.Action</h6>
                                    <small class="text-muted">
                                        @entry.ActionBy - @entry.ActionAt.ToString("MMM dd, yyyy HH:mm")
                                    </small>
                                    @if (!string.IsNullOrEmpty(entry.Notes))
                                    {
                                        <p class="mt-2 mb-0"><em>"@entry.Notes"</em></p>
                                    }
                                </div>
                            }
                        }
                    </div>
                </div>
            </div>
        </div>
    }
</div>
'@
[System.IO.File]::WriteAllText($detailsViewPath, $detailsViewContent, $utf8NoBom)
Write-Host "  Created: $detailsViewPath" -ForegroundColor Green

# ============================================================================
# TASK 5: Create _ViewImports for Razor Pages
# ============================================================================
Write-Host "[5/7] Creating Razor Pages configuration..." -ForegroundColor Yellow

$viewImportsPath = Join-Path $srcPath "Api\Pages\_ViewImports.cshtml"
$viewImportsContent = @'
@using Enterprise.Documentation.Api
@namespace Enterprise.Documentation.Api.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
'@
[System.IO.File]::WriteAllText($viewImportsPath, $viewImportsContent, $utf8NoBom)
Write-Host "  Created: $viewImportsPath" -ForegroundColor Green

# ============================================================================
# TASK 6: Create Program.cs Integration Snippet
# ============================================================================
Write-Host "[6/7] Creating Program.cs integration snippet..." -ForegroundColor Yellow

$snippetPath = Join-Path $projectRoot "PROGRAM-CS-ADDITIONS.txt"
$snippetContent = @'
// ============================================================================
// ADD THESE LINES TO Program.cs
// ============================================================================

// 1. Add this using at the top:
using Enterprise.Documentation.Api.Extensions;

// 2. Add this after other service registrations (e.g., after AddControllers):
builder.Services.AddRazorPages();
builder.Services.AddApprovalWorkflow();

// 3. Add this after app.MapControllers():
app.MapRazorPages();

// ============================================================================
// EXAMPLE SNIPPET FOR Program.cs
// ============================================================================
/*
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddRazorPages();           // ADD THIS
builder.Services.AddApprovalWorkflow();     // ADD THIS
// ... other services

var app = builder.Build();

// Configure pipeline
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();                        // ADD THIS

app.Run();
*/
'@
[System.IO.File]::WriteAllText($snippetPath, $snippetContent, $utf8NoBom)
Write-Host "  Created: $snippetPath" -ForegroundColor Green

# ============================================================================
# TASK 7: Summary
# ============================================================================
Write-Host "[7/7] Implementation complete!" -ForegroundColor Yellow

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  APPROVAL WORKFLOW IMPLEMENTATION COMPLETE" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Files created:" -ForegroundColor Cyan
Write-Host "  - src/Core/Domain/Enums/UserRole.cs"
Write-Host "  - src/Core/Infrastructure/Persistence/Repositories/ApprovalRepository.cs"
Write-Host "  - src/Api/Extensions/ApprovalServiceExtensions.cs"
Write-Host "  - src/Api/Pages/_ViewImports.cshtml"
Write-Host "  - src/Api/Pages/Approval/Index.cshtml"
Write-Host "  - src/Api/Pages/Approval/Index.cshtml.cs"
Write-Host "  - src/Api/Pages/Approval/Details.cshtml"
Write-Host "  - src/Api/Pages/Approval/Details.cshtml.cs"
Write-Host "  - PROGRAM-CS-ADDITIONS.txt"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Update Program.cs with the additions in PROGRAM-CS-ADDITIONS.txt"
Write-Host "  2. Run: dotnet build"
Write-Host "  3. Configure Teams webhook in appsettings.json"
Write-Host "  4. Test: http://localhost:5195/Approval"
Write-Host ""
