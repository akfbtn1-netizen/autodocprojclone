# ============================================================================
# BUILD APPROVAL WORKFLOW - PART 3
# ApprovalService Implementation, Repository, and DI Registration
# ============================================================================
# Run this AFTER Part 1 (SQL/Models) and Part 2 (DTOs/Interface/Controller)
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  APPROVAL WORKFLOW - PART 3" -ForegroundColor White
Write-Host "  ApprovalService, Repository, DI Registration" -ForegroundColor Gray
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# FILE 1: ApprovalRepository.cs
# ============================================================================
Write-Host "[1/5] Creating ApprovalRepository.cs..." -ForegroundColor Yellow

$repoDir = Join-Path $projectRoot "src\Infrastructure\Persistence\Repositories"
if (-not (Test-Path $repoDir)) {
    New-Item -ItemType Directory -Path $repoDir -Force | Out-Null
}

$approvalRepoContent = @'
using System.Data;
using Dapper;
using Enterprise.Documentation.Core.Domain.Models.Approval;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Infrastructure.Persistence.Repositories;

public interface IApprovalRepository
{
    // Document Approvals
    Task<DocumentApproval?> GetByIdAsync(Guid id);
    Task<DocumentApproval?> GetByDocumentIdAsync(string documentId);
    Task<IEnumerable<DocumentApproval>> GetPendingApprovalsAsync();
    Task<IEnumerable<DocumentApproval>> GetApprovalsByStatusAsync(string status);
    Task<IEnumerable<DocumentApproval>> GetApprovalsByApproverAsync(string approverEmail);
    Task<Guid> CreateAsync(DocumentApproval approval);
    Task UpdateAsync(DocumentApproval approval);

    // Approval History
    Task<IEnumerable<ApprovalHistoryEntry>> GetHistoryAsync(Guid approvalId);
    Task AddHistoryEntryAsync(ApprovalHistoryEntry entry);

    // Document Edits
    Task<IEnumerable<DocumentEdit>> GetEditsAsync(string documentId);
    Task AddEditAsync(DocumentEdit edit);

    // Regeneration Requests
    Task<RegenerationRequest?> GetRegenerationRequestAsync(Guid id);
    Task<Guid> CreateRegenerationRequestAsync(RegenerationRequest request);
    Task UpdateRegenerationRequestAsync(RegenerationRequest request);

    // Approvers
    Task<IEnumerable<Approver>> GetActiveApproversAsync();
    Task<Approver?> GetApproverByEmailAsync(string email);

    // Document ID Generation
    Task<int> GetNextSequenceNumberAsync(string documentType, int year);
}

