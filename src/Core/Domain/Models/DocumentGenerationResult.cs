using Enterprise.Documentation.Core.Domain.Enums;

namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Result model for document generation
/// </summary>
public class DocumentGenerationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputFilePath { get; set; }
    public string? DocumentId { get; set; }
    public TemplateComplexity TemplateUsed { get; set; }
    public TimeSpan GenerationTime { get; set; }
}
