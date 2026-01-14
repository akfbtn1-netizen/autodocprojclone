using Dapper;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// Continuous learning service that improves search quality over time.
/// Batched updates every 100 interactions or daily.
/// </summary>
public class ContinuousLearningService : IContinuousLearner
{
    private readonly string _connectionString;
    private readonly ILogger<ContinuousLearningService> _logger;
    private const int BatchThreshold = 100;

    public ContinuousLearningService(
        IConfiguration configuration,
        ILogger<ContinuousLearningService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordInteractionAsync(
        LearningInteraction interaction,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        await conn.ExecuteAsync(
            @"INSERT INTO DaQa.UserInteractions
              (QueryId, UserId, InteractionType, DocumentId, InteractionData, InteractionTime)
              VALUES
              (@QueryId, @UserId, @InteractionType, @DocumentId, @InteractionData, GETUTCDATE())",
            new
            {
                interaction.QueryId,
                interaction.UserId,
                interaction.InteractionType,
                interaction.DocumentId,
                InteractionData = interaction.Data != null
                    ? System.Text.Json.JsonSerializer.Serialize(interaction.Data)
                    : null
            });

        // Check if we should trigger a learning update
        var pendingCount = await GetPendingInteractionCountAsync(cancellationToken);
        if (pendingCount >= BatchThreshold)
        {
            _logger.LogInformation("Batch threshold reached ({Count}), triggering learning update", pendingCount);
            _ = TriggerLearningUpdateAsync(cancellationToken); // Fire and forget
        }
    }

    /// <inheritdoc />
    public async Task<LearningUpdateResult> TriggerLearningUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using var conn = new SqlConnection(_connectionString);

        // Get unprocessed interactions
        var interactions = await conn.QueryAsync<InteractionRecord>(
            @"SELECT QueryId, UserId, InteractionType, DocumentId, InteractionData
              FROM DaQa.UserInteractions
              WHERE IsProcessed = 0
              ORDER BY InteractionTime",
            cancellationToken);

        var interactionList = interactions.ToList();
        if (interactionList.Count == 0)
        {
            return new LearningUpdateResult(0, 0, 0, TimeSpan.Zero);
        }

        _logger.LogInformation("Processing {Count} interactions for learning", interactionList.Count);

        // Analyze click patterns to identify popular/unpopular results
        var clicksByDoc = interactionList
            .Where(i => i.InteractionType == "click")
            .GroupBy(i => i.DocumentId)
            .ToDictionary(g => g.Key ?? "", g => g.Count());

        // Analyze query patterns
        var queriesAnalyzed = interactionList
            .Select(i => i.QueryId)
            .Distinct()
            .Count();

        // Generate category suggestions from patterns
        var suggestionsGenerated = await GenerateAndStoreSuggestionsAsync(
            interactionList, conn, cancellationToken);

        // Mark interactions as processed
        var queryIds = interactionList.Select(i => i.QueryId).Distinct().ToList();
        if (queryIds.Count > 0)
        {
            await conn.ExecuteAsync(
                @"UPDATE DaQa.UserInteractions
                  SET IsProcessed = 1, ProcessedAt = GETUTCDATE()
                  WHERE QueryId IN @QueryIds",
                new { QueryIds = queryIds });
        }

        sw.Stop();

        _logger.LogInformation(
            "Learning update complete: {Interactions} interactions, {Queries} queries, {Suggestions} suggestions in {Elapsed}ms",
            interactionList.Count, queriesAnalyzed, suggestionsGenerated, sw.ElapsedMilliseconds);

        return new LearningUpdateResult(
            interactionList.Count,
            queriesAnalyzed,
            suggestionsGenerated,
            sw.Elapsed);
    }

    /// <inheritdoc />
    public async Task<List<CategorySuggestionDto>> GenerateCategorySuggestionsAsync(
        int maxSuggestions = 10,
        decimal minConfidence = 0.7m,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        var suggestions = await conn.QueryAsync<CategorySuggestionDto>(
            @"SELECT TOP (@Max)
                DocumentId,
                CurrentCategory,
                SuggestedCategory,
                Confidence,
                Reasoning
              FROM DaQa.CategorySuggestions
              WHERE IsApproved IS NULL
                AND Confidence >= @MinConfidence
              ORDER BY Confidence DESC",
            new { Max = maxSuggestions, MinConfidence = minConfidence });

        return suggestions.ToList();
    }

