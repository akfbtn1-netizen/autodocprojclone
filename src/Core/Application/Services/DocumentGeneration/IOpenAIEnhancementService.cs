using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

/// <summary>
/// Uses OpenAI to enhance documentation fields before draft creation
/// </summary>
public interface IOpenAIEnhancementService
{
    Task<EnhancedDocumentation> EnhanceDocumentationAsync(
        DocumentationEnhancementRequest request,
        CancellationToken cancellationToken = default);
}

public class DocumentationEnhancementRequest
{
    public required string ChangeType { get; set; }
    public required string Description { get; set; }          // Raw description from Excel
    public required string Documentation { get; set; }        // What was done (from Excel)
    public string? Table { get; set; }
    public string? Column { get; set; }
    public string? ModifiedStoredProcedures { get; set; }    // Comma-separated list
    public string? CABNumber { get; set; }
    public string? JiraNumber { get; set; }
}

public class EnhancedDocumentation
{
    public required string EnhancedDescription { get; set; }
    public required string EnhancedImplementation { get; set; }
    public List<string> KeyPoints { get; set; } = new();
    public List<string> TechnicalDetails { get; set; } = new();
}
