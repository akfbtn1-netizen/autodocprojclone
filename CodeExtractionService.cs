using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EnterpriseDocumentationPlatform.Infrastructure.Interfaces;
using EnterpriseDocumentationPlatform.Infrastructure.Models;

namespace EnterpriseDocumentationPlatform.Services
{
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
        private readonly ITeamsNotificationService _teamsNotificationService;
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
            ITeamsNotificationService teamsNotificationService,
            IConfiguration configuration)
        {
            _logger = logger;
            _workflowEventService = workflowEventService;
            _teamsNotificationService = teamsNotificationService;
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
                    EventType = "CodeExtractionStarted",
                    Status = "InProgress",
                    Message = $"Starting code extraction from {storedProcedureName} for {jiraNumber}",
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new { DocId = docId, StoredProcedure = storedProcedureName, JiraNumber = jiraNumber })
                }, ct);

                // Get stored procedure definition
                string? spDefinition = await GetStoredProcedureDefinitionAsync(storedProcedureName, ct);
                if (spDefinition == null)
                {
                    // SP not found - send warning notification but continue workflow
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
                    EventType = "CodeExtractionCompleted",
                    Status = "Completed",
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
                    EventType = "WorkflowFailed",
                    Status = "Failed",
                    Message = $"Fatal error during code extraction: {ex.Message}",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new { DocId = docId, StoredProcedure = storedProcedureName, JiraNumber = jiraNumber, Error = ex.Message })
                }, ct);

                // Send error notification
                await _teamsNotificationService.SendNotificationAsync(
                    "system@company.com", // Use system email or get from config
                    "❌ Code Extraction Failed",
                    $"Fatal error during code extraction for {docId}.\n\n" +
                    $"**Stored Procedure:** {storedProcedureName}\n" +
                    $"**Jira Number:** {jiraNumber}\n" +
                    $"**Error:** {ex.Message}\n\n" +
                    $"This workflow has been stopped and requires manual intervention.",
                    NotificationSeverity.Error,
                    docId,
                    ct);

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

            // Define marker patterns (case-insensitive)
            var patterns = new[]
            {
                // Pattern 1: -- BEGIN {JiraNumber} ... -- END {JiraNumber}
                $@"--\s*BEGIN\s+{Regex.Escape(jiraNumber)}\s*\r?\n(.*?)\r?\n\s*--\s*END\s+{Regex.Escape(jiraNumber)}",
                
                // Pattern 2: -- START {JiraNumber} ... -- END {JiraNumber}
                $@"--\s*START\s+{Regex.Escape(jiraNumber)}\s*\r?\n(.*?)\r?\n\s*--\s*END\s+{Regex.Escape(jiraNumber)}",
                
                // Pattern 3: /* BEGIN {JiraNumber} */ ... /* END {JiraNumber} */
                $@"/\*\s*BEGIN\s+{Regex.Escape(jiraNumber)}\s*\*/(.*?)/\*\s*END\s+{Regex.Escape(jiraNumber)}\s*\*/"
            };

            var extractedSections = new List<string>();

            foreach (var pattern in patterns)
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var matches = regex.Matches(spDefinition);

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var extractedCode = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(extractedCode))
                        {
                            extractedSections.Add(extractedCode);
                        }
                    }
                }
            }

            if (extractedSections.Any())
            {
                result.HasMarkers = true;
                result.MarkerCount = extractedSections.Count;
                result.ExtractedCode = string.Join("\n\n-- Next Section --\n\n", extractedSections);
                result.ExtractionMethod = "Markers";
            }
            else
            {
                result.HasMarkers = false;
                result.MarkerCount = 0;
                result.ExtractedCode = spDefinition; // Return full SP if no markers
                result.ExtractionMethod = "FullSP";
                result.Warnings = "No markers found for the specified Jira number. Returning full stored procedure.";
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
                EventType = "CodeExtractionFailed",
                Status = "Warning",
                Message = message,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new { Reason = "SPNotFound", StoredProcedure = storedProcedureName })
            }, ct);

            // Send Teams notification
            await _teamsNotificationService.SendNotificationAsync(
                "system@company.com", // Use system email or get from config
                "⚠️ Stored Procedure Not Found",
                $"**DocId:** {docId}\n" +
                $"**Stored Procedure:** {storedProcedureName}\n" +
                $"**Jira Number:** {jiraNumber}\n\n" +
                $"{message}",
                NotificationSeverity.Warning,
                docId,
                ct);
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

            // Publish warning event
            await _workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = "CodeExtractionCompleted",
                Status = "Warning",
                Message = message,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new { Reason = "NoMarkers", ExtractionMethod = "FullSP" })
            }, ct);

            // Send Teams notification
            await _teamsNotificationService.SendNotificationAsync(
                "system@company.com", // Use system email or get from config
                "⚠️ No Code Markers Found",
                $"**DocId:** {docId}\n" +
                $"**Stored Procedure:** {storedProcedureName}\n" +
                $"**Jira Number:** {jiraNumber}\n\n" +
                $"{message}\n\n" +
                $"**Expected Markers:**\n" +
                $"```\n" +
                $"-- BEGIN {jiraNumber}\n" +
                $"-- Your code changes here\n" +
                $"-- END {jiraNumber}\n" +
                $"```",
                NotificationSeverity.Warning,
                docId,
                ct);
        }

    }
}
