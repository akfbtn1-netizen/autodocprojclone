using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Complete MasterIndex entity representing the 119-column metadata catalog.
/// Maps to IRFS1.DaQa.MasterIndex table.
/// </summary>
public class MasterIndex
{
    // ===== PRIMARY KEY =====
    public int Id { get; set; }

    // ===== CORE TRACKING FIELDS =====
    public string JiraNumber { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string FullObjectPath { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string BusinessImpact { get; set; } = string.Empty;
    public string TechnicalComplexity { get; set; } = string.Empty;
    public string DataSensitivity { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentStatus { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public string BusinessOwner { get; set; } = string.Empty;
    public string TechnicalOwner { get; set; } = string.Empty;
    public string StakeholderGroup { get; set; } = string.Empty;
    public string BusinessJustification { get; set; } = string.Empty;
    public string UseCases { get; set; } = string.Empty;
    public string Dependencies { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Risks { get; set; } = string.Empty;
    public int? ParameterCount { get; set; }
    public string? ReturnType { get; set; }
    public string? ExecutionPlan { get; set; }
    public string? PerformanceNotes { get; set; }
    public string? SecurityConsiderations { get; set; }
    public string? ConfigurationSettings { get; set; }
    public string? EnvironmentNotes { get; set; }

    // ===== IDENTITY (10 columns) =====
    public int IndexId { get; set; }
    public string PhysicalName { get; set; } = string.Empty;
    public string? LogicalName { get; set; }
    public string SchemaName { get; set; } = "dbo";
    public string DatabaseName { get; set; } = "IRFS1";
    public string? ServerName { get; set; }
    public string ObjectType { get; set; } = string.Empty; // Table, View, StoredProcedure, Function
    public string? ParentObjectName { get; set; }
    public string? ColumnName { get; set; }
    public int? OrdinalPosition { get; set; }

    // ===== DATA TYPE (8 columns) =====
    public string? DataType { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsComputed { get; set; }

    // ===== CLASSIFICATION (12 columns) =====
    public string? SemanticCategory { get; set; } // AI-generated: Identifier, Measure, Dimension, Date, etc.
    public string? BusinessDomain { get; set; } // Claims, Policy, Billing, etc.
    public string? SubDomain { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Tags { get; set; } // Comma-separated
    public string? AIGeneratedTags { get; set; } // AI-suggested tags
    public string? Keywords { get; set; } // Search keywords
    public string? Synonyms { get; set; }
    public string? Abbreviations { get; set; }
    public string? RelatedTerms { get; set; }
    public string? GlossaryTermId { get; set; }

    // ===== DOCUMENTATION (15 columns) =====
    public string? Description { get; set; }
    public string? TechnicalSummary { get; set; }
    public string? BusinessPurpose { get; set; }
    public string? UsageNotes { get; set; }
    public string? ExampleValues { get; set; }
    public string? ValidValues { get; set; }
    public string? BusinessRules { get; set; }
    public string? Constraints { get; set; }
    public string? TransformationLogic { get; set; }
    public string? CalculationFormula { get; set; }
    public string? DataSource { get; set; }
    public string? SourceSystem { get; set; }
    public string? ETLProcess { get; set; }
    public string? RefreshFrequency { get; set; }
    public string? DataRetentionPolicy { get; set; }

    // ===== COMPLIANCE & SECURITY (12 columns) =====
    public bool PIIIndicator { get; set; }
    public string? PIITypes { get; set; } // SSN, DOB, Email, Phone, Address
    public string? DataClassification { get; set; } // Public, Internal, Confidential, Restricted
    public string? ComplianceTags { get; set; } // SOX, GLBA, StateInsurance
    public string? SecurityLevel { get; set; }
    public bool RequiresEncryption { get; set; }
    public bool RequiresMasking { get; set; }
    public string? MaskingRule { get; set; }
    public string? AccessRestrictions { get; set; }
    public string? DataOwner { get; set; }
    public string? DataSteward { get; set; }
    public string? ApprovalRequired { get; set; }

    // ===== DATA QUALITY (10 columns) =====
    public decimal? QualityScore { get; set; } // 0-100
    public decimal? CompletenessScore { get; set; }
    public decimal? AccuracyScore { get; set; }
    public decimal? ConsistencyScore { get; set; }
    public decimal? TimelinessScore { get; set; }
    public DateTime? LastValidated { get; set; }
    public string? ValidationRules { get; set; }
    public string? QualityIssues { get; set; }
    public string? DataProfile { get; set; } // JSON with statistics
    public int? NullPercentage { get; set; }

    // ===== LINEAGE & DEPENDENCIES (10 columns) =====
    public string? UpstreamDependencies { get; set; } // JSON array
    public string? DownstreamDependencies { get; set; } // JSON array
    public string? RelatedTables { get; set; }
    public string? ForeignKeyReferences { get; set; }
    public string? ReferencedBy { get; set; }
    public string? ImpactScore { get; set; } // LOW, MEDIUM, HIGH, CRITICAL
    public int? DirectDependentCount { get; set; }
    public int? CascadeDependentCount { get; set; }
    public bool HasDynamicSQL { get; set; }
    public string? LineageGraphId { get; set; }

    // ===== TECHNICAL DETAILS (10 columns) =====
    public string? Definition { get; set; } // SQL definition
    public int? LineCount { get; set; }
    public string? ComplexityLevel { get; set; } // Low, Medium, High, Critical
    public bool HasCursors { get; set; }
    public bool HasTempTables { get; set; }
    public bool HasTransactions { get; set; }
    public bool HasErrorHandling { get; set; }
    public string? IndexesUsed { get; set; }
    public string? ExecutionPlanNotes { get; set; }
    public string? OptimizationSuggestions { get; set; }

    // ===== DOCUMENT GENERATION (15 columns) =====
    public string? GeneratedDocPath { get; set; }
    public string? GeneratedDocId { get; set; } // DF-0001, EN-0002, etc.
    public DateTime? GeneratedDate { get; set; }
    public string? GeneratedBy { get; set; }
    public int? DocumentTier { get; set; } // 1, 2, or 3
    public string? TemplateUsed { get; set; }
    public string? ApprovalStatus { get; set; } // Draft, Pending, Approved, Rejected
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovalComments { get; set; }
    public string? RejectionReason { get; set; }
    public int? RegenerationCount { get; set; }
    public string? LastRegenerationFeedback { get; set; }
    public string? DocumentVersion { get; set; }
    public string? SharePointUrl { get; set; }

    // ===== AI METADATA (10 columns) =====
    public string? AIModel { get; set; } // gpt-4.1
    public int? TokensUsed { get; set; }
    public decimal? GenerationCostUSD { get; set; }
    public decimal? ConfidenceScore { get; set; } // 0.0-1.0
    public string? AIEnhancementStatus { get; set; }
    public DateTime? LastAIProcessed { get; set; }
    public string? AIGeneratedDescription { get; set; }
    public string? AIGeneratedSummary { get; set; }
    public string? AISuggestedCategory { get; set; }
    public string? AIProcessingNotes { get; set; }

    // ===== CHANGE TRACKING (7 columns) =====
    public string? JiraTicket { get; set; }
    public string? CABNumber { get; set; }
    public string? ChangeType { get; set; } // BR, EN, DF
    public string? ChangeDescription { get; set; }
    public DateTime? ChangeDate { get; set; }
    public string? ChangedBy { get; set; }
    public string? ChangeImpactAssessment { get; set; }

    // ===== AUDIT (5 columns) =====
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public bool IsActive { get; set; } = true;

    // === COMPUTED PROPERTIES ===
    public string ObjectPath => $"{DatabaseName}.{SchemaName}.{PhysicalName}";
    public bool IsOverdue => ApprovedDate == null && GeneratedDate.HasValue && 
                            GeneratedDate.Value.AddDays(7) < DateTime.UtcNow;
    public int DaysInReview => ApprovedDate.HasValue ? 
        (ApprovedDate.Value - (GeneratedDate ?? CreatedDate)).Days : 
        (DateTime.UtcNow - (GeneratedDate ?? CreatedDate)).Days;
}

/// <summary>
/// Schema metadata extraction result.
/// </summary>
public class SchemaMetadata
{
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string? Definition { get; set; }
    public List<TableReference>? ReferencedTables { get; set; }
    public List<ParameterInfo>? Parameters { get; set; }
    public bool HasDynamicSQL { get; set; }
    public bool HasCursors { get; set; }
    public bool HasExplicitTransactions { get; set; }
    public bool ContainsPII { get; set; }
    public string? DataClassification { get; set; }
    public string? BusinessDomain { get; set; }
    
    // Additional properties needed by SchemaMetadataService
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }
    public string? Description { get; set; }
    public List<string>? Dependencies { get; set; }
    public List<ColumnInfo>? Columns { get; set; }
    public List<string>? Indexes { get; set; }
    public List<string>? Permissions { get; set; }
}

/// <summary>
/// Excel change entry tracking entity
/// </summary>
public class ExcelChangeEntry
{
    public int Id { get; set; }
    public string JiraNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty; // BR, EN, DF, SP
    public string ChangeType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = "dbo";
    public string DatabaseName { get; set; } = "IRFS1";
    public string? ColumnName { get; set; }
    public int MasterIndexId { get; set; }
    public string? RequesterEmail { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Medium";
    public string? BusinessJustification { get; set; }
    public string? TechnicalNotes { get; set; }
    public string? TestingNotes { get; set; }
    public string? DeploymentNotes { get; set; }
    public string? RollbackPlan { get; set; }
    public string? EstimatedEffort { get; set; }
    public string? ActualEffort { get; set; }
    public string? AssignedTo { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public string? CompletedBy { get; set; }
}

/// <summary>
/// Document custom properties for DOCX metadata tracking
/// </summary>
public class DocumentCustomProperties
{
    public int? MasterIndexId { get; set; }
    public string? DocumentType { get; set; }
    public string? ObjectName { get; set; }
    public string? SchemaName { get; set; }
    public string? DatabaseName { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public string? AIModel { get; set; }
    public int? TokensUsed { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public int? Tier { get; set; }
    public string? SyncStatus { get; set; } // DRAFT, CURRENT, STALE, CONFLICT
    public string? ContentHash { get; set; }
    public DateTime? LastSync { get; set; }
    public bool? PIIIndicator { get; set; }
    public string? DataClassification { get; set; }
    public string? BusinessDomain { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
}

/// <summary>
/// Parameter information for stored procedures and functions
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsOutput { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Table reference tracking for dependencies
/// </summary>
public class TableReference
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // SELECT, INSERT, UPDATE, DELETE
    public string? Purpose { get; set; }
}

/// <summary>
/// AI response wrapper
/// </summary>
public class AIResponse
{
    public string Content { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public decimal? ConfidenceScore { get; set; }
}