using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Application.Services.Workflow;
using Microsoft.Data.SqlClient;

namespace Enterprise.Documentation.Core.Application.Services.CodeExtraction;

public interface ICodeExtractionService
{
    Task<CodeExtractionResult?> ExtractMarkedCodeAsync(
        string docId,
        string storedProcedureName,
        string jiraNumber,
        CancellationToken ct = default);
}

public class CodeExtractionResult
{
    public string StoredProcedureName { get; set; } = string.Empty;
    public string JiraNumber { get; set; } = string.Empty;
    public string ExtractedCode { get; set; } = string.Empty;
    public string FullStoredProcedure { get; set; } = string.Empty;
    public bool HasMarkers { get; set; }
    public int MarkerCount { get; set; }
    public string ExtractionMethod { get; set; } = string.Empty; // "Markers" or "FullSP"
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public string? Warnings { get; set; }
}

public class CodeExtractionService : ICodeExtractionService
{
    private readonly ILogger<CodeExtractionService> _logger;
    private readonly IWorkflowEventService _workflowEventService;
    private readonly string _connectionString;

    // Transient SQL error codes that should trigger retry
    private readonly HashSet<int> _transientErrorNumbers = new()
    {
        -2,    // Timeout
        2,     // Timeout
        53,    // Network path not found
        121,   // Semaphore timeout
        1205,  // Deadlock
        1222,  // Lock request timeout
        8645,  // Timeout waiting for memory
        8651,  // Could not perform operation
        40197, // Service busy
        40501, // Service busy
        40613, // Database not available
        49918, // Cannot process request
        49919  // Cannot process create or update request
    };

    public CodeExtractionService(
        ILogger<CodeExtractionService> logger,
        IWorkflowEventService workflowEventService,
        IConfiguration configuration)
    {
        _logger = logger;
        _workflowEventService = workflowEventService;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string not found");
    }

    public async Task<CodeExtractionResult?> ExtractMarkedCodeAsync(
        string docId,
        string storedProcedureName,
        string jiraNumber,
        CancellationToken ct = default)
    {
        var workflowId = $"WF-{docId}";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Starting code extraction for DocId: {DocId}, SP: {StoredProcedure}, Jira: {JiraNumber}",
                docId, storedProcedureName, jiraNumber);

