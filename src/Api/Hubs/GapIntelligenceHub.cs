// =============================================================================
// Agent #7: Gap Intelligence Agent - SignalR Hub
// Real-time notifications for gap detection events
// =============================================================================

using Microsoft.AspNetCore.SignalR;
using Enterprise.Documentation.Core.Application.Services.GapIntelligence;

namespace Enterprise.Documentation.Api.Hubs;

/// <summary>
/// SignalR hub for real-time Gap Intelligence updates.
/// Provides live notifications for detection runs, new gaps, and feedback.
/// </summary>
public class GapIntelligenceHub : Hub
{
    private readonly ILogger<GapIntelligenceHub> _logger;

    public GapIntelligenceHub(ILogger<GapIntelligenceHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("GapIntelligence client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("GapIntelligence client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to gaps for a specific schema
    /// </summary>
    public async Task SubscribeToSchema(string schemaName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"schema:{schemaName}");
        _logger.LogDebug("Client {ConnectionId} subscribed to schema {Schema}", Context.ConnectionId, schemaName);
    }

    /// <summary>
    /// Unsubscribe from schema updates
    /// </summary>
    public async Task UnsubscribeFromSchema(string schemaName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"schema:{schemaName}");
    }

    /// <summary>
    /// Subscribe to gaps of a specific severity
    /// </summary>
    public async Task SubscribeToSeverity(string severity)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"severity:{severity}");
        _logger.LogDebug("Client {ConnectionId} subscribed to severity {Severity}", Context.ConnectionId, severity);
    }

    /// <summary>
    /// Unsubscribe from severity updates
    /// </summary>
    public async Task UnsubscribeFromSeverity(string severity)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"severity:{severity}");
    }

    /// <summary>
    /// Subscribe to detection run progress
    /// </summary>
    public async Task SubscribeToDetectionRuns()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "detection-runs");
    }

    /// <summary>
    /// Unsubscribe from detection run progress
    /// </summary>
    public async Task UnsubscribeFromDetectionRuns()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "detection-runs");
    }
}

/// <summary>
/// Extension methods for broadcasting Gap Intelligence events through SignalR
/// </summary>
public static class GapIntelligenceHubExtensions
{
    /// <summary>
    /// Notify all clients of a newly detected gap
    /// </summary>
    public static async Task NotifyNewGap(this IHubContext<GapIntelligenceHub> hub, DetectedGap gap)
    {
        await hub.Clients.All.SendAsync("NewGapDetected", gap);
        await hub.Clients.Group($"schema:{gap.SchemaName}").SendAsync("NewGapDetected", gap);
        await hub.Clients.Group($"severity:{gap.Severity}").SendAsync("NewGapDetected", gap);
    }

    /// <summary>
    /// Notify clients that a gap has been resolved
    /// </summary>
    public static async Task NotifyGapResolved(this IHubContext<GapIntelligenceHub> hub, int gapId, string schema, string objectName)
    {
        await hub.Clients.All.SendAsync("GapResolved", new { GapId = gapId, Schema = schema, ObjectName = objectName, ResolvedAt = DateTime.UtcNow });
        await hub.Clients.Group($"schema:{schema}").SendAsync("GapResolved", new { GapId = gapId, ObjectName = objectName });
    }

    /// <summary>
    /// Notify clients of detection run progress
    /// </summary>
    public static async Task NotifyDetectionProgress(this IHubContext<GapIntelligenceHub> hub, int runId, int processed, int total)
    {
        var percentComplete = total > 0 ? (int)(processed * 100.0 / total) : 0;
        await hub.Clients.All.SendAsync("DetectionProgress", new
        {
            RunId = runId,
            Processed = processed,
            Total = total,
            PercentComplete = percentComplete,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Notify clients that a detection run has started
    /// </summary>
    public static async Task NotifyDetectionStarted(this IHubContext<GapIntelligenceHub> hub, string runType)
    {
        await hub.Clients.All.SendAsync("DetectionStarted", new { Type = runType, StartedAt = DateTime.UtcNow });
    }

    /// <summary>
    /// Notify clients that a detection run has completed
    /// </summary>
    public static async Task NotifyDetectionCompleted(this IHubContext<GapIntelligenceHub> hub, GapDetectionResult result)
    {
        await hub.Clients.All.SendAsync("DetectionCompleted", new
        {
            result.RunId,
            result.ObjectsScanned,
            result.GapsDetected,
            result.NewGaps,
            result.ResolvedGaps,
            Duration = result.CompletedAt.HasValue
                ? (int)(result.CompletedAt.Value - result.StartedAt).TotalMilliseconds
                : 0,
            CompletedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Notify clients that feedback was recorded
    /// </summary>
    public static async Task NotifyFeedbackRecorded(this IHubContext<GapIntelligenceHub> hub, string schemaName, string objectName, string feedbackType)
    {
        await hub.Clients.All.SendAsync("FeedbackRecorded", new
        {
            SchemaName = schemaName,
            ObjectName = objectName,
            FeedbackType = feedbackType,
            RecordedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Notify clients that clustering has completed
    /// </summary>
    public static async Task NotifyClusteringCompleted(this IHubContext<GapIntelligenceHub> hub, ClusteringResult result)
    {
        await hub.Clients.All.SendAsync("ClusteringCompleted", new
        {
            result.TotalObjects,
            result.ClustersCreated,
            ClusterNames = result.Clusters.Select(c => c.ClusterName).ToList(),
            CompletedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Notify clients of dashboard data update
    /// </summary>
    public static async Task NotifyDashboardUpdated(this IHubContext<GapIntelligenceHub> hub, GapDashboardData data)
    {
        await hub.Clients.All.SendAsync("DashboardUpdated", new
        {
            data.TotalOpenGaps,
            data.CriticalGaps,
            data.HighPriorityGaps,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
