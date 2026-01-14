// =============================================================================
// Agent #5: Post-Approval Pipeline - Domain Models
// Models for metadata extraction, finalization, lineage, and pipeline results
// =============================================================================

namespace Enterprise.Documentation.Core.Application.Services.PostApproval;

#region Metadata Models

/// <summary>
/// Lightweight metadata extracted during draft phase (no embeddings).
/// Stored in DocumentApprovals.ExtractedMetadata as JSON.
/// </summary>
public class ExtractedMetadata
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string? ColumnName { get; set; }
    public string ObjectType { get; set; } = string.Empty;

    // Base properties (no AI)
    public string? Description { get; set; }
    public string? Purpose { get; set; }
    public List<ParameterInfo> Parameters { get; set; } = new();
    public List<TableAccessInfo> TablesAccessed { get; set; } = new();
    public List<string> ColumnsModified { get; set; } = new();

    // Change tracking
    public string? JiraNumber { get; set; }
    public string? CABNumber { get; set; }
    public string? ChangeDescription { get; set; }
    public string? BracketedCode { get; set; }

    // Technical analysis
    public int? ComplexityScore { get; set; }
    public string? ComplexityTier { get; set; }
    public bool HasDynamicSql { get; set; }
    public bool HasCursors { get; set; }
    public bool HasTransactions { get; set; }
    public bool HasErrorHandling { get; set; }

    // Extraction metadata
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public string? ContentHash { get; set; }
}

/// <summary>
/// Full metadata after approval with embeddings and AI classification.
/// Generated only after document approval to optimize costs.
/// </summary>
public class FinalizedMetadata : ExtractedMetadata
{
    // AI-generated at approval time
    public float[]? SemanticEmbedding { get; set; }
    public EnrichedClassification? Classification { get; set; }

    // Approval info
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
    public string? ApproverComments { get; set; }
    public int ApprovalId { get; set; }

    // Processing timestamps
    public DateTime? EmbeddingGeneratedAt { get; set; }
    public DateTime? ClassificationEnrichedAt { get; set; }
    public DateTime? MasterIndexPopulatedAt { get; set; }
    public DateTime? DocxStampedAt { get; set; }
    public DateTime? LineageExtractedAt { get; set; }

    // Cost tracking
    public int TokensUsed { get; set; }
    public decimal GenerationCostUSD { get; set; }
    public string? AIModel { get; set; }

    // MasterIndex link
    public int? MasterIndexId { get; set; }
}

/// <summary>
/// AI-enriched classification with business domain, PII detection, and compliance tags.
/// </summary>
public class EnrichedClassification
{
    public string BusinessDomain { get; set; } = string.Empty;
    public List<string> DomainTags { get; set; } = new();
    public string DataClassification { get; set; } = "Internal"; // Public, Internal, Confidential, Restricted
    public string SemanticCategory { get; set; } = string.Empty;
    public List<string> BusinessProcesses { get; set; } = new();
    public string? ComplianceCategory { get; set; } // HIPAA, PCI, SOX, None
    public bool ContainsPII { get; set; }
    public List<string> PIITypes { get; set; } = new();
    public decimal ConfidenceScore { get; set; }
    public string? ClassificationRationale { get; set; }
}

/// <summary>
/// Stored procedure parameter information.
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? Direction { get; set; } // IN, OUT, INOUT
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public bool IsNullable { get; set; }
}

/// <summary>
/// Table access information from procedure analysis.
/// </summary>
public class TableAccessInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string AccessType { get; set; } = string.Empty; // READ, WRITE, DELETE, MERGE
    public List<string> ColumnsAccessed { get; set; } = new();
}

#endregion

#region Shadow Metadata Models

/// <summary>
/// Shadow Metadata read from document custom properties.
/// Enables documents to track their own sync state.
/// </summary>
public class ShadowMetadata
{
    public string DocumentId { get; set; } = string.Empty;
    public string SyncStatus { get; set; } = "DRAFT";
    public string ContentHash { get; set; } = string.Empty;
    public string? SchemaHash { get; set; }
    public int? MasterIndexId { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime? LastSynced { get; set; }
    public int? TokensUsed { get; set; }
    public decimal? GenerationCostUSD { get; set; }
    public string? AIModel { get; set; }
}

/// <summary>
/// Document synchronization status.
/// </summary>
public enum SyncStatus
{
    Draft,      // New document, not yet synced
    Current,    // Document matches database schema
    Stale,      // Database has changed, document needs update
    Pending,    // Update in progress
    Conflict,   // Manual changes detected, requires review
    Orphaned    // Source object no longer exists
}

/// <summary>
/// Result of stamping Shadow Metadata to a document.
/// </summary>
public class StampingResult
{
    public bool Success { get; set; }
    public string DocumentPath { get; set; } = string.Empty;
    public int PropertiesStamped { get; set; }
    public DateTime StampedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> StampedProperties { get; set; } = new();
}

#endregion

#region Lineage Models

/// <summary>
/// Result of column lineage extraction from a stored procedure.
/// </summary>
public class LineageExtractionResult
{
    public bool Success { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public List<ColumnLineageEntry> ColumnLineages { get; set; } = new();
    public List<string> ParseErrors { get; set; } = new();
    public int LinesAnalyzed { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>
/// Single column-level lineage entry.
/// Tracks data flow from source to target columns.
/// </summary>
public class ColumnLineageEntry
{
    public string SourceSchema { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetSchema { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string TargetColumn { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty; // READ, INSERT, UPDATE, DELETE, MERGE
    public string? TransformationExpression { get; set; }
    public bool IsPII { get; set; }
    public string? PIIType { get; set; }
    public int RiskWeight { get; set; }
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
}

/// <summary>
/// Dependency relationship between database objects.
/// </summary>
public class LineageDependency
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string DependencyType { get; set; } = string.Empty;
    public int Depth { get; set; }
    public List<string> AffectedColumns { get; set; } = new();
}

/// <summary>
/// Impact analysis result for a column change.
/// </summary>
public class ImpactAnalysisResult
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public int TotalAffectedObjects { get; set; }
    public int AffectedProcedures { get; set; }
    public int AffectedViews { get; set; }
    public int AffectedFunctions { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = "LOW"; // LOW, MEDIUM, HIGH, CRITICAL
    public List<LineageDependency> Dependencies { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

#endregion

#region Pipeline Result Models

/// <summary>
/// Complete result of post-approval pipeline execution.
/// </summary>
public class PostApprovalResult
{
    public bool Success { get; set; }
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public List<PipelineStep> Steps { get; set; } = new();
    public FinalizedMetadata? FinalizedMetadata { get; set; }
    public int? MasterIndexId { get; set; }
    public long TotalDurationMs { get; set; }
    public string? ErrorMessage { get; set; }

    public void AddStep(string name, string status, long durationMs = 0, string? details = null)
    {
        Steps.Add(new PipelineStep
        {
            Name = name,
            Status = status,
            DurationMs = durationMs,
            Details = details,
            CompletedAt = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Individual step in the post-approval pipeline.
/// </summary>
public class PipelineStep
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Completed, Warning, Failed, Skipped
    public long DurationMs { get; set; }
    public string? Details { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Result of MasterIndex population.
/// </summary>
public class PopulationResult
{
    public bool Success { get; set; }
    public int? MasterIndexId { get; set; }
    public bool IsUpdate { get; set; }
    public int ColumnsPopulated { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion
