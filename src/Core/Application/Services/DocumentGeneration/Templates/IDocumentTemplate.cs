namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;

/// <summary>
/// Interface for document templates that can generate content from data.
/// </summary>
public interface IDocumentTemplate
{
    /// <summary>
    /// Generates document content using the provided template data.
    /// </summary>
    /// <param name="data">Template data dictionary</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated document content</returns>
    Task<string> GenerateAsync(Dictionary<string, object> data, CancellationToken ct = default);
}