public class ApprovalRepository : IApprovalRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ApprovalRepository> _logger;

    public ApprovalRepository(
        IConfiguration configuration,
        ILogger<ApprovalRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");
        _logger = logger;
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    #region Document Approvals

    public async Task<DocumentApproval?> GetByIdAsync(Guid id)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<DocumentApproval>(
            @"SELECT * FROM DaQa.DocumentApprovals WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<DocumentApproval?> GetByDocumentIdAsync(string documentId)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<DocumentApproval>(
            @"SELECT * FROM DaQa.DocumentApprovals WHERE DocumentId = @DocumentId",
            new { DocumentId = documentId });
    }

    public async Task<IEnumerable<DocumentApproval>> GetPendingApprovalsAsync()
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<DocumentApproval>(
            @"SELECT * FROM DaQa.DocumentApprovals
              WHERE Status = 'Pending'
              ORDER BY QueuedAt ASC");
    }

    public async Task<IEnumerable<DocumentApproval>> GetApprovalsByStatusAsync(string status)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<DocumentApproval>(
            @"SELECT * FROM DaQa.DocumentApprovals
              WHERE Status = @Status
              ORDER BY QueuedAt DESC",
            new { Status = status });
    }

    public async Task<IEnumerable<DocumentApproval>> GetApprovalsByApproverAsync(string approverEmail)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<DocumentApproval>(
            @"SELECT * FROM DaQa.DocumentApprovals
              WHERE AssignedApprover = @ApproverEmail
              ORDER BY QueuedAt DESC",
            new { ApproverEmail = approverEmail });
    }

    public async Task<Guid> CreateAsync(DocumentApproval approval)
    {
        using var connection = CreateConnection();

        var sql = @"
            INSERT INTO DaQa.DocumentApprovals (
                Id, DocumentId, DocumentType, ObjectName, SchemaName, DatabaseName,
                TempFilePath, DestinationPath, Status, QueuedAt, AssignedApprover,
                GenerationDurationMs, TemplateUsed, TokensUsed, MasterIndexId
            ) VALUES (
                @Id, @DocumentId, @DocumentType, @ObjectName, @SchemaName, @DatabaseName,
                @TempFilePath, @DestinationPath, @Status, @QueuedAt, @AssignedApprover,
                @GenerationDurationMs, @TemplateUsed, @TokensUsed, @MasterIndexId
            )";

        await connection.ExecuteAsync(sql, approval);

        _logger.LogInformation("Created approval record {Id} for document {DocumentId}",
            approval.Id, approval.DocumentId);

        return approval.Id;
    }

    public async Task UpdateAsync(DocumentApproval approval)
    {
        using var connection = CreateConnection();

        var sql = @"
            UPDATE DaQa.DocumentApprovals SET
                Status = @Status,
                ApprovedAt = @ApprovedAt,
                ApprovedBy = @ApprovedBy,
                RejectedAt = @RejectedAt,
                RejectedBy = @RejectedBy,
                RejectionReason = @RejectionReason,
                FinalFilePath = @FinalFilePath,
                SharePointUrl = @SharePointUrl,
                EditCount = @EditCount,
                RegenerationCount = @RegenerationCount
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, approval);
    }

    #endregion

    #region Approval History

    public async Task<IEnumerable<ApprovalHistoryEntry>> GetHistoryAsync(Guid approvalId)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<ApprovalHistoryEntry>(
            @"SELECT * FROM DaQa.ApprovalHistory
              WHERE ApprovalId = @ApprovalId
              ORDER BY Timestamp ASC",
            new { ApprovalId = approvalId });
    }

    public async Task AddHistoryEntryAsync(ApprovalHistoryEntry entry)
    {
        using var connection = CreateConnection();

        var sql = @"
            INSERT INTO DaQa.ApprovalHistory (
                Id, ApprovalId, Action, PerformedBy, Timestamp, Details, PreviousStatus, NewStatus
            ) VALUES (
                @Id, @ApprovalId, @Action, @PerformedBy, @Timestamp, @Details, @PreviousStatus, @NewStatus
            )";

        await connection.ExecuteAsync(sql, entry);
    }

    #endregion

    #region Document Edits

    public async Task<IEnumerable<DocumentEdit>> GetEditsAsync(string documentId)
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<DocumentEdit>(
            @"SELECT * FROM DaQa.DocumentEdits
              WHERE DocumentId = @DocumentId
              ORDER BY EditedAt ASC",
            new { DocumentId = documentId });
    }

    public async Task AddEditAsync(DocumentEdit edit)
    {
        using var connection = CreateConnection();

        var sql = @"
            INSERT INTO DaQa.DocumentEdits (
                Id, DocumentId, ApprovalId, EditedBy, EditedAt, SectionName,
                OriginalContent, ModifiedContent, EditReason, TokenImpact
            ) VALUES (
                @Id, @DocumentId, @ApprovalId, @EditedBy, @EditedAt, @SectionName,
                @OriginalContent, @ModifiedContent, @EditReason, @TokenImpact
            )";

        await connection.ExecuteAsync(sql, edit);

        _logger.LogInformation("Recorded edit for document {DocumentId} by {EditedBy}",
            edit.DocumentId, edit.EditedBy);
    }

    #endregion

    #region Regeneration Requests

    public async Task<RegenerationRequest?> GetRegenerationRequestAsync(Guid id)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<RegenerationRequest>(
            @"SELECT * FROM DaQa.RegenerationRequests WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<Guid> CreateRegenerationRequestAsync(RegenerationRequest request)
    {
        using var connection = CreateConnection();

        var sql = @"
            INSERT INTO DaQa.RegenerationRequests (
                Id, OriginalApprovalId, RequestedBy, RequestedAt, Feedback,
                Status, NewApprovalId
            ) VALUES (
                @Id, @OriginalApprovalId, @RequestedBy, @RequestedAt, @Feedback,
                @Status, @NewApprovalId
            )";

        await connection.ExecuteAsync(sql, request);
        return request.Id;
    }

    public async Task UpdateRegenerationRequestAsync(RegenerationRequest request)
    {
        using var connection = CreateConnection();

        var sql = @"
            UPDATE DaQa.RegenerationRequests SET
                Status = @Status,
                CompletedAt = @CompletedAt,
                NewApprovalId = @NewApprovalId
            WHERE Id = @Id";

        await connection.ExecuteAsync(sql, request);
    }

    #endregion

    #region Approvers

    public async Task<IEnumerable<Approver>> GetActiveApproversAsync()
    {
        using var connection = CreateConnection();
        return await connection.QueryAsync<Approver>(
            @"SELECT * FROM DaQa.Approvers WHERE IsActive = 1 ORDER BY DisplayName");
    }

    public async Task<Approver?> GetApproverByEmailAsync(string email)
    {
        using var connection = CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<Approver>(
            @"SELECT * FROM DaQa.Approvers WHERE Email = @Email AND IsActive = 1",
            new { Email = email });
    }

    #endregion

    #region Document ID Generation

    public async Task<int> GetNextSequenceNumberAsync(string documentType, int year)
    {
        using var connection = CreateConnection();

        // Get highest sequence number for this type and year
        var maxSeq = await connection.QueryFirstOrDefaultAsync<int?>(
            @"SELECT MAX(CAST(
                SUBSTRING(DocumentId,
                    LEN(@TypePrefix) + 7,
                    LEN(DocumentId) - LEN(@TypePrefix) - 6
                ) AS INT))
              FROM DaQa.DocumentApprovals
              WHERE DocumentId LIKE @Pattern",
            new {
                TypePrefix = documentType,
                Pattern = $"{documentType}-{year}-%"
            });

        return (maxSeq ?? 0) + 1;
    }

    #endregion
}
'@

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText((Join-Path $repoDir "ApprovalRepository.cs"), $approvalRepoContent, $utf8NoBom)
Write-Host "  Created: ApprovalRepository.cs" -ForegroundColor Green

