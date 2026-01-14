// src/Core/Application/Services/Approval/ApprovalTrackingService.cs

using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using Enterprise.Documentation.Core.Application.Services.Workflow;
using Enterprise.Documentation.Core.Application.Services.StoredProcedure;
using Enterprise.Documentation.Core.Application.Helpers;

namespace Enterprise.Documentation.Core.Application.Services.Approval;

public interface IApprovalTrackingService
{
    Task<ApprovalResponse> ApproveDocumentAsync(int approvalId, ApproveDocumentRequest request, CancellationToken cancellationToken = default);
    Task<ApprovalResponse> RejectDocumentAsync(int approvalId, RejectDocumentRequest request, CancellationToken cancellationToken = default);
}

public class ApprovalTrackingService : IApprovalTrackingService
{
    private readonly ILogger<ApprovalTrackingService> _logger;
    private readonly IWorkflowEventService _workflowEventService;
    private readonly IComprehensiveMasterIndexService _masterIndexService;
    private readonly IStoredProcedureDocumentationService _spDocService;
    private readonly string _connectionString;

    public ApprovalTrackingService(
        ILogger<ApprovalTrackingService> logger,
        IWorkflowEventService workflowEventService,
        IComprehensiveMasterIndexService masterIndexService,
        IStoredProcedureDocumentationService spDocService,
        IConfiguration configuration)
    {
        _logger = logger;
        _workflowEventService = workflowEventService;
        _masterIndexService = masterIndexService;
        _spDocService = spDocService;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
    }

