using System.Diagnostics;
using Dapper;
using Enterprise.Documentation.Core.Application.DTOs.Search;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// Main search orchestrator coordinating all 5 search paths.
/// Implements quality-first hybrid architecture with optional ColBERT reranking.
/// </summary>
public class SearchOrchestrator : ISearchOrchestrator
{
    private readonly IQueryClassifier _queryClassifier;
    private readonly IVectorSearchService _vectorSearch;
    private readonly IGraphSearchService _graphSearch;
    private readonly IColBertReranker? _reranker;
    private readonly IContinuousLearner _learner;
    private readonly string _connectionString;
    private readonly ILogger<SearchOrchestrator> _logger;

    public SearchOrchestrator(
        IQueryClassifier queryClassifier,
        IVectorSearchService vectorSearch,
        IGraphSearchService graphSearch,
        IContinuousLearner learner,
        IConfiguration configuration,
        ILogger<SearchOrchestrator> logger,
        IColBertReranker? reranker = null)
    {
        _queryClassifier = queryClassifier;
        _vectorSearch = vectorSearch;
        _graphSearch = graphSearch;
        _learner = learner;
        _reranker = reranker;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SearchResponse> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var stageTimings = new Dictionary<string, TimeSpan>();

        // Generate query ID for tracking
        var queryId = Guid.NewGuid();

        _logger.LogInformation("Search started: {QueryId} - {Query}", queryId, request.Query);

        try
        {
            // 1. Classify the query
            var classificationSw = Stopwatch.StartNew();
            var routingPath = request.Options?.ForceRoutingPath;
            string? classificationReason = null;

            if (routingPath == null)
            {
                var classification = await _queryClassifier.ClassifyQueryAsync(request.Query, cancellationToken);
                routingPath = classification.PrimaryPath;
                classificationReason = classification.Reasoning;
            }

            stageTimings["classification"] = classificationSw.Elapsed;

            // 2. Execute the appropriate search strategy
            var searchSw = Stopwatch.StartNew();
            var searchResults = await ExecuteSearchPathAsync(
                request,
                routingPath.Value,
                cancellationToken);
            stageTimings["search"] = searchSw.Elapsed;

            // 3. Optional ColBERT reranking
            if (request.Options?.EnableReranking == true && _reranker != null && searchResults.Count > 0)
            {
                var rerankSw = Stopwatch.StartNew();
                var reranked = await _reranker.RerankAsync(
                    request.Query,
                    searchResults,
                    request.Options?.MaxResults ?? 20,
                    cancellationToken);

                // Update scores from reranking
                var rerankedMap = reranked.ToDictionary(r => r.DocumentId);
                foreach (var result in searchResults)
                {
                    if (rerankedMap.TryGetValue(result.DocumentId, out var ranked))
                    {
                        // Update with fused score
                        searchResults = searchResults
                            .Select(r => r.DocumentId == result.DocumentId
                                ? r with { Score = RelevanceScore.Create(ranked.FusedScore, r.Score.SemanticScore, ranked.ColBertScore) }
                                : r)
                            .OrderByDescending(r => r.Score.FusedScore)
                            .ToList();
                    }
                }

                stageTimings["reranking"] = rerankSw.Elapsed;
            }

            // 4. Apply filters and limit results
            var filteredResults = ApplyFilters(searchResults, request.Options);
            var finalResults = filteredResults
                .Take(request.Options?.MaxResults ?? 20)
                .ToList();

            // 5. Generate follow-up suggestions
            var suggestionSw = Stopwatch.StartNew();
            var followUps = await GenerateFollowUpSuggestionsAsync(
                request.Query,
                finalResults,
                routingPath.Value,
                cancellationToken);
            stageTimings["suggestions"] = suggestionSw.Elapsed;

            // 6. Log the query for analytics
            await LogQueryAsync(queryId, request, routingPath.Value, finalResults.Count, cancellationToken);

            sw.Stop();

            var response = new SearchResponse(
                QueryId: queryId,
                OriginalQuery: request.Query,
                ExpandedQuery: null,
                RoutingPath: routingPath.Value,
                Results: finalResults,
                FollowUpSuggestions: followUps,
                Metadata: new SearchMetadata(
                    TotalCandidates: searchResults.Count,
                    FilteredResults: finalResults.Count,
                    ProcessingTime: sw.Elapsed,
                    StageTimings: stageTimings,
                    CacheHit: false,
                    ClassificationReason: classificationReason));

            _logger.LogInformation(
                "Search completed: {QueryId} - {ResultCount} results in {Elapsed}ms via {Path}",
                queryId, finalResults.Count, sw.ElapsedMilliseconds, routingPath);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", request.Query);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetSuggestionsAsync(
        string partialQuery,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partialQuery) || partialQuery.Length < 2)
        {
            return new List<string>();
        }

