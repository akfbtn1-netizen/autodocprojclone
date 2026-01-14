
using MediatR;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Events;
using Enterprise.Documentation.Core.Application.Interfaces;

namespace Enterprise.Documentation.Core.Application.EventHandlers;

/// <summary>
/// Handler for DocumentCreatedEvent domain events.
/// Handles notifications, audit logging, and integration events.
/// </summary>
public class DocumentCreatedEventHandler : INotificationHandler<DocumentCreatedEvent>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<DocumentCreatedEventHandler> _logger;

    public DocumentCreatedEventHandler(
        IAuditLogRepository auditLogRepository,
        ILogger<DocumentCreatedEventHandler> logger)
    {
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task Handle(DocumentCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Processing DocumentCreatedEvent for Document {DocumentId} titled '{Title}'",
                notification.DocumentId.Value,
                notification.Title);

            // Create audit log entry
            var auditLog = new AuditLog(
                AuditLogId.New<AuditLogId>(),
                "Document",
                notification.DocumentId.Value.ToString(),
                "Created",
                $"Document '{notification.Title}' created in category '{notification.Category}'",
                notification.CreatedBy,
                DateTime.UtcNow,
                new Dictionary<string, object>
                {
                    ["DocumentId"] = notification.DocumentId.Value,
                    ["Title"] = notification.Title,
                    ["Category"] = notification.Category,
                    ["EventId"] = notification.EventId
                });

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);

            // TODO: Send notification to relevant users
            // TODO: Publish integration event for external systems
            // TODO: Update search index

            _logger.LogInformation(
                "Successfully processed DocumentCreatedEvent for Document {DocumentId}",
                notification.DocumentId.Value);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation processing DocumentCreatedEvent for Document {DocumentId}: {ErrorMessage}",
                notification.DocumentId.Value,
                ex.Message);
            
            // Don't rethrow - we don't want domain event handling failures to break the main operation
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "Timeout processing DocumentCreatedEvent for Document {DocumentId}: {ErrorMessage}",
                notification.DocumentId.Value,
                ex.Message);
            
            // Don't rethrow - we don't want domain event handling failures to break the main operation
            // Consider implementing retry logic or dead letter queue here
        }
    }
}