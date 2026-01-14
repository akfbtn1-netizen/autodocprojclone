using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.VectorIndexing;

/// <summary>
/// Vector indexing service implementation with OpenAI embeddings
/// Supports Pinecone and Weaviate vector databases
/// Enables semantic search and GraphRAG capabilities
/// </summary>
public class VectorIndexingService : IVectorIndexingService
{
    private readonly ILogger<VectorIndexingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _openAIApiKey;
    private readonly string _embeddingModel;
    private readonly string _vectorProvider;
    private readonly string _vectorApiKey;
    private readonly string _vectorEndpoint;
    private readonly string _vectorIndexName;
    private readonly int _embeddingDimensions;

    public VectorIndexingService(
        ILogger<VectorIndexingService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;

        // OpenAI configuration
        _openAIApiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
        _embeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";
        _embeddingDimensions = int.Parse(configuration["OpenAI:EmbeddingDimensions"] ?? "1536");

        // Vector database configuration
        _vectorProvider = configuration["VectorDB:Provider"] ?? "Pinecone"; // Pinecone or Weaviate
        _vectorApiKey = configuration["VectorDB:ApiKey"]
            ?? throw new InvalidOperationException("VectorDB:ApiKey not configured");
        _vectorEndpoint = configuration["VectorDB:Endpoint"]
            ?? throw new InvalidOperationException("VectorDB:Endpoint not configured");
        _vectorIndexName = configuration["VectorDB:IndexName"] ?? "documentation-index";

        // Configure HttpClient
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EnterpriseDocs/1.0");
    }

    #region Index Operations

