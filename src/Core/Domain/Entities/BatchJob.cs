using System;
using System.Collections.Generic;
using System.Linq;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Represents a batch documentation processing job
/// Supports multiple source types: Database schema, Folder scan, Excel import
/// </summary>
public class BatchJob
{
    public Guid BatchId { get; set; }

    // Source Information
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public BatchSourceType SourceType { get; set; }
    public string? SourcePath { get; set; }  // For folder batches

    // Status Tracking
    public BatchJobStatus Status { get; set; }
    public int TotalItems { get; set; }
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }

    // Confidence & Quality Tracking
    public int HighConfidenceCount { get; set; }  // >= 0.85
    public int MediumConfidenceCount { get; set; }  // 0.70-0.84
    public int LowConfidenceCount { get; set; }  // < 0.70
    public int RequiresReviewCount { get; set; }
    public double AverageConfidence { get; set; }

    // Vector Indexing
    public int VectorIndexedCount { get; set; }
    public int VectorIndexFailedCount { get; set; }

    // Timing
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    // Error Tracking
    public string? ErrorMessage { get; set; }
    public int TotalRetries { get; set; }

    // Audit
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }

    // Configuration
    public BatchProcessingOptions Options { get; set; } = new();

    // Navigation
    public List<BatchJobItem> Items { get; set; } = new();

    // Computed Properties
    public double ProgressPercentage =>
        TotalItems > 0 ? (ProcessedCount * 100.0 / TotalItems) : 0;

    public TimeSpan? Duration =>
        CompletedAt.HasValue ? CompletedAt.Value - StartedAt :
        Status == BatchJobStatus.Running ? DateTime.UtcNow - StartedAt : null;

    public bool HasWarnings => RequiresReviewCount > 0 || FailedCount > 0;

    public string StatusSummary =>
        $"{SuccessCount} succeeded, {FailedCount} failed, {RequiresReviewCount} need review";
}

/// <summary>
/// Source type for batch processing
/// </summary>
public enum BatchSourceType
{
    DatabaseSchema = 1,  // Enumerate stored procedures, tables, views from INFORMATION_SCHEMA
    FolderScan = 2,      // Scan folder for existing .docx files (reverse engineering)
    ExcelImport = 3,     // Import from Excel spreadsheet rows
    ManualUpload = 4     // Manual file upload via API
}

/// <summary>
/// Batch job execution status
/// </summary>
public enum BatchJobStatus
{
    Pending = 0,
    Queued = 1,
    Running = 2,
    Paused = 3,
    Completed = 4,
    CompletedWithErrors = 5,
    Failed = 6,
    Cancelled = 7
}

/// <summary>
/// Configuration options for batch processing
/// </summary>
public class BatchProcessingOptions
{
    // Metadata Extraction
    public bool ExtractMetadata { get; set; } = true;
    public bool UseOpenAIEnhancement { get; set; } = true;
    public double ConfidenceThreshold { get; set; } = 0.85;
    public bool RequireHumanReviewBelowThreshold { get; set; } = true;

    // DocId Generation
    public bool GenerateDocId { get; set; } = true;
    public bool ValidateDocIdUniqueness { get; set; } = true;

    // File Operations
    public bool MoveToCorrectPath { get; set; } = true;
    public bool RenameFilesToDocId { get; set; } = true;
    public bool BackupOriginals { get; set; } = true;

    // MasterIndex
    public bool PopulateMasterIndex { get; set; } = true;
    public bool CalculateQualityScores { get; set; } = true;

    // Vector Indexing
    public bool GenerateEmbeddings { get; set; } = true;
    public bool EnableSemanticSearch { get; set; } = true;

    // Approval Workflow
    public bool QueueForApproval { get; set; } = false;  // Auto-approve batch docs by default
    public bool RequireApprovalForLowConfidence { get; set; } = true;

    // Performance
    public int MaxParallelTasks { get; set; } = 4;
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    // Notifications
    public bool SendNotifications { get; set; } = true;
    public bool NotifyOnCompletion { get; set; } = true;
    public bool NotifyOnErrors { get; set; } = true;
    public List<string> NotificationRecipients { get; set; } = new();
}
