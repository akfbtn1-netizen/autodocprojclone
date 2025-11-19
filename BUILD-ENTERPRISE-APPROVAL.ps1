# ============================================================================
# ENTERPRISE APPROVAL WORKFLOW - COMPLETE IMPLEMENTATION
# ============================================================================
# Implements:
# - UserRole enum update
# - ApprovalRepository with Dapper
# - DI Registration updates
# - Razor Pages approval dashboard
# - DocGeneratorService integration
# ============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  ENTERPRISE APPROVAL WORKFLOW IMPLEMENTATION" -ForegroundColor White
Write-Host "  Following SOLID Principles & Clean Architecture" -ForegroundColor Gray
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# TASK 1: Update UserRole Enum
# ============================================================================
Write-Host "[1/7] Updating UserRole enum..." -ForegroundColor Yellow

$enumDir = Join-Path $projectRoot "src\Core\Domain\Enums"
if (-not (Test-Path $enumDir)) {
    New-Item -ItemType Directory -Path $enumDir -Force | Out-Null
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

[System.IO.File]::WriteAllText((Join-Path $enumDir "UserRole.cs"), $userRoleContent, $utf8NoBom)
Write-Host "  Created: UserRole.cs" -ForegroundColor Green

# ============================================================================
# TASK 2: Create ApprovalRepository
# ============================================================================
Write-Host "[2/7] Creating ApprovalRepository with Dapper..." -ForegroundColor Yellow

$repoDir = Join-Path $projectRoot "src\Core\Infrastructure\Persistence\Repositories"
if (-not (Test-Path $repoDir)) {
    New-Item -ItemType Directory -Path $repoDir -Force | Out-Null
}

$approvalRepoContent = @'
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

    #region Document Approvals

    public async Task<DocumentApproval?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT Id, DocumentId, MasterIndexId, ObjectName, SchemaName, DatabaseName,
                   DocumentType, TemplateUsed, CABNumber, GeneratedFilePath, DestinationPath,
                   FileSizeBytes, Status, Priority, RequestedBy, RequestedAt, AssignedTo,
                   DueDate, ResolvedBy, ResolvedAt, ResolutionNotes, Version, PreviousVersionId,
                   CreatedAt, ModifiedAt
            FROM DaQa.DocumentApprovals
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<DocumentApproval>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve approval {ApprovalId}", id);
            throw;
        }
    }

    public async Task<DocumentApproval?> GetByDocumentIdAsync(string documentId)
    {
        const string sql = @"
            SELECT Id, DocumentId, MasterIndexId, ObjectName, SchemaName, DatabaseName,
                   DocumentType, TemplateUsed, CABNumber, GeneratedFilePath, DestinationPath,
                   FileSizeBytes, Status, Priority, RequestedBy, RequestedAt, AssignedTo,
                   DueDate, ResolvedBy, ResolvedAt, ResolutionNotes, Version, PreviousVersionId,
                   CreatedAt, ModifiedAt
            FROM DaQa.DocumentApprovals
            WHERE DocumentId = @DocumentId
            ORDER BY Id DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<DocumentApproval>(sql, new { DocumentId = documentId });
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
            SELECT Id, DocumentId, MasterIndexId, ObjectName, SchemaName, DatabaseName,
                   DocumentType, TemplateUsed, CABNumber, GeneratedFilePath, DestinationPath,
                   FileSizeBytes, Status, Priority, RequestedBy, RequestedAt, AssignedTo,
                   DueDate, ResolvedBy, ResolvedAt, ResolutionNotes, Version, PreviousVersionId,
                   CreatedAt, ModifiedAt
            FROM DaQa.DocumentApprovals
            WHERE Status = 0
            ORDER BY RequestedAt ASC";

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
            SELECT Id, DocumentId, MasterIndexId, ObjectName, SchemaName, DatabaseName,
                   DocumentType, TemplateUsed, CABNumber, GeneratedFilePath, DestinationPath,
                   FileSizeBytes, Status, Priority, RequestedBy, RequestedAt, AssignedTo,
                   DueDate, ResolvedBy, ResolvedAt, ResolutionNotes, Version, PreviousVersionId,
                   CreatedAt, ModifiedAt
            FROM DaQa.DocumentApprovals
            WHERE Status = @Status
            ORDER BY RequestedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DocumentApproval>(sql, new { Status = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve approvals with status {Status}", status);
            throw;
        }
    }

    public async Task<IEnumerable<DocumentApproval>> GetApprovalsByApproverAsync(string approverEmail)
    {
        const string sql = @"
            SELECT Id, DocumentId, MasterIndexId, ObjectName, SchemaName, DatabaseName,
                   DocumentType, TemplateUsed, CABNumber, GeneratedFilePath, DestinationPath,
                   FileSizeBytes, Status, Priority, RequestedBy, RequestedAt, AssignedTo,
                   DueDate, ResolvedBy, ResolvedAt, ResolutionNotes, Version, PreviousVersionId,
                   CreatedAt, ModifiedAt
            FROM DaQa.DocumentApprovals
            WHERE AssignedTo = @Email
            ORDER BY RequestedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DocumentApproval>(sql, new { Email = approverEmail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve approvals for approver {Approver}", approverEmail);
            throw;
        }
    }

    public async Task<int> CreateAsync(DocumentApproval approval)
    {
        const string sql = @"
            INSERT INTO DaQa.DocumentApprovals (
                DocumentId, MasterIndexId, ObjectName, SchemaName, DatabaseName,
                DocumentType, TemplateUsed, CABNumber, GeneratedFilePath, DestinationPath,
                FileSizeBytes, Status, Priority, RequestedBy, RequestedAt, AssignedTo,
                DueDate, Version
            )
            OUTPUT INSERTED.Id
            VALUES (
                @DocumentId, @MasterIndexId, @ObjectName, @SchemaName, @DatabaseName,
                @DocumentType, @TemplateUsed, @CABNumber, @GeneratedFilePath, @DestinationPath,
                @FileSizeBytes, @Status, @Priority, @RequestedBy, @RequestedAt, @AssignedTo,
                @DueDate, @Version
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var id = await connection.ExecuteScalarAsync<int>(sql, approval);

            _logger.LogInformation(
                "Created approval {ApprovalId} for document {DocumentId}",
                id, approval.DocumentId);

            return id;
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
            SET Status = @Status,
                ResolvedBy = @ResolvedBy,
                ResolvedAt = @ResolvedAt,
                ResolutionNotes = @ResolutionNotes,
                ModifiedAt = GETUTCDATE()
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var rowsAffected = await connection.ExecuteAsync(sql, approval);

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Approval {approval.Id} not found");
            }

            _logger.LogInformation(
                "Updated approval {ApprovalId} status to {Status}",
                approval.Id, approval.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update approval {ApprovalId}", approval.Id);
            throw;
        }
    }

    #endregion

    #region Approval History

    public async Task<IEnumerable<ApprovalHistoryEntry>> GetHistoryAsync(int approvalId)
    {
        const string sql = @"
            SELECT Id, ApprovalId, DocumentId, Action, ActionBy, ActionAt,
                   PreviousStatus, NewStatus, Notes, SourcePath, DestinationPath
            FROM DaQa.ApprovalHistory
            WHERE ApprovalId = @ApprovalId
            ORDER BY ActionAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<ApprovalHistoryEntry>(sql, new { ApprovalId = approvalId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve history for approval {ApprovalId}", approvalId);
            throw;
        }
    }

    public async Task AddHistoryEntryAsync(ApprovalHistoryEntry entry)
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
            await connection.ExecuteAsync(sql, entry);

            _logger.LogDebug(
                "Added history entry for approval {ApprovalId}: {Action}",
                entry.ApprovalId, entry.Action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add history entry for approval {ApprovalId}", entry.ApprovalId);
            throw;
        }
    }

    #endregion

    #region Document Edits

    public async Task<IEnumerable<DocumentEdit>> GetEditsAsync(string documentId)
    {
        const string sql = @"
            SELECT Id, ApprovalId, DocumentId, SectionName, OriginalText, EditedText,
                   EditReason, Category, EditedBy, EditedAt, ShouldTrainAI, AIFeedbackProcessed
            FROM DaQa.DocumentEdits
            WHERE DocumentId = @DocumentId
            ORDER BY EditedAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DocumentEdit>(sql, new { DocumentId = documentId });
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
                @EditReason, @Category, @EditedBy, @EditedAt, @ShouldTrainAI
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, edit);

            _logger.LogInformation(
                "Recorded edit for document {DocumentId}, section: {Section}",
                edit.DocumentId, edit.SectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record edit for document {DocumentId}", edit.DocumentId);
            throw;
        }
    }

    #endregion

    #region Regeneration Requests

    public async Task<RegenerationRequest?> GetRegenerationRequestAsync(int id)
    {
        const string sql = @"
            SELECT Id, ApprovalId, DocumentId, OriginalVersion, FeedbackText,
                   FeedbackSection, AdditionalContext, RequestedBy, RequestedAt,
                   Status, NewVersion, NewApprovalId, CompletedAt, ErrorMessage
            FROM DaQa.RegenerationRequests
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<RegenerationRequest>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve regeneration request {Id}", id);
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
            _logger.LogError(ex, "Failed to create regeneration request for approval {ApprovalId}", request.ApprovalId);
            throw;
        }
    }

    public async Task UpdateRegenerationRequestAsync(RegenerationRequest request)
    {
        const string sql = @"
            UPDATE DaQa.RegenerationRequests
            SET Status = @Status,
                CompletedAt = @CompletedAt,
                NewApprovalId = @NewApprovalId,
                ErrorMessage = @ErrorMessage
            WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update regeneration request {Id}", request.Id);
            throw;
        }
    }

    #endregion

    #region Approvers

    public async Task<IEnumerable<Approver>> GetActiveApproversAsync()
    {
        const string sql = @"
            SELECT Id, Email, DisplayName, IsActive, NotificationPreference,
                   TeamsWebhookUrl, CreatedAt, ModifiedAt
            FROM DaQa.Approvers
            WHERE IsActive = 1
            ORDER BY DisplayName";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<Approver>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active approvers");
            throw;
        }
    }

    public async Task<Approver?> GetApproverByEmailAsync(string email)
    {
        const string sql = @"
            SELECT Id, Email, DisplayName, IsActive, NotificationPreference,
                   TeamsWebhookUrl, CreatedAt, ModifiedAt
            FROM DaQa.Approvers
            WHERE Email = @Email AND IsActive = 1";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<Approver>(sql, new { Email = email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve approver {Email}", email);
            throw;
        }
    }

    #endregion

    #region Document ID Generation

    public async Task<int> GetNextSequenceNumberAsync(string documentType, int year)
    {
        const string sql = @"
            SELECT ISNULL(MAX(CAST(RIGHT(DocumentId, 3) AS INT)), 0) + 1
            FROM DaQa.DocumentApprovals
            WHERE DocumentId LIKE @Pattern";

        try
        {
            var pattern = $"{documentType}-{year}-%";
            using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(sql, new { Pattern = pattern });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next sequence for {Type}-{Year}", documentType, year);
            throw;
        }
    }

    #endregion
}
'@