# ============================================================================
# FILE 2: ApprovalService.cs
# ============================================================================
Write-Host "[2/5] Creating ApprovalService.cs..." -ForegroundColor Yellow

$servicesDir = Join-Path $projectRoot "src\Core\Application\Services\Approval"
if (-not (Test-Path $servicesDir)) {
    New-Item -ItemType Directory -Path $servicesDir -Force | Out-Null
}

$approvalServiceContent = @'
using Enterprise.Documentation.Core.Application.DTOs.Approval;
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Domain.Models.Approval;
using Enterprise.Documentation.Infrastructure.Persistence.Repositories;
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

        // Base path for documentation catalog
        _catalogBasePath = configuration["DocGenerator:CatalogBasePath"]
            ?? @"C:\Temp\Documentation-Catalog";
    }

    #region Document ID Generation

    public async Task<string> GenerateDocumentIdAsync(string documentType)
    {
        // Map document types to prefix codes
        var typeCode = documentType.ToUpperInvariant() switch
        {
            "STOREDPROCEDURE" or "SP" => "SP",
            "TABLE" or "TBL" => "TBL",
            "VIEW" or "VW" => "VW",
            "FUNCTION" or "FN" => "FN",
            "DEFECTFIX" or "DEF" => "DEF",
            "BUSINESSREQUEST" or "BR" => "BR",
            "INDEX" or "IX" => "IX",
            "TRIGGER" or "TR" => "TR",
            _ => "DOC"
        };

        var year = DateTime.UtcNow.Year;
        var sequence = await _repository.GetNextSequenceNumberAsync(typeCode, year);

        // Format: TYPE-YYYY-NNN (e.g., SP-2025-001)
        var documentId = $"{typeCode}-{year}-{sequence:D3}";

        _logger.LogInformation("Generated document ID: {DocumentId}", documentId);
        return documentId;
    }

    #endregion

    #region Queue Management

    public async Task<QueueApprovalResponse> QueueForApprovalAsync(CreateApprovalRequest request)
    {
        try
        {
            // Generate document ID
            var documentId = await GenerateDocumentIdAsync(request.DocumentType);

            // Build destination path based on naming convention
            var destinationPath = BuildDestinationPath(
                request.DatabaseName,
                request.SchemaName,
                request.DocumentType,
                request.ObjectName);

            // Get default approver (round-robin or based on rules)
            var approvers = await _repository.GetActiveApproversAsync();
            var defaultApprover = approvers.FirstOrDefault()?.Email ?? "unassigned@company.com";

            // Create approval record
            var approval = new DocumentApproval
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                DocumentType = request.DocumentType,
                ObjectName = request.ObjectName,
                SchemaName = request.SchemaName,
                DatabaseName = request.DatabaseName,
                TempFilePath = request.TempFilePath,
                DestinationPath = destinationPath,
                Status = "Pending",
                QueuedAt = DateTime.UtcNow,
                AssignedApprover = defaultApprover,
                GenerationDurationMs = request.GenerationDurationMs,
                TemplateUsed = request.TemplateUsed,
                TokensUsed = request.TokensUsed,
                MasterIndexId = request.MasterIndexId
            };

            await _repository.CreateAsync(approval);

            // Add history entry
            await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
            {
                Id = Guid.NewGuid(),
                ApprovalId = approval.Id,
                Action = "Queued",
                PerformedBy = "System",
                Timestamp = DateTime.UtcNow,
                Details = $"Document queued for approval. Template: {request.TemplateUsed}",
                PreviousStatus = null,
                NewStatus = "Pending"
            });

            // Send Teams notification
            await _teamsService.SendApprovalNotificationAsync(
                documentId,
                request.ObjectName,
                request.DocumentType,
                defaultApprover,
                approval.Id);

            _logger.LogInformation(
                "Document {DocumentId} queued for approval. Approver: {Approver}",
                documentId, defaultApprover);

            return new QueueApprovalResponse
            {
                ApprovalId = approval.Id,
                DocumentId = documentId,
                Status = "Pending",
                AssignedApprover = defaultApprover,
                QueuedAt = approval.QueuedAt,
                DestinationPath = destinationPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue document for approval");
            throw;
        }
    }

    public async Task<IEnumerable<PendingApprovalDto>> GetPendingApprovalsAsync()
    {
        var approvals = await _repository.GetPendingApprovalsAsync();

        return approvals.Select(a => new PendingApprovalDto
        {
            ApprovalId = a.Id,
            DocumentId = a.DocumentId,
            DocumentType = a.DocumentType,
            ObjectName = a.ObjectName,
            SchemaName = a.SchemaName,
            DatabaseName = a.DatabaseName,
            QueuedAt = a.QueuedAt,
            AssignedApprover = a.AssignedApprover,
            PreviewUrl = $"/api/approval/{a.Id}/preview",
            EditCount = a.EditCount,
            RegenerationCount = a.RegenerationCount
        });
    }

    public async Task<ApprovalDetailDto?> GetApprovalDetailsAsync(Guid approvalId)
    {
        var approval = await _repository.GetByIdAsync(approvalId);
        if (approval == null) return null;

        var history = await _repository.GetHistoryAsync(approvalId);
        var edits = await _repository.GetEditsAsync(approval.DocumentId);

        return new ApprovalDetailDto
        {
            ApprovalId = approval.Id,
            DocumentId = approval.DocumentId,
            DocumentType = approval.DocumentType,
            ObjectName = approval.ObjectName,
            SchemaName = approval.SchemaName,
            DatabaseName = approval.DatabaseName,
            Status = approval.Status,
            QueuedAt = approval.QueuedAt,
            AssignedApprover = approval.AssignedApprover,
            TempFilePath = approval.TempFilePath,
            DestinationPath = approval.DestinationPath,
            GenerationDurationMs = approval.GenerationDurationMs,
            TemplateUsed = approval.TemplateUsed,
            TokensUsed = approval.TokensUsed,
            ApprovedAt = approval.ApprovedAt,
            ApprovedBy = approval.ApprovedBy,
            RejectedAt = approval.RejectedAt,
            RejectedBy = approval.RejectedBy,
            RejectionReason = approval.RejectionReason,
            FinalFilePath = approval.FinalFilePath,
            SharePointUrl = approval.SharePointUrl,
            EditCount = approval.EditCount,
            RegenerationCount = approval.RegenerationCount,
            History = history.Select(h => new ApprovalHistoryDto
            {
                Action = h.Action,
                PerformedBy = h.PerformedBy,
                Timestamp = h.Timestamp,
                Details = h.Details,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus
            }).ToList(),
            Edits = edits.Select(e => new DocumentEditDto
            {
                SectionName = e.SectionName,
                EditedBy = e.EditedBy,
                EditedAt = e.EditedAt,
                EditReason = e.EditReason,
                OriginalContent = e.OriginalContent,
                ModifiedContent = e.ModifiedContent
            }).ToList()
        };
    }

    #endregion

    #region Approval Actions

    public async Task<ApprovalActionResponse> ApproveDocumentAsync(Guid approvalId, ApproveDocumentRequest request)
    {
        var approval = await _repository.GetByIdAsync(approvalId);
        if (approval == null)
        {
            return new ApprovalActionResponse
            {
                Success = false,
                Message = "Approval record not found"
            };
        }

        if (approval.Status != "Pending")
        {
            return new ApprovalActionResponse
            {
                Success = false,
                Message = $"Cannot approve document in '{approval.Status}' status"
            };
        }

        try
        {
            // Move file to destination
            var finalPath = await MoveToDestinationAsync(approval);

            // Update approval record
            approval.Status = "Approved";
            approval.ApprovedAt = DateTime.UtcNow;
            approval.ApprovedBy = request.ApproverEmail;
            approval.FinalFilePath = finalPath;

            await _repository.UpdateAsync(approval);

            // Add history entry
            await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
            {
                Id = Guid.NewGuid(),
                ApprovalId = approvalId,
                Action = "Approved",
                PerformedBy = request.ApproverEmail,
                Timestamp = DateTime.UtcNow,
                Details = request.Comments ?? "Document approved",
                PreviousStatus = "Pending",
                NewStatus = "Approved"
            });

            // Send Teams notification
            await _teamsService.SendApprovalResultNotificationAsync(
                approval.DocumentId,
                approval.ObjectName,
                "Approved",
                request.ApproverEmail,
                request.Comments);

            _logger.LogInformation(
                "Document {DocumentId} approved by {Approver}",
                approval.DocumentId, request.ApproverEmail);

            return new ApprovalActionResponse
            {
                Success = true,
                DocumentId = approval.DocumentId,
                NewStatus = "Approved",
                Message = $"Document approved and moved to {finalPath}",
                FinalFilePath = finalPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve document {DocumentId}", approval.DocumentId);
            return new ApprovalActionResponse
            {
                Success = false,
                Message = $"Failed to approve: {ex.Message}"
            };
        }
    }

    public async Task<ApprovalActionResponse> RejectDocumentAsync(Guid approvalId, RejectDocumentRequest request)
    {
        var approval = await _repository.GetByIdAsync(approvalId);
        if (approval == null)
        {
            return new ApprovalActionResponse
            {
                Success = false,
                Message = "Approval record not found"
            };
        }

        if (approval.Status != "Pending")
        {
            return new ApprovalActionResponse
            {
                Success = false,
                Message = $"Cannot reject document in '{approval.Status}' status"
            };
        }

        // Update approval record
        approval.Status = "Rejected";
        approval.RejectedAt = DateTime.UtcNow;
        approval.RejectedBy = request.ApproverEmail;
        approval.RejectionReason = request.Reason;

        await _repository.UpdateAsync(approval);

        // Add history entry
        await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
        {
            Id = Guid.NewGuid(),
            ApprovalId = approvalId,
            Action = "Rejected",
            PerformedBy = request.ApproverEmail,
            Timestamp = DateTime.UtcNow,
            Details = request.Reason,
            PreviousStatus = "Pending",
            NewStatus = "Rejected"
        });

        // Send Teams notification
        await _teamsService.SendApprovalResultNotificationAsync(
            approval.DocumentId,
            approval.ObjectName,
            "Rejected",
            request.ApproverEmail,
            request.Reason);

        _logger.LogInformation(
            "Document {DocumentId} rejected by {Approver}. Reason: {Reason}",
            approval.DocumentId, request.ApproverEmail, request.Reason);

        return new ApprovalActionResponse
        {
            Success = true,
            DocumentId = approval.DocumentId,
            NewStatus = "Rejected",
            Message = "Document rejected"
        };
    }

    public async Task<ApprovalActionResponse> EditAndApproveAsync(Guid approvalId, EditAndApproveRequest request)
    {
        var approval = await _repository.GetByIdAsync(approvalId);
        if (approval == null)
        {
            return new ApprovalActionResponse
            {
                Success = false,
                Message = "Approval record not found"
            };
        }

        try
        {
            // Record all edits for AI training
            foreach (var edit in request.Edits)
            {
                await _repository.AddEditAsync(new DocumentEdit
                {
                    Id = Guid.NewGuid(),
                    DocumentId = approval.DocumentId,
                    ApprovalId = approvalId,
                    EditedBy = request.ApproverEmail,
                    EditedAt = DateTime.UtcNow,
                    SectionName = edit.SectionName,
                    OriginalContent = edit.OriginalContent,
                    ModifiedContent = edit.ModifiedContent,
                    EditReason = edit.EditReason,
                    TokenImpact = CalculateTokenImpact(edit.OriginalContent, edit.ModifiedContent)
                });
            }

            // Update edit count
            approval.EditCount += request.Edits.Count;

            // Move file to destination
            var finalPath = await MoveToDestinationAsync(approval);

            // Update approval record
            approval.Status = "Approved";
            approval.ApprovedAt = DateTime.UtcNow;
            approval.ApprovedBy = request.ApproverEmail;
            approval.FinalFilePath = finalPath;

            await _repository.UpdateAsync(approval);

            // Add history entry
            await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
            {
                Id = Guid.NewGuid(),
                ApprovalId = approvalId,
                Action = "EditedAndApproved",
                PerformedBy = request.ApproverEmail,
                Timestamp = DateTime.UtcNow,
                Details = $"Document approved with {request.Edits.Count} edits",
                PreviousStatus = "Pending",
                NewStatus = "Approved"
            });

            _logger.LogInformation(
                "Document {DocumentId} edited ({EditCount} edits) and approved by {Approver}",
                approval.DocumentId, request.Edits.Count, request.ApproverEmail);

            return new ApprovalActionResponse
            {
                Success = true,
                DocumentId = approval.DocumentId,
                NewStatus = "Approved",
                Message = $"Document approved with {request.Edits.Count} edits. Edits recorded for AI training.",
                FinalFilePath = finalPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit and approve document {DocumentId}", approval.DocumentId);
            return new ApprovalActionResponse
            {
                Success = false,
                Message = $"Failed to edit and approve: {ex.Message}"
            };
        }
    }

    public async Task<RegenerationResponse> RequestRegenerationAsync(Guid approvalId, RegenerateDocumentRequest request)
    {
        var approval = await _repository.GetByIdAsync(approvalId);
        if (approval == null)
        {
            return new RegenerationResponse
            {
                Success = false,
                Message = "Approval record not found"
            };
        }

        try
        {
            // Create regeneration request
            var regenRequest = new RegenerationRequest
            {
                Id = Guid.NewGuid(),
                OriginalApprovalId = approvalId,
                RequestedBy = request.RequestedBy,
                RequestedAt = DateTime.UtcNow,
                Feedback = request.Feedback,
                Status = "Pending"
            };

            await _repository.CreateRegenerationRequestAsync(regenRequest);

            // Update original approval
            approval.RegenerationCount++;
            approval.Status = "PendingRegeneration";
            await _repository.UpdateAsync(approval);

            // Add history entry
            await _repository.AddHistoryEntryAsync(new ApprovalHistoryEntry
            {
                Id = Guid.NewGuid(),
                ApprovalId = approvalId,
                Action = "RegenerationRequested",
                PerformedBy = request.RequestedBy,
                Timestamp = DateTime.UtcNow,
                Details = $"Regeneration requested with feedback: {request.Feedback}",
                PreviousStatus = "Pending",
                NewStatus = "PendingRegeneration"
            });

            _logger.LogInformation(
                "Regeneration requested for document {DocumentId}. Feedback: {Feedback}",
                approval.DocumentId, request.Feedback);

            // TODO: Trigger document regeneration with feedback
            // This would call DocGeneratorService with the feedback included

            return new RegenerationResponse
            {
                Success = true,
                RegenerationRequestId = regenRequest.Id,
                Message = "Document queued for regeneration with feedback",
                OriginalDocumentId = approval.DocumentId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request regeneration for {DocumentId}", approval.DocumentId);
            return new RegenerationResponse
            {
                Success = false,
                Message = $"Failed to request regeneration: {ex.Message}"
            };
        }
    }

    #endregion

    #region History and Analytics

    public async Task<IEnumerable<ApprovalHistoryDto>> GetApprovalHistoryAsync(Guid approvalId)
    {
        var history = await _repository.GetHistoryAsync(approvalId);

        return history.Select(h => new ApprovalHistoryDto
        {
            Action = h.Action,
            PerformedBy = h.PerformedBy,
            Timestamp = h.Timestamp,
            Details = h.Details,
            PreviousStatus = h.PreviousStatus,
            NewStatus = h.NewStatus
        });
    }

    public async Task<IEnumerable<DocumentEditDto>> GetDocumentEditsAsync(string documentId)
    {
        var edits = await _repository.GetEditsAsync(documentId);

        return edits.Select(e => new DocumentEditDto
        {
            SectionName = e.SectionName,
            EditedBy = e.EditedBy,
            EditedAt = e.EditedAt,
            EditReason = e.EditReason,
            OriginalContent = e.OriginalContent,
            ModifiedContent = e.ModifiedContent
        });
    }

    #endregion

    #region Helper Methods

    private string BuildDestinationPath(string databaseName, string schemaName, string documentType, string objectName)
    {
        // Map document type to folder name
        var typeFolder = documentType.ToUpperInvariant() switch
        {
            "STOREDPROCEDURE" or "SP" => "StoredProcedures",
            "TABLE" or "TBL" => "Tables",
            "VIEW" or "VW" => "Views",
            "FUNCTION" or "FN" => "Functions",
            "INDEX" or "IX" => "Indexes",
            "TRIGGER" or "TR" => "Triggers",
            _ => "Other"
        };

        // Build path: C:\Temp\Documentation-Catalog\Database\{DB}\{Schema}\{Type}\{ObjectName}\
        var path = Path.Combine(
            _catalogBasePath,
            "Database",
            databaseName ?? "IRFS1",
            schemaName ?? "dbo",
            typeFolder,
            objectName);

        return path;
    }

    private async Task<string> MoveToDestinationAsync(DocumentApproval approval)
    {
        // Ensure destination directory exists
        Directory.CreateDirectory(approval.DestinationPath);

        // Build final filename with document ID
        var sourceFileName = Path.GetFileName(approval.TempFilePath);
        var extension = Path.GetExtension(sourceFileName);
        var finalFileName = $"{approval.DocumentId}_{approval.ObjectName}{extension}";
        var finalPath = Path.Combine(approval.DestinationPath, finalFileName);

        // Copy file (keep original in temp for audit)
        if (File.Exists(approval.TempFilePath))
        {
            File.Copy(approval.TempFilePath, finalPath, overwrite: true);
            _logger.LogInformation("Copied document to {FinalPath}", finalPath);
        }
        else
        {
            throw new FileNotFoundException($"Source file not found: {approval.TempFilePath}");
        }

        return finalPath;
    }

    private int CalculateTokenImpact(string original, string modified)
    {
        // Simple token estimation (words / 0.75)
        var originalTokens = (int)((original?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0) / 0.75);
        var modifiedTokens = (int)((modified?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length ?? 0) / 0.75);

        return modifiedTokens - originalTokens;
    }

    #endregion
}
'@

