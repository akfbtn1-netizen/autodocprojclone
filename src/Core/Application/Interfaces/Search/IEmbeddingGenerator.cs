using Enterprise.Documentation.Core.Application.DTOs.Search;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// Dual-format embedding generator service.
/// Creates both natural language and structured JSON embeddings.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    /// Generate dual embeddings for a single MasterIndex entity.
    /// </summary>
    Task<DualEmbeddingResult> GenerateDualEmbeddingsAsync(
        MasterIndexEmbeddingInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch generate embeddings for multiple entities.
    /// </summary>
    Task<List<DualEmbeddingResult>> GenerateBatchAsync(
        List<MasterIndexEmbeddingInput> inputs,
        int batchSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embedding for a query string.
    /// </summary>
    Task<float[]> GenerateQueryEmbeddingAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark embeddings as stale for regeneration.
    /// </summary>
    Task MarkStaleAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh stale embeddings.
    /// </summary>
    Task<int> RefreshStaleEmbeddingsAsync(int batchSize = 100, CancellationToken cancellationToken = default);
}

public record MasterIndexEmbeddingInput(
    string DocId,
    string ObjectType,
    string ObjectName,
    string? SchemaName,
    string? DatabaseName,
    string? BusinessPurpose,
    string? TechnicalDescription,
    string? DataClassification,
    string? PiiType,
    string? Category,
    string? BusinessDomain,
    string? Tags,
    string? SemanticCategory,
    int DependencyCount,
    decimal QualityScore,
    DateTime? ModifiedDate,
    string? Owner,
    string? Steward);

public record DualEmbeddingResult(
    string DocumentId,
    string NaturalLanguageText,
    float[] NaturalLanguageVector,
    string NaturalLanguagePointId,
    string StructuredText,
    float[] StructuredVector,
    string StructuredPointId,
    bool WasCached);
