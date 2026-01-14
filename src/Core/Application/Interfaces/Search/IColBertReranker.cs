using Enterprise.Documentation.Core.Application.DTOs.Search;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// ColBERTv2 late-interaction reranker service.
/// Provides high-precision keyword matching on top of semantic results.
/// </summary>
public interface IColBertReranker
{
    /// <summary>
    /// Rerank search results using ColBERT late-interaction.
    /// </summary>
    Task<List<RankedResult>> RerankAsync(
        string query,
        List<SearchResultItem> candidates,
        int topK = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Score a single document against a query.
    /// </summary>
    Task<decimal> ScoreAsync(
        string query,
        string documentText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the ColBERT service is healthy.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

public record RankedResult(
    string DocumentId,
    decimal ColBertScore,
    decimal OriginalScore,
    decimal FusedScore,
    int OriginalRank,
    int NewRank);
