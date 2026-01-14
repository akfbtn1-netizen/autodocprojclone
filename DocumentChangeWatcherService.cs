// DocumentChangeWatcherService.cs
// PURPOSE: Detect rows without DocId, generate DocId, trigger draft workflow
// STEP 2 of DocumentationAutomation workflow

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;

namespace DocGenerator.Services;

public class DocumentChangeWatcherService : BackgroundService
{
    private readonly ILogger<DocumentChangeWatcherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly TimeSpan _pollInterval;
    private readonly bool _enabled;

    public DocumentChangeWatcherService(
        ILogger<DocumentChangeWatcherService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
        
        _pollInterval = TimeSpan.FromMinutes(
            _configuration.GetValue<int>("DocumentChangeWatcher:PollIntervalMinutes", 1));
        
        _enabled = _configuration.GetValue<bool>("DocumentChangeWatcher:Enabled", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("DocumentChangeWatcher is disabled in configuration");
            return;
        }

        _logger.LogInformation("DocumentChangeWatcher started. Poll interval: {Interval} minutes", 
            _pollInterval.TotalMinutes);
        
        // Small delay on startup to let ExcelChangeIntegrator sync first
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DocumentChangeWatcher main loop");
            }
            
            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingChangesAsync(CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        // Query for ONE row without DocId (TOP 1 for safety)
        var sql = @"
            SELECT TOP 1 
                Id, JiraNumber, CABNumber, TableName, ColumnName, 
                SchemaName, ChangeType, ChangeApplied, ReportedBy, 
                AssignedTo, DateRequested, StoredProcedureName, Status
            FROM DaQa.DocumentChanges
            WHERE Status = 'Completed' AND DocId IS NULL
            ORDER BY DateRequested ASC";

        var change = await connection.QueryFirstOrDefaultAsync<DocumentChange>(sql);

        if (change == null)
        {
            // No pending changes - this is normal
            _logger.LogDebug("No pending changes found");
            return;
        }

        _logger.LogInformation("Found pending change: Id={Id}, JiraNumber={JiraNumber}", 
            change.Id, change.JiraNumber);

        try
        {
            // Step 2.1: Determine document type
            var docType = DetermineDocumentType(change);
            _logger.LogInformation("Determined document type: {DocType}", docType);

            // Step 2.2: Generate DocId
            var docId = await GenerateDocIdAsync(connection, docType, ct);
            _logger.LogInformation("Generated DocId: {DocId}", docId);

            // Step 2.3: Update DocumentChanges with new DocId
            var updated = await UpdateDocIdAsync(connection, change.Id, docId, ct);
            if (!updated)
            {
                _logger.LogError("Failed to update DocId for Id={Id}", change.Id);
                return;
            }

            // Step 2.4: Publish WorkflowEvent
            await PublishWorkflowEventAsync(connection, docId, change.JiraNumber, ct);

            // Step 2.5: Trigger draft generation workflow
            await TriggerDraftWorkflowAsync(connection, docId, ct);

            _logger.LogInformation("Successfully processed change Id={Id}, assigned DocId={DocId}", 
                change.Id, docId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing change Id={Id}, JiraNumber={JiraNumber}", 
                change.Id, change.JiraNumber);
            
            // Log failure event
            await PublishWorkflowFailureAsync(connection, change.Id, change.JiraNumber, ex.Message, ct);
        }
    }

    private string DetermineDocumentType(DocumentChange change)
    {
        // Check explicit ChangeType field
        if (!string.IsNullOrWhiteSpace(change.ChangeType))
        {
            var type = change.ChangeType.ToLower();
            if (type.Contains("enhancement") || type.Contains("improve"))
                return "EN";
            if (type.Contains("defect") || type.Contains("bug") || type.Contains("fix"))
                return "DF";
            if (type.Contains("business") || type.Contains("request"))
                return "BR";
        }
        
        // Fallback: Check ChangeApplied description
        if (!string.IsNullOrWhiteSpace(change.ChangeApplied))
        {
            var desc = change.ChangeApplied.ToLower();
            if (desc.Contains("enhance") || desc.Contains("improve") || desc.Contains("add"))
                return "EN";
            if (desc.Contains("fix") || desc.Contains("defect") || desc.Contains("bug") || desc.Contains("correct"))
                return "DF";
        }
        
        // Default: Business Request
        return "BR";
    }

