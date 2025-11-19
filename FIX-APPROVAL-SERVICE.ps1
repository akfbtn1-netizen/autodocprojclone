# ============================================================================
# FIX APPROVAL SERVICE - Interface Implementation
# ============================================================================
# Fixes method signature mismatches between IApprovalService and ApprovalService
# ============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  FIXING APPROVAL SERVICE IMPLEMENTATION" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# Fix IApprovalRepository to use int instead of Guid
# ============================================================================
Write-Host "[1/3] Fixing IApprovalRepository.cs..." -ForegroundColor Yellow

$repoInterfaceContent = @'
using Enterprise.Documentation.Core.Domain.Models.Approval;

namespace Enterprise.Documentation.Core.Application.Interfaces.Approval;

public interface IApprovalRepository
{
    // Document Approvals
    Task<DocumentApproval?> GetByIdAsync(int id);
    Task<DocumentApproval?> GetByDocumentIdAsync(string documentId);
    Task<IEnumerable<DocumentApproval>> GetPendingApprovalsAsync();
    Task<IEnumerable<DocumentApproval>> GetApprovalsByStatusAsync(string status);
    Task<IEnumerable<DocumentApproval>> GetApprovalsByApproverAsync(string approverEmail);
    Task<int> CreateAsync(DocumentApproval approval);
    Task UpdateAsync(DocumentApproval approval);

    // Approval History
    Task<IEnumerable<ApprovalHistoryEntry>> GetHistoryAsync(int approvalId);
    Task AddHistoryEntryAsync(ApprovalHistoryEntry entry);

    // Document Edits
    Task<IEnumerable<DocumentEdit>> GetEditsAsync(string documentId);
    Task AddEditAsync(DocumentEdit edit);

    // Regeneration Requests
    Task<RegenerationRequest?> GetRegenerationRequestAsync(int id);
    Task<int> CreateRegenerationRequestAsync(RegenerationRequest request);
    Task UpdateRegenerationRequestAsync(RegenerationRequest request);

    // Approvers
    Task<IEnumerable<Approver>> GetActiveApproversAsync();
    Task<Approver?> GetApproverByEmailAsync(string email);

    // Document ID Generation
    Task<int> GetNextSequenceNumberAsync(string documentType, int year);
}
'@

$repoInterfacePath = Join-Path $projectRoot "src\Core\Application\Interfaces\Approval\IApprovalRepository.cs"
[System.IO.File]::WriteAllText($repoInterfacePath, $repoInterfaceContent, $utf8NoBom)
Write-Host "  Fixed IApprovalRepository.cs to use int" -ForegroundColor Green

# ============================================================================
# Rewrite ApprovalService.cs to match interface
# ============================================================================
Write-Host "[2/3] Rewriting ApprovalService.cs..." -ForegroundColor Yellow

$approvalServiceContent = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Application.DTOs.Approval;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Domain.Models.Approval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.Approval;

public class ApprovalService : IApprovalService
{
    private readonly IApprovalRepository _repository;
    private readonly ITeamsNotificationService _teamsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApprovalService> _logger;
    private readonly string _catalogBasePath;

    public ApprovalService(
        IApprovalRepository repository,
        ITeamsNotificationService teamsService,
        IConfiguration configuration,
        ILogger<ApprovalService> logger)
    {
        _repository = repository;
        _teamsService = teamsService;
        _configuration = configuration;
        _logger = logger;
        _catalogBasePath = configuration["DocGenerator:CatalogBasePath"]
            ?? @"C:\Temp\Documentation-Catalog";
    }

    #region Queue Management

    public async Task<ApprovalResponse> CreateApprovalAsync(
        CreateApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var documentId = request.DocumentId;
        if (string.IsNullOrEmpty(documentId))
        {
            documentId = await GenerateDocumentIdAsync(request.DocumentType, cancellationToken);
        }

        var destinationPath = BuildDestinationPath(
            request.DatabaseName,
            request.SchemaName,
            request.DocumentType,
            request.ObjectName);

        var approvers = await _repository.GetActiveApproversAsync();
        var defaultApprover = approvers.FirstOrDefault()?.Email ?? "unassigned@company.com";

        var approval = new DocumentApproval
        {
            DocumentId = documentId,
            DocumentType = request.DocumentType,
            ObjectName = request.ObjectName,
            SchemaName = request.SchemaName,
            DatabaseName = request.DatabaseName,
            GeneratedFilePath = request.GeneratedFilePath,
            DestinationPath = destinationPath,
            Status = ApprovalStatus.Pending,
            Priority = request.Priority,
            RequestedBy = request.RequestedBy,
            RequestedAt = DateTime.UtcNow,
            AssignedTo = defaultApprover,
            TemplateUsed = request.TemplateUsed,
            CABNumber = request.CABNumber,
            MasterIndexId = request.MasterIndexId
        };

        var approvalId = await _repository.CreateAsync(approval);

        await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
        {
            ApprovalId = approvalId,
            DocumentId = documentId,
            Action = ApprovalActions.Created,
            ActionBy = "System",
            ActionAt = DateTime.UtcNow,
            NewStatus = "Pending",
            Notes = $"Document queued for approval. Template: {request.TemplateUsed}"
        });

