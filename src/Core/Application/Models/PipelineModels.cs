namespace Enterprise.Documentation.Core.Application.Models;

/// <summary>
/// Analysis data for object complexity classification.
/// </summary>
public class ObjectAnalysis
{
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public int TablesAccessed { get; set; }
    public int ParameterCount { get; set; }
    public bool HasDynamicSQL { get; set; }
    public bool HasCursors { get; set; }
    public bool HasTransactions { get; set; }
    
    // Additional properties needed by TierClassifierService
    public int ColumnCount { get; set; }
    public string? BusinessCriticality { get; set; } = "MEDIUM";
    public string? DataClassification { get; set; } = "INTERNAL";
    public bool PIIIndicator { get; set; }
    public decimal ConfidenceScore { get; set; } = 0.80m;
}

/// <summary>
/// AI generation options for document enhancement.
/// </summary>
public class GenerationOptions
{
    public bool JsonMode { get; set; }
    public decimal Temperature { get; set; } = 0.3m;
    public int MaxTokens { get; set; } = 2000;
    public string Model { get; set; } = "gpt-4.1";
}

/// <summary>
/// Custom properties to embed in DOCX documents.
/// </summary>
public class DocumentCustomProperties
{
    public int MasterIndexId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string AIModel { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public decimal ConfidenceScore { get; set; }
    public int Tier { get; set; }
    public string SyncStatus { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public bool PIIIndicator { get; set; }
    public string? DataClassification { get; set; }
    public string? BusinessDomain { get; set; }
    
    // Added missing properties
    public DateTime LastSync { get; set; } = DateTime.UtcNow;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime? ApprovedDate { get; set; }
}

/// <summary>
/// Metadata associated with approval requests.
/// </summary>
public class ApprovalMetadata
{
    public int Tier { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string AIModel { get; set; } = string.Empty;
    public bool PIIDetected { get; set; }
    public int TablesAffected { get; set; }
}

/// <summary>
/// Configuration for template execution.
/// </summary>
public class TemplateConfig
{
    public string OutputPath { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public int Tier { get; set; }
}

/// <summary>
/// AI response for documentation generation.
/// </summary>
public class AIDocumentationResponse
{
    public dynamic Content { get; set; } = new { };
    public TokenUsage Tokens { get; set; } = new();
}

/// <summary>
/// Token usage information from AI service.
/// </summary>
public class TokenUsage
{
    public int Total { get; set; }
    public int Prompt { get; set; }
    public int Completion { get; set; }
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
}

/// <summary>
/// Table reference information.
/// </summary>
public class TableReference
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // SELECT, INSERT, UPDATE, DELETE
}

/// <summary>
/// Parameter information.
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsOutput { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Tier classification result.
/// </summary>
public class TierClassification
{
    public int Tier { get; set; }
    public string RecommendedModel { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Template information.
/// </summary>
public class TemplateInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Tier { get; set; }
}

/// <summary>
/// Template execution request.
/// </summary>
public class TemplateExecutionRequest
{
    public string TemplatePath { get; set; } = string.Empty;
    public object Data { get; set; } = new { };
    public object Metadata { get; set; } = new { };
    public TemplateConfig TemplateConfig { get; set; } = new();
    
    // Additional properties needed by DocGeneratorQueueProcessor
    public string TemplateType { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public object TemplateData { get; set; } = new { };
}

/// <summary>
/// Document generation result.
/// </summary>
public class DocumentGenerationResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public long ProcessingTimeMs { get; set; }
}