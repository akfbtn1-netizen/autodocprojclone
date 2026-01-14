// src/Core/Application/Services/Notifications/TeamsNotificationService.cs

using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Dapper;

namespace Enterprise.Documentation.Core.Application.Services.Notifications;

public interface ITeamsNotificationService
{
    Task SendDraftApprovalNotificationAsync(string docId, string jiraNumber, string assignedTo, CancellationToken cancellationToken = default);
}

public class TeamsNotificationService : ITeamsNotificationService
{
    private readonly ILogger<TeamsNotificationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _connectionString;
    private readonly string? _teamsWebhookUrl;
    private readonly bool _teamsEnabled;

    public TeamsNotificationService(
        ILogger<TeamsNotificationService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
        _teamsWebhookUrl = configuration["Teams:WebhookUrl"];
        _teamsEnabled = configuration.GetValue<bool>("Teams:Enabled", false);
    }

    public async Task SendDraftApprovalNotificationAsync(
        string docId, 
        string jiraNumber, 
        string assignedTo, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Log notification
            await LogNotificationAsync(docId, jiraNumber, assignedTo, cancellationToken);

            if (!_teamsEnabled || string.IsNullOrEmpty(_teamsWebhookUrl))
            {
                _logger.LogInformation("Teams notifications disabled - notification logged only");
                return;
            }

            var message = new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        contentUrl = (string?)null,
                        content = new
                        {
                            type = "AdaptiveCard",
                            version = "1.4",
                            body = new object[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    size = "Large",
                                    weight = "Bolder",
                                    text = "📋 New Document Awaiting Approval",
                                    color = "Accent"
                                },
                                new
                                {
                                    type = "FactSet",
                                    facts = new[]
                                    {
                                        new { title = "Document ID:", value = docId },
                                        new { title = "Jira Number:", value = jiraNumber },
                                        new { title = "Assigned To:", value = assignedTo },
                                        new { title = "Status:", value = "Draft - Awaiting Approval" }
                                    }
                                },
                                new
                                {
                                    type = "TextBlock",
                                    text = "A new draft document has been generated and requires your review and approval.",
                                    wrap = true
                                }
                            },
                            actions = new[]
                            {
                                new
                                {
                                    type = "Action.OpenUrl",
                                    title = "Review Document",
                                    url = $"http://localhost:3000/approval"
                                }
                            }
                        }
                    }
                }
            };

            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_teamsWebhookUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Teams notification sent successfully for {DocId}", docId);
            }
            else
            {
                _logger.LogWarning("Teams notification failed for {DocId}: {StatusCode}", 
                    docId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Teams notification for {DocId}", docId);
            // Don't throw - notification failures shouldn't break workflow
        }
    }

    private async Task LogNotificationAsync(string docId, string jiraNumber, string assignedTo, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO DaQa.TeamsNotificationLog (
                DocId, JiraNumber, RecipientName, NotificationType, 
                SentDate, Status, Message
            ) VALUES (
                @DocId, @JiraNumber, @RecipientName, @NotificationType,
                GETUTCDATE(), @Status, @Message
            )",
            new
            {
                DocId = docId,
                JiraNumber = jiraNumber,
                RecipientName = assignedTo,
                NotificationType = "DraftApproval",
                Status = _teamsEnabled ? "Sent" : "Logged",
                Message = $"Draft approval notification for {docId}"
            });
    }
}
