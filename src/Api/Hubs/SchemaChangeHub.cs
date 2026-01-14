// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector SignalR Hub
// Real-time notifications for schema changes and detection progress
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Wire to background detection service for live updates
// TODO [4]: Add group management for schema-specific subscriptions

using Enterprise.Documentation.Core.Application.DTOs.SchemaChange;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Enterprise.Documentation.Api.Hubs;

/// <summary>
/// SignalR hub for real-time schema change notifications.
/// </summary>
[Authorize]
public class SchemaChangeHub : Hub
{
    private readonly ILogger<SchemaChangeHub> _logger;

    public SchemaChangeHub(ILogger<SchemaChangeHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        _logger.LogInformation("User {UserId} connected to SchemaChangeHub", userId);

        // Add to default group for all schema change notifications
        await Groups.AddToGroupAsync(Context.ConnectionId, "AllChanges");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        _logger.LogInformation("User {UserId} disconnected from SchemaChangeHub", userId);

        if (exception != null)
        {
            _logger.LogError(exception, "User {UserId} disconnected with error", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to changes for a specific schema.
    /// </summary>
    public async Task SubscribeToSchema(string schemaName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Schema_{schemaName}");
        _logger.LogInformation("Client {ConnectionId} subscribed to schema {Schema}",
            Context.ConnectionId, schemaName);
    }

    /// <summary>
    /// Unsubscribe from a specific schema.
    /// </summary>
    public async Task UnsubscribeFromSchema(string schemaName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Schema_{schemaName}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from schema {Schema}",
            Context.ConnectionId, schemaName);
    }

    /// <summary>
    /// Subscribe to high-risk changes only.
    /// </summary>
    public async Task SubscribeToHighRisk()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "HighRiskChanges");
        _logger.LogInformation("Client {ConnectionId} subscribed to high-risk changes",
            Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe to a specific detection run for progress updates.
    /// </summary>
    public async Task SubscribeToDetectionRun(Guid runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Run_{runId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to detection run {RunId}",
            Context.ConnectionId, runId);
    }

    /// <summary>
    /// Unsubscribe from a detection run.
    /// </summary>
    public async Task UnsubscribeFromDetectionRun(Guid runId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Run_{runId}");
    }
}

/// <summary>
/// Service for broadcasting schema change events to connected clients.
/// </summary>
public interface ISchemaChangeNotifier
{
    Task NotifyChangeDetected(SchemaChangeDetectedNotification notification);
    Task NotifyDetectionProgress(DetectionProgressNotification notification);
    Task NotifyImpactAnalysisComplete(ImpactAnalysisCompleteNotification notification);
    Task NotifyDetectionComplete(Guid runId, int totalChanges, int highRiskChanges);
    Task NotifyDetectionFailed(Guid runId, string errorMessage);
}

/// <summary>
/// Implementation of schema change notifications via SignalR.
/// </summary>
public class SchemaChangeNotifier : ISchemaChangeNotifier
{
    private readonly IHubContext<SchemaChangeHub> _hubContext;
    private readonly ILogger<SchemaChangeNotifier> _logger;

    public SchemaChangeNotifier(
        IHubContext<SchemaChangeHub> hubContext,
        ILogger<SchemaChangeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyChangeDetected(SchemaChangeDetectedNotification notification)
    {
        _logger.LogInformation("Broadcasting schema change detected: {Schema}.{Object}",
            notification.SchemaName, notification.ObjectName);

        // Notify all subscribers
        await _hubContext.Clients.Group("AllChanges")
            .SendAsync("SchemaChangeDetected", notification);

        // Notify schema-specific subscribers
        await _hubContext.Clients.Group($"Schema_{notification.SchemaName}")
            .SendAsync("SchemaChangeDetected", notification);

        // Notify high-risk subscribers if applicable
        if (notification.RiskLevel is "HIGH" or "CRITICAL")
        {
            await _hubContext.Clients.Group("HighRiskChanges")
                .SendAsync("HighRiskChangeDetected", notification);
        }
    }

    public async Task NotifyDetectionProgress(DetectionProgressNotification notification)
    {
        await _hubContext.Clients.Group($"Run_{notification.RunId}")
            .SendAsync("DetectionProgress", notification);

        // Also notify all subscribers of detection activity
        await _hubContext.Clients.Group("AllChanges")
            .SendAsync("DetectionProgress", notification);
    }

    public async Task NotifyImpactAnalysisComplete(ImpactAnalysisCompleteNotification notification)
    {
        _logger.LogInformation("Broadcasting impact analysis complete for change {ChangeId}: Score={Score}, Risk={Risk}",
            notification.ChangeId, notification.ImpactScore, notification.RiskLevel);

        await _hubContext.Clients.Group("AllChanges")
            .SendAsync("ImpactAnalysisComplete", notification);

        if (notification.ApprovalRequired)
        {
            await _hubContext.Clients.Group("HighRiskChanges")
                .SendAsync("ApprovalRequired", notification);
        }
    }

    public async Task NotifyDetectionComplete(Guid runId, int totalChanges, int highRiskChanges)
    {
        var notification = new
        {
            RunId = runId,
            TotalChanges = totalChanges,
            HighRiskChanges = highRiskChanges,
            CompletedAt = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"Run_{runId}")
            .SendAsync("DetectionComplete", notification);

        await _hubContext.Clients.Group("AllChanges")
            .SendAsync("DetectionComplete", notification);
    }

    public async Task NotifyDetectionFailed(Guid runId, string errorMessage)
    {
        var notification = new
        {
            RunId = runId,
            ErrorMessage = errorMessage,
            FailedAt = DateTime.UtcNow
        };

        await _hubContext.Clients.Group($"Run_{runId}")
            .SendAsync("DetectionFailed", notification);

        await _hubContext.Clients.Group("AllChanges")
            .SendAsync("DetectionFailed", notification);
    }
}
