using Enterprise.Documentation.Core.Application.DTOs.Search;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// Main search orchestrator coordinating all 5 search paths.
/// Implements hybrid architecture with quality-first performance.
/// </summary>
public interface ISearchOrchestrator
{
    /// <summary>
    /// Execute a search query using the appropriate routing path.
    /// </summary>
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get search suggestions based on partial input.
    /// </summary>
    Task<List<string>> GetSuggestionsAsync(string partialQuery, int maxSuggestions = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get follow-up suggestions after a search.
    /// </summary>
    Task<List<FollowUpSuggestion>> GetFollowUpSuggestionsAsync(Guid queryId, CancellationToken cancellationToken = default);
}
