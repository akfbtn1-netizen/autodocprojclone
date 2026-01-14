using System.Text.Json;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// Classifies search queries into 5 routing paths.
/// Uses heuristics first (fast), falls back to AI for complex queries.
/// </summary>
public partial class QueryClassificationService : IQueryClassifier
{
    private readonly AzureOpenAIClient? _openAiClient;
    private readonly QueryClassificationOptions _options;
    private readonly ILogger<QueryClassificationService> _logger;

    // Patterns for heuristic classification
    private static readonly Regex KeywordPattern = KeywordPatternRegex();
    private static readonly Regex RelationshipPattern = RelationshipPatternRegex();
    private static readonly Regex MetadataPattern = MetadataPatternRegex();
    private static readonly Regex AgenticPattern = AgenticPatternRegex();
    private static readonly Regex ObjectNamePattern = ObjectNamePatternRegex();

    public QueryClassificationService(
        IOptions<QueryClassificationOptions> options,
        ILogger<QueryClassificationService> logger,
        AzureOpenAIClient? openAiClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _openAiClient = openAiClient;
    }

    /// <inheritdoc />
    public async Task<QueryClassification> ClassifyQueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return QueryClassification.Create(
                RoutingPath.Keyword,
                QueryComplexity.Simple,
                0.5m,
                "Empty query defaults to keyword search");
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();

        // Try heuristic classification first (fast path)
        var heuristicResult = TryHeuristicClassification(normalizedQuery);
        if (heuristicResult.Confidence >= _options.HeuristicConfidenceThreshold)
        {
            _logger.LogDebug(
                "Query classified via heuristics: {Path} (confidence: {Confidence})",
                heuristicResult.Path, heuristicResult.Confidence);
            return heuristicResult;
        }

        // Fall back to AI classification for complex/ambiguous queries
        if (_openAiClient != null && _options.EnableAiClassification)
        {
            var aiResult = await ClassifyWithAiAsync(query, cancellationToken);
            if (aiResult != null)
            {
                _logger.LogDebug(
                    "Query classified via AI: {Path} (confidence: {Confidence})",
                    aiResult.PrimaryPath, aiResult.Confidence);
                return aiResult;
            }
        }

