using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Enterprise.Documentation.Core.Application.Services.ApprovalTracking;

namespace Enterprise.Documentation.Core.Infrastructure.Services.ApprovalTracking;

public class ApprovalTrackingService : IApprovalTrackingService
{
    private readonly ILogger<ApprovalTrackingService> _logger;
    private readonly string _connectionString;

    public ApprovalTrackingService(
        ILogger<ApprovalTrackingService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public async Task TrackApprovalAsync(ApprovalAction action, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Tracking approval action: {Action} for DocId: {DocId} by {Approver}",
            action.Action, action.DocId, action.ApproverName);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Calculate content diff if edited
            string? contentDiff = null;
            if (action.Action == "Edited" && action.OriginalContent != null && action.EditedContent != null)
            {
                contentDiff = CalculateContentDiff(action.OriginalContent, action.EditedContent);
            }

            var sql = @"
                INSERT INTO DaQa.ApprovalTracking (
                    DocId,
                    Action,
                    ApproverUserId,
                    ApproverName,
                    ActionDate,
                    OriginalContent,
                    EditedContent,
                    ContentDiff,
                    ChangedFields,
                    RejectionReason,
                    RerequestPrompt,
                    ApproverFeedback,
                    QualityRating,
                    DocumentType,
                    ChangeType,
                    WasAIEnhanced,
                    CreatedDate
                )
                VALUES (
                    @DocId,
                    @Action,
                    @ApproverUserId,
                    @ApproverName,
                    @ActionDate,
                    @OriginalContent,
                    @EditedContent,
                    @ContentDiff,
                    @ChangedFields,
                    @RejectionReason,
                    @RerequestPrompt,
                    @ApproverFeedback,
                    @QualityRating,
                    @DocumentType,
                    @ChangeType,
                    @WasAIEnhanced,
                    @CreatedDate
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var trackingId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                action.DocId,
                action.Action,
                action.ApproverUserId,
                action.ApproverName,
                action.ActionDate,
                action.OriginalContent,
                action.EditedContent,
                ContentDiff = contentDiff,
                ChangedFields = action.ChangedFields != null ? JsonSerializer.Serialize(action.ChangedFields) : null,
                action.RejectionReason,
                action.RerequestPrompt,
                action.ApproverFeedback,
                action.QualityRating,
                action.DocumentType,
                action.ChangeType,
                action.WasAIEnhanced,
                CreatedDate = DateTime.UtcNow
            });

            _logger.LogInformation("Approval action tracked with TrackingId: {TrackingId}", trackingId);

            // Log summary for AI training insights
            if (action.Action == "Edited" && action.ChangedFields?.Any() == true)
            {
                _logger.LogInformation("Common edits for {DocumentType}: {Fields}",
                    action.DocumentType, string.Join(", ", action.ChangedFields));
            }

            if (action.Action == "Rejected")
            {
                _logger.LogWarning("Document rejected: {DocId}. Reason: {Reason}",
                    action.DocId, action.RejectionReason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking approval action for DocId: {DocId}", action.DocId);
            throw;
        }
    }

    public async Task<List<ApprovalFeedback>> GetFeedbackForTrainingAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving approval feedback for AI training (limit: {Limit})", limit);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT TOP (@Limit)
                    TrackingId,
                    DocId,
                    Action,
                    DocumentType,
                    ChangeType,
                    WasAIEnhanced,
                    QualityRating,
                    ApproverFeedback AS Feedback,
                    ChangedFields,
                    RejectionReason,
                    ActionDate
                FROM DaQa.ApprovalTracking
                WHERE Action IN ('Edited', 'Rejected', 'Rerequested')
                ORDER BY ActionDate DESC";

            var results = await connection.QueryAsync(sql, new { Limit = limit });

            var feedback = results.Select(r => new ApprovalFeedback
            {
                TrackingId = r.TrackingId,
                DocId = r.DocId,
                Action = r.Action,
                DocumentType = r.DocumentType,
                ChangeType = r.ChangeType,
                WasAIEnhanced = r.WasAIEnhanced ?? false,
                QualityRating = r.QualityRating,
                Feedback = r.Feedback,
                ChangedFields = string.IsNullOrWhiteSpace(r.ChangedFields)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(r.ChangedFields),
                RejectionReason = r.RejectionReason,
                ActionDate = r.ActionDate
            }).ToList();

            _logger.LogInformation("Retrieved {Count} approval feedback records for training", feedback.Count);

            return feedback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval feedback for training");
            throw;
        }
    }

    private string CalculateContentDiff(string original, string edited)
    {
        // Simple line-by-line diff for now
        var originalLines = original.Split('\n').Select(l => l.Trim()).ToList();
        var editedLines = edited.Split('\n').Select(l => l.Trim()).ToList();

        var diff = new List<string>();

        for (int i = 0; i < Math.Min(originalLines.Count, editedLines.Count); i++)
        {
            if (originalLines[i] != editedLines[i])
            {
                diff.Add($"Line {i + 1}:");
                diff.Add($"- {originalLines[i]}");
                diff.Add($"+ {editedLines[i]}");
            }
        }

        // Handle added lines
        if (editedLines.Count > originalLines.Count)
        {
            for (int i = originalLines.Count; i < editedLines.Count; i++)
            {
                diff.Add($"Line {i + 1} (added):");
                diff.Add($"+ {editedLines[i]}");
            }
        }

        // Handle removed lines
        if (originalLines.Count > editedLines.Count)
        {
            for (int i = editedLines.Count; i < originalLines.Count; i++)
            {
                diff.Add($"Line {i + 1} (removed):");
                diff.Add($"- {originalLines[i]}");
            }
        }

        return string.Join('\n', diff);
    }
}
