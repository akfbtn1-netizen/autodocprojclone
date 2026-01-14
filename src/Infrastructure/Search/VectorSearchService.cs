using System.Text.Json;
using Enterprise.Documentation.Core.Application.DTOs.Search;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// Vector search service implementation using Qdrant.
/// Supports dual-collection architecture for natural language and structured embeddings.
/// </summary>
public class VectorSearchService : IVectorSearchService
{
    private readonly QdrantClientWrapper _qdrantClient;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly QdrantOptions _options;
    private readonly ILogger<VectorSearchService> _logger;

    public VectorSearchService(
        QdrantClientWrapper qdrantClient,
        IEmbeddingGenerator embeddingGenerator,
        IOptions<QdrantOptions> options,
        ILogger<VectorSearchService> logger)
    {
        _qdrantClient = qdrantClient;
        _embeddingGenerator = embeddingGenerator;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<VectorSearchResult>> SearchNaturalLanguageAsync(
        string query,
        int topK = 20,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching natural language collection for: {Query}", query);

        var queryVector = await _embeddingGenerator.GenerateQueryEmbeddingAsync(query, cancellationToken);
        var results = await _qdrantClient.SearchAsync(
            _options.NaturalLanguageCollection,
            queryVector,
            topK,
            filters,
            cancellationToken);

        return results.Select(r => MapToVectorSearchResult(r, _options.NaturalLanguageCollection)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<VectorSearchResult>> SearchStructuredAsync(
        string query,
        int topK = 20,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching structured collection for: {Query}", query);

        var queryVector = await _embeddingGenerator.GenerateQueryEmbeddingAsync(query, cancellationToken);
        var results = await _qdrantClient.SearchAsync(
            _options.StructuredCollection,
            queryVector,
            topK,
            filters,
            cancellationToken);

        return results.Select(r => MapToVectorSearchResult(r, _options.StructuredCollection)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<VectorSearchResult>> SearchHybridAsync(
        string query,
        int topK = 20,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing hybrid search for: {Query}", query);

        // Search both collections in parallel
        var nlTask = SearchNaturalLanguageAsync(query, topK, filters, cancellationToken);
        var structuredTask = SearchStructuredAsync(query, topK, filters, cancellationToken);

        await Task.WhenAll(nlTask, structuredTask);

        var nlResults = await nlTask;
        var structuredResults = await structuredTask;

        // Use Reciprocal Rank Fusion (RRF) to combine results
        var fusedResults = FuseResultsRRF(nlResults, structuredResults, topK);

        _logger.LogDebug("Hybrid search returned {Count} fused results", fusedResults.Count);
        return fusedResults;
    }

    /// <inheritdoc />
    public async Task UpsertPointAsync(
        string collectionName,
        string pointId,
        float[] vector,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default)
    {
        await _qdrantClient.UpsertPointAsync(collectionName, pointId, vector, payload, cancellationToken);
        _logger.LogDebug("Upserted point {PointId} to collection {Collection}", pointId, collectionName);
    }

    /// <inheritdoc />
    public async Task DeletePointAsync(
        string collectionName,
        string pointId,
        CancellationToken cancellationToken = default)
    {
        await _qdrantClient.DeletePointAsync(collectionName, pointId, cancellationToken);
        _logger.LogDebug("Deleted point {PointId} from collection {Collection}", pointId, collectionName);
    }

    /// <inheritdoc />
    public async Task<CollectionStats> GetCollectionStatsAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, cancellationToken);
        if (info == null)
        {
            return new CollectionStats(0, 0, "not_found");
        }

        return new CollectionStats(info.PointsCount, info.VectorsCount, info.Status ?? "unknown");
    }

    /// <summary>
    /// Initialize collections on startup.
    /// </summary>
    public async Task InitializeCollectionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing Qdrant collections...");

        await _qdrantClient.EnsureCollectionExistsAsync(
            _options.NaturalLanguageCollection,
            _options.VectorSize,
            "Cosine",
            cancellationToken);

        await _qdrantClient.EnsureCollectionExistsAsync(
            _options.StructuredCollection,
            _options.VectorSize,
            "Cosine",
            cancellationToken);

        _logger.LogInformation("Qdrant collections initialized");
    }

    /// <summary>
    /// Reciprocal Rank Fusion to combine results from multiple searches.
    /// RRF(d) = Σ 1/(k + rank(d)) where k=60 is standard.
    /// </summary>
    private static List<VectorSearchResult> FuseResultsRRF(
        List<VectorSearchResult> list1,
        List<VectorSearchResult> list2,
        int topK,
        int k = 60)
    {
        var scores = new Dictionary<string, (float Score, VectorSearchResult Result)>();

        // Score from first list
        for (int i = 0; i < list1.Count; i++)
        {
            var result = list1[i];
            var rrfScore = 1.0f / (k + i + 1);

            if (scores.TryGetValue(result.DocumentId, out var existing))
            {
                scores[result.DocumentId] = (existing.Score + rrfScore, existing.Result);
            }
            else
            {
                scores[result.DocumentId] = (rrfScore, result);
            }
        }

        // Score from second list
        for (int i = 0; i < list2.Count; i++)
        {
            var result = list2[i];
            var rrfScore = 1.0f / (k + i + 1);

            if (scores.TryGetValue(result.DocumentId, out var existing))
            {
                scores[result.DocumentId] = (existing.Score + rrfScore, existing.Result);
            }
            else
            {
                scores[result.DocumentId] = (rrfScore, result);
            }
        }

        // Return top-k by fused score
        return scores
            .OrderByDescending(kvp => kvp.Value.Score)
            .Take(topK)
            .Select(kvp => kvp.Value.Result with { Score = kvp.Value.Score })
            .ToList();
    }

    private static VectorSearchResult MapToVectorSearchResult(QdrantSearchResult result, string collectionName)
    {
        var payload = new Dictionary<string, object>();

        if (result.Payload != null)
        {
            foreach (var (key, element) in result.Payload)
            {
                payload[key] = ConvertJsonElement(element);
            }
        }

        var docId = payload.TryGetValue("doc_id", out var id) ? id.ToString() ?? "" : result.Id ?? "";

        return new VectorSearchResult(
            DocumentId: docId,
            Score: result.Score,
            CollectionName: collectionName,
            PointId: result.Id ?? "",
            Payload: payload);
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.GetRawText()
        };
    }
}
