using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Enterprise.Documentation.Core.Application.Services.Workflow;

public interface IWorkflowEventService
{
    Task PublishEventAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default);
    Task<List<WorkflowEvent>> GetEventsAsync(int limit = 50, CancellationToken cancellationToken = default);
}

public class WorkflowEventService : IWorkflowEventService
{
    private readonly ILogger<WorkflowEventService> _logger;
    private readonly IConfiguration _configuration;

    public WorkflowEventService(ILogger<WorkflowEventService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task PublishEventAsync(WorkflowEvent workflowEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Publishing workflow event: {EventType} for {WorkflowId}", 
            workflowEvent.EventType, workflowEvent.WorkflowId);
            
        // TODO: Implement actual event publishing (SignalR, message bus, etc.)
        await Task.CompletedTask;
    }

    public async Task<List<WorkflowEvent>> GetEventsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var events = new List<WorkflowEvent>();
        
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var query = @"
                SELECT TOP (@limit) 
                    EventId,
                    WorkflowId,
                    EventType,
                    Status,
                    Message,
                    DurationMs,
                    Timestamp,
                    Metadata
                FROM DaQa.WorkflowEvents 
                ORDER BY Timestamp DESC";
            
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@limit", limit);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            while (await reader.ReadAsync(cancellationToken))
            {
                var eventItem = new WorkflowEvent
                {
                    EventId = reader.GetGuid(0).ToString(),
                    WorkflowId = reader.GetString(1),
                    EventType = Enum.Parse<WorkflowEventType>(reader.GetString(2)),
                    Status = Enum.Parse<WorkflowEventStatus>(reader.GetString(3)),
                    Message = reader.GetString(4),
                    DurationMs = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Timestamp = reader.GetDateTime(6),
                    Metadata = reader.IsDBNull(7) ? null : reader.GetString(7)
                };
                
                events.Add(eventItem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow events");
            throw;
        }
        
        return events;
    }
}

public class WorkflowEvent
{
    public string EventId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public WorkflowEventType EventType { get; set; }
    public WorkflowEventStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? DurationMs { get; set; }
    public string? Metadata { get; set; }
}

public enum WorkflowEventType
{
    DocumentApproved,
    DocumentRejected,
    FinalDocumentGenerationStarted,
    FinalDocumentGenerationCompleted,
    MasterIndexPopulationStarted,
    MasterIndexPopulationCompleted,
    FileSavedToSharePoint,
    WorkflowCompleted
}

public enum WorkflowEventStatus
{
    InProgress,
    Completed,
    Failed
}