using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Dapper;
using Microsoft.Data.SqlClient;
using Enterprise.Documentation.Core.Application.Services.CodeExtraction;
using Enterprise.Documentation.Core.Application.Services.Quality;
using Enterprise.Documentation.Core.Application.Services.Workflow;
using Enterprise.Documentation.Core.Application.Services.DraftGeneration;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Domain.Models;

namespace Enterprise.Documentation.Core.Application.Services.QueueProcessor;

/// <summary>
/// Background service that processes items in the DocumentationQueue table.
/// This service polls the queue, extracts code, audits quality, and generates draft documents.
/// </summary>
public class DocGeneratorQueueProcessor : BackgroundService
{
    private readonly ILogger<DocGeneratorQueueProcessor> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connectionString;
    private readonly TimeSpan _pollInterval;

    public DocGeneratorQueueProcessor(
        ILogger<DocGeneratorQueueProcessor> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string not found");

        // Default to 1 minute polling interval
        var intervalSecondsConfig = configuration["DocGeneratorQueueProcessor:PollIntervalSeconds"];
        var intervalSeconds = int.TryParse(intervalSecondsConfig, out var parsed) ? parsed : 60;
        _pollInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocGeneratorQueueProcessor started with {PollInterval} polling interval", _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DocGeneratorQueueProcessor");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("DocGeneratorQueueProcessor stopped");
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var codeExtractionService = scope.ServiceProvider.GetRequiredService<ICodeExtractionService>();
        var codeQualityService = scope.ServiceProvider.GetRequiredService<IEnterpriseCodeQualityAuditService>();
        var workflowEventService = scope.ServiceProvider.GetRequiredService<IWorkflowEventService>();
        var draftGenerationService = scope.ServiceProvider.GetRequiredService<IDraftGenerationService>();

        var queueItem = await GetNextPendingItemAsync(ct);
        if (queueItem == null)
        {
            _logger.LogDebug("No pending items in DocumentationQueue");
            return;
        }

        _logger.LogInformation("Processing queue item: DocId={DocId}, QueueId={QueueId}",
            queueItem.DocId, queueItem.Id);

        var change = await GetDocumentChangeAsync(queueItem.DocId, ct);
        if (change == null)
        {
            _logger.LogWarning("DocumentChange not found for DocId: {DocId}", queueItem.DocId);
            await UpdateQueueStatusAsync(queueItem.Id, "Failed", null, "DocumentChange not found", ct);
            return;
        }

        string? generatedFilePath = null;

        try
        {
            // STEP 3: Code extraction (if SP involved)
            CodeExtractionResult? codeResult = null;
            if (!string.IsNullOrWhiteSpace(change.StoredProcedureName))
            {
                codeResult = await codeExtractionService.ExtractMarkedCodeAsync(
                    change.DocId,
                    change.StoredProcedureName,
                    change.JiraNumber,
                    ct);
            }

            // STEP 4: Code quality audit
            CodeQualityResult? qualityResult = null;
            if (codeResult?.ExtractedCode != null && !string.IsNullOrWhiteSpace(codeResult.ExtractedCode))
            {
                qualityResult = await PerformCodeQualityAuditAsync(
                    change.DocId,
                    codeResult.ExtractedCode,
                    codeQualityService,
                    workflowEventService,
                    ct);
            }

            // STEP 5: Draft generation
            generatedFilePath = await GenerateDraftDocumentAsync(change, codeResult, qualityResult, workflowEventService, draftGenerationService, ct);

            // Mark as completed with file path
            await UpdateQueueStatusAsync(queueItem.Id, "Completed", generatedFilePath, "Document generation completed successfully", ct);

            // Create approval workflow entry
            await CreateApprovalWorkflowEntryAsync(change.DocId, change, ct);

            _logger.LogInformation("Successfully processed DocId: {DocId}", change.DocId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process DocId: {DocId}", change.DocId);
            await UpdateQueueStatusAsync(queueItem.Id, "Failed", null, ex.Message, ct);
        }
    }

