using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Enterprise.AzureOpenAI.Rag;

/// <summary>
/// Production-ready RAG implementation with Azure AI Search and Azure OpenAI
/// Implements agentic retrieval pattern with hybrid search and reranking
/// </summary>
public class EnterpriseRagService : IEnterpriseRagService
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly SearchClient _searchClient;
    private readonly ILogger<EnterpriseRagService> _logger;
    private readonly RagOptions _options;

    public EnterpriseRagService(
        AzureOpenAIClient openAiClient,
        SearchClient searchClient,
        IOptions<RagOptions> options,
        ILogger<EnterpriseRagService> logger)
    {
        _openAiClient = openAiClient;
        _searchClient = searchClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Execute RAG query with agentic retrieval pattern
    /// </summary>
    public async Task<RagResponse> QueryAsync(
        string question,
        List<ConversationMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting RAG query: {Question}", question);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Step 1: Query Planning - Generate optimized search queries
        var searchQueries = await GenerateSearchQueriesAsync(
            question, 
            conversationHistory, 
            cancellationToken);
        
        _logger.LogDebug("Generated {Count} search queries", searchQueries.Count);

        // Step 2: Parallel Hybrid Search
        var searchTasks = searchQueries.Select(q => ExecuteHybridSearchAsync(q, cancellationToken));
        var allResults = await Task.WhenAll(searchTasks);
        var flatResults = allResults.SelectMany(r => r).ToList();

        _logger.LogDebug("Retrieved {Count} total documents", flatResults.Count);

        // Step 3: Deduplicate and Rerank
        var rankedDocs = DeduplicateAndRank(flatResults);
        var topDocs = rankedDocs.Take(_options.TopK).ToList();

        _logger.LogDebug("Selected top {Count} documents after reranking", topDocs.Count);

        // Step 4: Generate Grounded Response
        var response = await GenerateGroundedResponseAsync(
            question, 
            topDocs, 
            conversationHistory,
            cancellationToken);

        stopwatch.Stop();

        return new RagResponse
        {
            Answer = response.Answer,
            Citations = response.Citations,
            SearchQueries = searchQueries,
            RetrievedDocuments = topDocs.Count,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

    private async Task<List<string>> GenerateSearchQueriesAsync(
        string question,
        List<ConversationMessage>? history,
        CancellationToken cancellationToken)
    {
        var chatClient = _openAiClient.GetChatClient(_options.QueryPlannerDeployment);

        var systemPrompt = """
            You are a search query optimizer. Generate 2-4 optimized search queries.
            Output only a JSON array of strings: ["query 1", "query 2"]
            """;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage($"Generate search queries for: {question}")
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = 200
        };

        try
        {
            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var json = response.Value.Content[0].Text;
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string> { question };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate search queries, using original question");
            return new List<string> { question };
        }
    }

    private async Task<List<SearchResult>> ExecuteHybridSearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var embedding = await GetEmbeddingAsync(query, cancellationToken);

        var searchOptions = new SearchOptions
        {
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions
            {
                SemanticConfigurationName = _options.SemanticConfigName,
                QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive) { Count = 3 }
            },
            VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(embedding)
                    {
                        KNearestNeighborsCount = _options.VectorSearchK,
                        Fields = { _options.VectorFieldName }
                    }
                }
            },
            Size = _options.SearchPageSize,
            Select = { "id", "title", "content", "url", "lastModified" }
        };

        try
        {
            var response = await _searchClient.SearchAsync<SearchDocument>(
                query, searchOptions, cancellationToken);

            var results = new List<SearchResult>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                results.Add(new SearchResult
                {
                    Id = result.Document.GetString("id"),
                    Title = result.Document.GetString("title"),
                    Content = result.Document.GetString("content"),
                    Url = result.Document.GetString("url"),
                    Score = result.Score ?? 0,
                    SemanticScore = result.SemanticSearch?.RerankerScore ?? 0,
                    Captions = result.SemanticSearch?.Captions?
                        .Select(c => c.Text).ToList() ?? new List<string>()
                });
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hybrid search failed for query: {Query}", query);
            return new List<SearchResult>();
        }
    }

    private async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(
        string text, CancellationToken cancellationToken)
    {
        var embeddingClient = _openAiClient.GetEmbeddingClient(_options.EmbeddingDeployment);
        var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return response.Value.Vector;
    }

    private List<SearchResult> DeduplicateAndRank(List<SearchResult> results)
    {
        return results
            .GroupBy(r => r.Id)
            .Select(g => g.OrderByDescending(r => r.CombinedScore).First())
            .OrderByDescending(r => r.CombinedScore)
            .ToList();
    }

    private async Task<GroundedResponse> GenerateGroundedResponseAsync(
        string question,
        List<SearchResult> documents,
        List<ConversationMessage>? history,
        CancellationToken cancellationToken)
    {
        var chatClient = _openAiClient.GetChatClient(_options.ResponseDeployment);

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Documents for reference:");
        for (int i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            contextBuilder.AppendLine($"[{i + 1}] {doc.Title}");
            contextBuilder.AppendLine(doc.Content);
            contextBuilder.AppendLine();
        }

        var systemPrompt = $"""
            Answer questions using the provided documents. Cite sources using [1], [2], etc.
            If documents don't contain the answer, say so.
            
            {contextBuilder}
            """;

        var messages = new List<ChatMessage> { new SystemChatMessage(systemPrompt) };
        
        if (history?.Any() == true)
        {
            foreach (var msg in history.TakeLast(6))
            {
                messages.Add(msg.Role == "user" 
                    ? new UserChatMessage(msg.Content)
                    : new AssistantChatMessage(msg.Content));
            }
        }

        messages.Add(new UserChatMessage(question));

        var options = new ChatCompletionOptions
        {
            Temperature = 0.3f,
            MaxOutputTokenCount = _options.MaxResponseTokens
        };

        var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var answer = response.Value.Content[0].Text;

        return new GroundedResponse
        {
            Answer = answer,
            Citations = ExtractCitations(answer, documents)
        };
    }

    private List<Citation> ExtractCitations(string answer, List<SearchResult> documents)
    {
        var citations = new List<Citation>();
        var regex = new Regex(@"\[(\d+)\]");
        var matches = regex.Matches(answer);

        var citedIndexes = matches
            .Select(m => int.Parse(m.Groups[1].Value))
            .Distinct()
            .Where(i => i > 0 && i <= documents.Count);

        foreach (var idx in citedIndexes)
        {
            var doc = documents[idx - 1];
            citations.Add(new Citation
            {
                Index = idx,
                Title = doc.Title,
                Url = doc.Url,
                Excerpt = doc.Captions.FirstOrDefault() 
                    ?? doc.Content.Substring(0, Math.Min(200, doc.Content.Length))
            });
        }
        return citations;
    }
}

#region Configuration and Models

public class RagOptions
{
    public string QueryPlannerDeployment { get; set; } = "gpt-5-mini";
    public string ResponseDeployment { get; set; } = "gpt-5";
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-large";
    public string SemanticConfigName { get; set; } = "default";
    public string VectorFieldName { get; set; } = "contentVector";
    public int TopK { get; set; } = 5;
    public int VectorSearchK { get; set; } = 50;
    public int SearchPageSize { get; set; } = 20;
    public int MaxResponseTokens { get; set; } = 2000;
}

public class RagResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public List<string> SearchQueries { get; set; } = new();
    public int RetrievedDocuments { get; set; }
    public long DurationMs { get; set; }
}

public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public double Score { get; set; }
    public double SemanticScore { get; set; }
    public List<string> Captions { get; set; } = new();
    public double CombinedScore => (Score * 0.3) + (SemanticScore * 0.7);
}

public class Citation
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}

public class GroundedResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
}

public class ConversationMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public interface IEnterpriseRagService
{
    Task<RagResponse> QueryAsync(
        string question,
        List<ConversationMessage>? conversationHistory = null,
        CancellationToken cancellationToken = default);
}

#endregion
