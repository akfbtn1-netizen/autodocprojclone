using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enterprise.Documentation.Core.Application.Services.Notifications;

/// <summary>
/// Sends notifications to Microsoft Teams with 24-hour batching
/// </summary>
public interface ITeamsNotificationService
{
    Task SendDraftReadyNotificationAsync(DraftReadyNotification notification, CancellationToken cancellationToken = default);
    Task SendDefectCreationReminderAsync(DefectCreationReminder reminder, CancellationToken cancellationToken = default);
    Task SendBatchedNotificationsAsync(CancellationToken cancellationToken = default);
}

public class DraftReadyNotification
{
    public required string DocId { get; set; }
    public required string DocumentType { get; set; }
    public required string Table { get; set; }
    public string? Column { get; set; }
    public required string JiraNumber { get; set; }
    public required string Description { get; set; }
    public required string DocumentPath { get; set; }
    public required string ApprovalUrl { get; set; }
}

public class DefectCreationReminder
{
    public required string CABNumber { get; set; }
    public required string Table { get; set; }
    public string? Column { get; set; }
    public required string Description { get; set; }
    public required DateTime DateEntered { get; set; }
}

public class TeamsNotificationService : ITeamsNotificationService
{
    private readonly ILogger<TeamsNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _draftsWebhookUrl;
    private readonly string _defectsWebhookUrl;
    private readonly string _approvalBaseUrl;

    // In-memory batch storage (24-hour rolling window)
    private static readonly List<DraftReadyNotification> _pendingDraftNotifications = new();
    private static readonly List<DefectCreationReminder> _pendingDefectReminders = new();
    private static DateTime? _lastDraftNotificationSent;
    private static DateTime? _lastDefectNotificationSent;
    private static readonly object _lock = new object();

    public TeamsNotificationService(
        ILogger<TeamsNotificationService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;

        _draftsWebhookUrl = configuration["Teams:DraftsWebhookUrl"]
            ?? throw new InvalidOperationException("Teams:DraftsWebhookUrl not configured");
        _defectsWebhookUrl = configuration["Teams:DefectsWebhookUrl"]
            ?? throw new InvalidOperationException("Teams:DefectsWebhookUrl not configured");
        _approvalBaseUrl = configuration["Teams:ApprovalBaseUrl"]
            ?? "http://localhost:5195/approvals";
    }

    public async Task SendDraftReadyNotificationAsync(
        DraftReadyNotification notification,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _pendingDraftNotifications.Add(notification);

            // If this is the first notification, send immediately
            if (_lastDraftNotificationSent == null)
            {
                _logger.LogInformation("First draft notification detected, sending immediately");
                _ = Task.Run(() => SendDraftBatchAsync(cancellationToken), cancellationToken);
                return Task.CompletedTask;
            }

            // If 24 hours have passed since last notification, send batch
            var timeSinceLastNotification = DateTime.UtcNow - _lastDraftNotificationSent.Value;
            if (timeSinceLastNotification.TotalHours >= 24)
            {
                _logger.LogInformation("24 hours elapsed since last notification, sending batch");
                _ = Task.Run(() => SendDraftBatchAsync(cancellationToken), cancellationToken);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Draft notification added to batch. Current batch size: {Count}. Next send in {Hours:F1} hours",
                _pendingDraftNotifications.Count,
                24 - timeSinceLastNotification.TotalHours);
        }

