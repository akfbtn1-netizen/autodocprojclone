using Enterprise.Documentation.Core.Application.DTOs.Search;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// Continuous learning service that improves search quality over time.
/// Batched updates every 100 interactions or daily.
/// </summary>
public interface IContinuousLearner
{
    /// <summary>
    /// Record a user interaction for learning.
    /// </summary>
    Task RecordInteractionAsync(
        LearningInteraction interaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trigger a learning update cycle.
    /// </summary>
    Task<LearningUpdateResult> TriggerLearningUpdateAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate category suggestions based on learned patterns.
    /// </summary>
    Task<List<CategorySuggestionDto>> GenerateCategorySuggestionsAsync(
        int maxSuggestions = 10,
        decimal minConfidence = 0.7m,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get learning analytics.
    /// </summary>
    Task<LearningAnalytics> GetAnalyticsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current interaction count since last update.
    /// </summary>
    Task<int> GetPendingInteractionCountAsync(CancellationToken cancellationToken = default);
}

public record LearningInteraction(
    Guid QueryId,
    string UserId,
    string InteractionType,
    string? DocumentId,
    Dictionary<string, object>? Data);

public record LearningUpdateResult(
    int InteractionsProcessed,
    int QueriesAnalyzed,
    int SuggestionsGenerated,
    TimeSpan Duration);

public record LearningAnalytics(
    int TotalQueries,
    int TotalInteractions,
    decimal AverageClickRank,
    decimal ClickThroughRate,
    Dictionary<string, int> QueryTypeDistribution,
    Dictionary<string, int> TopSearchTerms,
    DateTime LastUpdateTime);

public record CategorySuggestionDto(
    string DocumentId,
    string CurrentCategory,
    string SuggestedCategory,
    decimal Confidence,
    string Reasoning);
