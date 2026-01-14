using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enterprise.Documentation.Core.Application.Services.VectorIndexing;

/// <summary>
/// Vector indexing service for semantic search and GraphRAG
/// Supports embedding generation and vector database operations
/// </summary>
public interface IVectorIndexingService
{
    /// <summary>
    /// Index a document with vector embeddings for semantic search
    /// </summary>
    Task<string> IndexDocumentAsync(VectorIndexRequest request, CancellationToken ct = default);

    /// <summary>
    /// Batch index multiple documents
    /// </summary>
    Task<List<string>> IndexDocumentsBatchAsync(List<VectorIndexRequest> requests, CancellationToken ct = default);

    /// <summary>
    /// Semantic search for similar documents
    /// </summary>
    Task<List<VectorSearchResult>> SemanticSearchAsync(
        string query,
        int topK = 10,
        Dictionary<string, object>? filters = null,
        CancellationToken ct = default);

    /// <summary>
    /// Hybrid search combining keyword and semantic search
    /// </summary>
    Task<List<VectorSearchResult>> HybridSearchAsync(
        string query,
        int topK = 10,
        double semanticWeight = 0.7,
        Dictionary<string, object>? filters = null,
        CancellationToken ct = default);

    /// <summary>
    /// Delete document from vector index
    /// </summary>
    Task DeleteDocumentAsync(string vectorId, CancellationToken ct = default);

    /// <summary>
    /// Update document embeddings
    /// </summary>
    Task UpdateDocumentAsync(string vectorId, VectorIndexRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get vector statistics (total count, dimensions, etc.)
    /// </summary>
    Task<VectorIndexStats> GetIndexStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Request to index a document with vector embeddings
/// </summary>
public class VectorIndexRequest
{
    public string DocumentId { get; set; } = string.Empty;
    public string? Content { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Search result with similarity score
/// </summary>
public class VectorSearchResult
{
    public string DocumentId { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? Content { get; set; }
}

/// <summary>
/// Vector index statistics
/// </summary>
public class VectorIndexStats
{
    public long TotalVectors { get; set; }
    public int Dimensions { get; set; }
    public string IndexType { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
