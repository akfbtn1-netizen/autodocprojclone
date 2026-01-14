// =============================================================================
// Agent #5: Post-Approval Pipeline - SignalR Documentation Hub
// Real-time hub for documentation workflow events
// =============================================================================

using Microsoft.AspNetCore.SignalR;
using Enterprise.Documentation.Core.Application.Services.PostApproval;

namespace Enterprise.Documentation.Api.Hubs;

/// <summary>
/// Real-time hub for documentation workflow events.
/// Provides live updates for approval status, generation progress, and lineage changes.
/// </summary>
public class DocumentationHub : Hub
{
    private readonly ILogger<DocumentationHub> _logger;

    public DocumentationHub(ILogger<DocumentationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to DocumentationHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from DocumentationHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Subscribe to specific document updates
    public async Task SubscribeToDocument(string documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doc:{documentId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to document {DocId}", Context.ConnectionId, documentId);
    }

    public async Task UnsubscribeFromDocument(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"doc:{documentId}");
    }

    // Subscribe to schema-level updates
    public async Task SubscribeToSchema(string schemaName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"schema:{schemaName}");
    }

    public async Task UnsubscribeFromSchema(string schemaName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"schema:{schemaName}");
    }

    // Subscribe to approval workflow updates
    public async Task SubscribeToApprovals()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "approvals");
    }

    public async Task UnsubscribeFromApprovals()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "approvals");
    }

    // Subscribe to lineage updates
    public async Task SubscribeToLineage(string schemaName, string objectName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"lineage:{schemaName}.{objectName}");
    }

    public async Task UnsubscribeFromLineage(string schemaName, string objectName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"lineage:{schemaName}.{objectName}");
    }

    // Subscribe to pipeline progress updates
    public async Task SubscribeToPipeline(int approvalId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"pipeline:{approvalId}");
    }

    public async Task UnsubscribeFromPipeline(int approvalId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"pipeline:{approvalId}");
    }
}

/// <summary>
/// Extension methods for broadcasting events through DocumentationHub.
/// Used by services to notify clients of workflow events.
/// </summary>
public static class DocumentationHubExtensions
{
    #region Approval Events

    public static async Task NotifyApprovalSubmitted(
        this IHubContext<DocumentationHub> hub,
        string documentId,
        string objectName,
        string submittedBy)
    {
        await hub.Clients.Group("approvals").SendAsync("ApprovalSubmitted", new
        {
            DocumentId = documentId,
            ObjectName = objectName,
            SubmittedBy = submittedBy,
            SubmittedAt = DateTime.UtcNow
        });
    }

    public static async Task NotifyApprovalStatusChanged(
        this IHubContext<DocumentationHub> hub,
        string documentId,
        string newStatus,
        string changedBy,
        string? comments = null)
    {
        await hub.Clients.All.SendAsync("ApprovalStatusChanged", new
        {
            DocumentId = documentId,
            Status = newStatus,
            ChangedBy = changedBy,
            Comments = comments,
            ChangedAt = DateTime.UtcNow
        });

        await hub.Clients.Group($"doc:{documentId}").SendAsync("StatusUpdated", documentId, newStatus, DateTime.UtcNow);
    }

    public static async Task NotifyApprovalCompleted(
        this IHubContext<DocumentationHub> hub,
        string documentId,
        string approvedBy,
        int? masterIndexId)
    {
        await hub.Clients.All.SendAsync("ApprovalCompleted", new
        {
            DocumentId = documentId,
            ApprovedBy = approvedBy,
            MasterIndexId = masterIndexId,
            CompletedAt = DateTime.UtcNow
        });
    }

    #endregion

    #region Generation Events

    public static async Task NotifyGenerationStarted(
        this IHubContext<DocumentationHub> hub,
        string documentId,
        string objectName,
        string documentType)
    {
        await hub.Clients.All.SendAsync("GenerationStarted", new
        {
            DocumentId = documentId,
            ObjectName = objectName,
            DocumentType = documentType,
            StartedAt = DateTime.UtcNow
        });
    }

