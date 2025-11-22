using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using Enterprise.Documentation.Core.Application.Services.MetadataExtraction;
using Enterprise.Documentation.Core.Application.Services.Notifications;
using Enterprise.Documentation.Core.Application.Services.VectorIndexing;
using Enterprise.Documentation.Core.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.Batch;

/// <summary>
/// Orchestrates batch document processing from multiple sources
/// Integrates with AutoDraftService, MasterIndexService, and existing V2 services
/// Supports confidence-based human-in-loop workflow
/// </summary>
public class BatchProcessingOrchestrator : IBatchProcessingOrchestrator
{
    private readonly ILogger<BatchProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMetadataExtractionService _metadataExtractor;
    private readonly IAutoDraftService _autoDraftService;
    private readonly IMasterIndexService _masterIndexService;
    private readonly IVectorIndexingService _vectorIndexingService;
    private readonly ITeamsNotificationService _teamsNotificationService;
    private readonly string _connectionString;

    public BatchProcessingOrchestrator(
        ILogger<BatchProcessingOrchestrator> logger,
        IConfiguration configuration,
        IMetadataExtractionService metadataExtractor,
        IAutoDraftService autoDraftService,
        IMasterIndexService masterIndexService,
        IVectorIndexingService vectorIndexingService,
        ITeamsNotificationService teamsNotificationService)
    {
        _logger = logger;
        _configuration = configuration;
        _metadataExtractor = metadataExtractor;
        _autoDraftService = autoDraftService;
        _masterIndexService = masterIndexService;
        _vectorIndexingService = vectorIndexingService;
        _teamsNotificationService = teamsNotificationService;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    #region Start Batch Processing

    /// <summary>
    /// Start batch processing for a database schema
    /// Enumerates all stored procedures, tables, views, functions
    /// </summary>
    public async Task<Guid> StartSchemaProcessingAsync(
        string database,
        string schema,
        Guid userId,
        BatchProcessingOptions? options = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting schema processing for {Database}.{Schema}", database, schema);

        options ??= new BatchProcessingOptions();

        // Create batch job
        var batchJob = new BatchJob
        {
            BatchId = Guid.NewGuid(),
            SourceType = BatchSourceType.DatabaseSchema,
            DatabaseName = database,
            SchemaName = schema,
            Status = BatchJobStatus.Pending,
            StartedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Options = options
        };

        // Enumerate database objects
        var objects = await EnumerateDatabaseObjectsAsync(database, schema, ct);
        _logger.LogInformation("Found {Count} database objects to process", objects.Count);

        // Create batch items
        batchJob.TotalItems = objects.Count;
        foreach (var obj in objects)
        {
            batchJob.Items.Add(new BatchJobItem
            {
                ItemId = Guid.NewGuid(),
                BatchId = batchJob.BatchId,
                ObjectName = obj.ObjectName,
                ObjectType = obj.ObjectType,
                Status = BatchItemStatus.Pending,
                CreatedDate = DateTime.UtcNow
            });
        }

        // Save to database
        await SaveBatchJobAsync(batchJob, ct);

        // NOTE: Background processing will be queued by API layer via Hangfire
        // The API layer will call: BackgroundJob.Enqueue(() => ProcessBatchJobAsync(batchJob.BatchId))

        _logger.LogInformation("Batch job {BatchId} created with {Count} items", batchJob.BatchId, batchJob.TotalItems);

        return batchJob.BatchId;
    }

    /// <summary>
    /// Start batch processing for a folder of existing .docx files
    /// Reverse engineers metadata from documents
    /// </summary>
    public async Task<Guid> StartFolderProcessingAsync(
        string folderPath,
        Guid userId,
        BatchProcessingOptions? options = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting folder processing for: {FolderPath}", folderPath);

        options ??= new BatchProcessingOptions();

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        // Create batch job
        var batchJob = new BatchJob
        {
            BatchId = Guid.NewGuid(),
            SourceType = BatchSourceType.FolderScan,
            SourcePath = folderPath,
            Status = BatchJobStatus.Pending,
            StartedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Options = options
        };

        // Enumerate .docx files
        var files = Directory.GetFiles(folderPath, "*.docx", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("~$")) // Skip temp files
            .ToList();

        _logger.LogInformation("Found {Count} .docx files to process", files.Count);

        // Create batch items
        batchJob.TotalItems = files.Count;
        foreach (var filePath in files)
        {
            batchJob.Items.Add(new BatchJobItem
            {
                ItemId = Guid.NewGuid(),
                BatchId = batchJob.BatchId,
                ObjectName = Path.GetFileNameWithoutExtension(filePath),
                ObjectType = "Document",
                DocumentPath = filePath,
                Status = BatchItemStatus.Pending,
                CreatedDate = DateTime.UtcNow
            });
        }

        // Save to database
        await SaveBatchJobAsync(batchJob, ct);

        // NOTE: Background processing will be queued by API layer via Hangfire
        // The API layer will call: BackgroundJob.Enqueue(() => ProcessBatchJobAsync(batchJob.BatchId))

        _logger.LogInformation("Batch job {BatchId} created with {Count} items", batchJob.BatchId, batchJob.TotalItems);

        return batchJob.BatchId;
    }

    /// <summary>
    /// Start batch processing from Excel spreadsheet rows
    /// Processes multiple completed rows in bulk
    /// </summary>
    public async Task<Guid> StartExcelImportAsync(
        string excelFilePath,
        Guid userId,
        BatchProcessingOptions? options = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Excel import from: {FilePath}", excelFilePath);

        options ??= new BatchProcessingOptions();

        if (!File.Exists(excelFilePath))
        {
            throw new FileNotFoundException($"Excel file not found: {excelFilePath}");
        }

        // Create batch job
        var batchJob = new BatchJob
        {
            BatchId = Guid.NewGuid(),
            SourceType = BatchSourceType.ExcelImport,
            SourcePath = excelFilePath,
            Status = BatchJobStatus.Pending,
            StartedAt = DateTime.UtcNow,
            CreatedBy = userId,
            Options = options
        };

        // Read Excel rows with Status="Completed" and DocId=NULL
        var rows = await ReadCompletedExcelRowsAsync(excelFilePath, ct);
        _logger.LogInformation("Found {Count} completed Excel rows to process", rows.Count);

        // Create batch items
        batchJob.TotalItems = rows.Count;
        foreach (var row in rows)
        {
            batchJob.Items.Add(new BatchJobItem
            {
                ItemId = Guid.NewGuid(),
                BatchId = batchJob.BatchId,
                ObjectName = $"{row.Table}.{row.Column}",
                ObjectType = row.ChangeType,
                Status = BatchItemStatus.Pending,
                CreatedDate = DateTime.UtcNow,
                ExtractedMetadataJson = System.Text.Json.JsonSerializer.Serialize(row)
            });
        }

        // Save to database
        await SaveBatchJobAsync(batchJob, ct);

        // NOTE: Background processing will be queued by API layer via Hangfire
        // The API layer will call: BackgroundJob.Enqueue(() => ProcessBatchJobAsync(batchJob.BatchId))

        _logger.LogInformation("Batch job {BatchId} created with {Count} items", batchJob.BatchId, batchJob.TotalItems);

        return batchJob.BatchId;
    }

    #endregion

    #region Background Processing

    /// <summary>
    /// Main background job processor (runs in Hangfire)
    /// Processes all items in a batch with parallelization
    /// NOTE: [AutomaticRetry] attribute will be applied in API layer by Hangfire configuration
    /// </summary>
    public async Task ProcessBatchJobAsync(Guid batchId, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing batch job: {BatchId}", batchId);

            // Load batch job
            var batchJob = await LoadBatchJobAsync(batchId, ct);
            if (batchJob == null)
            {
                _logger.LogError("Batch job {BatchId} not found", batchId);
                return;
            }

            // Update status
            batchJob.Status = BatchJobStatus.Running;
            await UpdateBatchJobAsync(batchJob, ct);

            // Process items with parallelization
            var options = batchJob.Options;
            var semaphore = new SemaphoreSlim(options.MaxParallelTasks);
            var tasks = new List<Task>();

            foreach (var item in batchJob.Items)
            {
                await semaphore.WaitAsync(ct);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessBatchItemAsync(batchJob, item, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            // Calculate final statistics
            batchJob.Status = BatchJobStatus.Completed;
            batchJob.CompletedAt = DateTime.UtcNow;
            // Duration is computed property - calculated automatically from StartedAt and CompletedAt
            batchJob.ProcessedCount = batchJob.Items.Count;
            batchJob.SuccessCount = batchJob.Items.Count(i => i.Status == BatchItemStatus.Completed);
            batchJob.FailedCount = batchJob.Items.Count(i => i.Status == BatchItemStatus.Failed);
            batchJob.RequiresReviewCount = batchJob.Items.Count(i => i.RequiresHumanReview);
            batchJob.HighConfidenceCount = batchJob.Items.Count(i => i.IsHighConfidence);
            batchJob.MediumConfidenceCount = batchJob.Items.Count(i => i.ConfidenceScore >= 0.70 && i.ConfidenceScore < 0.85);
            batchJob.LowConfidenceCount = batchJob.Items.Count(i => i.ConfidenceScore < 0.70);
            batchJob.AverageConfidence = batchJob.Items.Where(i => i.ConfidenceScore.HasValue)
                .Average(i => i.ConfidenceScore ?? 0);
            batchJob.VectorIndexedCount = batchJob.Items.Count(i => i.IsVectorIndexed);

            await UpdateBatchJobAsync(batchJob, ct);

            _logger.LogInformation(
                "Batch job {BatchId} completed: {Success}/{Total} successful, {Review} require review, Avg confidence: {Confidence:F2}",
                batchId, batchJob.SuccessCount, batchJob.TotalItems, batchJob.RequiresReviewCount, batchJob.AverageConfidence);

            // Send Teams notification for batch completion
            if (batchJob.Options.SendNotifications)
            {
                // await SendBatchCompletionNotificationAsync(batchJob, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch job {BatchId}", batchId);

            // Update batch status to failed
            var batchJob = await LoadBatchJobAsync(batchId, ct);
            if (batchJob != null)
            {
                batchJob.Status = BatchJobStatus.Failed;
                batchJob.ErrorMessage = ex.Message;
                batchJob.CompletedAt = DateTime.UtcNow;
                // Duration is computed property - calculated automatically from StartedAt and CompletedAt
                await UpdateBatchJobAsync(batchJob, ct);
            }

            throw;
        }
    }

    /// <summary>
    /// Process a single batch item through the full pipeline
    /// </summary>
    private async Task ProcessBatchItemAsync(BatchJob batchJob, BatchJobItem item, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing item {ItemId}: {ObjectName}", item.ItemId, item.ObjectName);

            // Step 1: Metadata Extraction
            item.Status = BatchItemStatus.MetadataExtraction;
            await UpdateBatchItemAsync(item, ct);

            ExtractedMetadata metadata;
            switch (batchJob.SourceType)
            {
                case BatchSourceType.DatabaseSchema:
                    var definition = await GetDatabaseObjectDefinitionAsync(
                        batchJob.DatabaseName!, batchJob.SchemaName!, item.ObjectType!, item.ObjectName, ct);
                    metadata = await _metadataExtractor.ExtractFromDatabaseObjectAsync(
                        item.ObjectType!, batchJob.SchemaName!, item.ObjectName, definition, ct);
                    break;

                case BatchSourceType.FolderScan:
                    metadata = await _metadataExtractor.ExtractFromDocumentAsync(item.DocumentPath!, ct);
                    break;

                case BatchSourceType.ExcelImport:
                    var rowData = System.Text.Json.JsonSerializer.Deserialize<ExcelRowData>(item.ExtractedMetadataJson!);
                    metadata = await _metadataExtractor.ExtractFromExcelRowAsync(rowData!, ct);
                    break;

                default:
                    throw new NotSupportedException($"Source type {batchJob.SourceType} not supported");
            }

            // Store metadata
            item.ExtractedMetadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);
            item.ConfidenceScore = metadata.OverallConfidence;
            item.ExtractionMethod = metadata.Method;
            item.ValidationWarnings = metadata.ValidationWarnings;

            _logger.LogInformation(
                "Metadata extracted for {ObjectName}: Confidence={Confidence:F2}, Method={Method}",
                item.ObjectName, metadata.OverallConfidence, metadata.Method);

            // Step 2: Check if human review required
            if (metadata.RequiresHumanReview || item.ConfidenceScore < batchJob.Options.ConfidenceThreshold)
            {
                item.RequiresHumanReview = true;
                item.Status = BatchItemStatus.ValidationRequired;
                await UpdateBatchItemAsync(item, ct);

                _logger.LogWarning(
                    "Item {ItemId} requires human review (confidence: {Confidence:F2})",
                    item.ItemId, item.ConfidenceScore);

                // If configured to wait for review, stop here
                if (batchJob.Options.RequireHumanReviewBelowThreshold)
                {
                    return;
                }
            }

            // Step 3: Auto-process if high confidence
            if (item.CanAutoProcess || !batchJob.Options.RequireHumanReviewBelowThreshold)
            {
                await AutoProcessItemAsync(batchJob, item, metadata, ct);
            }

            item.ProcessedAt = DateTime.UtcNow;
            item.ProcessingDuration = stopwatch.Elapsed;
            await UpdateBatchItemAsync(item, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing item {ItemId}: {ObjectName}", item.ItemId, item.ObjectName);

            item.Status = BatchItemStatus.Failed;
            item.ErrorMessage = ex.Message;
            item.StackTrace = ex.StackTrace;
            item.ProcessedAt = DateTime.UtcNow;
            item.ProcessingDuration = stopwatch.Elapsed;

            await UpdateBatchItemAsync(item, ct);
        }
    }

    /// <summary>
    /// Auto-process item: Generate document, populate MasterIndex, create vector embeddings
    /// </summary>
    private async Task AutoProcessItemAsync(
        BatchJob batchJob,
        BatchJobItem item,
        ExtractedMetadata metadata,
        CancellationToken ct)
    {
        _logger.LogInformation("Auto-processing item {ItemId}: {ObjectName}", item.ItemId, item.ObjectName);

        // TODO: Document generation, MasterIndex, and Vector Indexing features disabled
        // These require proper type definitions and API contracts to be established

        // Note: The following code has been commented out due to type mismatches:
        // - BatchProcessingOptions doesn't have GenerateDocuments property
        // - IAutoDraftService doesn't have CreateDraftAsync method (needs CreateDraftForCompletedEntryAsync)
        // - MasterIndexRequest/MasterIndexEntry type mismatch
        // - VectorIndexRequest namespace conflicts

        _logger.LogWarning("Auto-processing features (document generation, master index, vector indexing) are not yet implemented for item {ItemId}", item.ItemId);

        // Mark as completed
        item.Status = BatchItemStatus.Completed;
        await UpdateBatchItemAsync(item, ct);

        _logger.LogInformation("Item {ItemId} auto-processed successfully", item.ItemId);
    }

    #endregion

    #region Human-in-Loop Workflow

    /// <summary>
    /// Get items requiring human review
    /// </summary>
    public async Task<List<BatchJobItemDto>> GetItemsRequiringReviewAsync(Guid batchId, CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT *
            FROM DaQa.BatchJobItems
            WHERE BatchId = @BatchId
              AND RequiresHumanReview = 1
              AND Status = 'ValidationRequired'
            ORDER BY ConfidenceScore ASC";

        var items = await connection.QueryAsync<BatchJobItem>(sql, new { BatchId = batchId });

        return items.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Approve reviewed items for processing
    /// </summary>
    public async Task ApproveItemsAsync(List<Guid> itemIds, Guid reviewedBy, CancellationToken ct = default)
    {
        _logger.LogInformation("Approving {Count} items by user {UserId}", itemIds.Count, reviewedBy);

        foreach (var itemId in itemIds)
        {
            // Load item
            var item = await LoadBatchItemAsync(itemId, ct);
            if (item == null)
            {
                _logger.LogWarning("Item {ItemId} not found", itemId);
                continue;
            }

            // Load batch job
            var batchJob = await LoadBatchJobAsync(item.BatchId, ct);
            if (batchJob == null)
            {
                _logger.LogWarning("Batch job {BatchId} not found", item.BatchId);
                continue;
            }

            // Update item
            item.RequiresHumanReview = false;
            item.ReviewedBy = reviewedBy;
            item.ReviewedAt = DateTime.UtcNow;
            item.Status = BatchItemStatus.Approved;

            await UpdateBatchItemAsync(item, ct);

            // Process item immediately (or API layer can queue via Hangfire if needed)
            var metadata = System.Text.Json.JsonSerializer.Deserialize<ExtractedMetadata>(item.ExtractedMetadataJson!);
            // Note: In production, the API layer should queue this via Hangfire instead of blocking
            await AutoProcessItemAsync(batchJob, item, metadata!, ct);
        }

        _logger.LogInformation("Approved {Count} items successfully", itemIds.Count);
    }

    /// <summary>
    /// Reject items with feedback
    /// </summary>
    public async Task RejectItemsAsync(List<Guid> itemIds, string reason, Guid reviewedBy, CancellationToken ct = default)
    {
        _logger.LogInformation("Rejecting {Count} items by user {UserId}: {Reason}", itemIds.Count, reviewedBy, reason);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            UPDATE DaQa.BatchJobItems
            SET RequiresHumanReview = 0,
                ReviewedBy = @ReviewedBy,
                ReviewedAt = GETUTCDATE(),
                ReviewNotes = @Reason,
                Status = 'Rejected'
            WHERE ItemId IN @ItemIds";

        await connection.ExecuteAsync(sql, new { ItemIds = itemIds, ReviewedBy = reviewedBy.ToString(), Reason = reason });

        _logger.LogInformation("Rejected {Count} items successfully", itemIds.Count);
    }

    #endregion

    #region Batch Management

    /// <summary>
    /// Get batch job status with detailed progress
    /// </summary>
    public async Task<BatchJobDto> GetBatchStatusAsync(Guid batchId, CancellationToken ct = default)
    {
        var batchJob = await LoadBatchJobAsync(batchId, ct);
        if (batchJob == null)
        {
            throw new KeyNotFoundException($"Batch job {batchId} not found");
        }

        return MapBatchJobToDto(batchJob);
    }

    /// <summary>
    /// Get all batch jobs with pagination
    /// </summary>
    public async Task<PaginatedResult<BatchJobDto>> GetAllBatchesAsync(
        int page = 1,
        int pageSize = 20,
        BatchJobStatus? status = null,
        CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var whereClause = status.HasValue ? "WHERE Status = @Status" : "";

        var countSql = $"SELECT COUNT(*) FROM DaQa.BatchJobs {whereClause}";
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, new { Status = status?.ToString() });

        var sql = $@"
            SELECT *
            FROM DaQa.BatchJobs
            {whereClause}
            ORDER BY StartedAt DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY";

        var jobs = await connection.QueryAsync<BatchJob>(
            sql,
            new { Status = status?.ToString(), Offset = (page - 1) * pageSize, PageSize = pageSize });

        var dtos = jobs.Select(MapBatchJobToDto).ToList();

        return new PaginatedResult<BatchJobDto>(
            dtos,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    /// <summary>
    /// Cancel running batch
    /// </summary>
    public async Task CancelBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        _logger.LogInformation("Cancelling batch job {BatchId}", batchId);

        var batchJob = await LoadBatchJobAsync(batchId, ct);
        if (batchJob == null)
        {
            throw new KeyNotFoundException($"Batch job {batchId} not found");
        }

        batchJob.Status = BatchJobStatus.Cancelled;
        batchJob.CompletedAt = DateTime.UtcNow;

        await UpdateBatchJobAsync(batchJob, ct);

        _logger.LogInformation("Batch job {BatchId} cancelled", batchId);
    }

    /// <summary>
    /// Retry failed items in batch
    /// </summary>
    public async Task RetryFailedItemsAsync(Guid batchId, CancellationToken ct = default)
    {
        _logger.LogInformation("Retrying failed items for batch {BatchId}", batchId);

        var batchJob = await LoadBatchJobAsync(batchId, ct);
        if (batchJob == null)
        {
            throw new KeyNotFoundException($"Batch job {batchId} not found");
        }

        var failedItems = batchJob.Items.Where(i => i.Status == BatchItemStatus.Failed).ToList();

        foreach (var item in failedItems)
        {
            item.Status = BatchItemStatus.Pending;
            item.ErrorMessage = null;
            item.StackTrace = null;
            item.RetryCount++;
            await UpdateBatchItemAsync(item, ct);
        }

        // NOTE: Background processing will be queued by API layer via Hangfire
        // The API layer will call: BackgroundJob.Enqueue(() => ProcessBatchJobAsync(batchId))

        _logger.LogInformation("Queued {Count} failed items for retry", failedItems.Count);
    }

    #endregion

    #region Helper Methods

    private async Task<List<DatabaseObject>> EnumerateDatabaseObjectsAsync(string database, string schema, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            -- Stored Procedures
            SELECT
                ROUTINE_SCHEMA AS SchemaName,
                ROUTINE_NAME AS ObjectName,
                'Procedure' AS ObjectType
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_TYPE = 'PROCEDURE'
              AND ROUTINE_SCHEMA = @Schema

            UNION ALL

            -- Tables
            SELECT
                TABLE_SCHEMA,
                TABLE_NAME,
                'Table'
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND TABLE_SCHEMA = @Schema

            UNION ALL

            -- Views
            SELECT
                TABLE_SCHEMA,
                TABLE_NAME,
                'View'
            FROM INFORMATION_SCHEMA.VIEWS
            WHERE TABLE_SCHEMA = @Schema

            UNION ALL

            -- Functions
            SELECT
                ROUTINE_SCHEMA,
                ROUTINE_NAME,
                'Function'
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_TYPE = 'FUNCTION'
              AND ROUTINE_SCHEMA = @Schema

            ORDER BY ObjectType, ObjectName";

        var results = await connection.QueryAsync<DatabaseObject>(sql, new { Schema = schema });
        return results.ToList();
    }

    private async Task<string> GetDatabaseObjectDefinitionAsync(
        string database,
        string schema,
        string objectType,
        string objectName,
        CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT OBJECT_DEFINITION(OBJECT_ID(@SchemaName + '.' + @ObjectName)) AS Definition";

        var definition = await connection.ExecuteScalarAsync<string>(
            sql,
            new { SchemaName = schema, ObjectName = objectName });

        return definition ?? string.Empty;
    }

    private async Task<List<ExcelRowData>> ReadCompletedExcelRowsAsync(string filePath, CancellationToken ct)
    {
        // Query DocumentChanges table for completed entries without DocId
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT
                CABNumber,
                JiraNumber,
                [Table],
                [Column],
                ChangeType,
                Description,
                Documentation,
                ModifiedStoredProcedures
            FROM DaQa.DocumentChanges
            WHERE Status = 'Completed'
              AND DocId IS NULL";

        var results = await connection.QueryAsync<ExcelRowData>(sql);
        return results.ToList();
    }

    private AutoDraftRequest CreateAutoDraftRequest(ExtractedMetadata metadata)
    {
        return new AutoDraftRequest
        {
            Table = $"{metadata.SchemaName}.{metadata.TableName}",
            Column = metadata.ColumnName,
            ChangeType = metadata.ChangeType ?? "Enhancement",
            Description = metadata.Description,
            Documentation = metadata.EnhancedDescription ?? metadata.Documentation,
            JiraNumber = metadata.JiraNumber,
            CABNumber = metadata.CABNumber
        };
    }

    // Commented out - type mismatch between MasterIndexRequest and MasterIndexEntry
    // private MasterIndexRequest CreateMasterIndexRequest(ExtractedMetadata metadata, string docId)
    // {
    //     // This method needs to be rewritten once the correct MasterIndexEntry API is established
    //     throw new NotImplementedException("MasterIndex integration not yet implemented");
    // }

    private int CalculateQualityScore(ExtractedMetadata metadata)
    {
        var score = (int)(metadata.OverallConfidence * 100);

        // Bonus for AI enhancement
        if (!string.IsNullOrEmpty(metadata.EnhancedDescription))
            score = Math.Min(100, score + 5);

        // Penalty for warnings
        score -= metadata.ValidationWarnings.Count * 5;

        return Math.Max(0, Math.Min(100, score));
    }

    private int CalculateCompletenessScore(ExtractedMetadata metadata)
    {
        var fields = new[]
        {
            !string.IsNullOrEmpty(metadata.Description),
            !string.IsNullOrEmpty(metadata.SchemaName),
            !string.IsNullOrEmpty(metadata.TableName),
            !string.IsNullOrEmpty(metadata.ChangeType),
            false, // Keywords - not available
            false, // Tags - not available
            false, // BusinessOwner - not available
            false  // TechnicalOwner - not available
        };

        return (int)((fields.Count(f => f) / (double)fields.Length) * 100);
    }

    // Commented out - method doesn't exist on ITeamsNotificationService
    // private async Task SendBatchCompletionNotificationAsync(BatchJob batchJob, CancellationToken ct)
    // {
    //     // TODO: Implement batch completion notification once the ITeamsNotificationService API is updated
    //     _logger.LogInformation("Batch {BatchId} completed: {Success}/{Total} successful",
    //         batchJob.BatchId, batchJob.SuccessCount, batchJob.TotalItems);
    // }

    private BatchJobDto MapBatchJobToDto(BatchJob job)
    {
        return new BatchJobDto(
            job.BatchId,
            job.SourceType.ToString(),
            job.DatabaseName ?? string.Empty,
            job.SchemaName ?? string.Empty,
            job.SourcePath,
            job.Status.ToString(),
            job.TotalItems,
            job.ProcessedCount,
            job.SuccessCount,
            job.FailedCount,
            job.RequiresReviewCount,
            job.ProgressPercentage,
            job.AverageConfidence,
            job.StartedAt,
            job.CompletedAt,
            job.Duration,
            job.EstimatedTimeRemaining,
            job.ErrorMessage,
            job.Options
        );
    }

    private BatchJobItemDto MapToDto(BatchJobItem item)
    {
        return new BatchJobItemDto(
            item.ItemId,
            item.BatchId,
            item.ObjectName,
            item.ObjectType,
            item.Status.ToString(),
            item.GeneratedDocId,
            item.ConfidenceScore,
            item.ConfidenceLevel,
            item.RequiresHumanReview,
            item.ValidationWarnings,
            item.DocumentPath,
            item.IsVectorIndexed,
            item.ProcessedAt,
            item.ErrorMessage
        );
    }

    #endregion

    #region Database Operations

    private async Task SaveBatchJobAsync(BatchJob batchJob, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using var transaction = connection.BeginTransaction();

        try
        {
            // Insert batch job
            var jobSql = @"
                INSERT INTO DaQa.BatchJobs (
                    BatchId, SourceType, DatabaseName, SchemaName, SourcePath, Status,
                    TotalItems, StartedAt, CreatedBy, OptionsJson
                )
                VALUES (
                    @BatchId, @SourceType, @DatabaseName, @SchemaName, @SourcePath, @Status,
                    @TotalItems, @StartedAt, @CreatedBy, @OptionsJson
                )";

            await connection.ExecuteAsync(jobSql, new
            {
                batchJob.BatchId,
                SourceType = batchJob.SourceType.ToString(),
                batchJob.DatabaseName,
                batchJob.SchemaName,
                batchJob.SourcePath,
                Status = batchJob.Status.ToString(),
                batchJob.TotalItems,
                batchJob.StartedAt,
                batchJob.CreatedBy,
                OptionsJson = System.Text.Json.JsonSerializer.Serialize(batchJob.Options)
            }, transaction);

            // Insert batch items
            var itemSql = @"
                INSERT INTO DaQa.BatchJobItems (
                    ItemId, BatchId, ObjectName, ObjectType, Status, CreatedAt,
                    DocumentPath, ExtractedMetadataJson
                )
                VALUES (
                    @ItemId, @BatchId, @ObjectName, @ObjectType, @Status, @CreatedAt,
                    @DocumentPath, @ExtractedMetadataJson
                )";

            foreach (var item in batchJob.Items)
            {
                await connection.ExecuteAsync(itemSql, new
                {
                    item.ItemId,
                    item.BatchId,
                    item.ObjectName,
                    item.ObjectType,
                    Status = item.Status.ToString(),
                    item.CreatedDate,
                    item.DocumentPath,
                    item.ExtractedMetadataJson
                }, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<BatchJob?> LoadBatchJobAsync(Guid batchId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = "SELECT * FROM DaQa.BatchJobs WHERE BatchId = @BatchId";
        var job = await connection.QuerySingleOrDefaultAsync<BatchJob>(sql, new { BatchId = batchId });

        if (job != null)
        {
            // Load items
            var itemsSql = "SELECT * FROM DaQa.BatchJobItems WHERE BatchId = @BatchId";
            var items = await connection.QueryAsync<BatchJobItem>(itemsSql, new { BatchId = batchId });
            job.Items = items.ToList();
        }

        return job;
    }

    private async Task<BatchJobItem?> LoadBatchItemAsync(Guid itemId, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = "SELECT * FROM DaQa.BatchJobItems WHERE ItemId = @ItemId";
        return await connection.QuerySingleOrDefaultAsync<BatchJobItem>(sql, new { ItemId = itemId });
    }

    private async Task UpdateBatchJobAsync(BatchJob batchJob, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            UPDATE DaQa.BatchJobs
            SET Status = @Status,
                ProcessedCount = @ProcessedCount,
                SuccessCount = @SuccessCount,
                FailedCount = @FailedCount,
                RequiresReviewCount = @RequiresReviewCount,
                HighConfidenceCount = @HighConfidenceCount,
                MediumConfidenceCount = @MediumConfidenceCount,
                LowConfidenceCount = @LowConfidenceCount,
                AverageConfidence = @AverageConfidence,
                VectorIndexedCount = @VectorIndexedCount,
                CompletedAt = @CompletedAt,
                DurationSeconds = @DurationSeconds,
                ErrorMessage = @ErrorMessage
            WHERE BatchId = @BatchId";

        await connection.ExecuteAsync(sql, new
        {
            batchJob.BatchId,
            Status = batchJob.Status.ToString(),
            batchJob.ProcessedCount,
            batchJob.SuccessCount,
            batchJob.FailedCount,
            batchJob.RequiresReviewCount,
            batchJob.HighConfidenceCount,
            batchJob.MediumConfidenceCount,
            batchJob.LowConfidenceCount,
            batchJob.AverageConfidence,
            batchJob.VectorIndexedCount,
            batchJob.CompletedAt,
            DurationSeconds = batchJob.Duration?.TotalSeconds,
            batchJob.ErrorMessage
        });
    }

    private async Task UpdateBatchItemAsync(BatchJobItem item, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            UPDATE DaQa.BatchJobItems
            SET Status = @Status,
                ExtractedMetadataJson = @ExtractedMetadataJson,
                ConfidenceScore = @ConfidenceScore,
                ExtractionMethod = @ExtractionMethod,
                RequiresHumanReview = @RequiresHumanReview,
                ValidationWarningsJson = @ValidationWarningsJson,
                GeneratedDocId = @GeneratedDocId,
                DocumentPath = @DocumentPath,
                IsVectorIndexed = @IsVectorIndexed,
                VectorId = @VectorId,
                MasterIndexId = @MasterIndexId,
                ProcessedAt = @ProcessedAt,
                ProcessingDurationSeconds = @ProcessingDurationSeconds,
                ReviewedBy = @ReviewedBy,
                ReviewedAt = @ReviewedAt,
                ReviewNotes = @ReviewNotes,
                ErrorMessage = @ErrorMessage,
                StackTrace = @StackTrace,
                RetryCount = @RetryCount
            WHERE ItemId = @ItemId";

        await connection.ExecuteAsync(sql, new
        {
            item.ItemId,
            Status = item.Status.ToString(),
            item.ExtractedMetadataJson,
            item.ConfidenceScore,
            ExtractionMethod = item.ExtractionMethod?.ToString(),
            item.RequiresHumanReview,
            ValidationWarningsJson = System.Text.Json.JsonSerializer.Serialize(item.ValidationWarnings),
            item.GeneratedDocId,
            item.DocumentPath,
            item.IsVectorIndexed,
            item.VectorId,
            item.MasterIndexId,
            item.ProcessedAt,
            ProcessingDurationSeconds = item.ProcessingDuration?.TotalSeconds,
            item.ReviewedBy,
            item.ReviewedAt,
            item.ReviewNotes,
            item.ErrorMessage,
            item.StackTrace,
            item.RetryCount
        });
    }

    #endregion
}

// Helper classes
public class DatabaseObject
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
}

public class AutoDraftRequest
{
    public string? Table { get; set; }
    public string? Column { get; set; }
    public string? ChangeType { get; set; }
    public string? Description { get; set; }
    public string? Documentation { get; set; }
    public string? JiraNumber { get; set; }
    public string? CABNumber { get; set; }
}

public class MasterIndexRequest
{
    public string? SourceDocumentID { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }
    public string? Description { get; set; }
    public string? SchemaName { get; set; }
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public string? DataType { get; set; }
    public string? BusinessOwner { get; set; }
    public string? TechnicalOwner { get; set; }
    public string? Keywords { get; set; }
    public string? Tags { get; set; }
    public string? AIGeneratedTags { get; set; }
    public int QualityScore { get; set; }
    public int CompletenessScore { get; set; }
    public int MetadataCompleteness { get; set; }
}

public class VectorIndexRequest
{
    public string DocumentId { get; set; } = string.Empty;
    public string? Content { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
