using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// Classifies search queries into 5 routing paths for optimal processing.
/// </summary>
public interface IQueryClassifier
{
    /// <summary>
    /// Classify a query and determine the optimal routing path.
    /// Uses heuristics first, falls back to AI for complex queries.
    /// </summary>
    Task<QueryClassification> ClassifyQueryAsync(string query, CancellationToken cancellationToken = default);
}
