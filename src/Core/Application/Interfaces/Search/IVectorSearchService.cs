using Enterprise.Documentation.Core.Application.DTOs.Search;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// Vector search service using Qdrant for semantic search.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Search using natural language embeddings.
    /// </summary>
    Task<List<VectorSearchResult>> SearchNaturalLanguageAsync(
        string query,
        int topK = 20,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search using structured embeddings.
    /// </summary>
    Task<List<VectorSearchResult>> SearchStructuredAsync(
        string query,
        int topK = 20,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hybrid search combining both embedding types.
    /// </summary>
    Task<List<VectorSearchResult>> SearchHybridAsync(
        string query,
        int topK = 20,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert a point into the vector database.
    /// </summary>
    Task UpsertPointAsync(
        string collectionName,
        string pointId,
        float[] vector,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a point from the vector database.
    /// </summary>
    Task DeletePointAsync(string collectionName, string pointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get collection statistics.
    /// </summary>
    Task<CollectionStats> GetCollectionStatsAsync(string collectionName, CancellationToken cancellationToken = default);
}

public record CollectionStats(long PointCount, long VectorCount, string Status);