        // Default to semantic search for ambiguous queries
        return QueryClassification.Create(
            RoutingPath.Semantic,
            DetermineComplexity(normalizedQuery),
            0.6m,
            "Defaulting to semantic search for ambiguous query");
    }

    private QueryClassification TryHeuristicClassification(string query)
    {
        // 1. Check for explicit object names (keyword search)
        if (ObjectNamePattern.IsMatch(query) || IsExactObjectNameQuery(query))
        {
            return QueryClassification.Create(
                RoutingPath.Keyword,
                QueryComplexity.Simple,
                0.95m,
                "Query contains specific object name pattern");
        }

        // 2. Check for relationship queries
        if (RelationshipPattern.IsMatch(query))
        {
            return QueryClassification.Create(
                RoutingPath.Relationship,
                QueryComplexity.Medium,
                0.9m,
                "Query asks about relationships or lineage");
        }

        // 3. Check for metadata queries
        if (MetadataPattern.IsMatch(query))
        {
            return QueryClassification.Create(
                RoutingPath.Metadata,
                QueryComplexity.Simple,
                0.85m,
                "Query filters by metadata attributes");
        }

        // 4. Check for agentic/complex queries
        if (AgenticPattern.IsMatch(query))
        {
            return QueryClassification.Create(
                RoutingPath.Agentic,
                QueryComplexity.Complex,
                0.85m,
                "Query requires multi-step reasoning");
        }

        // 5. Check for pure keyword queries
        if (KeywordPattern.IsMatch(query) && query.Split(' ').Length <= 3)
        {
            return QueryClassification.Create(
                RoutingPath.Keyword,
                QueryComplexity.Simple,
                0.8m,
                "Short query suitable for keyword search");
        }

        // Not confident enough with heuristics
        return QueryClassification.Create(
            RoutingPath.Semantic,
            DetermineComplexity(query),
            0.5m,
            "Heuristics inconclusive");
    }

    private bool IsExactObjectNameQuery(string query)
    {
        // Patterns like "dbo.TableName", "schema.table.column", "sp_ProcedureName"
        var parts = query.Split(new[] { '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        return parts.Any(p =>
            p.StartsWith("sp_", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("fn_", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("vw_", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("tbl_", StringComparison.OrdinalIgnoreCase) ||
            p.StartsWith("usp_", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<QueryClassification?> ClassifyWithAiAsync(
        string query,
        CancellationToken cancellationToken)
    {
        if (_openAiClient == null) return null;

        try
        {
            var chatClient = _openAiClient.GetChatClient(_options.DeploymentName);

            var systemPrompt = """
                You are a query classifier for a database documentation search system.
                Classify the user query into one of these routing paths:

                1. KEYWORD - Exact object name lookups (e.g., "dbo.Customer", "sp_GetOrders")
                2. SEMANTIC - Natural language questions about purpose or function
                3. RELATIONSHIP - Questions about lineage, dependencies, impact
                4. METADATA - Filtering by attributes (PII, category, owner, tier)
                5. AGENTIC - Complex multi-step queries requiring reasoning

                Respond in JSON format:
                {"path": "KEYWORD|SEMANTIC|RELATIONSHIP|METADATA|AGENTIC", "confidence": 0.0-1.0, "reasoning": "brief explanation"}
                """;

            var response = await chatClient.CompleteChatAsync(
                new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(query)
                },
                new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 150,
                    Temperature = 0.1f
                },
                cancellationToken);

            var content = response.Value.Content[0].Text;
            var result = JsonSerializer.Deserialize<AiClassificationResult>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
            {
                var path = ParseRoutingPath(result.Path);
                return QueryClassification.Create(
                    path,
                    DetermineComplexity(query),
                    (decimal)result.Confidence,
                    result.Reasoning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI classification failed, using default");
        }

        return null;
    }

    private static RoutingPath ParseRoutingPath(string path) => path.ToUpperInvariant() switch
    {
        "KEYWORD" => RoutingPath.Keyword,
        "SEMANTIC" => RoutingPath.Semantic,
        "RELATIONSHIP" => RoutingPath.Relationship,
        "METADATA" => RoutingPath.Metadata,
        "AGENTIC" => RoutingPath.Agentic,
        _ => RoutingPath.Semantic
    };

    private static QueryComplexity DetermineComplexity(string query)
    {
        var wordCount = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return wordCount switch
        {
            <= 3 => QueryComplexity.Simple,
            <= 8 => QueryComplexity.Medium,
            _ => QueryComplexity.Complex
        };
    }

    // Regex patterns for heuristic classification
    [GeneratedRegex(@"^[\w]+\.[\w]+(\.[a-zA-Z]+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex KeywordPatternRegex();

    [GeneratedRegex(@"\b(depend|lineage|upstream|downstream|impact|flow|uses|used by|references|parent|child)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RelationshipPatternRegex();

    [GeneratedRegex(@"\b(pii|category|tier|owner|classification|tagged|security|sensitive|where|filter)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MetadataPatternRegex();

    [GeneratedRegex(@"\b(explain|analyze|compare|evaluate|why|how does|what if|trace all|find all|comprehensive)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AgenticPatternRegex();

    [GeneratedRegex(@"^(sp_|fn_|vw_|tbl_|usp_|dbo\.|[\w]+\.)[\w]+$", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectNamePatternRegex();

    private class AiClassificationResult
    {
        public string Path { get; set; } = "SEMANTIC";
        public double Confidence { get; set; } = 0.5;
        public string? Reasoning { get; set; }
    }
}

public class QueryClassificationOptions
{
    public const string SectionName = "QueryClassification";

    public bool EnableAiClassification { get; set; } = true;
    public string DeploymentName { get; set; } = "gpt-4o";
    public decimal HeuristicConfidenceThreshold { get; set; } = 0.75m;
}