            // Publish workflow start event
            await _workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.DocumentApproved, // Using existing enum value
                Status = WorkflowEventStatus.InProgress,
                Message = $"Starting code extraction from {storedProcedureName} for {jiraNumber}",
                Metadata = System.Text.Json.JsonSerializer.Serialize(new { DocId = docId, StoredProcedure = storedProcedureName, JiraNumber = jiraNumber })
            }, ct);

            // Get stored procedure definition
            string? spDefinition = await GetStoredProcedureDefinitionAsync(storedProcedureName, ct);
            if (spDefinition == null)
            {
                // SP not found - log warning but continue workflow
                await HandleStoredProcedureNotFoundAsync(docId, storedProcedureName, jiraNumber, workflowId, ct);
                return null;
            }

            // Extract marked code
            var result = ExtractMarkedCodeFromDefinition(spDefinition, storedProcedureName, jiraNumber);
            
            // Handle no markers found
            if (!result.HasMarkers)
            {
                await HandleNoMarkersFoundAsync(docId, storedProcedureName, jiraNumber, workflowId, result, ct);
            }

            stopwatch.Stop();

            // Publish success event
            await _workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.WorkflowCompleted,
                Status = WorkflowEventStatus.Completed,
                Message = $"Code extraction completed. Method: {result.ExtractionMethod}, Markers: {result.MarkerCount}",
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new 
                { 
                    DocId = docId, 
                    StoredProcedure = storedProcedureName, 
                    JiraNumber = jiraNumber,
                    ExtractionMethod = result.ExtractionMethod,
                    MarkerCount = result.MarkerCount,
                    HasWarnings = !string.IsNullOrEmpty(result.Warnings)
                })
            }, ct);

            _logger.LogInformation(
                "Code extraction completed successfully for DocId: {DocId}, Method: {ExtractionMethod}, Markers: {MarkerCount}",
                docId, result.ExtractionMethod, result.MarkerCount);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, 
                "Fatal error during code extraction for DocId: {DocId}, SP: {StoredProcedure}, Jira: {JiraNumber}",
                docId, storedProcedureName, jiraNumber);

            // Publish failure event
            await _workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.WorkflowCompleted,
                Status = WorkflowEventStatus.Failed,
                Message = $"Fatal error during code extraction: {ex.Message}",
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new { DocId = docId, StoredProcedure = storedProcedureName, JiraNumber = jiraNumber, Error = ex.Message })
            }, ct);

            throw; // Re-throw to stop the workflow
        }
    }

    private async Task<string?> GetStoredProcedureDefinitionAsync(string storedProcedureName, CancellationToken ct)
    {
        var maxRetries = 1;
        var retryCount = 0;

        while (retryCount <= maxRetries)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(ct);

                // Parse schema and object name
                var (schemaName, objectName) = ParseStoredProcedureName(storedProcedureName);

                // Query sys.sql_modules for the stored procedure definition
                const string query = @"
                    SELECT m.definition
                    FROM sys.sql_modules m
                    INNER JOIN sys.objects o ON m.object_id = o.object_id
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    WHERE o.type = 'P'
                    AND s.name = @SchemaName
                    AND o.name = @ObjectName";

                var definition = await connection.QuerySingleOrDefaultAsync<string>(
                    query,
                    new { SchemaName = schemaName, ObjectName = objectName },
                    commandTimeout: 30);

                return definition;
            }
            catch (SqlException ex) when (_transientErrorNumbers.Contains(ex.Number) && retryCount < maxRetries)
            {
                retryCount++;
                _logger.LogWarning(
                    "Transient SQL error {ErrorNumber} during SP retrieval (attempt {RetryCount}/{MaxRetries}): {Message}",
                    ex.Number, retryCount, maxRetries + 1, ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, 
                    "SQL error during stored procedure retrieval: {StoredProcedure}, Error: {ErrorNumber}",
                    storedProcedureName, ex.Number);
                throw;
            }
        }

        return null; // Should never reach here
    }

    private (string schemaName, string objectName) ParseStoredProcedureName(string storedProcedureName)
    {
        if (storedProcedureName.Contains('.'))
        {
            var parts = storedProcedureName.Split('.', 2);
            return (parts[0], parts[1]);
        }
        return ("dbo", storedProcedureName);
    }

    private CodeExtractionResult ExtractMarkedCodeFromDefinition(
    string spDefinition, 
    string storedProcedureName, 
    string jiraNumber)
{
    var result = new CodeExtractionResult
    {
        StoredProcedureName = storedProcedureName,
        JiraNumber = jiraNumber,
        FullStoredProcedure = spDefinition
    };

    // Extract the ticket number from jiraNumber (BAS-9818 -> 9818)
    var requestedTicketMatch = Regex.Match(jiraNumber, @"\d{3,4}");
    if (!requestedTicketMatch.Success)
    {
        _logger.LogWarning("Invalid JIRA format: {JiraNumber}. Using full definition.", jiraNumber);
        result.HasMarkers = false;
        result.MarkerCount = 0;
        result.ExtractedCode = spDefinition;
        result.ExtractionMethod = "FullSP";
        result.Warnings = $"Invalid JIRA format: {jiraNumber}. Using full stored procedure.";
        return result;
    }
    
    var requestedTicketNumber = requestedTicketMatch.Value;

    // Pattern: -- Begin BAS#### or ----- Begin BAS####
    // Ultra-flexible to handle: BAS-9818, BAS9818, BAS 9818, BAS- 9818, etc.
    var beginPattern = $@"-{{1,}}\s*Begin\s*\[?\s*BAS\s*-?\s*{requestedTicketNumber}\s*\]?";
    var endPattern = $@"-{{1,}}\s*End\s*\[?\s*BAS\s*-?\s*{requestedTicketNumber}\s*\]?";

    var beginMatch = Regex.Match(spDefinition, beginPattern, RegexOptions.IgnoreCase);
    var endMatch = Regex.Match(spDefinition, endPattern, RegexOptions.IgnoreCase);

if (beginMatch.Success && endMatch.Success)
{
    // Find the ALTER/CREATE PROCEDURE line before the Begin marker
    // Search backwards from beginMatch to find the most recent line starting with ALTER or CREATE
    var beforeMarker = spDefinition.Substring(0, beginMatch.Index);
    var lines = beforeMarker.Split('\n');
    
    int procedureLineIndex = -1;
    for (int i = lines.Length - 1; i >= 0; i--)
    {
        var trimmedLine = lines[i].Trim();
        if (trimmedLine.StartsWith("ALTER PROCEDURE", StringComparison.OrdinalIgnoreCase) ||
            trimmedLine.StartsWith("CREATE PROCEDURE", StringComparison.OrdinalIgnoreCase))
        {
            procedureLineIndex = i;
            break;
        }
    }
    
    // Calculate start position
    int startIndex;
    if (procedureLineIndex >= 0)
    {
        // Calculate character position of that line
        startIndex = string.Join("\n", lines.Take(procedureLineIndex)).Length;
        if (procedureLineIndex > 0) startIndex += 1; // Add newline
        
        _logger.LogInformation("Including procedure header starting at line {LineNum}", procedureLineIndex + 1);
    }
    else
    {
        // Fallback: start after Begin marker
        startIndex = beginMatch.Index + beginMatch.Length;
        _logger.LogWarning("Could not find procedure header, starting after Begin marker");
    }
    
    var endIndex = endMatch.Index;
    
    if (endIndex <= startIndex)
    {
        _logger.LogWarning("Invalid marker positions for {Jira}: End marker appears before Begin marker. Using full definition.", jiraNumber);
        result.HasMarkers = false;
        result.MarkerCount = 0;
        result.ExtractedCode = spDefinition;
        result.ExtractionMethod = "FullSP";
        result.Warnings = $"Invalid marker positions for {jiraNumber}. Using full stored procedure.";
        return result;
    }
    
    var extractedCode = spDefinition.Substring(startIndex, endIndex - startIndex).Trim();
    
    _logger.LogInformation("Extracted marked section for {Jira} from line {Start} to {End}", 
        jiraNumber, procedureLineIndex + 1, endIndex);
    
    result.HasMarkers = true;
    result.MarkerCount = 1;
    result.ExtractedCode = extractedCode;
    result.ExtractionMethod = "Markers";
}
    else if (beginMatch.Success && !endMatch.Success)
    {
        // Begin marker found but no matching End marker
        _logger.LogWarning("Found Begin marker for {Jira} but no matching End marker. Using full definition.", jiraNumber);
        result.HasMarkers = false;
        result.MarkerCount = 0;
        result.ExtractedCode = spDefinition;
        result.ExtractionMethod = "FullSP";
        result.Warnings = $"Found Begin marker for {jiraNumber} but no matching End marker. Using full stored procedure.";
    }
    else if (!beginMatch.Success && endMatch.Success)
    {
        // End marker found but no matching Begin marker
        _logger.LogWarning("Found End marker for {Jira} but no matching Begin marker. Using full definition.", jiraNumber);
        result.HasMarkers = false;
        result.MarkerCount = 0;
        result.ExtractedCode = spDefinition;
        result.ExtractionMethod = "FullSP";
        result.Warnings = $"Found End marker for {jiraNumber} but no matching Begin marker. Using full stored procedure.";
    }
    else
    {
        // No markers found for requested JIRA - return full definition
        _logger.LogInformation("No markers found for {Jira}, returning full SP definition", jiraNumber);
        result.HasMarkers = false;
        result.MarkerCount = 0;
        result.ExtractedCode = spDefinition;
        result.ExtractionMethod = "FullSP";
        result.Warnings = $"No markers found for {jiraNumber}. Returning full stored procedure.";
    }

    return result;
}

    private async Task HandleStoredProcedureNotFoundAsync(
        string docId,
        string storedProcedureName,
        string jiraNumber,
        string workflowId,
        CancellationToken ct)
    {
        var message = $"Stored procedure {storedProcedureName} not found in database for {jiraNumber}. " +
                     "Change documentation will be created without SP documentation.";

        _logger.LogWarning("SP not found: {StoredProcedure} for Jira: {JiraNumber}", 
            storedProcedureName, jiraNumber);

        // Publish warning event
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.WorkflowCompleted,
            Status = WorkflowEventStatus.Failed,
            Message = message,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { Reason = "SPNotFound", StoredProcedure = storedProcedureName })
        }, ct);
    }

    private async Task HandleNoMarkersFoundAsync(
        string docId,
        string storedProcedureName,
        string jiraNumber,
        string workflowId,
        CodeExtractionResult result,
        CancellationToken ct)
    {
        var message = $"No code markers found for {jiraNumber} in {storedProcedureName}. " +
                     "Using full stored procedure for documentation.";

        _logger.LogWarning("No markers found: SP {StoredProcedure} for Jira: {JiraNumber}", 
            storedProcedureName, jiraNumber);

        // Publish warning event (completed with warning)
        await _workflowEventService.PublishEventAsync(new WorkflowEvent
        {
            WorkflowId = workflowId,
            EventType = WorkflowEventType.WorkflowCompleted,
            Status = WorkflowEventStatus.Completed,
            Message = message,
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { Reason = "NoMarkers", ExtractionMethod = "FullSP" })
        }, ct);
    }
}