    private async Task CreateApprovalWorkflowEntryAsync(string docId, DocumentChange change, CancellationToken ct)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var sql = @"
                INSERT INTO DaQa.ApprovalWorkflow (
                    DocumentId,
                    DocIdString,
                    DocumentType,
                    RequestedBy,
                    RequestedDate,
                    ApproverEmail,
                    ApprovalStatus,
                    Comments
                ) VALUES (
                    NEWID(),
                    @DocIdString,
                    @DocumentType,
                    @RequestedBy,
                    GETUTCDATE(),
                    @ApproverEmail,
                    'Pending',
                    @Comments
                )";
            
            await connection.ExecuteAsync(sql, new
            {
                DocIdString = docId,
                DocumentType = docId.Split('-')[0],
                RequestedBy = change.AssignedTo ?? "System",
                ApproverEmail = change.AssignedTo ?? "admin@company.com",
                Comments = change.Description ?? "Pending approval"
            });
            
            _logger.LogInformation("✅ Created approval workflow entry for DocId: {DocId}", docId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create approval workflow entry for DocId: {DocId}", docId);
            // Don't throw - non-critical
        }
    }

    private async Task<QueueItem?> GetNextPendingItemAsync(CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string query = @"
            SELECT TOP 1
                CAST(QueueId as UNIQUEIDENTIFIER) as Id,
                COALESCE(DocIdString, CAST(ChangeId as NVARCHAR(50))) as DocId,
                ISNULL(Status, 'Pending') as Status,
                ISNULL(CreatedDate, GETUTCDATE()) as CreatedAt,
                ISNULL(CreatedDate, GETUTCDATE()) as UpdatedAt
            FROM DaQa.DocumentationQueue
            WHERE Status = 'Pending'
            ORDER BY CreatedDate ASC";

        var item = await connection.QueryFirstOrDefaultAsync<QueueItem>(query);
        return item;
    }

    private async Task<DocumentChange?> GetDocumentChangeAsync(string docId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string query = @"
            SELECT 
                DocId,
                JiraNumber,
                TableName,
                ColumnName,
                LocationOfCodeChange as StoredProcedureName,
                Description,
                AssignedTo,
                ChangeType,
                Priority,
                Severity
            FROM DaQa.DocumentChanges
            WHERE DocId = @DocId";

        var change = await connection.QueryFirstOrDefaultAsync<DocumentChange>(query, new { DocId = docId });
        return change;
    }

    private async Task<CodeQualityResult?> PerformCodeQualityAuditAsync(
        string docId,
        string code,
        IEnterpriseCodeQualityAuditService codeQualityService,
        IWorkflowEventService workflowEventService,
        CancellationToken ct)
    {
        var workflowId = $"WF-{docId}";
        var startTime = DateTime.UtcNow;

        try
        {
            await workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.DocumentApproved,
                Status = WorkflowEventStatus.InProgress,
                Message = "Analyzing code quality",
                Timestamp = DateTime.UtcNow
            }, ct);

            var result = await codeQualityService.AuditCodeQualityAsync(code, ct);
            var isPoor = result.Grade == "D" || result.Grade == "F";

            var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            await workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.WorkflowCompleted,
                Status = isPoor ? WorkflowEventStatus.Failed : WorkflowEventStatus.Completed,
                Message = $"Code quality: {result.Grade} ({result.Score}/100) - {result.Category}",
                DurationMs = duration,
                Timestamp = DateTime.UtcNow,
                Metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    result.Score,
                    result.Grade,
                    result.Category,
                    IssueCount = result.Issues?.Count ?? 0
                })
            }, ct);

            await UpdateCodeQualityAsync(docId, result.Score, result.Grade, ct);

            if (isPoor)
            {
                _logger.LogWarning("Poor code quality detected for {DocId}: {Grade} ({Score}/100)",
                    docId, result.Grade, result.Score);
            }
            else
            {
                _logger.LogInformation("Code quality audit completed for {DocId}: {Grade} ({Score}/100)",
                    docId, result.Grade, result.Score);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Code quality audit failed for {DocId}, continuing workflow", docId);

            await workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.WorkflowCompleted,
                Status = WorkflowEventStatus.Failed,
                Message = $"Code quality audit failed: {ex.Message}",
                Timestamp = DateTime.UtcNow
            }, ct);

            return null;
        }
    }

    private async Task UpdateCodeQualityAsync(string docId, int score, string grade, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            UPDATE DaQa.DocumentChanges
            SET 
                CodeQualityScore = @Score,
                CodeQualityGrade = @Grade,
                UpdatedAt = GETUTCDATE()
            WHERE DocId = @DocId";
        
        await connection.ExecuteAsync(sql, new
        {
            DocId = docId,
            Score = score,
            Grade = grade
        });
    }

    private async Task<string?> GenerateDraftDocumentAsync(
        DocumentChange change,
        CodeExtractionResult? codeResult,
        CodeQualityResult? qualityResult,
        IWorkflowEventService workflowEventService,
        IDraftGenerationService draftGenerationService,
        CancellationToken ct)
    {
        var workflowId = $"WF-{change.DocId}";
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting draft generation for DocId: {DocId}", change.DocId);

            await workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.DocumentApproved,
                Status = WorkflowEventStatus.InProgress,
                Message = "Generating draft document",
                Timestamp = DateTime.UtcNow
            }, ct);


