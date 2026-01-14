// ApprovalTrackingService.cs
// FINAL VERSION - Schema corrected with working WorkflowEvents

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;

namespace DocGenerator.Services;

public class ApprovalTrackingService : IApprovalTrackingService
{
    private readonly ILogger<ApprovalTrackingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public ApprovalTrackingService(
        ILogger<ApprovalTrackingService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
    }

    public async Task ProcessApprovedDocumentAsync(
        string documentId,
        string approvedBy,
        string? comments,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Processing approved document: {DocumentId}", documentId);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Step 1: Update ApprovalWorkflow status
            var updateSql = @"
                UPDATE DaQa.ApprovalWorkflow
                SET ApprovalStatus = 'Approved',
                    ApprovedBy = @ApprovedBy,
                    ApprovedDate = GETUTCDATE(),
                    Comments = @Comments
                WHERE DocumentId = @DocumentId
                  AND ApprovalStatus = 'Pending'";

            var rowsUpdated = await connection.ExecuteAsync(updateSql, new
            {
                DocumentId = Guid.Parse(documentId),
                ApprovedBy = approvedBy,
                Comments = comments
            });

            if (rowsUpdated == 0)
            {
                _logger.LogWarning("No pending approval found for DocumentId: {DocumentId}", documentId);
                return;
            }

            _logger.LogInformation("Approval status updated for {DocumentId}", documentId);

            // Step 2: Log workflow event
            try
            {
                // DON'T specify EventId or Timestamp - let table defaults handle it
                var eventSql = @"
                    INSERT INTO DaQa.WorkflowEvents (
                        WorkflowId, EventType, Status, Message
                    ) VALUES (
                        @WorkflowId, @EventType, @Status, @Message
                    )";

                await connection.ExecuteAsync(eventSql, new
                {
                    WorkflowId = $"WF-{documentId}",
                    EventType = "DocumentApproved",
                    Status = "Completed",
                    Message = $"Document approved by {approvedBy}"
                });

                _logger.LogInformation("Workflow event logged for {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log workflow event (non-critical)");
            }

            // Step 3: Update DocumentChanges status
            try
            {
                var docUpdateSql = @"
                    UPDATE DaQa.DocumentChanges
                    SET Status = 'Approved',
                        UpdatedAt = GETUTCDATE()
                    WHERE DocId IN (
                        SELECT dc.DocId
                        FROM DaQa.DocumentChanges dc
                        INNER JOIN DaQa.ApprovalWorkflow aw ON CAST(dc.DocId AS NVARCHAR(50)) = CAST(aw.DocumentId AS NVARCHAR(50))
                        WHERE aw.DocumentId = @DocumentId
                    )";

                await connection.ExecuteAsync(docUpdateSql, new
                {
                    DocumentId = Guid.Parse(documentId)
                });

                _logger.LogInformation("DocumentChanges updated for {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update DocumentChanges (non-critical)");
            }

            // Step 4: Log completion event
            try
            {
                var completionSql = @"
                    INSERT INTO DaQa.WorkflowEvents (
                        WorkflowId, EventType, Status, Message
                    ) VALUES (
                        @WorkflowId, @EventType, @Status, @Message
                    )";

                await connection.ExecuteAsync(completionSql, new
                {
                    WorkflowId = $"WF-{documentId}",
                    EventType = "WorkflowCompleted",
                    Status = "Completed",
                    Message = "Approval workflow completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log completion event (non-critical)");
            }

            _logger.LogInformation("Document {DocumentId} approved successfully", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process approved document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task RejectDocumentAsync(
        string documentId,
        string rejectionReason,
        string rejectedBy,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Rejecting document: {DocumentId}", documentId);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Update ApprovalWorkflow status
            var updateSql = @"
                UPDATE DaQa.ApprovalWorkflow
                SET ApprovalStatus = 'Rejected',
                    ApprovedBy = @RejectedBy,
                    ApprovedDate = GETUTCDATE(),
                    RejectionReason = @RejectionReason,
                    Comments = CONCAT(ISNULL(Comments, ''), ' | Rejected: ', @RejectionReason)
                WHERE DocumentId = @DocumentId
                  AND ApprovalStatus = 'Pending'";

            var rowsUpdated = await connection.ExecuteAsync(updateSql, new
            {
                DocumentId = Guid.Parse(documentId),
                RejectedBy = rejectedBy,
                RejectionReason = rejectionReason
            });

            if (rowsUpdated == 0)
            {
                _logger.LogWarning("No pending approval found for DocumentId: {DocumentId}", documentId);
                return;
            }

            _logger.LogInformation("Document {DocumentId} rejected successfully", documentId);

            // Log workflow event
            try
            {
                var eventSql = @"
                    INSERT INTO DaQa.WorkflowEvents (
                        WorkflowId, EventType, Status, Message
                    ) VALUES (
                        @WorkflowId, @EventType, @Status, @Message
                    )";

                await connection.ExecuteAsync(eventSql, new
                {
                    WorkflowId = $"WF-{documentId}",
                    EventType = "DocumentRejected",
                    Status = "Completed",
                    Message = $"Document rejected by {rejectedBy}: {rejectionReason}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log workflow event (non-critical)");
            }

            // Update DocumentChanges status
            try
            {
                var docUpdateSql = @"
                    UPDATE DaQa.DocumentChanges
                    SET Status = 'Rejected',
                        UpdatedAt = GETUTCDATE()
                    WHERE DocId IN (
                        SELECT dc.DocId
                        FROM DaQa.DocumentChanges dc
                        INNER JOIN DaQa.ApprovalWorkflow aw ON CAST(dc.DocId AS NVARCHAR(50)) = CAST(aw.DocumentId AS NVARCHAR(50))
                        WHERE aw.DocumentId = @DocumentId
                    )";

                await connection.ExecuteAsync(docUpdateSql, new
                {
                    DocumentId = Guid.Parse(documentId)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update DocumentChanges (non-critical)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<ApprovalWorkflowStatus?> GetApprovalStatusAsync(
        string documentId,
        CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT 
                ApprovalId,
                ApprovalStatus as Status,
                ApprovedBy,
                ApprovedDate,
                RejectionReason,
                0 as RejectionCount
            FROM DaQa.ApprovalWorkflow
            WHERE DocumentId = @DocumentId";

        return await connection.QueryFirstOrDefaultAsync<ApprovalWorkflowStatus>(sql,
            new { DocumentId = Guid.Parse(documentId) });
    }
}
