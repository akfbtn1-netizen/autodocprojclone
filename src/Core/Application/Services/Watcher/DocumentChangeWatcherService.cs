// DocumentChangeWatcherService.cs
// PURPOSE: Detect rows without DocId, generate DocId, trigger draft workflow
// STEP 2 of DocumentationAutomation workflow

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using Enterprise.Documentation.Core.Application.Services.ExcelSync;

namespace Enterprise.Documentation.Core.Application.Services.Watcher;

public class DocumentChangeWatcherService : BackgroundService
{
    private readonly ILogger<DocumentChangeWatcherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly TimeSpan _pollInterval;
    private readonly bool _enabled;
    private readonly IExcelChangeIntegratorService _excelService;

    public DocumentChangeWatcherService(
        ILogger<DocumentChangeWatcherService> logger,
        IConfiguration configuration,
        IExcelChangeIntegratorService excelService)
    {
        _logger = logger;
        _configuration = configuration;
        _excelService = excelService;
        
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
        
        var pollMinutes = int.TryParse(_configuration["DocumentChangeWatcher:PollIntervalMinutes"], out var minutes) ? minutes : 1;
        _pollInterval = TimeSpan.FromMinutes(pollMinutes);
        
        var enabledStr = _configuration["DocumentChangeWatcher:Enabled"];
        _enabled = string.IsNullOrEmpty(enabledStr) || bool.Parse(enabledStr);
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

        // Query for ONE row that's ready for processing  
        // Look for records with DocIds that haven't been processed to MasterIndex yet
        var sql = @"
            SELECT TOP 1 
                Id, JiraNumber, CABNumber, TableName, ColumnName, DocId,
                'dbo' as SchemaName, ChangeType, ChangeApplied, ReportedBy, 
                AssignedTo, Date as DateRequested, LocationOfCodeChange as StoredProcedureName, Status
            FROM DaQa.DocumentChanges dc
            WHERE Status = 'Completed'
              AND DocId IS NOT NULL                -- Must have DocId assigned
              AND ISNULL(DocId, '') != 'TBD'       -- CRITICAL: Skip TBD rows forever
              AND DocId != ''                     -- Must have actual DocId
              AND JiraNumber IS NOT NULL
              AND Description IS NOT NULL          -- Required for documentation
              AND ChangeApplied IS NOT NULL        -- Required for documentation  
              AND LocationOfCodeChange IS NOT NULL -- Required for documentation
              AND ReportedBy IS NOT NULL
              AND AssignedTo IS NOT NULL
              AND NOT EXISTS (                    -- Not yet in MasterIndex
                  SELECT 1 FROM DaQa.MasterIndex mi 
                  WHERE mi.DocId = dc.DocId
              )
            ORDER BY Date ASC";

        var change = await connection.QueryFirstOrDefaultAsync<DocumentChange>(sql);

        if (change == null)
        {
            // No pending changes - this is normal
            _logger.LogDebug("No pending changes found");
            return;
        }

        _logger.LogInformation("Found pending change: Id={Id}, JiraNumber={JiraNumber}, DocId={DocId}", 
            change.Id, change.JiraNumber, change.DocId);

        try
        {
            // DocId is already assigned from Excel import, use it directly
            var docId = change.DocId;
            _logger.LogInformation("Using existing DocId: {DocId}", docId);

            // Step 2.1: Write DocId back to Excel (non-critical)
            try
            {
                if (!string.IsNullOrEmpty(docId))
                {
                    await _excelService.WriteDocIdToExcelAsync(change.JiraNumber, docId, ct);
                    _logger.LogDebug("DocId {DocId} written back to Excel for JIRA {JiraNumber}", docId, change.JiraNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write DocId back to Excel for JIRA {JiraNumber} (non-critical)", change.JiraNumber);
                // Continue processing - Excel write-back is optional
            }

            // Step 2.4: Publish WorkflowEvent
            if (!string.IsNullOrEmpty(docId))
            {
                await PublishWorkflowEventAsync(connection, docId, change.JiraNumber, ct);
            }

            // Step 2.5: Trigger draft generation workflow
            if (!string.IsNullOrEmpty(docId))
            {
                await TriggerDraftWorkflowAsync(connection, docId, change.JiraNumber, change.StoredProcedureName, ct);
            }

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

    private async Task TriggerDraftWorkflowAsync(SqlConnection connection, string docId, string jiraNumber, string? storedProcedureName, CancellationToken ct)
    {
        var sql = @"
            INSERT INTO DaQa.DocumentationQueue (
                DocIdString, ObjectName, Status, Priority, CreatedDate
            ) VALUES (
                @DocIdString, @ObjectName, 'Pending', 'Medium', GETUTCDATE()
            )";

        // 1. Always queue the change document (EN-0001, DE-0001, etc.)
        await connection.ExecuteAsync(sql, new { DocIdString = docId, ObjectName = jiraNumber });
        _logger.LogInformation("Queued change document {DocId} for JIRA {JiraNumber}", docId, jiraNumber);

        // 2. If there's a stored procedure, check if SP documentation exists and queue accordingly
        if (!string.IsNullOrWhiteSpace(storedProcedureName))
        {
            var spDocId = await GetOrCreateStoredProcDocIdAsync(connection, storedProcedureName, ct);
            
            // Queue the stored procedure documentation
            await connection.ExecuteAsync(sql, new { DocIdString = spDocId, ObjectName = storedProcedureName });
            _logger.LogInformation("Queued SP document {SpDocId} for stored procedure {StoredProcedureName}", spDocId, storedProcedureName);
        }
    }

    private async Task<string> GetOrCreateStoredProcDocIdAsync(SqlConnection connection, string storedProcedureName, CancellationToken ct)
    {
        // Check if SP documentation already exists in MasterIndex
        var existingDocSql = @"
            SELECT DocId FROM DaQa.MasterIndex 
            WHERE SourceDocumentID = @StoredProcName AND DocumentType = 'SP'";
        
        var existingDocId = await connection.QueryFirstOrDefaultAsync<string>(existingDocSql, 
            new { StoredProcName = storedProcedureName });

        if (!string.IsNullOrEmpty(existingDocId))
        {
            _logger.LogInformation("Found existing SP documentation: {ExistingDocId} for {StoredProcedureName}", 
                existingDocId, storedProcedureName);
            return existingDocId; // Return existing DocId for revision
        }

        // Generate new SP DocId
        var newSpDocId = await GenerateDocIdAsync(connection, "SP", ct);
        _logger.LogInformation("Generated new SP DocId: {NewSpDocId} for {StoredProcedureName}", 
            newSpDocId, storedProcedureName);
        
        return newSpDocId;
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
    public string? DocId { get; set; }
}