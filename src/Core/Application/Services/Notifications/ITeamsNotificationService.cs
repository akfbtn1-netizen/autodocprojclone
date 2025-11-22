using System;
using System.Threading;
using System.Threading.Tasks;

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
