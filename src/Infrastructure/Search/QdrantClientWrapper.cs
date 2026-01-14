using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// HTTP client wrapper for Qdrant vector database.
/// Uses REST API for compatibility and simplicity.
/// </summary>
public class QdrantClientWrapper : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QdrantClientWrapper> _logger;
    private readonly QdrantOptions _options;
    private bool _disposed;

    public QdrantClientWrapper(
        HttpClient httpClient,
        IOptions<QdrantOptions> options,
        ILogger<QdrantClientWrapper> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.Endpoint);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
        }
    }

    /// <summary>
    /// Create a collection if it doesn't exist.
    /// </summary>
    public async Task EnsureCollectionExistsAsync(
        string collectionName,
        int vectorSize = 3072,
        string distance = "Cosine",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await CollectionExistsAsync(collectionName, cancellationToken);
            if (exists)
            {
                _logger.LogDebug("Collection {Collection} already exists", collectionName);
                return;
            }

            var request = new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = distance
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{collectionName}",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Created collection {Collection} with vector size {Size}", collectionName, vectorSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure collection {Collection} exists", collectionName);
            throw;
        }
    }

    /// <summary>
    /// Check if a collection exists.
    /// </summary>
    public async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/collections/{collectionName}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Upsert (insert or update) a point.
    /// </summary>
    public async Task UpsertPointAsync(
        string collectionName,
        string pointId,
        float[] vector,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            points = new[]
            {
                new
                {
                    id = pointId,
                    vector = vector,
                    payload = payload
                }
            }
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{collectionName}/points",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to upsert point {PointId} to {Collection}: {Error}", pointId, collectionName, error);
            throw new InvalidOperationException($"Qdrant upsert failed: {error}");
        }
    }

    /// <summary>
    /// Batch upsert multiple points.
    /// </summary>
    public async Task UpsertBatchAsync(
        string collectionName,
        List<QdrantPoint> points,
        CancellationToken cancellationToken = default)
    {
        if (points.Count == 0) return;

        var request = new
        {
            points = points.Select(p => new
            {
                id = p.Id,
                vector = p.Vector,
                payload = p.Payload
            }).ToArray()
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"/collections/{collectionName}/points",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to batch upsert {Count} points to {Collection}: {Error}", points.Count, collectionName, error);
            throw new InvalidOperationException($"Qdrant batch upsert failed: {error}");
        }

        _logger.LogDebug("Upserted {Count} points to {Collection}", points.Count, collectionName);
    }

    /// <summary>
    /// Delete a point by ID.
    /// </summary>
    public async Task DeletePointAsync(
        string collectionName,
        string pointId,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            points = new[] { pointId }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/delete",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to delete point {PointId} from {Collection}: {Error}", pointId, collectionName, error);
        }
    }

    /// <summary>
    /// Search for similar vectors.
    /// </summary>
    public async Task<List<QdrantSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 20,
        Dictionary<string, object>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var request = new Dictionary<string, object>
        {
            ["vector"] = queryVector,
            ["limit"] = topK,
            ["with_payload"] = true
        };

        if (filters != null && filters.Count > 0)
        {
            request["filter"] = BuildFilter(filters);
        }

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{collectionName}/points/search",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Search failed in {Collection}: {Error}", collectionName, error);
            throw new InvalidOperationException($"Qdrant search failed: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(cancellationToken);
        return result?.Result ?? new List<QdrantSearchResult>();
    }

    /// <summary>
    /// Get collection info.
    /// </summary>
    public async Task<QdrantCollectionInfo?> GetCollectionInfoAsync(
        string collectionName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/collections/{collectionName}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<QdrantCollectionResponse>(cancellationToken);
            return result?.Result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get collection info for {Collection}", collectionName);
            return null;
        }
    }

    private static object BuildFilter(Dictionary<string, object> filters)
    {
        var must = new List<object>();

        foreach (var (key, value) in filters)
        {
            must.Add(new
            {
                key = key,
                match = new { value = value }
            });
        }

        return new { must = must };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}

public class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Endpoint { get; set; } = "http://localhost:6333";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string NaturalLanguageCollection { get; set; } = "masterindex_nl";
    public string StructuredCollection { get; set; } = "masterindex_structured";
    public int VectorSize { get; set; } = 3072; // text-embedding-3-large
}

public record QdrantPoint(string Id, float[] Vector, Dictionary<string, object> Payload);

public class QdrantSearchResponse
{
    public List<QdrantSearchResult>? Result { get; set; }
}

public class QdrantSearchResult
{
    public string? Id { get; set; }
    public float Score { get; set; }
    public Dictionary<string, JsonElement>? Payload { get; set; }
}

public class QdrantCollectionResponse
{
    public QdrantCollectionInfo? Result { get; set; }
}

public class QdrantCollectionInfo
{
    public long PointsCount { get; set; }
    public long VectorsCount { get; set; }
    public string? Status { get; set; }
}
