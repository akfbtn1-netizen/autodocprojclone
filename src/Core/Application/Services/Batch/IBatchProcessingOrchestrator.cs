using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Application.Services.Batch;

/// <summary>
/// Orchestrates batch document processing from multiple sources
/// Integrates with existing AutoDraftService, MasterIndexService, etc.
/// </summary>
public interface IBatchProcessingOrchestrator
{
    /// <summary>
    /// Start batch processing for a database schema
    /// Enumerates all stored procedures, tables, views, functions
    /// </summary>
    Task<Guid> StartSchemaProcessingAsync(
        string database,
        string schema,
        Guid userId,
        BatchProcessingOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Start batch processing for a folder of existing .docx files
    /// Reverse engineers metadata from documents
    /// </summary>
    Task<Guid> StartFolderProcessingAsync(
        string folderPath,
        Guid userId,
        BatchProcessingOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Start batch processing from Excel spreadsheet rows
    /// Processes multiple completed rows in bulk
    /// </summary>
    Task<Guid> StartExcelImportAsync(
        string excelFilePath,
        Guid userId,
        BatchProcessingOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get batch job status with detailed progress
    /// </summary>
    Task<BatchJobDto> GetBatchStatusAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>
    /// Get all batch jobs with pagination
    /// </summary>
    Task<PaginatedResult<BatchJobDto>> GetAllBatchesAsync(
        int page = 1,
        int pageSize = 20,
        BatchJobStatus? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get items requiring human review
    /// </summary>
    Task<List<BatchJobItemDto>> GetItemsRequiringReviewAsync(
        Guid batchId,
        CancellationToken ct = default);

    /// <summary>
    /// Approve reviewed items for processing
    /// </summary>
    Task ApproveItemsAsync(
        List<Guid> itemIds,
        Guid reviewedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Reject items with feedback
    /// </summary>
    Task RejectItemsAsync(
        List<Guid> itemIds,
        string reason,
        Guid reviewedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Cancel running batch
    /// </summary>
    Task CancelBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>
    /// Retry failed items in batch
    /// </summary>
    Task RetryFailedItemsAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>
    /// Process a batch job (called by background job processor)
    /// </summary>
    Task ProcessBatchJobAsync(Guid batchId, CancellationToken ct = default);
}

/// <summary>
/// Batch job DTO for API responses
/// </summary>
public record BatchJobDto(
    Guid BatchId,
    string SourceType,
    string DatabaseName,
    string SchemaName,
    string? SourcePath,
    string Status,
    int TotalItems,
    int ProcessedCount,
    int SuccessCount,
    int FailedCount,
    int RequiresReviewCount,
    double ProgressPercentage,
    double AverageConfidence,
    DateTime StartedAt,
    DateTime? CompletedAt,
    TimeSpan? Duration,
    TimeSpan? EstimatedTimeRemaining,
    string? ErrorMessage,
    BatchProcessingOptions Options
);

/// <summary>
/// Batch job item DTO for API responses
/// </summary>
public record BatchJobItemDto(
    Guid ItemId,
    Guid BatchId,
    string ObjectName,
    string? ObjectType,
    string Status,
    string? GeneratedDocId,
    double? ConfidenceScore,
    string ConfidenceLevel,
    bool RequiresHumanReview,
    List<string> ValidationWarnings,
    string? DocumentPath,
    bool IsVectorIndexed,
    DateTime? ProcessedAt,
    string? ErrorMessage
);

/// <summary>
/// Paginated result wrapper
/// </summary>
public record PaginatedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