        return Task.CompletedTask;
    }

    public async Task SendDefectCreationReminderAsync(
        DefectCreationReminder reminder,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _pendingDefectReminders.Add(reminder);

            // If this is the first reminder, send immediately
            if (_lastDefectNotificationSent == null)
            {
                _logger.LogInformation("First defect reminder detected, sending immediately");
                _ = Task.Run(() => SendDefectBatchAsync(cancellationToken), cancellationToken);
                return Task.CompletedTask;
            }

            // If 24 hours have passed since last notification, send batch
            var timeSinceLastNotification = DateTime.UtcNow - _lastDefectNotificationSent.Value;
            if (timeSinceLastNotification.TotalHours >= 24)
            {
                _logger.LogInformation("24 hours elapsed since last defect notification, sending batch");
                _ = Task.Run(() => SendDefectBatchAsync(cancellationToken), cancellationToken);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Defect reminder added to batch. Current batch size: {Count}. Next send in {Hours:F1} hours",
                _pendingDefectReminders.Count,
                24 - timeSinceLastNotification.TotalHours);
        }

        return Task.CompletedTask;
    }

    public async Task SendBatchedNotificationsAsync(CancellationToken cancellationToken = default)
    {
        // This method is called periodically by a background service to send any pending notifications
        await SendDraftBatchAsync(cancellationToken);
        await SendDefectBatchAsync(cancellationToken);
    }

    private async Task SendDraftBatchAsync(CancellationToken cancellationToken)
    {
        List<DraftReadyNotification> notificationsToSend;

        lock (_lock)
        {
            if (_pendingDraftNotifications.Count == 0)
                return;

            notificationsToSend = new List<DraftReadyNotification>(_pendingDraftNotifications);
            _pendingDraftNotifications.Clear();
            _lastDraftNotificationSent = DateTime.UtcNow;
        }

        try
        {
            var card = BuildDraftReadyAdaptiveCard(notificationsToSend);
            var response = await _httpClient.PostAsJsonAsync(_draftsWebhookUrl, card, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent Teams notification for {Count} draft(s) ready for approval",
                    notificationsToSend.Count);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Teams notification. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams notification for draft approvals");
        }
    }

    private async Task SendDefectBatchAsync(CancellationToken cancellationToken)
    {
        List<DefectCreationReminder> remindersToSend;

        lock (_lock)
        {
            if (_pendingDefectReminders.Count == 0)
                return;

            remindersToSend = new List<DefectCreationReminder>(_pendingDefectReminders);
            _pendingDefectReminders.Clear();
            _lastDefectNotificationSent = DateTime.UtcNow;
        }

        try
        {
            var card = BuildDefectReminderAdaptiveCard(remindersToSend);
            var response = await _httpClient.PostAsJsonAsync(_defectsWebhookUrl, card, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent Teams notification for {Count} defect(s) needing Jira tickets",
                    remindersToSend.Count);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Teams defect notification. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams notification for defect reminders");
        }
    }

    private object BuildDraftReadyAdaptiveCard(List<DraftReadyNotification> notifications)
    {
        var count = notifications.Count;
        var title = count == 1
            ? "📋 New Document Ready for Approval"
            : $"📋 {count} Documents Ready for Approval";

        var facts = new List<object>();
        var actions = new List<object>();

        foreach (var notif in notifications)
        {
            var objectDisplay = string.IsNullOrEmpty(notif.Column)
                ? notif.Table
                : $"{notif.Table} - {notif.Column}";

            facts.Add(new
            {
                title = $"**{notif.DocId}**",
                value = $"Type: {notif.DocumentType}  \nObject: {objectDisplay}  \nJira: {notif.JiraNumber}  \nDescription: {notif.Description}"
            });

            actions.Add(new
            {
                type = "Action.OpenUrl",
                title = $"Review {notif.DocId}",
                url = $"{_approvalBaseUrl}?docId={notif.DocId}"
            });
        }

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = title,
                                weight = "Bolder",
                                size = "Large",
                                color = "Accent"
                            },
                            new
                            {
                                type = "TextBlock",
                                text = $"The following document{(count > 1 ? "s have" : " has")} been auto-generated and {(count > 1 ? "are" : "is")} ready for review.",
                                wrap = true,
                                spacing = "Medium"
                            },
                            new
                            {
                                type = "FactSet",
                                facts = facts.ToArray()
                            }
                        },
                        actions = actions.ToArray()
                    }
                }
            }
        };
    }

    private object BuildDefectReminderAdaptiveCard(List<DefectCreationReminder> reminders)
    {
        var count = reminders.Count;
        var title = count == 1
            ? "⚠️ Defect Needs Jira Ticket"
            : $"⚠️ {count} Defects Need Jira Tickets";

        var facts = new List<object>();

        foreach (var reminder in reminders)
        {
            var objectDisplay = string.IsNullOrEmpty(reminder.Column)
                ? reminder.Table
                : $"{reminder.Table} - {reminder.Column}";

            facts.Add(new
            {
                title = $"**{reminder.CABNumber}**",
                value = $"Object: {objectDisplay}  \nReported: {reminder.DateEntered:yyyy-MM-dd}  \nDescription: {reminder.Description}"
            });
        }

        return new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new
                            {
                                type = "TextBlock",
                                text = title,
                                weight = "Bolder",
                                size = "Large",
                                color = "Warning"
                            },
                            new
                            {
                                type = "TextBlock",
                                text = $"The following defect{(count > 1 ? "s require" : " requires")} Jira ticket creation.",
                                wrap = true,
                                spacing = "Medium"
                            },
                            new
                            {
                                type = "FactSet",
                                facts = facts.ToArray()
                            },
                            new
                            {
                                type = "TextBlock",
                                text = "Please create Jira tickets for these defects before they can proceed.",
                                wrap = true,
                                weight = "Lighter",
                                isSubtle = true,
                                spacing = "Medium"
                            }
                        }
                    }
                }
            }
        };
    }
}
