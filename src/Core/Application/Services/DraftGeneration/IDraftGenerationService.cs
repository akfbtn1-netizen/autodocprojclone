using Enterprise.Documentation.Core.Application.Services.CodeExtraction;
using Enterprise.Documentation.Core.Application.Services.Quality;

namespace Enterprise.Documentation.Core.Application.Services.DraftGeneration;

/// <summary>
/// Service responsible for generating draft documentation from extracted data.
/// Step 5 of the DocumentationAutomation workflow.
/// </summary>
public interface IDraftGenerationService
{
    /// <summary>
    /// Generates a draft document based on change information, extracted code, and quality analysis.
    /// </summary>
    /// <param name="docId">The document identifier</param>
    /// <param name="changeEntry">Information about the database change</param>
    /// <param name="codeResult">Results from code extraction (may be null)</param>
    /// <param name="qualityResult">Results from code quality audit (may be null)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Draft generation result</returns>
    Task<DraftGenerationResult> GenerateDraftAsync(
        string docId,
        Enterprise.Documentation.Core.Domain.Models.DocumentChangeEntry changeEntry,
        CodeExtractionResult? codeResult,
        CodeQualityResult? qualityResult,
        CancellationToken ct = default);
}

/// <summary>
/// Information about a database change for documentation generation.
/// </summary>
public class ChangeData
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

/// <summary>
/// Result of draft document generation.
/// </summary>
public class DraftGenerationResult
{
    public bool Success { get; set; }
    public string? DraftContent { get; set; }
    public string? TemplateUsed { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Metadata about the generation process
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}