        await _teamsService.SendApprovalNotificationAsync(
            documentId,
            request.ObjectName,
            request.DocumentType,
            request.RequestedBy,
            approvalId,
            cancellationToken);

        _logger.LogInformation("Document {DocumentId} queued for approval", documentId);

        return new ApprovalResponse
        {
            Id = approvalId,
            DocumentId = documentId,
            ObjectName = request.ObjectName,
            SchemaName = request.SchemaName,
            DatabaseName = request.DatabaseName,
            DocumentType = request.DocumentType,
            TemplateUsed = request.TemplateUsed,
            CABNumber = request.CABNumber,
            GeneratedFilePath = request.GeneratedFilePath,
            DestinationPath = destinationPath,
            Status = "Pending",
            Priority = request.Priority.ToString(),
            RequestedBy = request.RequestedBy,
            RequestedAt = DateTime.UtcNow,
            AssignedTo = defaultApprover
        };
    }

    public async Task<PendingApprovalsResponse> GetPendingApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        var approvals = await _repository.GetPendingApprovalsAsync();
        var list = approvals.Select(MapToResponse).ToList();

        return new PendingApprovalsResponse
        {
            Approvals = list,
            TotalCount = list.Count,
            PendingCount = list.Count,
            UrgentCount = list.Count(a => a.Priority == "Urgent")
        };
    }

    public async Task<ApprovalResponse?> GetApprovalByIdAsync(
        int approvalId,
        CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByIdAsync(approvalId);
        return approval == null ? null : MapToResponse(approval);
    }

    public async Task<ApprovalResponse?> GetApprovalByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByDocumentIdAsync(documentId);
        return approval == null ? null : MapToResponse(approval);
    }

    #endregion

    #region Approval Actions

    public async Task<ApprovalResponse> ApproveDocumentAsync(
        int approvalId,
        ApproveDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByIdAsync(approvalId)
            ?? throw new KeyNotFoundException($"Approval {approvalId} not found");

        if (approval.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot approve document in '{approval.Status}' status");

        string? finalPath = null;
        if (request.MoveToDestination && !string.IsNullOrEmpty(approval.DestinationPath))
        {
            finalPath = await MoveToDestinationAsync(approval);
        }

        approval.Status = ApprovalStatus.Approved;
        approval.ResolvedBy = request.ApprovedBy;
        approval.ResolvedAt = DateTime.UtcNow;
        approval.ResolutionNotes = request.Notes;

        await _repository.UpdateAsync(approval);

        await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
        {
            ApprovalId = approvalId,
            DocumentId = approval.DocumentId,
            Action = ApprovalActions.Approved,
            ActionBy = request.ApprovedBy,
            ActionAt = DateTime.UtcNow,
            PreviousStatus = "Pending",
            NewStatus = "Approved",
            Notes = request.Notes,
            DestinationPath = finalPath
        });

        await _teamsService.SendApprovalResultNotificationAsync(
            approval.DocumentId,
            approval.ObjectName,
            "Approved",
            request.ApprovedBy,
            request.Notes,
            cancellationToken);

        _logger.LogInformation("Document {DocumentId} approved by {ApprovedBy}", approval.DocumentId, request.ApprovedBy);

        return MapToResponse(approval);
    }

    public async Task<ApprovalResponse> RejectDocumentAsync(
        int approvalId,
        RejectDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByIdAsync(approvalId)
            ?? throw new KeyNotFoundException($"Approval {approvalId} not found");

        if (approval.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot reject document in '{approval.Status}' status");

        approval.Status = ApprovalStatus.Rejected;
        approval.ResolvedBy = request.RejectedBy;
        approval.ResolvedAt = DateTime.UtcNow;
        approval.ResolutionNotes = request.Reason;

        await _repository.UpdateAsync(approval);

        await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
        {
            ApprovalId = approvalId,
            DocumentId = approval.DocumentId,
            Action = ApprovalActions.Rejected,
            ActionBy = request.RejectedBy,
            ActionAt = DateTime.UtcNow,
            PreviousStatus = "Pending",
            NewStatus = "Rejected",
            Notes = request.Reason
        });

        await _teamsService.SendApprovalResultNotificationAsync(
            approval.DocumentId,
            approval.ObjectName,
            "Rejected",
            request.RejectedBy,
            request.Reason,
            cancellationToken);

        _logger.LogInformation("Document {DocumentId} rejected by {RejectedBy}: {Reason}",
            approval.DocumentId, request.RejectedBy, request.Reason);

        return MapToResponse(approval);
    }

    public async Task<ApprovalResponse> EditAndApproveDocumentAsync(
        int approvalId,
        EditAndApproveRequest request,
        CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByIdAsync(approvalId)
            ?? throw new KeyNotFoundException($"Approval {approvalId} not found");

        if (approval.Status != ApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot edit document in '{approval.Status}' status");

        // Record edits for AI training
        foreach (var edit in request.Edits)
        {
            await _repository.AddEditAsync(new DocumentEdit
            {
                ApprovalId = approvalId,
                DocumentId = approval.DocumentId,
                SectionName = edit.SectionName,
                OriginalText = edit.OriginalText,
                EditedText = edit.EditedText,
                EditReason = edit.EditReason,
                EditedBy = request.EditedBy,
                EditedAt = DateTime.UtcNow,
                ShouldTrainAI = true
            });
        }

        var finalPath = await MoveToDestinationAsync(approval);

        approval.Status = ApprovalStatus.Approved;
        approval.ResolvedBy = request.EditedBy;
        approval.ResolvedAt = DateTime.UtcNow;
        approval.ResolutionNotes = $"Approved with {request.Edits.Count} edits. {request.ApprovalNotes}";

        await _repository.UpdateAsync(approval);

        await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
        {
            ApprovalId = approvalId,
            DocumentId = approval.DocumentId,
            Action = ApprovalActions.Edited,
            ActionBy = request.EditedBy,
            ActionAt = DateTime.UtcNow,
            PreviousStatus = "Pending",
            NewStatus = "Approved",
            Notes = $"Edited ({request.Edits.Count} changes) and approved",
            DestinationPath = finalPath
        });

        _logger.LogInformation("Document {DocumentId} edited ({EditCount} edits) and approved by {EditedBy}",
            approval.DocumentId, request.Edits.Count, request.EditedBy);

        return MapToResponse(approval);
    }

    public async Task<ApprovalResponse> RequestRegenerationAsync(
        int approvalId,
        RegenerateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByIdAsync(approvalId)
            ?? throw new KeyNotFoundException($"Approval {approvalId} not found");

        var regenRequest = new RegenerationRequest
        {
            ApprovalId = approvalId,
            DocumentId = approval.DocumentId,
            OriginalVersion = approval.Version,
            FeedbackText = request.FeedbackText,
            FeedbackSection = request.FeedbackSection,
            AdditionalContext = request.AdditionalContext,
            RequestedBy = request.RequestedBy,
            RequestedAt = DateTime.UtcNow,
            Status = RegenerationStatus.Pending
        };

        await _repository.CreateRegenerationRequestAsync(regenRequest);

        approval.Status = ApprovalStatus.Regenerating;
        await _repository.UpdateAsync(approval);

        await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
        {
            ApprovalId = approvalId,
            DocumentId = approval.DocumentId,
            Action = ApprovalActions.Regenerated,
            ActionBy = request.RequestedBy,
            ActionAt = DateTime.UtcNow,
            PreviousStatus = "Pending",
            NewStatus = "Regenerating",
            Notes = $"Regeneration requested: {request.FeedbackText}"
        });

        _logger.LogInformation("Regeneration requested for {DocumentId} by {RequestedBy}",
            approval.DocumentId, request.RequestedBy);

        return MapToResponse(approval);
    }

    #endregion

    #region History & Analytics

    public async Task<ApprovalHistoryResponse> GetApprovalHistoryAsync(
        int approvalId,
        CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByIdAsync(approvalId);
        var history = await _repository.GetHistoryAsync(approvalId);

        return new ApprovalHistoryResponse
        {
            ApprovalId = approvalId,
            DocumentId = approval?.DocumentId ?? string.Empty,
            History = history.Select(h => new HistoryEntryDto
            {
                Action = h.Action,
                ActionBy = h.ActionBy,
                ActionAt = h.ActionAt,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                Notes = h.Notes
            }).ToList()
        };
    }

    public async Task<EditHistoryResponse> GetEditHistoryAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var edits = await _repository.GetEditsAsync(documentId);

        return new EditHistoryResponse
        {
            DocumentId = documentId,
            Edits = edits.Select(e => new EditEntryDto
            {
                SectionName = e.SectionName,
                OriginalText = e.OriginalText,
                EditedText = e.EditedText,
                EditReason = e.EditReason,
                Category = e.Category.ToString(),
                EditedBy = e.EditedBy,
                EditedAt = e.EditedAt
            }).ToList(),
            TotalEdits = edits.Count()
        };
    }

    #endregion

    #region Document ID Generation

    public async Task<string> GenerateDocumentIdAsync(
        string documentType,
        CancellationToken cancellationToken = default)
    {
        var typeCode = documentType.ToUpperInvariant() switch
        {
            "STOREDPROCEDURE" or "SP" => "SP",
            "TABLE" or "TBL" => "TBL",
            "VIEW" or "VW" => "VW",
            "FUNCTION" or "FN" => "FN",
            "DEFECTFIX" or "DEF" => "DEF",
            "BUSINESSREQUEST" or "BR" => "BR",
            _ => "DOC"
        };

        var year = DateTime.UtcNow.Year;
        var sequence = await _repository.GetNextSequenceNumberAsync(typeCode, year);

        var documentId = $"{typeCode}-{year}-{sequence:D3}";
        _logger.LogInformation("Generated document ID: {DocumentId}", documentId);

        return documentId;
    }

    #endregion

    #region Private Helpers

    private string BuildDestinationPath(string databaseName, string schemaName, string documentType, string objectName)
    {
        var typeFolder = documentType.ToUpperInvariant() switch
        {
            "STOREDPROCEDURE" or "SP" => "StoredProcedures",
            "TABLE" or "TBL" => "Tables",
            "VIEW" or "VW" => "Views",
            "FUNCTION" or "FN" => "Functions",
            _ => "Other"
        };

        return Path.Combine(_catalogBasePath, "Database", databaseName ?? "IRFS1", schemaName ?? "dbo", typeFolder, objectName);
    }

    private async Task<string> MoveToDestinationAsync(DocumentApproval approval)
    {
        if (string.IsNullOrEmpty(approval.DestinationPath))
            throw new InvalidOperationException("No destination path specified");

        Directory.CreateDirectory(approval.DestinationPath);

        var sourceFileName = Path.GetFileName(approval.GeneratedFilePath);
        var extension = Path.GetExtension(sourceFileName);
        var finalFileName = $"{approval.DocumentId}_{approval.ObjectName}{extension}";
        var finalPath = Path.Combine(approval.DestinationPath, finalFileName);

        if (File.Exists(approval.GeneratedFilePath))
        {
            File.Copy(approval.GeneratedFilePath, finalPath, overwrite: true);
            _logger.LogInformation("Copied document to {FinalPath}", finalPath);
        }
        else
        {
            throw new FileNotFoundException($"Source file not found: {approval.GeneratedFilePath}");
        }

        return finalPath;
    }

    private static ApprovalResponse MapToResponse(DocumentApproval approval)
    {
        return new ApprovalResponse
        {
            Id = approval.Id,
            DocumentId = approval.DocumentId,
            ObjectName = approval.ObjectName,
            SchemaName = approval.SchemaName,
            DatabaseName = approval.DatabaseName,
            DocumentType = approval.DocumentType,
            TemplateUsed = approval.TemplateUsed,
            CABNumber = approval.CABNumber,
            GeneratedFilePath = approval.GeneratedFilePath,
            DestinationPath = approval.DestinationPath,
            Status = approval.Status.ToString(),
            Priority = approval.Priority.ToString(),
            RequestedBy = approval.RequestedBy,
            RequestedAt = approval.RequestedAt,
            AssignedTo = approval.AssignedTo,
            DueDate = approval.DueDate,
            ResolvedBy = approval.ResolvedBy,
            ResolvedAt = approval.ResolvedAt,
            ResolutionNotes = approval.ResolutionNotes,
            Version = approval.Version,
            FileSizeBytes = approval.FileSizeBytes
        };
    }

    #endregion
}
'@

$servicePath = Join-Path $projectRoot "src\Core\Application\Services\Approval\ApprovalService.cs"
[System.IO.File]::WriteAllText($servicePath, $approvalServiceContent, $utf8NoBom)
Write-Host "  Rewrote ApprovalService.cs" -ForegroundColor Green

# ============================================================================
# Fix ExcelWatcherService.cs - add missing using
# ============================================================================
Write-Host "[3/3] Fixing ExcelWatcherService.cs..." -ForegroundColor Yellow

$watcherPath = Join-Path $projectRoot "src\Core\Application\Services\Watcher\ExcelWatcherService.cs"
if (Test-Path $watcherPath) {
    $content = [System.IO.File]::ReadAllText($watcherPath)

    # Add missing using if not present
    if ($content -notmatch 'using Microsoft\.Extensions\.DependencyInjection;') {
        $content = "using Microsoft.Extensions.DependencyInjection;`n" + $content
    }

    [System.IO.File]::WriteAllText($watcherPath, $content, $utf8NoBom)
    Write-Host "  Fixed ExcelWatcherService.cs" -ForegroundColor Green
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  FIXES COMPLETE - Rebuild now" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