[System.IO.File]::WriteAllText((Join-Path $servicesDir "ApprovalService.cs"), $approvalServiceContent, $utf8NoBom)
Write-Host "  Created: ApprovalService.cs" -ForegroundColor Green

# ============================================================================
# FILE 3: DI Registration Extension
# ============================================================================
Write-Host "[3/5] Creating ServiceCollectionExtensions.cs..." -ForegroundColor Yellow

$extensionsDir = Join-Path $projectRoot "src\Api\Extensions"
if (-not (Test-Path $extensionsDir)) {
    New-Item -ItemType Directory -Path $extensionsDir -Force | Out-Null
}

$diExtensionsContent = @'
using Enterprise.Documentation.Core.Application.Interfaces.Approval;
using Enterprise.Documentation.Core.Application.Services.Approval;
using Enterprise.Documentation.Infrastructure.Persistence.Repositories;

namespace Enterprise.Documentation.Api.Extensions;

public static class ApprovalServiceExtensions
{
    public static IServiceCollection AddApprovalWorkflow(this IServiceCollection services)
    {
        // Repository
        services.AddScoped<IApprovalRepository, ApprovalRepository>();

        // Services
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<ITeamsNotificationService, TeamsNotificationService>();

        return services;
    }
}
'@

[System.IO.File]::WriteAllText((Join-Path $extensionsDir "ApprovalServiceExtensions.cs"), $diExtensionsContent, $utf8NoBom)
Write-Host "  Created: ApprovalServiceExtensions.cs" -ForegroundColor Green

