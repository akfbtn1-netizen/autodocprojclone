// =============================================================================
// Agent #5: Post-Approval Pipeline - Orchestrator Service
// Coordinates the complete post-approval workflow after document approval
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Enterprise.Documentation.Core.Application.Services.PostApproval;

/// <summary>
/// Orchestrates the complete post-approval pipeline:
/// 1. Finalize Metadata (embedding + classification)
/// 2. Stamp Document (Shadow Metadata)
/// 3. Populate MasterIndex (115 columns)
/// 4. Extract Lineage (if stored procedure)
/// 5. Broadcast Updates (SignalR)
/// </summary>
public class PostApprovalOrchestrator : IPostApprovalOrchestrator
{
    private readonly ILogger<PostApprovalOrchestrator> _logger;
    private readonly IMetadataFinalizationService _finalizationService;
    private readonly IMetadataStampingService _stampingService;
    private readonly IMasterIndexPopulationService _populationService;
    private readonly IColumnLineageService _lineageService;
    private readonly IHubContext<Api.Hubs.DocumentationHub>? _hubContext;
    private readonly string _connectionString;

    public PostApprovalOrchestrator(
        ILogger<PostApprovalOrchestrator> logger,
        IMetadataFinalizationService finalizationService,
        IMetadataStampingService stampingService,
        IMasterIndexPopulationService populationService,
        IColumnLineageService lineageService,
        IConfiguration configuration,
        IHubContext<Api.Hubs.DocumentationHub>? hubContext = null)
    {
        _logger = logger;
        _finalizationService = finalizationService;
        _stampingService = stampingService;
        _populationService = populationService;
        _lineageService = lineageService;
        _hubContext = hubContext;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<PostApprovalResult> ExecuteAsync(
        int approvalId,
        string approvedBy,
        string? comments = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new PostApprovalResult { ApprovalId = approvalId };

        _logger.LogInformation("Starting post-approval pipeline for approval {ApprovalId}", approvalId);

        try
        {
            // Notify pipeline started
            await NotifyPipelineStartedAsync(approvalId, "");

            // Step 1: Load approval and draft metadata
            var stepSw = Stopwatch.StartNew();
            var approval = await LoadApprovalAsync(approvalId, ct);
            if (approval == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Approval {approvalId} not found";
                return result;
            }

            result.DocumentId = approval.DocumentId;
            var draftMetadata = JsonSerializer.Deserialize<ExtractedMetadata>(approval.ExtractedMetadata!)
                ?? throw new InvalidOperationException("Draft metadata not found");

            result.AddStep("LoadApproval", "Completed", stepSw.ElapsedMilliseconds, $"Document: {approval.DocumentId}");
            await NotifyPipelineStepCompletedAsync(approvalId, "LoadApproval", "Completed", stepSw.ElapsedMilliseconds);

            // Step 2: Finalize Metadata (embedding + classification)
            stepSw.Restart();
            var finalizedMetadata = await _finalizationService.FinalizeMetadataAsync(
                approvalId, approval.DocumentPath, draftMetadata, ct);
            finalizedMetadata.ApprovedBy = approvedBy;
            finalizedMetadata.ApprovedAt = DateTime.UtcNow;
            finalizedMetadata.ApproverComments = comments;

            result.AddStep("FinalizeMetadata", "Completed", stepSw.ElapsedMilliseconds,
                $"Tokens: {finalizedMetadata.TokensUsed}, Cost: ${finalizedMetadata.GenerationCostUSD:F4}");
            await NotifyPipelineStepCompletedAsync(approvalId, "FinalizeMetadata", "Completed", stepSw.ElapsedMilliseconds);

            // Step 3: Stamp Document with Shadow Metadata
            stepSw.Restart();
            var stampResult = await _stampingService.StampDocumentAsync(approval.DocumentPath, finalizedMetadata, ct);

            var stampStatus = stampResult.Success ? "Completed" : "Warning";
            result.AddStep("StampDocument", stampStatus, stepSw.ElapsedMilliseconds,
                stampResult.Success ? $"Properties: {stampResult.PropertiesStamped}" : stampResult.ErrorMessage);
            await NotifyPipelineStepCompletedAsync(approvalId, "StampDocument", stampStatus, stepSw.ElapsedMilliseconds);

            // Step 4: Populate MasterIndex
            stepSw.Restart();
            var populationResult = await _populationService.PopulateAsync(approvalId, finalizedMetadata, ct);
            finalizedMetadata.MasterIndexId = populationResult.MasterIndexId;
            result.MasterIndexId = populationResult.MasterIndexId;

            var popStatus = populationResult.Success ? "Completed" : "Failed";
            result.AddStep("PopulateMasterIndex", popStatus, stepSw.ElapsedMilliseconds,
                populationResult.Success
                    ? $"MasterIndexId: {populationResult.MasterIndexId}, Columns: {populationResult.ColumnsPopulated}"
                    : populationResult.ErrorMessage);
            await NotifyPipelineStepCompletedAsync(approvalId, "PopulateMasterIndex", popStatus, stepSw.ElapsedMilliseconds);

            // Step 5: Extract Lineage (if stored procedure)
            if (finalizedMetadata.ObjectType == "StoredProcedure" || finalizedMetadata.ObjectType == "P")
            {
                stepSw.Restart();
                var definition = await GetProcedureDefinitionAsync(finalizedMetadata.SchemaName, finalizedMetadata.ObjectName, ct);

                if (!string.IsNullOrEmpty(definition))
                {
                    var lineageResult = await _lineageService.ExtractLineageAsync(
                        finalizedMetadata.SchemaName, finalizedMetadata.ObjectName, definition, ct);

                    var lineageStatus = lineageResult.Success ? "Completed" : "Warning";
                    result.AddStep("ExtractLineage", lineageStatus, stepSw.ElapsedMilliseconds,
                        $"Lineage entries: {lineageResult.ColumnLineages.Count}");
                    await NotifyPipelineStepCompletedAsync(approvalId, "ExtractLineage", lineageStatus, stepSw.ElapsedMilliseconds);

                    if (lineageResult.Success)
                    {
                        await NotifyLineageExtractedAsync(
                            finalizedMetadata.SchemaName,
                            finalizedMetadata.ObjectName,
                            lineageResult.ColumnLineages.Count);
                    }
                }
            }
            else
            {
                result.AddStep("ExtractLineage", "Skipped", 0, "Not a stored procedure");
            }

            // Step 6: Update approval status
            stepSw.Restart();
            await UpdateApprovalStatusAsync(approvalId, "Approved", finalizedMetadata, ct);
            result.AddStep("UpdateApprovalStatus", "Completed", stepSw.ElapsedMilliseconds);
            await NotifyPipelineStepCompletedAsync(approvalId, "UpdateApprovalStatus", "Completed", stepSw.ElapsedMilliseconds);

            // Log pipeline execution
            await LogPipelineExecutionAsync(result, finalizedMetadata);

            // Complete
            result.Success = true;
            result.FinalizedMetadata = finalizedMetadata;

            // Broadcast completion
            await NotifyApprovalCompletedAsync(result.DocumentId, approvedBy, result.MasterIndexId);
            await NotifyPipelineCompletedAsync(result);

            _logger.LogInformation(
                "Post-approval pipeline completed for {DocId}: MasterIndex={MasterIndexId}, Duration={Duration}ms",
                result.DocumentId, result.MasterIndexId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-approval pipeline failed for approval {ApprovalId}", approvalId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.AddStep("Error", "Failed", 0, ex.Message);
        }

        sw.Stop();
        result.TotalDurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<PostApprovalResult> RetryStepAsync(
        int approvalId,
        string stepName,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Retrying step {Step} for approval {ApprovalId}", stepName, approvalId);

        // TODO [5]: Implement step-specific retry logic
        // For now, just re-run the entire pipeline
        var approval = await LoadApprovalAsync(approvalId, ct);
        if (approval == null)
        {
            return new PostApprovalResult { Success = false, ErrorMessage = "Approval not found" };
        }

        return await ExecuteAsync(approvalId, approval.ReviewedBy ?? "System", null, ct);
    }

    #region Private Methods

    private async Task<ApprovalRecord?> LoadApprovalAsync(int approvalId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<ApprovalRecord>(@"
            SELECT
                ApprovalID, DocumentId, DocumentPath, DocumentType,
                SchemaName, ObjectName, ExtractedMetadata, FinalizedMetadata,
                Status, ReviewedBy, ReviewedDate, Comments
            FROM DaQa.DocumentApprovals
            WHERE ApprovalID = @ApprovalId",
            new { ApprovalId = approvalId });
    }

    private async Task<string?> GetProcedureDefinitionAsync(string schema, string name, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<string>(@"
            SELECT m.definition
            FROM sys.procedures p
            JOIN sys.schemas s ON p.schema_id = s.schema_id
            JOIN sys.sql_modules m ON p.object_id = m.object_id
            WHERE s.name = @Schema AND p.name = @Name",
            new { Schema = schema, Name = name });
    }

    private async Task UpdateApprovalStatusAsync(
        int approvalId,
        string status,
        FinalizedMetadata metadata,
        CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(@"
            UPDATE DaQa.DocumentApprovals SET
                Status = @Status,
                ReviewedDate = GETUTCDATE(),
                FinalizedMetadata = @Metadata,
                EmbeddingGeneratedAt = @EmbeddingAt,
                ClassificationEnrichedAt = @ClassificationAt,
                MasterIndexPopulatedAt = @MasterIndexAt,
                DocxStampedAt = @StampedAt
            WHERE ApprovalID = @ApprovalId",
            new
            {
                ApprovalId = approvalId,
                Status = status,
                Metadata = JsonSerializer.Serialize(metadata),
                EmbeddingAt = metadata.EmbeddingGeneratedAt,
                ClassificationAt = metadata.ClassificationEnrichedAt,
                MasterIndexAt = metadata.MasterIndexPopulatedAt,
                StampedAt = metadata.DocxStampedAt
            });
    }

    private async Task LogPipelineExecutionAsync(PostApprovalResult result, FinalizedMetadata metadata)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(@"
            INSERT INTO DaQa.PostApprovalPipelineLog (
                ApprovalId, DocumentId, PipelineStatus, StartedAt, CompletedAt,
                TotalDurationMs, StepsJson, ErrorMessage, TokensUsed,
                GenerationCostUSD, MasterIndexId, ExecutedBy
            ) VALUES (
                @ApprovalId, @DocumentId, @Status, @StartedAt, GETUTCDATE(),
                @DurationMs, @StepsJson, @ErrorMessage, @TokensUsed,
                @CostUSD, @MasterIndexId, @ExecutedBy
            )",
            new
            {
                result.ApprovalId,
                result.DocumentId,
                Status = result.Success ? "COMPLETED" : "FAILED",
                StartedAt = DateTime.UtcNow.AddMilliseconds(-result.TotalDurationMs),
                DurationMs = result.TotalDurationMs,
                StepsJson = JsonSerializer.Serialize(result.Steps),
                result.ErrorMessage,
                TokensUsed = metadata.TokensUsed,
                CostUSD = metadata.GenerationCostUSD,
                result.MasterIndexId,
                ExecutedBy = metadata.ApprovedBy
            });
    }

    private class ApprovalRecord
    {
        public int ApprovalID { get; set; }
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentPath { get; set; } = string.Empty;
        public string? DocumentType { get; set; }
        public string? SchemaName { get; set; }
        public string? ObjectName { get; set; }
        public string? ExtractedMetadata { get; set; }
        public string? FinalizedMetadata { get; set; }
        public string? Status { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string? Comments { get; set; }
    }

    #endregion

    #region SignalR Notifications

    private async Task NotifyPipelineStartedAsync(int approvalId, string documentId)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.All.SendAsync("PostApprovalPipelineStarted", new
        {
            ApprovalId = approvalId,
            DocumentId = documentId,
            StartedAt = DateTime.UtcNow
        });
    }

    private async Task NotifyPipelineStepCompletedAsync(int approvalId, string stepName, string status, long durationMs)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.All.SendAsync("PipelineStepCompleted", new
        {
            ApprovalId = approvalId,
            StepName = stepName,
            Status = status,
            DurationMs = durationMs,
            CompletedAt = DateTime.UtcNow
        });
    }

    private async Task NotifyPipelineCompletedAsync(PostApprovalResult result)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.All.SendAsync("PostApprovalPipelineCompleted", new
        {
            result.ApprovalId,
            result.DocumentId,
            result.Success,
            result.MasterIndexId,
            result.TotalDurationMs,
            Steps = result.Steps.Select(s => new { s.Name, s.Status, s.DurationMs }),
            CompletedAt = DateTime.UtcNow
        });
    }

    private async Task NotifyApprovalCompletedAsync(string documentId, string approvedBy, int? masterIndexId)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.All.SendAsync("ApprovalCompleted", new
        {
            DocumentId = documentId,
            ApprovedBy = approvedBy,
            MasterIndexId = masterIndexId,
            CompletedAt = DateTime.UtcNow
        });
    }

    private async Task NotifyLineageExtractedAsync(string schemaName, string objectName, int lineageCount)
    {
        if (_hubContext == null) return;
        await _hubContext.Clients.Group($"lineage:{schemaName}.{objectName}").SendAsync("LineageExtracted", new
        {
            SchemaName = schemaName,
            ObjectName = objectName,
            LineageCount = lineageCount,
            ExtractedAt = DateTime.UtcNow
        });

        await _hubContext.Clients.Group($"schema:{schemaName}").SendAsync("SchemaLineageUpdated", new
        {
            SchemaName = schemaName,
            ObjectName = objectName,
            LineageCount = lineageCount
        });
    }

    #endregion
}
