using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Api.Hubs;

public interface IApprovalHubClient
{
    Task DocumentGenerated(DocumentGeneratedEvent evt);
    Task ApprovalRequested(ApprovalRequestedEvent evt);
    Task ApprovalDecision(ApprovalDecisionEvent evt);
    Task ApprovalCompleted(ApprovalCompletedEvent evt);
    Task ApprovalRejected(ApprovalRejectedEvent evt);
    Task MasterIndexUpdated(MasterIndexUpdatedEvent evt);
    Task MasterIndexCreated(MasterIndexCreatedEvent evt);
    Task MasterIndexDeleted(MasterIndexDeletedEvent evt);
    Task StatisticsChanged(StatisticsChangedEvent evt);
    Task AgentStatusChanged(AgentStatusEvent evt);
    Task AgentError(AgentErrorEvent evt);
    Task DocumentUpdated(DocumentUpdatedEvent evt);
    Task DocumentSyncStatusChanged(DocumentSyncEvent evt);
    Task BulkOperationCompleted(BulkOperationEvent evt);
}

public class ApprovalHub : Hub<IApprovalHubClient>
{
    private readonly ILogger<ApprovalHub> _logger;

    public ApprovalHub(ILogger<ApprovalHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinApprovalGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"approver-{userId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-approvers");
        
        _logger.LogDebug("User {UserId} joined approval groups", userId);
    }

    public async Task LeaveApprovalGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"approver-{userId}");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "all-approvers");
        
        _logger.LogDebug("User {UserId} left approval groups", userId);
    }

    public async Task JoinDocumentGroup(string documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"document-{documentId}");
        _logger.LogDebug("User joined document group {DocumentId}", documentId);
    }

    public async Task LeaveDocumentGroup(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"document-{documentId}");
        _logger.LogDebug("User left document group {DocumentId}", documentId);
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-users");
        _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

// Event publisher service
public class ApprovalHubNotifier : IApprovalNotifier
{
    private readonly IHubContext<ApprovalHub, IApprovalHubClient> _hub;
    private readonly ILogger<ApprovalHubNotifier> _logger;