        await using var conn = new SqlConnection(_connectionString);

        // Get recent successful queries matching the prefix
        var suggestions = await conn.QueryAsync<string>(
            @"SELECT DISTINCT TOP (@MaxSuggestions) Query
              FROM DaQa.SearchQueries
              WHERE Query LIKE @Prefix + '%'
                AND ResultCount > 0
              ORDER BY ExecutedAt DESC",
            new { MaxSuggestions = maxSuggestions, Prefix = partialQuery });

        return suggestions.ToList();
    }

    /// <inheritdoc />
    public async Task<List<FollowUpSuggestion>> GetFollowUpSuggestionsAsync(
        Guid queryId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        var query = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT Query FROM DaQa.SearchQueries WHERE QueryId = @QueryId",
            new { QueryId = queryId });

        if (string.IsNullOrEmpty(query))
        {
            return new List<FollowUpSuggestion>();
        }

        // Generate new suggestions based on the original query
        return await GenerateFollowUpSuggestionsAsync(query, new List<SearchResultItem>(), RoutingPath.Semantic, cancellationToken);
    }

    private async Task<List<SearchResultItem>> ExecuteSearchPathAsync(
        SearchRequest request,
        RoutingPath path,
        CancellationToken cancellationToken)
    {
        return path switch
        {
            RoutingPath.Keyword => await ExecuteKeywordSearchAsync(request, cancellationToken),
            RoutingPath.Semantic => await ExecuteSemanticSearchAsync(request, cancellationToken),
            RoutingPath.Relationship => await ExecuteRelationshipSearchAsync(request, cancellationToken),
            RoutingPath.Metadata => await ExecuteMetadataSearchAsync(request, cancellationToken),
            RoutingPath.Agentic => await ExecuteAgenticSearchAsync(request, cancellationToken),
            _ => await ExecuteSemanticSearchAsync(request, cancellationToken)
        };
    }

    private async Task<List<SearchResultItem>> ExecuteKeywordSearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_connectionString);

        var results = await conn.QueryAsync<MasterIndexResult>(
            @"SELECT TOP 50
                CAST(IndexId AS VARCHAR(50)) AS DocumentId,
                ObjectType, TableName, ColumnName, LogicalName,
                SchemaName, DatabaseName, BusinessPurpose,
                Category, DataClassification
              FROM DaQa.MasterIndex
              WHERE TableName LIKE '%' + @Query + '%'
                 OR ColumnName LIKE '%' + @Query + '%'
                 OR LogicalName LIKE '%' + @Query + '%'
                 OR PhysicalName LIKE '%' + @Query + '%'
              ORDER BY
                CASE WHEN TableName = @Query OR ColumnName = @Query THEN 0 ELSE 1 END,
                TableName, ColumnName",
            new { request.Query });

        return results.Select((r, idx) => MapToSearchResultItem(r, 1.0m - (idx * 0.02m))).ToList();
    }

    private async Task<List<SearchResultItem>> ExecuteSemanticSearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        // Execute hybrid vector search
        var vectorResults = await _vectorSearch.SearchHybridAsync(
            request.Query,
            request.Options?.MaxResults ?? 20,
            BuildVectorFilters(request.Options),
            cancellationToken);

        // Fetch full metadata from SQL
        if (vectorResults.Count == 0)
        {
            return new List<SearchResultItem>();
        }

        var docIds = vectorResults.Select(r => r.DocumentId).ToList();

        await using var conn = new SqlConnection(_connectionString);
        var metadata = await conn.QueryAsync<MasterIndexResult>(
            @"SELECT
                CAST(IndexId AS VARCHAR(50)) AS DocumentId,
                ObjectType, TableName, ColumnName, LogicalName,
                SchemaName, DatabaseName, BusinessPurpose,
                Category, DataClassification
              FROM DaQa.MasterIndex
              WHERE CAST(IndexId AS VARCHAR(50)) IN @DocIds",
            new { DocIds = docIds });

        var metadataMap = metadata.ToDictionary(m => m.DocumentId);

        return vectorResults
            .Where(vr => metadataMap.ContainsKey(vr.DocumentId))
            .Select(vr =>
            {
                var m = metadataMap[vr.DocumentId];
                return MapToSearchResultItem(m, (decimal)vr.Score);
            })
            .ToList();
    }

    private async Task<List<SearchResultItem>> ExecuteRelationshipSearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        // Extract entity from query
        var entityId = ExtractEntityId(request.Query);

        if (string.IsNullOrEmpty(entityId))
        {
            // Fall back to semantic search to find the entity first
            return await ExecuteSemanticSearchAsync(request, cancellationToken);
        }

        // Determine if upstream or downstream
        var isUpstream = request.Query.Contains("depend", StringComparison.OrdinalIgnoreCase) ||
                        request.Query.Contains("upstream", StringComparison.OrdinalIgnoreCase);

        var graphResults = isUpstream
            ? await _graphSearch.FindDependenciesAsync(entityId, 3, cancellationToken)
            : await _graphSearch.FindDependentsAsync(entityId, 3, cancellationToken);

        // Include PII flows if requested
        if (request.Options?.IncludePiiFlows == true)
        {
            var piiFlows = await _graphSearch.TracePiiFlowAsync(entityId, cancellationToken);
            // Merge PII information into results
        }

        return graphResults.Select((r, idx) => new SearchResultItem(
            DocumentId: r.NodeId,
            ObjectType: r.NodeType,
            ObjectName: r.ObjectName,
            SchemaName: r.SchemaName,
            DatabaseName: r.DatabaseName,
            Description: null,
            BusinessPurpose: null,
            Category: null,
            DataClassification: null,
            Score: RelevanceScore.Create(1.0m - (idx * 0.05m), 0, 0),
            MatchedTerms: null,
            Lineage: new LineageInfo(
                r.Depth,
                0,
                r.ParentNodeId != null ? new List<string> { r.ParentNodeId } : null,
                null),
            PiiInfo: null)).ToList();
    }

    private async Task<List<SearchResultItem>> ExecuteMetadataSearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        // Parse metadata filters from query
        var filters = ParseMetadataFilters(request.Query);

        await using var conn = new SqlConnection(_connectionString);

        var sql = @"SELECT TOP 50
            CAST(IndexId AS VARCHAR(50)) AS DocumentId,
            ObjectType, TableName, ColumnName, LogicalName,
            SchemaName, DatabaseName, BusinessPurpose,
            Category, DataClassification
          FROM DaQa.MasterIndex
          WHERE 1=1";

        var parameters = new DynamicParameters();

        if (filters.TryGetValue("category", out var category))
        {
            sql += " AND Category = @Category";
            parameters.Add("Category", category);
        }

        if (filters.TryGetValue("database", out var database))
        {
            sql += " AND DatabaseName = @Database";
            parameters.Add("Database", database);
        }

        if (filters.TryGetValue("pii", out var pii) && pii.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            sql += " AND PIIIndicator = 1";
        }

        if (filters.TryGetValue("classification", out var classification))
        {
            sql += " AND DataClassification = @Classification";
            parameters.Add("Classification", classification);
        }

        sql += " ORDER BY TableName, ColumnName";

        var results = await conn.QueryAsync<MasterIndexResult>(sql, parameters);

        return results.Select((r, idx) => MapToSearchResultItem(r, 1.0m - (idx * 0.01m))).ToList();
    }

    private async Task<List<SearchResultItem>> ExecuteAgenticSearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        // Agentic search combines multiple paths
        // 1. First do semantic search to find relevant entities
        var semanticResults = await ExecuteSemanticSearchAsync(request, cancellationToken);

        if (semanticResults.Count == 0)
        {
            return semanticResults;
        }

        // 2. Expand with relationship information
        var topResult = semanticResults.First();
        var relationships = await _graphSearch.FindDependentsAsync(
            topResult.DocumentId, 2, cancellationToken);

        // 3. Combine results
        var combinedResults = new List<SearchResultItem>(semanticResults);

        foreach (var rel in relationships.Take(10))
        {
            if (!combinedResults.Any(r => r.DocumentId == rel.NodeId))
            {
                combinedResults.Add(new SearchResultItem(
                    DocumentId: rel.NodeId,
                    ObjectType: rel.NodeType,
                    ObjectName: rel.ObjectName,
                    SchemaName: rel.SchemaName,
                    DatabaseName: rel.DatabaseName,
                    Description: null,
                    BusinessPurpose: null,
                    Category: null,
                    DataClassification: null,
                    Score: RelevanceScore.Create(0.7m, 0, 0),
                    MatchedTerms: new List<string> { "related" },
                    Lineage: new LineageInfo(rel.Depth, 0, null, null),
                    PiiInfo: null));
            }
        }

        return combinedResults;
    }

    private static List<SearchResultItem> ApplyFilters(
        List<SearchResultItem> results,
        SearchOptions? options)
    {
        if (options == null) return results;

        var filtered = results.AsEnumerable();

        if (options.FilterDatabases?.Count > 0)
        {
            filtered = filtered.Where(r =>
                r.DatabaseName != null &&
                options.FilterDatabases.Contains(r.DatabaseName, StringComparer.OrdinalIgnoreCase));
        }

        if (options.FilterObjectTypes?.Count > 0)
        {
            filtered = filtered.Where(r =>
                r.ObjectType != null &&
                options.FilterObjectTypes.Contains(r.ObjectType, StringComparer.OrdinalIgnoreCase));
        }

        if (options.FilterCategories?.Count > 0)
        {
            filtered = filtered.Where(r =>
                r.Category != null &&
                options.FilterCategories.Contains(r.Category, StringComparer.OrdinalIgnoreCase));
        }

        if (options.MinConfidence > 0)
        {
            filtered = filtered.Where(r => r.Score.FusedScore >= options.MinConfidence);
        }

        return filtered.ToList();
    }

    private async Task<List<FollowUpSuggestion>> GenerateFollowUpSuggestionsAsync(
        string query,
        List<SearchResultItem> results,
        RoutingPath path,
        CancellationToken cancellationToken)
    {
        var suggestions = new List<FollowUpSuggestion>();

        // Generate path-appropriate suggestions
        switch (path)
        {
            case RoutingPath.Keyword:
                if (results.Count > 0)
                {
                    suggestions.Add(new FollowUpSuggestion(
                        $"Show lineage for {results.First().ObjectName}",
                        "relationship",
                        0.8m,
                        "Explore dependencies"));
                }
                break;

            case RoutingPath.Semantic:
                suggestions.Add(new FollowUpSuggestion(
                    $"Filter results by PII columns",
                    "metadata",
                    0.7m,
                    "Narrow to sensitive data"));
                break;

            case RoutingPath.Relationship:
                suggestions.Add(new FollowUpSuggestion(
                    "Trace PII data flow",
                    "compliance",
                    0.85m,
                    "Compliance tracking"));
                break;
        }

        // Add generic suggestions
        if (results.Count > 10)
        {
            suggestions.Add(new FollowUpSuggestion(
                "Refine search with specific database",
                "filter",
                0.6m,
                "Reduce result set"));
        }

        return suggestions;
    }

    private async Task LogQueryAsync(
        Guid queryId,
        SearchRequest request,
        RoutingPath routingPath,
        int resultCount,
        CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_connectionString);

        await conn.ExecuteAsync(
            @"INSERT INTO DaQa.SearchQueries
              (QueryId, UserId, Query, RoutingPath, ResultCount, ExecutedAt)
              VALUES
              (@QueryId, @UserId, @Query, @RoutingPath, @ResultCount, GETUTCDATE())",
            new
            {
                QueryId = queryId,
                request.UserId,
                request.Query,
                RoutingPath = routingPath.ToString(),
                ResultCount = resultCount
            });
    }

    private static Dictionary<string, object>? BuildVectorFilters(SearchOptions? options)
    {
        if (options == null) return null;

        var filters = new Dictionary<string, object>();

        if (options.FilterDatabases?.Count == 1)
        {
            filters["database"] = options.FilterDatabases.First();
        }

        if (options.FilterObjectTypes?.Count == 1)
        {
            filters["object_type"] = options.FilterObjectTypes.First();
        }

        return filters.Count > 0 ? filters : null;
    }

    private static string? ExtractEntityId(string query)
    {
        // Simple extraction - look for patterns like "dbo.TableName"
        var parts = query.Split(' ');
        foreach (var part in parts)
        {
            if (part.Contains('.') && !part.StartsWith("http"))
            {
                return part;
            }
        }
        return null;
    }

    private static Dictionary<string, string> ParseMetadataFilters(string query)
    {
        var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Parse patterns like "category:claims" or "pii:true"
        var parts = query.Split(' ');
        foreach (var part in parts)
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex > 0 && colonIndex < part.Length - 1)
            {
                var key = part[..colonIndex];
                var value = part[(colonIndex + 1)..];
                filters[key] = value;
            }
        }

        return filters;
    }

    private static SearchResultItem MapToSearchResultItem(MasterIndexResult r, decimal score)
    {
        return new SearchResultItem(
            DocumentId: r.DocumentId,
            ObjectType: r.ObjectType,
            ObjectName: r.TableName ?? r.ColumnName ?? r.LogicalName ?? "Unknown",
            SchemaName: r.SchemaName,
            DatabaseName: r.DatabaseName,
            Description: null,
            BusinessPurpose: r.BusinessPurpose,
            Category: r.Category,
            DataClassification: r.DataClassification,
            Score: RelevanceScore.Create(score, score, 0),
            MatchedTerms: null,
            Lineage: null,
            PiiInfo: null);
    }

    private record MasterIndexResult(
        string DocumentId,
        string? ObjectType,
        string? TableName,
        string? ColumnName,
        string? LogicalName,
        string? SchemaName,
        string? DatabaseName,
        string? BusinessPurpose,
        string? Category,
        string? DataClassification);
}