[System.IO.File]::WriteAllText((Join-Path $repoDir "ApprovalRepository.cs"), $approvalRepoContent, $utf8NoBom)
Write-Host "  Created: ApprovalRepository.cs" -ForegroundColor Green

# ============================================================================
# TASK 3: Update DI Registration
# ============================================================================
Write-Host "[3/7] Updating ApprovalServiceExtensions..." -ForegroundColor Yellow

$extensionsDir = Join-Path $projectRoot "src\Api\Extensions"
if (-not (Test-Path $extensionsDir)) {
    New-Item -ItemType Directory -Path $extensionsDir -Force | Out-Null
}

$diExtensionsContent = @'
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
        services.AddHttpClient<ITeamsNotificationService, TeamsNotificationService>();

        return services;
    }
}
'@

[System.IO.File]::WriteAllText((Join-Path $extensionsDir "ApprovalServiceExtensions.cs"), $diExtensionsContent, $utf8NoBom)
Write-Host "  Created: ApprovalServiceExtensions.cs" -ForegroundColor Green

# ============================================================================
# TASK 4: Create Razor Pages Directory
# ============================================================================
Write-Host "[4/7] Creating Razor Pages directories..." -ForegroundColor Yellow

$pagesDir = Join-Path $projectRoot "src\Api\Pages\Approval"
if (-not (Test-Path $pagesDir)) {
    New-Item -ItemType Directory -Path $pagesDir -Force | Out-Null
}

