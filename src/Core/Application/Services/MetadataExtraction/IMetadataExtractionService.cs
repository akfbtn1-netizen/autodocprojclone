using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Application.Services.MetadataExtraction;

/// <summary>
/// Service for extracting metadata from various sources with confidence scoring
/// </summary>
public interface IMetadataExtractionService
{
    /// <summary>
    /// Extract metadata from database object definition
    /// Uses INFORMATION_SCHEMA + NER + OpenAI
    /// </summary>
    Task<ExtractedMetadata> ExtractFromDatabaseObjectAsync(
        string objectType,
        string schemaName,
        string objectName,
        string definition,
        CancellationToken ct = default);

    /// <summary>
    /// Extract metadata from existing .docx file (reverse engineering)
    /// Uses document structure parsing + NER + OpenAI
    /// </summary>
    Task<ExtractedMetadata> ExtractFromDocumentAsync(
        string filePath,
        CancellationToken ct = default);

    /// <summary>
    /// Extract metadata from Excel row data
    /// Uses Excel column mapping + validation
    /// </summary>
    Task<ExtractedMetadata> ExtractFromExcelRowAsync(
        ExcelRowData rowData,
        CancellationToken ct = default);

    /// <summary>
    /// Validate extracted metadata against database schema
    /// </summary>
    Task<ValidationResult> ValidateMetadataAsync(
        ExtractedMetadata metadata,
        CancellationToken ct = default);

    /// <summary>
    /// Enhance metadata using OpenAI
    /// </summary>
    Task<ExtractedMetadata> EnhanceWithAIAsync(
        ExtractedMetadata metadata,
        CancellationToken ct = default);
}

/// <summary>
/// Excel row data for metadata extraction
/// </summary>
public class ExcelRowData
{
    public string? Date { get; set; }
    public string? JiraNumber { get; set; }
    public string? CABNumber { get; set; }
    public string? SprintNumber { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Severity { get; set; }
    public string? Table { get; set; }
    public string? Column { get; set; }
    public string? ChangeType { get; set; }
    public string? Description { get; set; }
    public string? Documentation { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public string? ModifiedStoredProcedures { get; set; }
}

/// <summary>
/// Validation result for extracted metadata
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, string> SuggestedCorrections { get; set; } = new();
    public double ValidationScore { get; set; }
}