    public static async Task NotifyGenerationProgress(
        this IHubContext<DocumentationHub> hub,
        string documentId,
        int percentComplete,
        string currentStep)
    {
        await hub.Clients.Group($"doc:{documentId}").SendAsync("GenerationProgress", new
        {
            DocumentId = documentId,
            PercentComplete = percentComplete,
            CurrentStep = currentStep,
            Timestamp = DateTime.UtcNow
        });
    }

    public static async Task NotifyGenerationCompleted(
        this IHubContext<DocumentationHub> hub,
        string documentId,
        string documentPath,
        int tokensUsed,
        decimal costUSD)
    {
        await hub.Clients.All.SendAsync("GenerationCompleted", new
        {
            DocumentId = documentId,
            DocumentPath = documentPath,
            TokensUsed = tokensUsed,
            CostUSD = costUSD,
            CompletedAt = DateTime.UtcNow
        });
    }

    #endregion

    #region Post-Approval Pipeline Events

    public static async Task NotifyPipelineStarted(
        this IHubContext<DocumentationHub> hub,
        int approvalId,
        string documentId)
    {
        await hub.Clients.All.SendAsync("PostApprovalPipelineStarted", new
        {
            ApprovalId = approvalId,
            DocumentId = documentId,
            StartedAt = DateTime.UtcNow
        });
    }

    public static async Task NotifyPipelineStepCompleted(
        this IHubContext<DocumentationHub> hub,
        int approvalId,
        string stepName,
        string status,
        long durationMs)
    {
        await hub.Clients.All.SendAsync("PipelineStepCompleted", new
        {
            ApprovalId = approvalId,
            StepName = stepName,
            Status = status,
            DurationMs = durationMs,
            CompletedAt = DateTime.UtcNow
        });
    }

    public static async Task NotifyPipelineCompleted(
        this IHubContext<DocumentationHub> hub,
        PostApprovalResult result)
    {
        await hub.Clients.All.SendAsync("PostApprovalPipelineCompleted", new
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

    #endregion

    #region Lineage Events

    public static async Task NotifyLineageExtracted(
        this IHubContext<DocumentationHub> hub,
        string schemaName,
        string objectName,
        int lineageCount)
    {
        await hub.Clients.Group($"lineage:{schemaName}.{objectName}").SendAsync("LineageExtracted", new
        {
            SchemaName = schemaName,
            ObjectName = objectName,
            LineageCount = lineageCount,
            ExtractedAt = DateTime.UtcNow
        });

        await hub.Clients.Group($"schema:{schemaName}").SendAsync("SchemaLineageUpdated", new
        {
            SchemaName = schemaName,
            ObjectName = objectName,
            LineageCount = lineageCount
        });
    }

    public static async Task NotifyImpactAnalysisComplete(
        this IHubContext<DocumentationHub> hub,
        ImpactAnalysisResult result)
    {
        await hub.Clients.All.SendAsync("ImpactAnalysisComplete", new
        {
            result.SchemaName,
            result.TableName,
            result.ColumnName,
            result.TotalAffectedObjects,
            result.RiskLevel,
            result.RiskScore
        });
    }

    #endregion

    #region Schema Change Events

    public static async Task NotifySchemaChanged(
        this IHubContext<DocumentationHub> hub,
        string schemaName,
        string objectName,
        string changeType)
    {
        await hub.Clients.Group($"schema:{schemaName}").SendAsync("SchemaChanged", new
        {
            SchemaName = schemaName,
            ObjectName = objectName,
            ChangeType = changeType,
            DetectedAt = DateTime.UtcNow
        });
    }

    public static async Task NotifyDocumentStale(
        this IHubContext<DocumentationHub> hub,
        string documentId,
        string reason)
    {
        await hub.Clients.Group($"doc:{documentId}").SendAsync("DocumentStale", new
        {
            DocumentId = documentId,
            Reason = reason,
            DetectedAt = DateTime.UtcNow
        });
    }

    #endregion
}