var changeEntry = new DocumentChangeEntry
{
    DocId = change.DocId,
    JiraNumber = change.JiraNumber,
    TableName = change.TableName,
    ColumnName = change.ColumnName,
    ModifiedStoredProcedures = change.StoredProcedureName,
    Description = change.Description,
    AssignedTo = change.AssignedTo,
    ChangeType = change.ChangeType,
    Priority = change.Priority,
    Severity = change.Severity
};

var draftResult = await draftGenerationService.GenerateDraftAsync(
    change.DocId,
    changeEntry,
    codeResult,
    qualityResult,
    ct);

if (string.IsNullOrEmpty(draftResult.ErrorMessage))

            {
                var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation("Draft generation completed successfully for DocId: {DocId}, Template: {Template}",
                    change.DocId, draftResult.TemplateUsed);

                // Generate Word document
                string? outputPath = null;
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var templateExecutorService = scope.ServiceProvider.GetRequiredService<ITemplateExecutorService>();

				var changeDataForTemplate = await GetDocumentChangeAsync(change.DocId, ct);
                    if (changeDataForTemplate == null)
                    {
                        throw new InvalidOperationException($"DocumentChange not found for DocId: {change.DocId}");
                    }


                    var draftsPath = _configuration["DocumentWorkflow:DraftsPath"] ?? "C:\\Temp\\Documentation-Catalog\\Drafts";
                    var fileName = $"{change.DocId}_DRAFT_{changeDataForTemplate.TableName?.Replace(".", "_") ?? "Unknown"}.docx";
                    outputPath = Path.Combine(draftsPath, fileName);

                    var templateRequest = new DocumentGeneration.TemplateExecutionRequest
                    {
                        TemplateType = draftResult.DocumentType,  // Use this instead!
                        OutputPath = outputPath,
                        TemplateData = new Dictionary<string, object>
                        {
                            // Fix field names to match what templates expect
                            ["doc_id"] = change.DocId ?? "UNKNOWN",
                            ["DocId"] = change.DocId ?? "UNKNOWN", // Keep both for compatibility
                            ["jira_number"] = changeDataForTemplate.JiraNumber ?? "",
                            ["JiraNumber"] = changeDataForTemplate.JiraNumber ?? "",
                            ["description"] = changeDataForTemplate.Description ?? "",
                            ["Description"] = changeDataForTemplate.Description ?? "",
                            ["table_name"] = changeDataForTemplate.TableName ?? "",
                            ["TableName"] = changeDataForTemplate.TableName ?? "",
                            ["column_name"] = changeDataForTemplate.ColumnName ?? "",
                            ["ColumnName"] = changeDataForTemplate.ColumnName ?? "",
                            ["change_type"] = changeDataForTemplate.ChangeType ?? "",
                            ["ChangeType"] = changeDataForTemplate.ChangeType ?? "",
                            ["priority"] = changeDataForTemplate.Priority ?? "",
                            ["Priority"] = changeDataForTemplate.Priority ?? "",
                            ["assigned_to"] = changeDataForTemplate.AssignedTo ?? "",
                            ["AssignedTo"] = changeDataForTemplate.AssignedTo ?? "",
                            ["sp_name"] = changeDataForTemplate.StoredProcedureName ?? "unknown_procedure",
                            ["StoredProcedureName"] = changeDataForTemplate.StoredProcedureName ?? "",
                            ["schema"] = "dbo", // Default since not available in DocumentChange
                            ["date"] = DateTime.Now.ToString("yyyy-MM-dd"), // Default to current date
                            ["status"] = "Completed" // Default status
                        }
                    };

                    await templateExecutorService.GenerateDocumentAsync(templateRequest, ct);
                    _logger.LogInformation("Word document generated successfully for DocId: {DocId} at {OutputPath}", 
                        change.DocId, templateRequest.OutputPath);
                }
                catch (Exception templateEx)
                {
                    _logger.LogError(templateEx, "Failed to generate Word document for DocId: {DocId}", change.DocId);
                    // Continue execution
                }

                await workflowEventService.PublishEventAsync(new WorkflowEvent
                {
                    WorkflowId = workflowId,
                    EventType = WorkflowEventType.WorkflowCompleted,
                    Status = WorkflowEventStatus.Completed,
                    Message = $"Draft generation completed using {draftResult.TemplateUsed}",
                    DurationMs = duration,
                    Timestamp = DateTime.UtcNow,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        TemplateUsed = draftResult.TemplateUsed,
                        DocumentUrl = draftResult.DocumentUrl,
                        WarningCount = draftResult.Warnings.Count,
                        HasCodeResult = codeResult != null,
                        HasQualityResult = qualityResult != null,
                        QualityGrade = qualityResult?.Grade
                    })
                }, ct);

                return outputPath;
            }
            else
            {
                _logger.LogError("Draft generation failed for DocId: {DocId}: {Error}", 
                    change.DocId, draftResult.ErrorMessage);

                await workflowEventService.PublishEventAsync(new WorkflowEvent
                {
                    WorkflowId = workflowId,
                    EventType = WorkflowEventType.WorkflowCompleted,
                    Status = WorkflowEventStatus.Failed,
                    Message = $"Draft generation failed: {draftResult.ErrorMessage}",
                    DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Timestamp = DateTime.UtcNow
                }, ct);

                throw new InvalidOperationException($"Draft generation failed: {draftResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex, "Draft generation failed for DocId: {DocId}", change.DocId);

            await workflowEventService.PublishEventAsync(new WorkflowEvent
            {
                WorkflowId = workflowId,
                EventType = WorkflowEventType.WorkflowCompleted,
                Status = WorkflowEventStatus.Failed,
                Message = $"Draft generation failed: {ex.Message}",
                DurationMs = duration,
                Timestamp = DateTime.UtcNow
            }, ct);

            throw;
        }
    }

    private async Task UpdateQueueStatusAsync(Guid queueId, string status, string? documentUrl, string? errorMessage, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        
        var sql = @"
            UPDATE DaQa.DocumentationQueue
            SET 
                Status = @Status,
                DocumentUrl = @DocumentUrl,
                ErrorMessage = @ErrorMessage,
                CompletedDate = CASE WHEN @Status = 'Completed' THEN GETUTCDATE() ELSE CompletedDate END
            WHERE QueueId = @QueueId";
        
        await connection.ExecuteAsync(sql, new
        {
            QueueId = queueId,
            Status = status,
            DocumentUrl = documentUrl,
            ErrorMessage = errorMessage
        });
    }

    public class QueueItem
    {
        public Guid Id { get; set; }
        public string DocId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class DocumentChange
    {
        public string DocId { get; set; } = string.Empty;
        public string JiraNumber { get; set; } = string.Empty;
        public string? TableName { get; set; }
        public string? ColumnName { get; set; }
        public string? StoredProcedureName { get; set; }
        public string? Description { get; set; }
        public string? AssignedTo { get; set; }
        public string? ChangeType { get; set; }
        public string? Priority { get; set; }
        public string? Severity { get; set; }
    }
}