# ============================================================================
# FILE 4: DocGeneratorService Integration
# ============================================================================
Write-Host "[4/5] Creating integration snippet for DocGeneratorService..." -ForegroundColor Yellow

$integrationSnippet = @'
// ============================================================================
// ADD THIS TO YOUR DocGeneratorService.cs AFTER SUCCESSFUL DOCUMENT GENERATION
// ============================================================================

// Inject IApprovalService in constructor:
// private readonly IApprovalService _approvalService;

// After successful document generation, queue for approval:
/*
if (result.Success && result.DocumentPath != null)
{
    var approvalRequest = new CreateApprovalRequest
    {
        DocumentType = documentType,  // "StoredProcedure", "DefectFix", etc.
        ObjectName = objectName,
        SchemaName = schemaName ?? "dbo",
        DatabaseName = databaseName ?? "IRFS1",
        TempFilePath = result.DocumentPath,
        GenerationDurationMs = (int)result.GenerationTime.TotalMilliseconds,
        TemplateUsed = templateFileName,
        TokensUsed = tokensUsed,
        MasterIndexId = masterIndexId
    };

    var approvalResponse = await _approvalService.QueueForApprovalAsync(approvalRequest);

    _logger.LogInformation(
        "Document {DocumentId} queued for approval. Approver: {Approver}",
        approvalResponse.DocumentId,
        approvalResponse.AssignedApprover);

    // Return document ID to caller
    result.DocumentId = approvalResponse.DocumentId;
}
*/
'@

