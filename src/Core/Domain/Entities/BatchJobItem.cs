using System;
using System.Collections.Generic;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Individual item in a batch processing job
/// Represents a single document/object to be processed
/// </summary>
public class BatchJobItem
{
    public Guid ItemId { get; set; }
    public Guid BatchId { get; set; }

    // Object Information
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string? ObjectType { get; set; }  // "StoredProcedure", "Table", "View", "Document", etc.
    public string? SourceFilePath { get; set; }  // For folder scans

    // Processing Status
    public BatchItemStatus Status { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public TimeSpan? ProcessingDuration { get; set; }

    // Generated Outputs
    public string? GeneratedDocId { get; set; }
    public string? DocumentPath { get; set; }
    public Guid? ApprovalId { get; set; }

    // Metadata Extraction
    public string? ExtractedMetadataJson { get; set; }  // Serialized ExtractedMetadata
    public double? ConfidenceScore { get; set; }
    public ExtractionMethod? ExtractionMethod { get; set; }

    // Validation
    public bool RequiresHumanReview { get; set; }
    public List<string> ValidationWarnings { get; set; } = new();
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Vector Indexing
    public bool IsVectorIndexed { get; set; }
    public string? VectorId { get; set; }
    public DateTime? VectorIndexedAt { get; set; }

    // MasterIndex
    public int? MasterIndexId { get; set; }
    public DateTime? IndexedAt { get; set; }

    // Error Handling
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastRetryAt { get; set; }

    // Audit
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }

    // Navigation
    public BatchJob Batch { get; set; } = null!;

    // Computed Properties
    public bool IsHighConfidence => ConfidenceScore.HasValue && ConfidenceScore.Value >= 0.85;
    public bool IsMediumConfidence => ConfidenceScore.HasValue && ConfidenceScore.Value >= 0.70 && ConfidenceScore.Value < 0.85;
    public bool IsLowConfidence => ConfidenceScore.HasValue && ConfidenceScore.Value < 0.70;

    public string ConfidenceLevel
    {
        get
        {
            if (!ConfidenceScore.HasValue) return "Unknown";
            if (ConfidenceScore.Value >= 0.85) return "High";
            if (ConfidenceScore.Value >= 0.70) return "Medium";
            return "Low";
        }
    }

    public bool CanAutoProcess => IsHighConfidence && !RequiresHumanReview && ValidationWarnings.Count == 0;
}

/// <summary>
/// Processing status for batch items
/// </summary>
public enum BatchItemStatus
{
    Pending = 0,
    MetadataExtraction = 1,
    ValidationRequired = 2,
    Validated = 3,
    Processing = 4,
    DocIdGenerated = 5,
    DocumentCreated = 6,
    VectorIndexing = 7,
    Completed = 8,
    Failed = 9,
    Skipped = 10,
    RequiresReview = 11,
    Approved = 12,
    Rejected = 13
}

/// <summary>
/// Method used to extract metadata
/// </summary>
public enum ExtractionMethod
{
    DatabaseSchema = 1,      // From INFORMATION_SCHEMA
    DocumentParsing = 2,     // From .docx structure
    OpenAIExtraction = 3,    // AI-based extraction
    Hybrid = 4,              // Combination of methods
    Manual = 5               // Human-entered
}

/// <summary>
/// Extracted metadata with confidence scores
/// </summary>
public class ExtractedMetadata
{
    // Core Fields (from Excel/Document)
    public string? TableName { get; set; }
    public string? SchemaName { get; set; }
    public string? ColumnName { get; set; }
    public string? ChangeType { get; set; }  // "Business Request", "Enhancement", "Defect Fix"
    public string? Description { get; set; }
    public string? Documentation { get; set; }
    public string? JiraNumber { get; set; }
    public string? CABNumber { get; set; }
    public List<string>? ModifiedProcedures { get; set; }

    // Additional Metadata
    public string? Priority { get; set; }
    public string? Severity { get; set; }
    public string? Sprint { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? DateEntered { get; set; }

    // Confidence Scores (0.0 - 1.0)
    public double OverallConfidence { get; set; }
    public Dictionary<string, double> FieldConfidences { get; set; } = new();

    // Extraction Details
    public ExtractionMethod Method { get; set; }
    public DateTime ExtractedAt { get; set; }
    public string? ExtractedBy { get; set; }

    // Validation
    public bool RequiresHumanReview => OverallConfidence < 0.85;
    public List<string> ValidationWarnings { get; set; } = new();
    public List<string> ValidationErrors { get; set; } = new();

    // Additional Context
    public Dictionary<string, object> AdditionalMetadata { get; set; } = new();

    // AI Enhancement
    public string? EnhancedDescription { get; set; }
    public string? EnhancedDocumentation { get; set; }
    public List<string>? AIGeneratedTags { get; set; }
    public string? SemanticCategory { get; set; }

    // Database Validation
    public bool TableExists { get; set; }
    public bool ColumnExists { get; set; }
    public List<string>? SuggestedCorrections { get; set; }
}
