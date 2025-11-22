using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.Notifications;

/// <summary>
/// Background service that periodically sends batched Teams notifications
/// Runs every hour to check for pending notifications
/// </summary>
public class NotificationBatchingService : BackgroundService
{
    private readonly ILogger<NotificationBatchingService> _logger;
    private readonly ITeamsNotificationService _teamsNotificationService;
    private readonly TimeSpan _checkInterval;

    public NotificationBatchingService(
        ILogger<NotificationBatchingService> logger,
        ITeamsNotificationService teamsNotificationService,
        IConfiguration configuration)
    {
        _logger = logger;
        _teamsNotificationService = teamsNotificationService;

        // Check for batched notifications every hour
        var intervalMinutes = configuration.GetSection("Teams:BatchCheckIntervalMinutes").Get<int>() ?? 60;
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Batching Service started. Check interval: {Interval} minutes",
            _checkInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);

                _logger.LogDebug("Checking for batched notifications to send");
                await _teamsNotificationService.SendBatchedNotificationsAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Normal cancellation, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification batching service");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("Notification Batching Service stopped");
    }
}