    /// <inheritdoc />
    public async Task<LearningAnalytics> GetAnalyticsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        var sinceDate = since ?? DateTime.UtcNow.AddDays(-30);

        // Total queries
        var totalQueries = await conn.QuerySingleAsync<int>(
            @"SELECT COUNT(*) FROM DaQa.SearchQueries
              WHERE ExecutedAt >= @Since",
            new { Since = sinceDate });

        // Total interactions
        var totalInteractions = await conn.QuerySingleAsync<int>(
            @"SELECT COUNT(*) FROM DaQa.UserInteractions
              WHERE InteractionTime >= @Since",
            new { Since = sinceDate });

        // Average click rank
        var avgClickRank = await conn.QuerySingleOrDefaultAsync<decimal?>(
            @"SELECT AVG(CAST(JSON_VALUE(InteractionData, '$.rank') AS DECIMAL(10,2)))
              FROM DaQa.UserInteractions
              WHERE InteractionType = 'click'
                AND InteractionTime >= @Since
                AND JSON_VALUE(InteractionData, '$.rank') IS NOT NULL",
            new { Since = sinceDate }) ?? 0;

        // Click-through rate
        var clickCount = await conn.QuerySingleAsync<int>(
            @"SELECT COUNT(*) FROM DaQa.UserInteractions
              WHERE InteractionType = 'click' AND InteractionTime >= @Since",
            new { Since = sinceDate });

        var ctr = totalQueries > 0 ? (decimal)clickCount / totalQueries : 0;

        // Query type distribution
        var queryTypes = await conn.QueryAsync<(string Type, int Count)>(
            @"SELECT RoutingPath AS Type, COUNT(*) AS Count
              FROM DaQa.SearchQueries
              WHERE ExecutedAt >= @Since
              GROUP BY RoutingPath",
            new { Since = sinceDate });

        var queryTypeDistribution = queryTypes.ToDictionary(x => x.Type, x => x.Count);

        // Top search terms (simplified - extract first word)
        var topTerms = await conn.QueryAsync<(string Term, int Count)>(
            @"SELECT TOP 20
                LEFT(Query, CHARINDEX(' ', Query + ' ') - 1) AS Term,
                COUNT(*) AS Count
              FROM DaQa.SearchQueries
              WHERE ExecutedAt >= @Since
              GROUP BY LEFT(Query, CHARINDEX(' ', Query + ' ') - 1)
              ORDER BY COUNT(*) DESC",
            new { Since = sinceDate });

        var topSearchTerms = topTerms.ToDictionary(x => x.Term, x => x.Count);

        // Last update time
        var lastUpdate = await conn.QuerySingleOrDefaultAsync<DateTime?>(
            @"SELECT MAX(ProcessedAt)
              FROM DaQa.UserInteractions
              WHERE IsProcessed = 1");

        return new LearningAnalytics(
            totalQueries,
            totalInteractions,
            avgClickRank,
            ctr,
            queryTypeDistribution,
            topSearchTerms,
            lastUpdate ?? DateTime.UtcNow);
    }

    /// <inheritdoc />
    public async Task<int> GetPendingInteractionCountAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        return await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM DaQa.UserInteractions WHERE IsProcessed = 0");
    }

    private async Task<int> GenerateAndStoreSuggestionsAsync(
        List<InteractionRecord> interactions,
        SqlConnection conn,
        CancellationToken cancellationToken)
    {
        // Simple heuristic: if a document is frequently clicked for queries
        // mentioning a category it's not in, suggest a category change

        var clickPatterns = interactions
            .Where(i => i.InteractionType == "click" && i.DocumentId != null)
            .GroupBy(i => i.DocumentId!)
            .Where(g => g.Count() >= 3) // At least 3 clicks
            .ToList();

        var suggestionsCount = 0;

        foreach (var pattern in clickPatterns)
        {
            // For now, just log - real implementation would analyze query terms
            // and compare with document categories
            _logger.LogDebug(
                "Document {DocId} clicked {Count} times - potential for recategorization",
                pattern.Key, pattern.Count());
        }

        return suggestionsCount;
    }

    private record InteractionRecord(
        Guid QueryId,
        string UserId,
        string InteractionType,
        string? DocumentId,
        string? InteractionData);
}