    public ApprovalHubNotifier(
        IHubContext<ApprovalHub, IApprovalHubClient> hub,
        ILogger<ApprovalHubNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyDocumentGenerated(string documentId, string title, decimal confidence)
    {
        var evt = new DocumentGeneratedEvent
        {
            DocumentId = documentId,
            Title = title,
            ConfidenceScore = confidence,
            GeneratedAt = DateTime.UtcNow,
            Status = "Generated"
        };

        await _hub.Clients.Group("all-approvers").DocumentGenerated(evt);
        
        _logger.LogInformation("Notified document generated: {DocumentId} ({Confidence:F2})", 
            documentId, confidence);
    }

    public async Task NotifyApprovalRequested(Guid approvalId, string title, string priority, string requester)
    {
        var evt = new ApprovalRequestedEvent
        {
            ApprovalId = approvalId,
            DocumentTitle = title,
            Priority = priority,
            RequesterName = requester,
            RequestedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-approvers").ApprovalRequested(evt);
        
        _logger.LogInformation("Notified approval requested: {ApprovalId} - {Title}", 
            approvalId, title);
    }

    public async Task NotifyApprovalDecision(Guid approvalId, string decision, string decidedBy)
    {
        var evt = new ApprovalDecisionEvent
        {
            ApprovalId = approvalId,
            Decision = decision,
            DecidedBy = decidedBy,
            DecisionDate = DateTime.UtcNow
        };

        // Notify all users and specific document watchers
        await _hub.Clients.Group("all-users").ApprovalDecision(evt);
        await _hub.Clients.Group($"document-{approvalId}").ApprovalDecision(evt);
        
        _logger.LogInformation("Notified approval decision: {ApprovalId} - {Decision} by {DecidedBy}", 
            approvalId, decision, decidedBy);
    }

    public async Task NotifyMasterIndexUpdated(int indexId, string field, object? oldValue, object? newValue)
    {
        var evt = new MasterIndexUpdatedEvent
        {
            IndexId = indexId,
            FieldName = field,
            OldValue = oldValue?.ToString(),
            NewValue = newValue?.ToString(),
            UpdatedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").MasterIndexUpdated(evt);
        
        _logger.LogDebug("Notified MasterIndex update: {IndexId} - {Field}", indexId, field);
    }

    public async Task NotifyAgentStatusChanged(string agentName, string status, int queueDepth)
    {
        var evt = new AgentStatusEvent
        {
            AgentName = agentName,
            Status = status,
            QueueDepth = queueDepth,
            StatusChanged = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").AgentStatusChanged(evt);
        
        _logger.LogDebug("Notified agent status change: {AgentName} - {Status}", agentName, status);
    }

    public async Task NotifyDocumentSyncStatusChanged(string documentPath, string syncStatus)
    {
        var evt = new DocumentSyncEvent
        {
            DocumentPath = documentPath,
            SyncStatus = syncStatus,
            SyncedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").DocumentSyncStatusChanged(evt);
        
        _logger.LogDebug("Notified document sync status change: {DocumentPath} - {SyncStatus}", 
            documentPath, syncStatus);
    }

    public async Task NotifyBulkOperationCompleted(string operation, int totalItems, int successCount, int failureCount)
    {
        var evt = new BulkOperationEvent
        {
            Operation = operation,
            TotalItems = totalItems,
            SuccessCount = successCount,
            FailureCount = failureCount,
            CompletedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-approvers").BulkOperationCompleted(evt);

        _logger.LogInformation("Notified bulk operation completed: {Operation} - {SuccessCount}/{TotalItems} succeeded",
            operation, successCount, totalItems);
    }

    public async Task NotifyApprovalCompleted(Guid approvalId, string documentTitle, string approvedBy)
    {
        var evt = new ApprovalCompletedEvent
        {
            ApprovalId = approvalId,
            DocumentTitle = documentTitle,
            ApprovedBy = approvedBy,
            CompletedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").ApprovalCompleted(evt);

        _logger.LogInformation("Notified approval completed: {ApprovalId} by {ApprovedBy}",
            approvalId, approvedBy);
    }

    public async Task NotifyApprovalRejected(Guid approvalId, string documentTitle, string rejectedBy, string reason)
    {
        var evt = new ApprovalRejectedEvent
        {
            ApprovalId = approvalId,
            DocumentTitle = documentTitle,
            RejectedBy = rejectedBy,
            Reason = reason,
            RejectedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").ApprovalRejected(evt);

        _logger.LogInformation("Notified approval rejected: {ApprovalId} by {RejectedBy}",
            approvalId, rejectedBy);
    }

    public async Task NotifyMasterIndexCreated(int indexId, string objectName)
    {
        var evt = new MasterIndexCreatedEvent
        {
            IndexId = indexId,
            ObjectName = objectName,
            CreatedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").MasterIndexCreated(evt);

        _logger.LogDebug("Notified MasterIndex created: {IndexId}", indexId);
    }

    public async Task NotifyMasterIndexDeleted(int indexId)
    {
        var evt = new MasterIndexDeletedEvent
        {
            IndexId = indexId,
            DeletedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").MasterIndexDeleted(evt);

        _logger.LogDebug("Notified MasterIndex deleted: {IndexId}", indexId);
    }

    public async Task NotifyStatisticsChanged()
    {
        var evt = new StatisticsChangedEvent
        {
            UpdatedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").StatisticsChanged(evt);

        _logger.LogDebug("Notified statistics changed");
    }

    public async Task NotifyAgentError(string agentName, string errorMessage)
    {
        var evt = new AgentErrorEvent
        {
            AgentName = agentName,
            ErrorMessage = errorMessage,
            OccurredAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").AgentError(evt);

        _logger.LogError("Notified agent error: {AgentName} - {ErrorMessage}", agentName, errorMessage);
    }

    public async Task NotifyDocumentUpdated(string documentId, string updateType)
    {
        var evt = new DocumentUpdatedEvent
        {
            DocumentId = documentId,
            UpdateType = updateType,
            UpdatedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group("all-users").DocumentUpdated(evt);

        _logger.LogDebug("Notified document updated: {DocumentId} - {UpdateType}", documentId, updateType);
    }
}

// Event models
public class DocumentGeneratedEvent
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ApprovalRequestedEvent
{
    public Guid ApprovalId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
}

public class ApprovalDecisionEvent
{
    public Guid ApprovalId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string DecidedBy { get; set; } = string.Empty;
    public DateTime DecisionDate { get; set; }
}

public class MasterIndexUpdatedEvent
{
    public int IndexId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AgentStatusEvent
{
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int QueueDepth { get; set; }
    public DateTime StatusChanged { get; set; }
}

public class DocumentSyncEvent
{
    public string DocumentPath { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
}

public class BulkOperationEvent
{
    public string Operation { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime CompletedAt { get; set; }
}

// Extension methods for easier notification
public static class ApprovalNotifierExtensions
{
    public static async Task NotifyDocumentApproved(this IApprovalNotifier notifier, 
        Guid approvalId, string approvedBy, string documentTitle)
    {
        await notifier.NotifyApprovalDecision(approvalId, "Approved", approvedBy);
    }

    public static async Task NotifyDocumentRejected(this IApprovalNotifier notifier, 
        Guid approvalId, string rejectedBy, string reason)
    {
        await notifier.NotifyApprovalDecision(approvalId, $"Rejected: {reason}", rejectedBy);
    }

    public static async Task NotifyHighPriorityApproval(this IApprovalNotifier notifier, 
        Guid approvalId, string documentTitle, string requester)
    {
        await notifier.NotifyApprovalRequested(approvalId, $"🔴 URGENT: {documentTitle}", "urgent", requester);
    }
}

public interface IApprovalNotifier
{
    Task NotifyDocumentGenerated(string documentId, string title, decimal confidence);
    Task NotifyApprovalRequested(Guid approvalId, string title, string priority, string requester);
    Task NotifyApprovalDecision(Guid approvalId, string decision, string decidedBy);
    Task NotifyApprovalCompleted(Guid approvalId, string documentTitle, string approvedBy);
    Task NotifyApprovalRejected(Guid approvalId, string documentTitle, string rejectedBy, string reason);
    Task NotifyMasterIndexCreated(int indexId, string objectName);
    Task NotifyMasterIndexDeleted(int indexId);
    Task NotifyStatisticsChanged();
    Task NotifyAgentError(string agentName, string errorMessage);
    Task NotifyDocumentUpdated(string documentId, string updateType);
}

// Additional event models for frontend alignment
public class ApprovalCompletedEvent
{
    public Guid ApprovalId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}

public class ApprovalRejectedEvent
{
    public Guid ApprovalId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string RejectedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime RejectedAt { get; set; }
}

public class MasterIndexCreatedEvent
{
    public int IndexId { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MasterIndexDeletedEvent
{
    public int IndexId { get; set; }
    public DateTime DeletedAt { get; set; }
}

public class StatisticsChangedEvent
{
    public DateTime UpdatedAt { get; set; }
}

public class AgentErrorEvent
{
    public string AgentName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}

public class DocumentUpdatedEvent
{
    public string DocumentId { get; set; } = string.Empty;
    public string UpdateType { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}