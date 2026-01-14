using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Application.Services;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Interface for document generation pipeline
/// </summary>
public interface IDocumentGenerationPipeline
{
    /// <summary>
    /// Processes a document generation request end-to-end
    /// </summary>
    Task<GenerationResult> GenerateDocumentAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates that a document generation request can be processed
    /// </summary>
    Task<bool> ValidateRequestAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default);
}