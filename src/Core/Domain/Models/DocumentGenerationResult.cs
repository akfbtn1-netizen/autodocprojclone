namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Represents the result of a document generation operation.
/// </summary>
public class DocumentGenerationResult
{
    /// <summary>
    /// Indicates whether the generation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The path to the generated document if successful.
    /// </summary>
    public string? DocumentPath { get; set; }

    /// <summary>
    /// Error message if the generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken to generate the document.
    /// </summary>
    public TimeSpan GenerationTime { get; set; }
}
