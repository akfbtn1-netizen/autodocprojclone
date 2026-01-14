using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using Enterprise.Documentation.Core.Application.Services.StoredProcedure;
using Enterprise.Documentation.Core.Application.Services.Notifications;
using Enterprise.Documentation.Core.Application.Helpers;
using Enterprise.Documentation.Core.Application.DTOs;

namespace Enterprise.Documentation.Core.Application.Services.Approval;

/// <summary>
/// ApprovalTrackingService with WorkflowEvents logging - matches interface exactly
/// </summary>
public class ApprovalTrackingService : IApprovalTrackingService
{
    private readonly ILogger<ApprovalTrackingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly IComprehensiveMasterIndexService _masterIndexService;
    private readonly IStoredProcedureDocumentationService _storedProcService;
    private readonly ITeamsNotificationService _teamsService;

    public ApprovalTrackingService(
        ILogger<ApprovalTrackingService> logger,
        IConfiguration configuration,
        IComprehensiveMasterIndexService masterIndexService,
        IStoredProcedureDocumentationService storedProcService,
        ITeamsNotificationService teamsService)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
        _masterIndexService = masterIndexService;
        _storedProcService = storedProcService;
        _teamsService = teamsService;
    }

    // Legacy methods for backward compatibility
    public async Task<ApprovalResponse> ApproveDocumentAsync(int approvalId, ApproveDocumentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Legacy approve method called for approval {ApprovalId}", approvalId);
        
        // Convert int to Guid and delegate to new API
        var newRequest = new ApprovalRequest
        {
            IsApproved = true,
            ApprovedBy = request.ApprovedBy,
            Comments = request.Comments
        };
        
        // For legacy compatibility, we'll use a mock Guid
        var result = await ProcessApprovalAsync(Guid.NewGuid(), newRequest);
        
        return new ApprovalResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }

    public async Task<ApprovalResponse> RejectDocumentAsync(int approvalId, RejectDocumentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Legacy reject method called for approval {ApprovalId}", approvalId);
        
        // Convert int to Guid and delegate to new API
        var newRequest = new ApprovalRequest
        {
            IsApproved = false,
            ApprovedBy = request.RejectedBy,
            RejectionReason = request.RejectionReason
        };
        
        // For legacy compatibility, we'll use a mock Guid
        var result = await ProcessApprovalAsync(Guid.NewGuid(), newRequest);
        
        return new ApprovalResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }

    // Step 8 API Methods with WorkflowEvents logging
    public async Task<IEnumerable<ApprovalDto>> GetPendingApprovalsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT 
                ApprovalId,
                DocumentId,
                DocumentType,
                RequestedBy,
                RequestedDate,
                ApproverEmail,
                ApprovalStatus,
                ApprovedDate,
                Comments,
                RejectionReason,
                ApprovedBy
            FROM DaQa.ApprovalWorkflow 
            WHERE ApprovalStatus = 'Pending'
            ORDER BY RequestedDate ASC";

        var approvals = await connection.QueryAsync<ApprovalDto>(sql);
        return approvals;
    }

    // Get single approval
    public async Task<ApprovalDto?> GetApprovalAsync(Guid approvalId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT 
                ApprovalId,
                DocumentId,
                DocumentType,
                RequestedBy,
                RequestedDate,
                ApproverEmail,
                ApprovalStatus,
                ApprovedDate,
                ApprovedBy,
                Comments,
                RejectionReason
            FROM DaQa.ApprovalWorkflow
            WHERE ApprovalId = @ApprovalId";

        return await connection.QueryFirstOrDefaultAsync<ApprovalDto>(sql, 
            new { ApprovalId = approvalId });
    }

    // Process approval (main method called by controller)
    public async Task<ApprovalResult> ProcessApprovalAsync(Guid approvalId, ApprovalRequest request)
    {
        _logger.LogInformation("🔥 USING UPDATED VERSION WITH WORKFLOW EVENTS 🔥");
        _logger.LogInformation("Processing approval: {ApprovalId}", approvalId);

        try
        {
            // Get approval details
            var approval = await GetApprovalAsync(approvalId);
            if (approval == null)
            {
                return new ApprovalResult 
                { 
                    Success = false, 
                    Message = $"Approval {approvalId} not found" 
                };
            }

            if (approval.ApprovalStatus != "Pending")
            {
                return new ApprovalResult 
                { 
                    Success = false, 
                    Message = $"Approval is not pending (current status: {approval.ApprovalStatus})" 
                };
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Update ApprovalWorkflow
            var updateSql = @"
                UPDATE DaQa.ApprovalWorkflow
                SET ApprovalStatus = @Status,
                    ApprovedBy = @ApprovedBy,
                    ApprovedDate = GETUTCDATE(),
                    Comments = @Comments,
                    RejectionReason = @RejectionReason
                WHERE ApprovalId = @ApprovalId
                  AND ApprovalStatus = 'Pending'";

            var rowsUpdated = await connection.ExecuteAsync(updateSql, new
            {
                ApprovalId = approvalId,
                Status = request.IsApproved ? "Approved" : "Rejected",
                ApprovedBy = request.ApprovedBy ?? "System",
                Comments = request.Comments,
                RejectionReason = request.RejectionReason
            });

            if (rowsUpdated == 0)
            {
                return new ApprovalResult 
                { 
                    Success = false, 
                    Message = "Failed to update approval (may have been processed already)" 
                };
            }

            _logger.LogInformation("Approval status updated to {Status}", 
                request.IsApproved ? "Approved" : "Rejected");

            // Get document details from DocumentChanges (Phase 2 - Task 2.1)
            DocumentChangeDetails? docDetails = null;
            try
            {
                var docDetailsSql = @"
                    SELECT 
                        dc.DocId,
                        dc.TableName,
                        dc.SchemaName,
                        dc.ColumnName,
                        dc.ChangeType,
                        dc.Description,
                        dc.AssignedTo,
                        dc.StoredProcedureName,
                        dc.JiraNumber,
                        dc.ChangeApplied,
                        dc.LocationOfCodeChange
                    FROM DaQa.DocumentChanges dc
					INNER JOIN DaQa.ApprovalWorkflow aw ON dc.DocId = aw.DocIdString
                    WHERE aw.ApprovalId = @ApprovalId";

                docDetails = await connection.QueryFirstOrDefaultAsync<DocumentChangeDetails>(docDetailsSql, 
                    new { ApprovalId = approvalId });

                if (docDetails == null)
                {
                    _logger.LogWarning("Could not find document details for ApprovalId: {ApprovalId}", approvalId);
                }
                else
                {
                    _logger.LogInformation("Document details retrieved: DocId={DocId}, TableName={TableName}, ChangeType={ChangeType}", 
                        docDetails.DocId, docDetails.TableName, docDetails.ChangeType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve document details for ApprovalId: {ApprovalId}", approvalId);
            }

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
                    WorkflowId = $"WF-{approval.DocumentId}",
                    EventType = request.IsApproved ? "DocumentApproved" : "DocumentRejected",
                    Status = "Completed",
                    Message = request.IsApproved 
                        ? $"Document approved by {request.ApprovedBy}" 
                        : $"Document rejected by {request.ApprovedBy}: {request.RejectionReason}"
                });

                _logger.LogInformation("✅ Workflow event logged successfully");
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
                    SET Status = @Status,
                        UpdatedAt = GETUTCDATE()
                    WHERE DocId IN (
                        SELECT dc.DocId
                        FROM DaQa.DocumentChanges dc
                        INNER JOIN DaQa.ApprovalWorkflow aw 
						ON dc.DocId = aw.DocIdString
                        WHERE aw.ApprovalId = @ApprovalId
                    )";

                await connection.ExecuteAsync(docUpdateSql, new
                {
                    ApprovalId = approvalId,
                    Status = request.IsApproved ? "Approved" : "Rejected"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update DocumentChanges (non-critical)");
            }

            // Log completion event
            if (request.IsApproved)
            {
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
                        WorkflowId = $"WF-{approval.DocumentId}",
                        EventType = "WorkflowCompleted",
                        Status = "Completed",
                        Message = "Approval workflow completed successfully"
                    });

                    _logger.LogInformation("✅ Completion event logged successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log completion event (non-critical)");
                }
            }

            _logger.LogInformation("Approval {ApprovalId} processed: {Status}", 
                approvalId, request.IsApproved ? "Approved" : "Rejected");

            // Call post-approval workflow if document was approved
            if (request.IsApproved)
            {
                try
                {
                    await ProcessApprovedDocumentAsync(approval, request, docDetails);
                    _logger.LogInformation("✅ Post-approval workflow completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Post-approval workflow failed for {ApprovalId}", approvalId);
                    // Don't fail the approval - it's already processed
                }
            }

            return new ApprovalResult 
            { 
                Success = true, 
                Message = request.IsApproved ? "Document approved successfully" : "Document rejected successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process approval {ApprovalId}", approvalId);
            return new ApprovalResult 
            { 
                Success = false, 
                Message = $"Error: {ex.Message}" 
            };
        }
    }

    /// <summary>
    /// Handles post-approval workflow: file movement, metadata population, and notifications
    /// </summary>
    private async Task ProcessApprovedDocumentAsync(ApprovalDto approval, ApprovalRequest request, DocumentChangeDetails? docDetails)
    {
        _logger.LogInformation("🚀 Starting post-approval workflow for document {DocumentId}", approval.DocumentId);

        // Step 1: Move draft file to final location
        await MoveDocumentFromDraftToFinalAsync(approval, docDetails);

        // Step 2: Populate MasterIndex with comprehensive metadata
     	await PopulateMasterIndexAsync(approval, docDetails);

        // Step 3: Embed custom properties in Word document  
        await EmbedCustomPropertiesAsync(approval, docDetails);

        // Step 4: Update stored procedure documentation (if applicable)
        await UpdateStoredProcedureDocumentationAsync(approval, docDetails);

        // Step 5: Send Teams notification to assignee
        await SendTeamsNotificationAsync(approval, request, docDetails);

        _logger.LogInformation("✅ Post-approval workflow completed for {DocumentId}", approval.DocumentId);
    }

    private async Task MoveDocumentFromDraftToFinalAsync(ApprovalDto approval, DocumentChangeDetails? docDetails)
    {
        if (docDetails == null)
        {
            _logger.LogWarning("Cannot move document - docDetails is null for {DocumentId}", approval.DocumentId);
            return;
        }

        try
        {
            // ═══════════════════════════════════════════════════════════
            // PHASE 3: CONSTRUCT FILE PATHS
            // ═══════════════════════════════════════════════════════════

            var draftsPath = _configuration["DocumentWorkflow:DraftsPath"] 
                ?? "C:\\Users\\Alexander.Kirby\\Desktop\\Doctest\\Documentation-Catalog\\Drafts";
            var finalPath = _configuration["DocumentWorkflow:FinalPath"]
                ?? "C:\\Users\\Alexander.Kirby\\Desktop\\Doctest\\Documentation-Catalog\\Database";

            // Build draft file path
            var draftFileName = $"{docDetails.DocId}_DRAFT_{SanitizeFileName(docDetails.TableName ?? docDetails.StoredProcedureName)}.docx";
            var draftFilePath = Path.Combine(draftsPath, draftFileName);

            // Build final file path (database-mirrored structure)
            var finalFolder = Path.Combine(
                finalPath,
                "IRFS1", // Database name
                docDetails.SchemaName ?? "dbo",
                docDetails.TableName ?? "StoredProcedures"
            );

            var finalFileName = $"{docDetails.DocId}_{SanitizeFileName(docDetails.TableName ?? docDetails.StoredProcedureName)}.docx";
            var finalFilePath = Path.Combine(finalFolder, finalFileName);

            _logger.LogInformation("Draft path: {DraftPath}", draftFilePath);
            _logger.LogInformation("Final path: {FinalPath}", finalFilePath);

            // ═══════════════════════════════════════════════════════════
            // PHASE 4: MOVE FILE
            // ═══════════════════════════════════════════════════════════

            // Check if draft exists
            if (!File.Exists(draftFilePath))
            {
                _logger.LogWarning("Draft file not found: {DraftPath}", draftFilePath);
                // Continue anyway - might be manual upload case
            }
            else
            {
                try
                {
                    // Create target directory
                    Directory.CreateDirectory(finalFolder);
                    
                    // Copy draft to final location
                    File.Copy(draftFilePath, finalFilePath, overwrite: true);
                    
                    _logger.LogInformation("Document moved to final location: {FinalPath}", finalFilePath);
                    
                    // Optional: Delete draft after successful copy
                    // File.Delete(draftFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to move document from {Draft} to {Final}", draftFilePath, finalFilePath);
                    // Continue - non-blocking
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file paths for document {DocumentId}", approval.DocumentId);
        }

        // Mark as completed for now
        await Task.CompletedTask;
    }

    private async Task PopulateMasterIndexAsync(ApprovalDto approval, DocumentChangeDetails? docDetails)
    {
        if (docDetails == null)
        {
            _logger.LogWarning("Cannot populate MasterIndex - docDetails is null for {DocumentId}", approval.DocumentId);
            return;
        }

        // ═══════════════════════════════════════════════════════════
        // PHASE 5: POPULATE MASTERINDEX
        // ═══════════════════════════════════════════════════════════

        try
        {
            _logger.LogInformation("Populating MasterIndex for DocId: {DocId}", docDetails.DocId);
            
            // Build final file path
            var finalPath = _configuration["DocumentWorkflow:FinalPath"]
                ?? "C:\\Users\\Alexander.Kirby\\Desktop\\Doctest\\Documentation-Catalog\\Database";
            var finalFolder = Path.Combine(
                finalPath,
                "IRFS1", // Database name
                docDetails.SchemaName ?? "dbo",
                docDetails.TableName ?? "StoredProcedures"
            );
            var finalFileName = $"{docDetails.DocId}_{SanitizeFileName(docDetails.TableName ?? docDetails.StoredProcedureName)}.docx";
            var finalFilePath = Path.Combine(finalFolder, finalFileName);
            
            await _masterIndexService.PopulateMasterIndexFromApprovedDocumentAsync(
                docId: docDetails.DocId,
                filePath: finalFilePath,
                jiraNumber: docDetails.JiraNumber ?? "BR-AUTO",
                cancellationToken: CancellationToken.None
            );
            
            _logger.LogInformation("✅ MasterIndex populated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to populate MasterIndex (non-critical)");
            // Continue - don't block workflow
        }
    }

    private async Task EmbedCustomPropertiesAsync(ApprovalDto approval, DocumentChangeDetails? docDetails)
    {
        if (docDetails == null)
        {
            _logger.LogWarning("Cannot embed custom properties - docDetails is null for {DocumentId}", approval.DocumentId);
            return;
        }

        // ═══════════════════════════════════════════════════════════
        // PHASE 6: EMBED CUSTOMPROPERTIES
        // ═══════════════════════════════════════════════════════════

        try
        {
            _logger.LogInformation("Embedding CustomProperties in document");
            
            // Build final file path
            var finalPath = _configuration["DocumentWorkflow:FinalPath"]
                ?? "C:\\Users\\Alexander.Kirby\\Desktop\\Doctest\\Documentation-Catalog\\Database";
            var finalFolder = Path.Combine(
                finalPath,
                "IRFS1", // Database name
                docDetails.SchemaName ?? "dbo",
                docDetails.TableName ?? "StoredProcedures"
            );
            var finalFileName = $"{docDetails.DocId}_{SanitizeFileName(docDetails.TableName ?? docDetails.StoredProcedureName)}.docx";
            var finalFilePath = Path.Combine(finalFolder, finalFileName);
            
            // Use CustomPropertiesHelper (static class)
            var properties = new Dictionary<string, string>
            {
                { "DocId", docDetails.DocId },
                { "ApprovalDate", DateTime.UtcNow.ToString("yyyy-MM-dd") },
                { "ApprovedBy", approval.ApprovedBy ?? "System" },
                { "DocumentType", DetermineDocumentType(docDetails.ChangeType) },
                { "SchemaName", docDetails.SchemaName ?? "" },
                { "TableName", docDetails.TableName ?? "" },
                { "Version", "1.0" }
            };
            
            CustomPropertiesHelper.AddCustomProperties(finalFilePath, properties);
            
            _logger.LogInformation("✅ CustomProperties embedded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to embed CustomProperties (non-critical)");
        }

        await Task.CompletedTask;
    }

    private async Task UpdateStoredProcedureDocumentationAsync(ApprovalDto approval, DocumentChangeDetails? docDetails)
    {
        if (docDetails == null)
        {
            _logger.LogWarning("Cannot update SP documentation - docDetails is null for {DocumentId}", approval.DocumentId);
            return;
        }

        // ═══════════════════════════════════════════════════════════
        // PHASE 7: SP DOCUMENTATION (IF APPLICABLE)
        // ═══════════════════════════════════════════════════════════

        // Only process if this change involves a stored procedure
        if (!string.IsNullOrWhiteSpace(docDetails.StoredProcedureName))
        {
            try
            {
                _logger.LogInformation("Processing SP documentation for: {SPName}", docDetails.StoredProcedureName);
                
                await _storedProcService.CreateOrUpdateSPDocumentationAsync(
                    procedureName: docDetails.StoredProcedureName,
                    documentId: docDetails.DocId,
                    cancellationToken: CancellationToken.None
                );
                
                _logger.LogInformation("✅ SP documentation processed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process SP documentation (non-critical)");
            }
        }
        else
        {
            _logger.LogDebug("Skipping SP documentation - no stored procedure name in docDetails for {DocumentId}", approval.DocumentId);
        }
    }

    private async Task SendTeamsNotificationAsync(ApprovalDto approval, ApprovalRequest request, DocumentChangeDetails? docDetails)
    {
        try
        {
            var teamsEnabled = _configuration.GetSection("TeamsNotifications")["Enabled"] == "true";
            if (!teamsEnabled)
            {
                _logger.LogDebug("Teams notifications are disabled");
                return;
            }

            var approvalBaseUrl = _configuration["TeamsNotifications:ApprovalBaseUrl"] ?? "https://portal.company.com";
            var finalPath = _configuration["DocumentWorkflow:FinalPath"] ?? "C:\\Temp\\Final";
            var documentPath = Path.Combine(finalPath, $"{approval.DocumentId}.docx");

            var notification = new Enterprise.Documentation.Core.Application.Services.Notifications.DraftReadyNotification
            {
                DocId = approval.DocumentId.ToString(),
                DocumentType = approval.DocumentType,
                Table = docDetails?.TableName ?? "ApprovalWorkflow", // Use actual table from docDetails
                JiraNumber = docDetails?.JiraNumber ?? ExtractJiraNumber(approval.Comments ?? approval.RequestedBy) ?? "BR-AUTO",
                Description = docDetails?.Description ?? $"Document {approval.DocumentId} has been approved and is ready for use",
                DocumentPath = documentPath,
                ApprovalUrl = $"{approvalBaseUrl}/documents/{approval.DocumentId}"
            };

            await _teamsService.SendDraftReadyNotificationAsync(notification);

            _logger.LogInformation("📢 Teams notification sent for approved document {DocumentId}", approval.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Teams notification for document {DocumentId}", approval.DocumentId);
            // Don't throw - notifications are nice-to-have
        }
    }

    private static string? ExtractJiraNumber(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(input, @"BR-\d{4}");
        return match.Success ? match.Value : null;
    }

    private static string? ExtractStoredProcedureName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Try to extract SP name from various patterns
        var patterns = new[]
        {
            @"sp_(\w+)",           // sp_ProcedureName
            @"usp_(\w+)",          // usp_ProcedureName
            @"proc_(\w+)",         // proc_ProcedureName
            @"SP\.(\w+)",          // SP.ProcedureName
            @"StoredProcedure_(\w+)" // StoredProcedure_Name
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
                return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Determines document type based on change type
    /// </summary>
    private string DetermineDocumentType(string? changeType)
    {
        if (string.IsNullOrWhiteSpace(changeType))
            return "BusinessRequest";
            
        if (changeType.Contains("Enhancement", StringComparison.OrdinalIgnoreCase))
            return "Enhancement";
        if (changeType.Contains("Defect", StringComparison.OrdinalIgnoreCase) || 
            changeType.Contains("Bug", StringComparison.OrdinalIgnoreCase))
            return "Defect";
            
        return "BusinessRequest";
    }

    /// <summary>
    /// Removes invalid file name characters and replaces them with underscores
    /// </summary>
    private string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unknown";
            
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        
        // Also replace spaces with underscores for cleaner filenames
        sanitized = sanitized.Replace(" ", "_");
        
        return sanitized;
    }

    // Get approval stats
    public async Task<ApprovalStats> GetApprovalStatsAsync()
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT 
                COUNT(*) as TotalApprovals,
                SUM(CASE WHEN ApprovalStatus = 'Pending' THEN 1 ELSE 0 END) as PendingApprovals,
                SUM(CASE WHEN ApprovalStatus = 'Approved' THEN 1 ELSE 0 END) as ApprovedCount,
                SUM(CASE WHEN ApprovalStatus = 'Rejected' THEN 1 ELSE 0 END) as RejectedCount
            FROM DaQa.ApprovalWorkflow";

        var result = await connection.QueryFirstAsync<ApprovalStats>(sql);
        return result;
    }

}