    public async Task<ApprovalResponse> ApproveDocumentAsync(
        int approvalId,
        ApproveDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting approval process for ApprovalId: {ApprovalId}", approvalId);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 1: Get approval details and validate
        // ═══════════════════════════════════════════════════════════════════
        var approval = await GetApprovalDetailsAsync(approvalId, cancellationToken);
        if (approval == null)
        {
            throw new InvalidOperationException($"Approval {approvalId} not found");
        }

        if (approval.Status != "Pending")
        {
            throw new InvalidOperationException($"Approval {approvalId} is not in Pending status (current: {approval.Status})");
        }

        // Generate WorkflowId from DocumentId
        var workflowId = $"WF-{approval.DocumentId}";

        // ═══════════════════════════════════════════════════════════════════
        // STEP 2: Publish DocumentApproved event
        // ═══════════════════════════════════════════════════════════════════
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.DocumentApproved,
            Status = WorkflowEventStatus.Completed,
            Message = $"Document {approval.DocumentId} approved by {request.ApprovedBy}",
            Timestamp = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                ApprovalId = approvalId,
                DocumentId = approval.DocumentId,
                ApprovedBy = request.ApprovedBy,
                Comments = request.Comments
            })
        }, cancellationToken);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 3: Update approval status in database
        // ═══════════════════════════════════════════════════════════════════
        await UpdateApprovalStatusAsync(
            approvalId,
            "Approved",
            request.ApprovedBy,
            request.Comments,
            cancellationToken);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 4: Generate final document (remove DRAFT from filename)
        // ═══════════════════════════════════════════════════════════════════
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.FinalDocumentGenerationStarted,
            Status = WorkflowEventStatus.InProgress,
            Message = "Generating final document (removing DRAFT)",
            Timestamp = DateTime.UtcNow
        }, cancellationToken);

        var finalGenStart = DateTime.UtcNow;

        var draftPath = await GetDraftDocumentPathAsync(approval.DocumentId, cancellationToken);
        if (string.IsNullOrEmpty(draftPath) || !File.Exists(draftPath))
        {
            throw new FileNotFoundException($"Draft document not found for {approval.DocumentId}");
        }

        // Create final path by removing DRAFT
        var finalPath = draftPath.Replace("_DRAFT_", "_");
        
        // Copy draft to final location
        File.Copy(draftPath, finalPath, overwrite: true);

        var finalGenDuration = (int)(DateTime.UtcNow - finalGenStart).TotalMilliseconds;

        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.FinalDocumentGenerationCompleted,
            Status = WorkflowEventStatus.Completed,
            Message = $"Final document created: {Path.GetFileName(finalPath)}",
            DurationMs = finalGenDuration,
            Timestamp = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                DraftPath = draftPath,
                FinalPath = finalPath,
                FileSize = new FileInfo(finalPath).Length
            })
        }, cancellationToken);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 5: Populate MasterIndex (116 fields, 14 phases)
        // ═══════════════════════════════════════════════════════════════════
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.MasterIndexPopulationStarted,
            Status = WorkflowEventStatus.InProgress,
            Message = "Populating MasterIndex with comprehensive metadata (116 fields, 14 phases)",
            Timestamp = DateTime.UtcNow
        }, cancellationToken);

        var masterIndexStart = DateTime.UtcNow;

        // THIS IS THE CRITICAL CALL - Returns IndexId
        var indexId = await _masterIndexService.PopulateMasterIndexFromApprovedDocumentAsync(
            approval.DocumentId,
            finalPath,
            approval.JiraNumber,
            cancellationToken);

        var masterIndexDuration = (int)(DateTime.UtcNow - masterIndexStart).TotalMilliseconds;

        // Get completeness stats
        var stats = await GetMasterIndexStatsAsync(indexId, cancellationToken);

        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.MasterIndexPopulationCompleted,
            Status = WorkflowEventStatus.Completed,
            Message = $"MasterIndex populated: {stats.PopulatedFields}/116 fields ({stats.CompletenessPercentage}%)",
            DurationMs = masterIndexDuration,
            Timestamp = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                IndexId = indexId,
                PopulatedFields = stats.PopulatedFields,
                TotalFields = 116,
                CompletenessPercentage = stats.CompletenessPercentage,
                QualityScore = stats.QualityScore
            })
        }, cancellationToken);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 6: Embed CustomProperties in final document
        // ═══════════════════════════════════════════════════════════════════
        _logger.LogInformation("Embedding CustomProperties metadata in final document");

        try
        {
            CustomPropertiesExtensions.EmbedApprovalMetadata(
                finalPath,
                approval.DocumentId,
                approval.JiraNumber,
                indexId,
                new ApprovalMetadataDto
                {
                    SchemaName = approval.SchemaName,
                    TableName = approval.TableName,
                    ColumnName = approval.ColumnName,
                    CodeQualityScore = approval.CodeQualityScore,
                    CodeQualityGrade = approval.CodeQualityGrade,
                    ReportedBy = approval.ReportedBy,
                    AssignedTo = approval.AssignedTo,
                    ApprovedBy = request.ApprovedBy,
                    DateRequested = approval.DateRequested
                });

            _logger.LogInformation("CustomProperties embedded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to embed CustomProperties - continuing workflow");
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 7: Check and update/create Stored Procedure documentation
        // ═══════════════════════════════════════════════════════════════════
        if (!string.IsNullOrEmpty(approval.StoredProcedureName))
        {
            _logger.LogInformation("Checking SP documentation for {ProcedureName}", approval.StoredProcedureName);

            try
            {
                var spDocId = await _spDocService.CreateOrUpdateSPDocumentationAsync(
                    approval.StoredProcedureName,
                    approval.DocumentId,
                    cancellationToken);

                _logger.LogInformation("SP documentation {Action}: {SpDocId}",
                    await _spDocService.SPDocumentationExistsAsync(approval.StoredProcedureName, cancellationToken)
                        ? "updated" : "created",
                    spDocId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create/update SP documentation - continuing workflow");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 8: Publish FileSavedToSharePoint event
        // ═══════════════════════════════════════════════════════════════════
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.FileSavedToSharePoint,
            Status = WorkflowEventStatus.Completed,
            Message = $"Final document saved: {Path.GetFileName(finalPath)}",
            Timestamp = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                FilePath = finalPath,
                FileSize = new FileInfo(finalPath).Length,
                MasterIndexId = indexId
            })
        }, cancellationToken);

        // ═══════════════════════════════════════════════════════════════════
        // STEP 9: Mark workflow as complete
        // ═══════════════════════════════════════════════════════════════════
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.WorkflowCompleted,
            Status = WorkflowEventStatus.Completed,
            Message = $"End-to-end workflow completed for {approval.DocumentId}",
            Timestamp = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                DocumentId = approval.DocumentId,
                FinalPath = finalPath,
                MasterIndexId = indexId,
                MetadataCompleteness = stats.CompletenessPercentage,
                ApprovedBy = request.ApprovedBy
            })
        }, cancellationToken);

        _logger.LogInformation("Approval workflow completed successfully for {DocumentId}", approval.DocumentId);

        return new ApprovalResponse
        {
            Success = true,
            Message = $"Document {approval.DocumentId} approved successfully",
            DocumentId = approval.DocumentId,
            FinalDocumentPath = finalPath,
            MasterIndexId = indexId,
            MetadataCompleteness = stats.CompletenessPercentage,
            WorkflowId = workflowId
        };
    }

    public async Task<ApprovalResponse> RejectDocumentAsync(
        int approvalId,
        RejectDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Rejecting document for ApprovalId: {ApprovalId}", approvalId);

        var approval = await GetApprovalDetailsAsync(approvalId, cancellationToken);
        if (approval == null)
        {
            throw new InvalidOperationException($"Approval {approvalId} not found");
        }

        var workflowId = $"WF-{approval.DocumentId}";

        // Update status
        await UpdateApprovalStatusAsync(
            approvalId,
            "Rejected",
            request.RejectedBy,
            request.RejectionReason,
            cancellationToken);

        // Publish rejection event
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.DocumentRejected,
            Status = WorkflowEventStatus.Completed,
            Message = $"Document {approval.DocumentId} rejected by {request.RejectedBy}",
            Timestamp = DateTime.UtcNow,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                ApprovalId = approvalId,
                DocumentId = approval.DocumentId,
                RejectedBy = request.RejectedBy,
                RejectionReason = request.RejectionReason
            })
        }, cancellationToken);

        return new ApprovalResponse
        {
            Success = true,
            Message = $"Document {approval.DocumentId} rejected",
            DocumentId = approval.DocumentId
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIVATE HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════

    private async Task<ApprovalDetails?> GetApprovalDetailsAsync(int approvalId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var approval = await connection.QueryFirstOrDefaultAsync<ApprovalDetails>(@"
            SELECT 
                aw.ApprovalId,
                aw.DocumentId,
                aw.Status,
                dc.JiraNumber,
                dc.TableName,
                dc.ColumnName,
                dc.SchemaName,
                dc.ReportedBy,
                dc.AssignedTo,
                dc.DateRequested,
                dc.CodeQualityScore,
                dc.CodeQualityGrade,
                dc.StoredProcedureName
            FROM DaQa.ApprovalWorkflow aw
            INNER JOIN DaQa.DocumentChanges dc ON aw.DocumentId = dc.DocId
            WHERE aw.ApprovalId = @ApprovalId",
            new { ApprovalId = approvalId });

        return approval;
    }

    private async Task UpdateApprovalStatusAsync(
        int approvalId,
        string status,
        string approvedBy,
        string? comments,
        CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await connection.ExecuteAsync(@"
            UPDATE DaQa.ApprovalWorkflow
            SET Status = @Status,
                ApprovedBy = @ApprovedBy,
                ApprovedDate = GETUTCDATE(),
                Comments = @Comments
            WHERE ApprovalId = @ApprovalId",
            new
            {
                ApprovalId = approvalId,
                Status = status,
                ApprovedBy = approvedBy,
                Comments = comments
            });
    }

    private async Task<string?> GetDraftDocumentPathAsync(string docId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var path = await connection.QueryFirstOrDefaultAsync<string>(@"
            SELECT DraftFilePath
            FROM DaQa.DocumentChanges
            WHERE DocId = @DocId",
            new { DocId = docId });

        return path;
    }

    private async Task<MasterIndexStats> GetMasterIndexStatsAsync(string indexId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var stats = await connection.QueryFirstOrDefaultAsync<MasterIndexStats>(@"
            SELECT 
                CompletenessScore AS PopulatedFields,
                MetadataCompleteness AS CompletenessPercentage,
                QualityScore
            FROM DaQa.MasterIndex
            WHERE IndexID = @IndexId",
            new { IndexId = indexId });

        return stats ?? new MasterIndexStats
        {
            PopulatedFields = 0,
            CompletenessPercentage = 0,
            QualityScore = 0
        };
    }
}

// ═══════════════════════════════════════════════════════════════════
// MODELS
// ═══════════════════════════════════════════════════════════════════

public class ApprovalDetails
{
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string JiraNumber { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public string? SchemaName { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? DateRequested { get; set; }
    public int? CodeQualityScore { get; set; }
    public string? CodeQualityGrade { get; set; }
    public string? StoredProcedureName { get; set; }
}

public class ApproveDocumentRequest
{
    public string ApprovedBy { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

public class RejectDocumentRequest
{
    public string RejectedBy { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
}

public class ApprovalResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string? FinalDocumentPath { get; set; }
    public string? MasterIndexId { get; set; }
    public int? MetadataCompleteness { get; set; }
    public string? WorkflowId { get; set; }
}

public class MasterIndexStats
{
    public int PopulatedFields { get; set; }
    public int CompletenessPercentage { get; set; }
    public decimal? QualityScore { get; set; }
}