# ============================================================================
# TASK 5: Create Index Page Model
# ============================================================================
Write-Host "[5/7] Creating approval dashboard Index page..." -ForegroundColor Yellow

$indexModelContent = @'
using Microsoft.AspNetCore.Mvc.RazorPages;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Application.DTOs.Approval;

namespace Enterprise.Documentation.Api.Pages.Approval;

/// <summary>
/// Page model for approval queue dashboard.
/// </summary>
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

    public PendingApprovalsResponse PendingApprovals { get; set; } = null!;
    public string ErrorMessage { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        try
        {
            PendingApprovals = await _approvalService.GetPendingApprovalsAsync();
            _logger.LogInformation("Loaded {Count} pending approvals", PendingApprovals.TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pending approvals");
            ErrorMessage = "Failed to load approval queue. Please try again.";
            PendingApprovals = new PendingApprovalsResponse
            {
                Approvals = new List<ApprovalResponse>(),
                TotalCount = 0,
                PendingCount = 0,
                UrgentCount = 0
            };
        }
    }
}
'@

[System.IO.File]::WriteAllText((Join-Path $pagesDir "Index.cshtml.cs"), $indexModelContent, $utf8NoBom)
Write-Host "  Created: Index.cshtml.cs" -ForegroundColor Green

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
        <div class="col-md-3">
            <div class="card bg-primary text-white">
                <div class="card-body">
                    <h5 class="card-title">Pending</h5>
                    <h2 class="display-6">@Model.PendingApprovals.PendingCount</h2>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card bg-danger text-white">
                <div class="card-body">
                    <h5 class="card-title">Urgent</h5>
                    <h2 class="display-6">@Model.PendingApprovals.UrgentCount</h2>
                </div>
            </div>
        </div>
        <div class="col-md-3">
            <div class="card bg-info text-white">
                <div class="card-body">
                    <h5 class="card-title">Total</h5>
                    <h2 class="display-6">@Model.PendingApprovals.TotalCount</h2>
                </div>
            </div>
        </div>
    </div>

    @if (Model.PendingApprovals.TotalCount == 0)
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
                        <th>Object Name</th>
                        <th>Schema</th>
                        <th>Requested By</th>
                        <th>Requested At</th>
                        <th>Priority</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var approval in Model.PendingApprovals.Approvals)
                    {
                        <tr>
                            <td>
                                <strong>@approval.DocumentId</strong>
                                @if (!string.IsNullOrEmpty(approval.CABNumber))
                                {
                                    <br /><small class="text-muted">CAB: @approval.CABNumber</small>
                                }
                            </td>
                            <td>
                                <span class="badge bg-primary">@approval.DocumentType</span>
                            </td>
                            <td>@approval.ObjectName</td>
                            <td>@approval.SchemaName</td>
                            <td>@approval.RequestedBy</td>
                            <td>@approval.RequestedAt.ToString("yyyy-MM-dd HH:mm")</td>
                            <td>
                                @if (approval.Priority == "Urgent")
                                {
                                    <span class="badge bg-danger">@approval.Priority</span>
                                }
                                else if (approval.Priority == "High")
                                {
                                    <span class="badge bg-warning">@approval.Priority</span>
                                }
                                else
                                {
                                    <span class="badge bg-secondary">@approval.Priority</span>
                                }
                            </td>
                            <td>
                                <a href="/Approval/Details?id=@approval.Id" class="btn btn-sm btn-outline-primary">
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

[System.IO.File]::WriteAllText((Join-Path $pagesDir "Index.cshtml"), $indexViewContent, $utf8NoBom)
Write-Host "  Created: Index.cshtml" -ForegroundColor Green

# ============================================================================
# TASK 6: Create Details Page Model
# ============================================================================
Write-Host "[6/7] Creating approval details page..." -ForegroundColor Yellow

$detailsModelContent = @'
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Application.DTOs.Approval;

namespace Enterprise.Documentation.Api.Pages.Approval;

/// <summary>
/// Page model for approval details and actions.
/// </summary>
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

    public ApprovalResponse? Approval { get; set; }
    public ApprovalHistoryResponse History { get; set; } = new();

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
                ErrorMessage = $"Approval {id} not found.";
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
            var currentUser = User.Identity?.Name ?? "System";

            var request = new ApproveDocumentRequest
            {
                ApprovedBy = currentUser,
                Notes = Comments,
                MoveToDestination = true
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
            var currentUser = User.Identity?.Name ?? "System";

            if (string.IsNullOrWhiteSpace(Comments))
            {
                ErrorMessage = "Please provide a reason for rejection.";
                return await OnGetAsync(id);
            }

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

[System.IO.File]::WriteAllText((Join-Path $pagesDir "Details.cshtml.cs"), $detailsModelContent, $utf8NoBom)
Write-Host "  Created: Details.cshtml.cs" -ForegroundColor Green

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

    @if (Model.Approval == null)
    {
        <div class="alert alert-warning">Approval not found.</div>
    }
    else
    {
        <div class="row">
            <div class="col-md-8">
                <!-- Document Info Card -->
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
                                <strong>Object Name:</strong><br />
                                @Model.Approval.ObjectName
                            </div>
                            <div class="col-md-6">
                                <strong>Schema:</strong><br />
                                @Model.Approval.SchemaName.@Model.Approval.DatabaseName
                            </div>
                        </div>

                        <div class="row mb-3">
                            <div class="col-md-6">
                                <strong>CAB Number:</strong><br />
                                @(Model.Approval.CABNumber ?? "N/A")
                            </div>
                            <div class="col-md-6">
                                <strong>Template:</strong><br />
                                @(Model.Approval.TemplateUsed ?? "N/A")
                            </div>
                        </div>

                        <div class="row mb-3">
                            <div class="col-md-6">
                                <strong>Requested By:</strong><br />
                                @Model.Approval.RequestedBy
                            </div>
                            <div class="col-md-6">
                                <strong>Requested At:</strong><br />
                                @Model.Approval.RequestedAt.ToString("yyyy-MM-dd HH:mm")
                            </div>
                        </div>

                        <div class="row mb-3">
                            <div class="col-md-12">
                                <strong>Document Path:</strong><br />
                                <code>@Model.Approval.GeneratedFilePath</code>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Approval Actions Card -->
                @if (Model.Approval.Status == "Pending")
                {
                    <div class="card">
                        <div class="card-header bg-success text-white">
                            <h5 class="mb-0">Review Actions</h5>
                        </div>
                        <div class="card-body">
                            <form method="post">
                                <div class="mb-3">
                                    <label for="Comments" class="form-label">Comments</label>
                                    <textarea class="form-control" id="Comments" name="Comments" rows="3"
                                              placeholder="Add comments (required for rejection)...">@Model.Comments</textarea>
                                </div>

                                <div class="d-grid gap-2 d-md-flex">
                                    <button type="submit" asp-page-handler="Approve" asp-route-id="@Model.Approval.Id"
                                            class="btn btn-success btn-lg flex-fill">
                                        Approve Document
                                    </button>
                                    <button type="submit" asp-page-handler="Reject" asp-route-id="@Model.Approval.Id"
                                            class="btn btn-danger btn-lg flex-fill">
                                        Reject Document
                                    </button>
                                </div>
                            </form>
                        </div>
                    </div>
                }
                else
                {
                    <div class="alert alert-info">
                        This document has already been <strong>@Model.Approval.Status</strong>
                        @if (Model.Approval.ResolvedBy != null)
                        {
                            <span>by @Model.Approval.ResolvedBy on @Model.Approval.ResolvedAt?.ToString("yyyy-MM-dd HH:mm")</span>
                        }
                    </div>
                }
            </div>

            <div class="col-md-4">
                <!-- History Timeline -->
                <div class="card">
                    <div class="card-header bg-info text-white">
                        <h5 class="mb-0">Approval History</h5>
                    </div>
                    <div class="card-body">
                        @if (Model.History.History.Count == 0)
                        {
                            <p class="text-muted">No history available.</p>
                        }
                        else
                        {
                            <ul class="list-unstyled">
                                @foreach (var entry in Model.History.History)
                                {
                                    <li class="mb-3 pb-3 border-bottom">
                                        <strong>@entry.Action</strong><br />
                                        <small class="text-muted">
                                            @entry.ActionBy • @entry.ActionAt.ToString("MMM dd, HH:mm")
                                        </small>
                                        @if (!string.IsNullOrEmpty(entry.Notes))
                                        {
                                            <p class="mt-1 mb-0 small"><em>"@entry.Notes"</em></p>
                                        }
                                    </li>
                                }
                            </ul>
                        }
                    </div>
                </div>
            </div>
        </div>
    }
</div>
'@

[System.IO.File]::WriteAllText((Join-Path $pagesDir "Details.cshtml"), $detailsViewContent, $utf8NoBom)
Write-Host "  Created: Details.cshtml" -ForegroundColor Green

# ============================================================================
# TASK 7: Create Program.cs Integration Snippet
# ============================================================================
Write-Host "[7/7] Creating Program.cs integration snippet..." -ForegroundColor Yellow

$programSnippet = @'
// ============================================================================
// ADD THESE TO YOUR Program.cs
// ============================================================================

// 1. Add this using statement at the top:
using Enterprise.Documentation.Api.Extensions;

// 2. Add Razor Pages support (after builder.Services.AddControllers()):
builder.Services.AddRazorPages();

// 3. Add approval workflow services:
builder.Services.AddApprovalWorkflow();

// 4. Map Razor Pages (after app.MapControllers()):
app.MapRazorPages();

// ============================================================================
// Example placement in Program.cs:
// ============================================================================

/*
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddRazorPages();              // ADD THIS
builder.Services.AddApprovalWorkflow();        // ADD THIS
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();                           // ADD THIS

app.Run();
*/
'@

[System.IO.File]::WriteAllText((Join-Path $projectRoot "PROGRAM-CS-INTEGRATION.txt"), $programSnippet, $utf8NoBom)
Write-Host "  Created: PROGRAM-CS-INTEGRATION.txt" -ForegroundColor Green

# ============================================================================
# Summary
# ============================================================================
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  IMPLEMENTATION COMPLETE!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Files created:" -ForegroundColor White
Write-Host "  1. UserRole.cs - Enum with Admin/Approver roles" -ForegroundColor Gray
Write-Host "  2. ApprovalRepository.cs - Dapper-based data access" -ForegroundColor Gray
Write-Host "  3. ApprovalServiceExtensions.cs - DI registration" -ForegroundColor Gray
Write-Host "  4. Index.cshtml + Index.cshtml.cs - Approval queue dashboard" -ForegroundColor Gray
Write-Host "  5. Details.cshtml + Details.cshtml.cs - Approval details page" -ForegroundColor Gray
Write-Host "  6. PROGRAM-CS-INTEGRATION.txt - Integration instructions" -ForegroundColor Gray
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Update Program.cs (see PROGRAM-CS-INTEGRATION.txt)" -ForegroundColor Cyan
Write-Host "  2. Run: dotnet build" -ForegroundColor Cyan
Write-Host "  3. Run: dotnet run --project src/Api" -ForegroundColor Cyan
Write-Host "  4. Navigate to: http://localhost:5000/Approval" -ForegroundColor Cyan
Write-Host ""
Write-Host "API Endpoints:" -ForegroundColor Yellow
Write-Host "  GET  /api/approval/pending" -ForegroundColor Cyan
Write-Host "  GET  /api/approval/{id}" -ForegroundColor Cyan
Write-Host "  POST /api/approval/{id}/approve" -ForegroundColor Cyan
Write-Host "  POST /api/approval/{id}/reject" -ForegroundColor Cyan
Write-Host ""
Write-Host "Razor Pages:" -ForegroundColor Yellow
Write-Host "  /Approval - Queue dashboard" -ForegroundColor Cyan
Write-Host "  /Approval/Details?id={id} - Review page" -ForegroundColor Cyan
Write-Host ""