[System.IO.File]::WriteAllText((Join-Path $projectRoot "INTEGRATION-DOCGENERATOR.txt"), $integrationSnippet, $utf8NoBom)
Write-Host "  Created: INTEGRATION-DOCGENERATOR.txt" -ForegroundColor Green

# ============================================================================
# FILE 5: Program.cs Registration Snippet
# ============================================================================
Write-Host "[5/5] Creating Program.cs registration snippet..." -ForegroundColor Yellow

$programSnippet = @'
// ============================================================================
// ADD THIS TO YOUR Program.cs (after builder.Services...)
// ============================================================================

// Add this using statement at the top:
// using Enterprise.Documentation.Api.Extensions;

// Add this line in your service registrations:
// builder.Services.AddApprovalWorkflow();

// Example placement:
/*
var builder = WebApplication.CreateBuilder(args);

// ... existing service registrations ...

// Add approval workflow services
builder.Services.AddApprovalWorkflow();

// ... rest of configuration ...
*/
'@

[System.IO.File]::WriteAllText((Join-Path $projectRoot "INTEGRATION-PROGRAM.txt"), $programSnippet, $utf8NoBom)
Write-Host "  Created: INTEGRATION-PROGRAM.txt" -ForegroundColor Green

# ============================================================================
# Summary
# ============================================================================
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  PART 3 COMPLETE!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Created files:" -ForegroundColor White
Write-Host "  1. ApprovalRepository.cs - Database access layer" -ForegroundColor Gray
Write-Host "  2. ApprovalService.cs - Business logic implementation" -ForegroundColor Gray
Write-Host "  3. ApprovalServiceExtensions.cs - DI registration" -ForegroundColor Gray
Write-Host "  4. INTEGRATION-DOCGENERATOR.txt - Integration snippet" -ForegroundColor Gray
Write-Host "  5. INTEGRATION-PROGRAM.txt - Program.cs snippet" -ForegroundColor Gray
Write-Host ""
Write-Host "Key features implemented:" -ForegroundColor White
Write-Host "  - Document ID generation (TYPE-YYYY-NNN format)" -ForegroundColor Cyan
Write-Host "  - Folder path building for catalog structure" -ForegroundColor Cyan
Write-Host "  - Edit tracking for AI training improvement" -ForegroundColor Cyan
Write-Host "  - Teams notifications on queue/approve/reject" -ForegroundColor Cyan
Write-Host "  - Regeneration with feedback support" -ForegroundColor Cyan
Write-Host "  - Complete approval history tracking" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run SQL scripts from Part 1 to create DaQa tables" -ForegroundColor White
Write-Host "  2. Add 'using' statements and call AddApprovalWorkflow() in Program.cs" -ForegroundColor White
Write-Host "  3. Add IApprovalService injection to DocGeneratorService" -ForegroundColor White
Write-Host "  4. Rebuild solution and test the approval endpoints" -ForegroundColor White
Write-Host ""
Write-Host "API Endpoints available:" -ForegroundColor Yellow
Write-Host "  GET  /api/approval/pending" -ForegroundColor Cyan
Write-Host "  GET  /api/approval/{id}" -ForegroundColor Cyan
Write-Host "  POST /api/approval/{id}/approve" -ForegroundColor Cyan
Write-Host "  POST /api/approval/{id}/reject" -ForegroundColor Cyan
Write-Host "  POST /api/approval/{id}/edit" -ForegroundColor Cyan
Write-Host "  POST /api/approval/{id}/regenerate" -ForegroundColor Cyan
Write-Host "  GET  /api/approval/{id}/history" -ForegroundColor Cyan
Write-Host "  GET  /api/approval/generate-id/{documentType}" -ForegroundColor Cyan
Write-Host ""
