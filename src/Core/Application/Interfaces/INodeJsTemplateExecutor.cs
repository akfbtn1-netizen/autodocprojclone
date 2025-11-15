using Enterprise.Documentation.Core.Domain.Models;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Service for executing Node.js-based template rendering
/// </summary>
public interface INodeJsTemplateExecutor
{
    /// <summary>
    /// Executes a template using Node.js and generates a document
    /// </summary>
    Task<DocumentGenerationResult> ExecuteTemplateAsync(
        string templateFileName,
        object templateData,
        string outputFileName,
        CancellationToken cancellationToken = default);
}
