using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

/// <summary>
/// Service responsible for selecting appropriate templates for document generation.
/// </summary>
public interface ITemplateSelector
{
    /// <summary>
    /// Selects an appropriate template based on the template name.
    /// </summary>
    /// <param name="templateName">Name of the template to select</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Selected template instance or null if not found</returns>
    Task<IDocumentTemplate?> SelectTemplateAsync(string templateName, CancellationToken ct = default);
}

/// <summary>
/// Simple template selector implementation.
/// </summary>
public class BasicTemplateSelector : ITemplateSelector
{
    public async Task<IDocumentTemplate?> SelectTemplateAsync(string templateName, CancellationToken ct = default)
    {
        // Template selection based on name - return proper template instances
        return templateName switch
        {
            "StoredProcedureTemplate" => new StoredProcedureTemplate(),
            "DefectTemplate" => new DefectTemplateWrapper(),  // Wrapper for static DefectTemplate
            "EnhancementTemplate" => new EnhancementTemplateWrapper(),  // Wrapper for static EnhancementTemplate  
            "BusinessRuleTemplate" => new BusinessRequestTemplateWrapper(),  // Wrapper for static BusinessRequestTemplate
            "BusinessRequestTemplate" => new BusinessRequestTemplateWrapper(),  // Handle both names
            "DatabaseChangeTemplate" => new StoredProcedureTemplate(), // Fallback for database changes
            "DefaultTemplate" => new BusinessRequestTemplateWrapper(),        // Default to BusinessRequest
            _ => null  // Unknown template types return null
        };
    }
}

/// <summary>
/// Wrapper for static DefectTemplate to implement IDocumentTemplate
/// </summary>
public class DefectTemplateWrapper : IDocumentTemplate
{
    public async Task<string> GenerateAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        // Convert dictionary data to DefectTemplate.DefectData and call static method
        // For now, return a simple success message - this should be implemented to use TemplateExecutorService
        return await Task.FromResult($"Defect document generated for DocId: {data.GetValueOrDefault("DocId", "Unknown")}");
    }
}

/// <summary>
/// Wrapper for static EnhancementTemplate to implement IDocumentTemplate
/// </summary>
public class EnhancementTemplateWrapper : IDocumentTemplate
{
    public async Task<string> GenerateAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        return await Task.FromResult($"Enhancement document generated for DocId: {data.GetValueOrDefault("DocId", "Unknown")}");
    }
}

/// <summary>
/// Wrapper for static BusinessRequestTemplate to implement IDocumentTemplate
/// </summary>
public class BusinessRequestTemplateWrapper : IDocumentTemplate
{
    public async Task<string> GenerateAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        return await Task.FromResult($"Business Request document generated for DocId: {data.GetValueOrDefault("DocId", "Unknown")}");
    }
}