    private async Task<string> GenerateDocIdAsync(SqlConnection connection, string docType, CancellationToken ct)
    {
        // Query for max existing DocId of this type
        var sql = @"
            SELECT ISNULL(MAX(CAST(SUBSTRING(DocId, 4, 10) AS INT)), 0)
            FROM DaQa.DocumentChanges
            WHERE DocId LIKE @Pattern";

        var maxNumber = await connection.QueryFirstOrDefaultAsync<int>(sql, 
            new { Pattern = $"{docType}-%" });

        var nextNumber = maxNumber + 1;
        var docId = $"{docType}-{nextNumber:D4}";

        // Verify no collision (shouldn't happen with TOP 1, but safety check)
        var exists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM DaQa.DocumentChanges WHERE DocId = @DocId",
            new { DocId = docId });

        if (exists > 0)
        {
            _logger.LogWarning("DocId collision detected: {DocId}, incrementing", docId);
            // Recursively try next number
            return await GenerateDocIdAsync(connection, docType, ct);
        }

        return docId;
    }

    private async Task<bool> UpdateDocIdAsync(SqlConnection connection, int id, string docId, CancellationToken ct)
    {
        var sql = @"
            UPDATE DaQa.DocumentChanges
            SET DocId = @DocId,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND DocId IS NULL";

        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, DocId = docId });

        if (rowsAffected == 0)
        {
            _logger.LogWarning("Update failed for Id={Id} - may have been processed by another instance", id);
            return false;
        }

        return true;
    }

    private async Task PublishWorkflowEventAsync(
        SqlConnection connection, 
        string docId, 
        string jiraNumber, 
        CancellationToken ct)
    {
        var workflowId = $"WF-{docId}";
        
        var sql = @"
            INSERT INTO DaQa.WorkflowEvents (
                WorkflowId, EventType, Status, Message, Timestamp, Metadata
            ) VALUES (
                @WorkflowId, 
                'WatcherDetectedNewRow', 
                'Completed', 
                @Message, 
                GETUTCDATE(),
                @Metadata
            )";

        var message = $"New change detected: {jiraNumber}, assigned DocId: {docId}";
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            DocId = docId,
            JiraNumber = jiraNumber,
            Source = "DocumentChangeWatcher"
        });

        await connection.ExecuteAsync(sql, new
        {
            WorkflowId = workflowId,
            Message = message,
            Metadata = metadata
        });

        _logger.LogInformation("Published WorkflowEvent: {WorkflowId} - {EventType}", 
            workflowId, "WatcherDetectedNewRow");
    }

    private async Task TriggerDraftWorkflowAsync(SqlConnection connection, string docId, CancellationToken ct)
    {
        // Insert into DocumentationQueue for DocGeneratorService to pick up
        var sql = @"
            INSERT INTO DaQa.DocumentationQueue (
                DocId, Status, Priority, QueuedDate
            ) VALUES (
                @DocId, 'Pending', 'Medium', GETUTCDATE()
            )";

        await connection.ExecuteAsync(sql, new { DocId = docId });

        _logger.LogInformation("Queued {DocId} for draft generation in DocumentationQueue", docId);
    }

    private async Task PublishWorkflowFailureAsync(
        SqlConnection connection,
        int id,
        string jiraNumber,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            var sql = @"
                INSERT INTO DaQa.WorkflowEvents (
                    WorkflowId, EventType, Status, Message, Timestamp, Metadata
                ) VALUES (
                    @WorkflowId,
                    'WorkflowFailed',
                    'Failed',
                    @Message,
                    GETUTCDATE(),
                    @Metadata
                )";

            var workflowId = $"WF-Error-{id}";
            var message = $"Failed to process change Id={id}, JiraNumber={jiraNumber}";
            var metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                ChangeId = id,
                JiraNumber = jiraNumber,
                Error = errorMessage,
                Source = "DocumentChangeWatcher"
            });

            await connection.ExecuteAsync(sql, new
            {
                WorkflowId = workflowId,
                Message = message,
                Metadata = metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log workflow failure event");
        }
    }
}

// DTO for DocumentChanges query
public class DocumentChange
{
    public int Id { get; set; }
    public string JiraNumber { get; set; } = string.Empty;
    public string? CABNumber { get; set; }
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public string? SchemaName { get; set; }
    public string? ChangeType { get; set; }
    public string? ChangeApplied { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? DateRequested { get; set; }
    public string? StoredProcedureName { get; set; }
    public string Status { get; set; } = string.Empty;
}