    /// <summary>
    /// Index a document with vector embeddings
    /// </summary>
    public async Task<string> IndexDocumentAsync(VectorIndexRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Indexing document: {DocumentId}", request.DocumentId);

        try
        {
            // Step 1: Generate embeddings
            var embeddings = await GenerateEmbeddingsAsync(request.Content ?? string.Empty, ct);

            _logger.LogInformation("Generated embeddings: {Dimensions} dimensions", embeddings.Length);

            // Step 2: Upsert to vector database
            var vectorId = await UpsertToVectorDBAsync(
                request.DocumentId,
                embeddings,
                request.Metadata,
                ct);

            _logger.LogInformation("Document indexed successfully: VectorId={VectorId}", vectorId);

            return vectorId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId}", request.DocumentId);
            throw;
        }
    }

    /// <summary>
    /// Batch index multiple documents for efficiency
    /// </summary>
    public async Task<List<string>> IndexDocumentsBatchAsync(
        List<VectorIndexRequest> requests,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Batch indexing {Count} documents", requests.Count);

        var vectorIds = new List<string>();

        // Process in batches of 100 (OpenAI limit)
        const int batchSize = 100;
        for (int i = 0; i < requests.Count; i += batchSize)
        {
            var batch = requests.Skip(i).Take(batchSize).ToList();

            // Generate embeddings for batch
            var contents = batch.Select(r => r.Content ?? string.Empty).ToList();
            var embeddingsList = await GenerateEmbeddingsBatchAsync(contents, ct);

            // Upsert to vector database
            for (int j = 0; j < batch.Count; j++)
            {
                var vectorId = await UpsertToVectorDBAsync(
                    batch[j].DocumentId,
                    embeddingsList[j],
                    batch[j].Metadata,
                    ct);

                vectorIds.Add(vectorId);
            }

            _logger.LogInformation("Processed batch {Current}/{Total}", i + batch.Count, requests.Count);
        }

        _logger.LogInformation("Batch indexing completed: {Count} documents", vectorIds.Count);

        return vectorIds;
    }

    /// <summary>
    /// Update document embeddings
    /// </summary>
    public async Task UpdateDocumentAsync(
        string vectorId,
        VectorIndexRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Updating document: {VectorId}", vectorId);

        // Generate new embeddings
        var embeddings = await GenerateEmbeddingsAsync(request.Content ?? string.Empty, ct);

        // Update in vector database
        await UpsertToVectorDBAsync(vectorId, embeddings, request.Metadata, ct);

        _logger.LogInformation("Document updated successfully: {VectorId}", vectorId);
    }

    /// <summary>
    /// Delete document from vector index
    /// </summary>
    public async Task DeleteDocumentAsync(string vectorId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting document: {VectorId}", vectorId);

        if (_vectorProvider == "Pinecone")
        {
            await DeleteFromPineconeAsync(vectorId, ct);
        }
        else if (_vectorProvider == "Weaviate")
        {
            await DeleteFromWeaviateAsync(vectorId, ct);
        }
        else
        {
            throw new NotSupportedException($"Vector provider {_vectorProvider} not supported");
        }

        _logger.LogInformation("Document deleted successfully: {VectorId}", vectorId);
    }

    #endregion

    #region Search Operations

    /// <summary>
    /// Semantic search using vector similarity
    /// </summary>
    public async Task<List<VectorSearchResult>> SemanticSearchAsync(
        string query,
        int topK = 10,
        Dictionary<string, object>? filters = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Semantic search: Query='{Query}', TopK={TopK}", query, topK);

        try
        {
            // Generate query embeddings
            var queryEmbeddings = await GenerateEmbeddingsAsync(query, ct);

            // Search vector database
            var results = await QueryVectorDBAsync(queryEmbeddings, topK, filters, ct);

            _logger.LogInformation("Semantic search completed: {Count} results", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic search failed for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Hybrid search combining keyword and semantic search
    /// Uses reciprocal rank fusion (RRF) for combining results
    /// </summary>
    public async Task<List<VectorSearchResult>> HybridSearchAsync(
        string query,
        int topK = 10,
        double semanticWeight = 0.7,
        Dictionary<string, object>? filters = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Hybrid search: Query='{Query}', TopK={TopK}, SemanticWeight={Weight}",
            query, topK, semanticWeight);

        try
        {
            // Perform semantic search
            var semanticResults = await SemanticSearchAsync(query, topK * 2, filters, ct);

            // Perform keyword search (using full-text search against MasterIndex)
            var keywordResults = await KeywordSearchAsync(query, topK * 2, filters, ct);

            // Combine using Reciprocal Rank Fusion (RRF)
            var combinedResults = CombineSearchResultsRRF(
                semanticResults,
                keywordResults,
                semanticWeight);

            var topResults = combinedResults.Take(topK).ToList();

            _logger.LogInformation("Hybrid search completed: {Count} results", topResults.Count);

            return topResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hybrid search failed for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Get vector index statistics
    /// </summary>
    public async Task<VectorIndexStats> GetIndexStatsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Getting vector index statistics");

        if (_vectorProvider == "Pinecone")
        {
            return await GetPineconeStatsAsync(ct);
        }
        else if (_vectorProvider == "Weaviate")
        {
            return await GetWeaviateStatsAsync(ct);
        }
        else
        {
            throw new NotSupportedException($"Vector provider {_vectorProvider} not supported");
        }
    }

    #endregion

    #region OpenAI Embeddings

    /// <summary>
    /// Generate embeddings using OpenAI API
    /// </summary>
    private async Task<float[]> GenerateEmbeddingsAsync(string text, CancellationToken ct)
    {
        var request = new
        {
            input = text,
            model = _embeddingModel
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        httpRequest.Headers.Add("Authorization", $"Bearer {_openAIApiKey}");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(ct);
        if (result?.Data == null || result.Data.Count == 0)
        {
            throw new InvalidOperationException("Failed to generate embeddings");
        }

        return result.Data[0].Embedding;
    }

    /// <summary>
    /// Generate embeddings for multiple texts in batch
    /// </summary>
    private async Task<List<float[]>> GenerateEmbeddingsBatchAsync(List<string> texts, CancellationToken ct)
    {
        var request = new
        {
            input = texts,
            model = _embeddingModel
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        httpRequest.Headers.Add("Authorization", $"Bearer {_openAIApiKey}");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(ct);
        if (result?.Data == null || result.Data.Count == 0)
        {
            throw new InvalidOperationException("Failed to generate embeddings");
        }

        return result.Data.Select(d => d.Embedding).ToList();
    }

    #endregion

    #region Pinecone Operations

    /// <summary>
    /// Upsert vector to Pinecone
    /// </summary>
    private async Task<string> UpsertToPineconeAsync(
        string id,
        float[] embeddings,
        Dictionary<string, object> metadata,
        CancellationToken ct)
    {
        var request = new
        {
            vectors = new[]
            {
                new
                {
                    id,
                    values = embeddings,
                    metadata
                }
            },
            @namespace = "default"
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_vectorEndpoint}/vectors/upsert");
        httpRequest.Headers.Add("Api-Key", _vectorApiKey);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        return id;
    }

    /// <summary>
    /// Query Pinecone for similar vectors
    /// </summary>
    private async Task<List<VectorSearchResult>> QueryPineconeAsync(
        float[] queryEmbeddings,
        int topK,
        Dictionary<string, object>? filters,
        CancellationToken ct)
    {
        var request = new
        {
            vector = queryEmbeddings,
            topK,
            filter = filters,
            includeMetadata = true,
            @namespace = "default"
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_vectorEndpoint}/query");
        httpRequest.Headers.Add("Api-Key", _vectorApiKey);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PineconeQueryResponse>(ct);
        if (result?.Matches == null)
        {
            return new List<VectorSearchResult>();
        }

        return result.Matches.Select(m => new VectorSearchResult
        {
            DocumentId = m.Id,
            SimilarityScore = m.Score,
            Metadata = m.Metadata ?? new Dictionary<string, object>()
        }).ToList();
    }

    /// <summary>
    /// Delete from Pinecone
    /// </summary>
    private async Task DeleteFromPineconeAsync(string id, CancellationToken ct)
    {
        var request = new
        {
            ids = new[] { id },
            @namespace = "default"
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_vectorEndpoint}/vectors/delete");
        httpRequest.Headers.Add("Api-Key", _vectorApiKey);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Get Pinecone statistics
    /// </summary>
    private async Task<VectorIndexStats> GetPineconeStatsAsync(CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_vectorEndpoint}/describe_index_stats");
        httpRequest.Headers.Add("Api-Key", _vectorApiKey);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PineconeStatsResponse>(ct);

        return new VectorIndexStats
        {
            TotalVectors = result?.TotalVectorCount ?? 0,
            Dimensions = result?.Dimension ?? _embeddingDimensions,
            IndexType = "Pinecone",
            LastUpdated = DateTime.UtcNow
        };
    }

    #endregion

    #region Weaviate Operations

    /// <summary>
    /// Upsert vector to Weaviate
    /// </summary>
    private async Task<string> UpsertToWeaviateAsync(
        string id,
        float[] embeddings,
        Dictionary<string, object> metadata,
        CancellationToken ct)
    {
        var request = new
        {
            @class = "Documentation",
            id,
            properties = metadata,
            vector = embeddings
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_vectorEndpoint}/v1/objects");
        httpRequest.Headers.Add("Authorization", $"Bearer {_vectorApiKey}");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        return id;
    }

    /// <summary>
    /// Query Weaviate for similar vectors
    /// </summary>
    private async Task<List<VectorSearchResult>> QueryWeaviateAsync(
        float[] queryEmbeddings,
        int topK,
        Dictionary<string, object>? filters,
        CancellationToken ct)
    {
        var graphqlQuery = $@"
        {{
            Get {{
                Documentation(
                    nearVector: {{
                        vector: [{string.Join(",", queryEmbeddings)}]
                    }}
                    limit: {topK}
                ) {{
                    _additional {{
                        id
                        distance
                    }}
                }}
            }}
        }}";

        var request = new { query = graphqlQuery };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_vectorEndpoint}/v1/graphql");
        httpRequest.Headers.Add("Authorization", $"Bearer {_vectorApiKey}");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<WeaviateQueryResponse>(ct);
        if (result?.Data?.Get?.Documentation == null)
        {
            return new List<VectorSearchResult>();
        }

        return result.Data.Get.Documentation.Select(d => new VectorSearchResult
        {
            DocumentId = d.Additional.Id,
            SimilarityScore = 1.0 - d.Additional.Distance, // Convert distance to similarity
            Metadata = new Dictionary<string, object>()
        }).ToList();
    }

    /// <summary>
    /// Delete from Weaviate
    /// </summary>
    private async Task DeleteFromWeaviateAsync(string id, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{_vectorEndpoint}/v1/objects/{id}");
        httpRequest.Headers.Add("Authorization", $"Bearer {_vectorApiKey}");

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Get Weaviate statistics
    /// </summary>
    private async Task<VectorIndexStats> GetWeaviateStatsAsync(CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_vectorEndpoint}/v1/schema");
        httpRequest.Headers.Add("Authorization", $"Bearer {_vectorApiKey}");

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        return new VectorIndexStats
        {
            TotalVectors = 0, // Would need additional query
            Dimensions = _embeddingDimensions,
            IndexType = "Weaviate",
            LastUpdated = DateTime.UtcNow
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Router for vector database operations
    /// </summary>
    private async Task<string> UpsertToVectorDBAsync(
        string id,
        float[] embeddings,
        Dictionary<string, object> metadata,
        CancellationToken ct)
    {
        if (_vectorProvider == "Pinecone")
        {
            return await UpsertToPineconeAsync(id, embeddings, metadata, ct);
        }
        else if (_vectorProvider == "Weaviate")
        {
            return await UpsertToWeaviateAsync(id, embeddings, metadata, ct);
        }
        else
        {
            throw new NotSupportedException($"Vector provider {_vectorProvider} not supported");
        }
    }

    /// <summary>
    /// Router for vector database queries
    /// </summary>
    private async Task<List<VectorSearchResult>> QueryVectorDBAsync(
        float[] queryEmbeddings,
        int topK,
        Dictionary<string, object>? filters,
        CancellationToken ct)
    {
        if (_vectorProvider == "Pinecone")
        {
            return await QueryPineconeAsync(queryEmbeddings, topK, filters, ct);
        }
        else if (_vectorProvider == "Weaviate")
        {
            return await QueryWeaviateAsync(queryEmbeddings, topK, filters, ct);
        }
        else
        {
            throw new NotSupportedException($"Vector provider {_vectorProvider} not supported");
        }
    }

    /// <summary>
    /// Keyword search using SQL full-text search
    /// </summary>
    private async Task<List<VectorSearchResult>> KeywordSearchAsync(
        string query,
        int topK,
        Dictionary<string, object>? filters,
        CancellationToken ct)
    {
        // This would query the MasterIndex full-text search
        // For now, return empty list (implement when integrating with MasterIndex)
        await Task.CompletedTask;
        return new List<VectorSearchResult>();
    }

    /// <summary>
    /// Combine search results using Reciprocal Rank Fusion (RRF)
    /// </summary>
    private List<VectorSearchResult> CombineSearchResultsRRF(
        List<VectorSearchResult> semanticResults,
        List<VectorSearchResult> keywordResults,
        double semanticWeight)
    {
        const int k = 60; // RRF constant

        var scores = new Dictionary<string, double>();

        // Calculate RRF scores for semantic results
        for (int i = 0; i < semanticResults.Count; i++)
        {
            var docId = semanticResults[i].DocumentId;
            var rrfScore = semanticWeight / (k + i + 1);

            if (!scores.ContainsKey(docId))
                scores[docId] = 0;

            scores[docId] += rrfScore;
        }

        // Calculate RRF scores for keyword results
        for (int i = 0; i < keywordResults.Count; i++)
        {
            var docId = keywordResults[i].DocumentId;
            var rrfScore = (1.0 - semanticWeight) / (k + i + 1);

            if (!scores.ContainsKey(docId))
                scores[docId] = 0;

            scores[docId] += rrfScore;
        }

        // Create combined results
        var allResults = semanticResults.Concat(keywordResults)
            .GroupBy(r => r.DocumentId)
            .Select(g => g.First())
            .ToDictionary(r => r.DocumentId);

        var combinedResults = scores
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new VectorSearchResult
            {
                DocumentId = kvp.Key,
                SimilarityScore = kvp.Value,
                Metadata = allResults.ContainsKey(kvp.Key) ? allResults[kvp.Key].Metadata : new Dictionary<string, object>(),
                Content = allResults.ContainsKey(kvp.Key) ? allResults[kvp.Key].Content : null
            })
            .ToList();

        return combinedResults;
    }

    #endregion

    #region Response Models

    private class OpenAIEmbeddingResponse
    {
        public List<EmbeddingData> Data { get; set; } = new();
    }

    private class EmbeddingData
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    private class PineconeQueryResponse
    {
        public List<PineconeMatch> Matches { get; set; } = new();
    }

    private class PineconeMatch
    {
        public string Id { get; set; } = string.Empty;
        public double Score { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    private class PineconeStatsResponse
    {
        public long TotalVectorCount { get; set; }
        public int Dimension { get; set; }
    }

    private class WeaviateQueryResponse
    {
        public WeaviateData? Data { get; set; }
    }

    private class WeaviateData
    {
        public WeaviateGet? Get { get; set; }
    }

    private class WeaviateGet
    {
        public List<WeaviateDocument>? Documentation { get; set; }
    }

    private class WeaviateDocument
    {
        public WeaviateAdditional Additional { get; set; } = new();
    }

    private class WeaviateAdditional
    {
        public string Id { get; set; } = string.Empty;
        public double Distance { get; set; }
    }

    #endregion
}
