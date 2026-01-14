// =============================================================================
// Agent #7: Gap Intelligence Agent - Semantic Clustering Service
// Uses Azure OpenAI embeddings for K-means clustering of database objects
// =============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure;
using Azure.AI.OpenAI;

namespace Enterprise.Documentation.Core.Application.Services.GapIntelligence;

/// <summary>
/// Semantic clustering service using Azure OpenAI embeddings.
/// Groups similar database objects to detect undocumented outliers in documented clusters.
/// </summary>
public class SemanticClusteringService : ISemanticClusteringService
{
    private readonly ILogger<SemanticClusteringService> _logger;
    private readonly OpenAIClient? _openAIClient;
    private readonly string _embeddingDeployment;

    public SemanticClusteringService(ILogger<SemanticClusteringService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-ada-002";

        var endpoint = configuration["AzureOpenAI:Endpoint"];
        var apiKey = configuration["AzureOpenAI:ApiKey"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            _openAIClient = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            _logger.LogInformation("SemanticClusteringService initialized with Azure OpenAI endpoint");
        }
        else
        {
            _logger.LogWarning("Azure OpenAI not configured - clustering will use fallback embeddings");
        }
    }

    /// <summary>
    /// Generate embedding for a database object based on its metadata
    /// </summary>
    public async Task<float[]> GenerateObjectEmbeddingAsync(string schema, string objectName, string? description = null, CancellationToken ct = default)
    {
        if (_openAIClient == null)
        {
            // Return fallback hash-based embedding when Azure OpenAI not configured
            return GenerateFallbackEmbedding($"{schema}.{objectName}");
        }

        try
        {
            var text = $"Database object: {schema}.{objectName}. {description ?? "No description available."}";

            var response = await _openAIClient.GetEmbeddingsAsync(new EmbeddingsOptions(_embeddingDeployment, new[] { text }), ct);
            return response.Value.Data[0].Embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for {Schema}.{Object}, using fallback", schema, objectName);
            return GenerateFallbackEmbedding($"{schema}.{objectName}");
        }
    }

    /// <summary>
    /// Cluster objects using K-means algorithm
    /// </summary>
    public async Task<List<SemanticCluster>> ClusterObjectsAsync(List<ObjectEmbedding> embeddings, int k = 10, CancellationToken ct = default)
    {
        if (embeddings.Count < k)
            k = Math.Max(1, embeddings.Count / 2);

        if (embeddings.Count == 0)
            return new List<SemanticCluster>();

        // Simple K-means implementation
        var clusters = new List<SemanticCluster>();
        var rng = new Random(42); // Fixed seed for reproducibility

        // Filter out embeddings with null vectors
        var validEmbeddings = embeddings.Where(e => e.Embedding != null && e.Embedding.Length > 0).ToList();
        if (validEmbeddings.Count == 0)
            return clusters;

        // Initialize centroids randomly
        var centroids = validEmbeddings.OrderBy(_ => rng.Next()).Take(k).Select(e => e.Embedding!).ToList();
        var assignments = new int[validEmbeddings.Count];

        // K-means iterations
        for (int iteration = 0; iteration < 10; iteration++)
        {
            // Assign each point to nearest centroid
            for (int i = 0; i < validEmbeddings.Count; i++)
            {
                if (validEmbeddings[i].Embedding == null) continue;
                assignments[i] = FindNearestCentroid(validEmbeddings[i].Embedding!, centroids);
            }

            // Update centroids
            for (int c = 0; c < k; c++)
            {
                var memberEmbeddings = validEmbeddings
                    .Where((_, i) => assignments[i] == c && validEmbeddings[i].Embedding != null)
                    .Select(e => e.Embedding!)
                    .ToList();

                if (memberEmbeddings.Any())
                {
                    centroids[c] = AverageVectors(memberEmbeddings);
                }
            }
        }

        // Build cluster objects
        for (int c = 0; c < k; c++)
        {
            var members = validEmbeddings
                .Select((e, i) => (Embedding: e, Index: i))
                .Where(x => assignments[x.Index] == c)
                .Select(x => new ClusterMember
                {
                    SchemaName = x.Embedding.SchemaName,
                    ObjectName = x.Embedding.ObjectName,
                    ObjectType = x.Embedding.ObjectType,
                    IsDocumented = x.Embedding.IsDocumented,
                    DistanceFromCentroid = x.Embedding.Embedding != null
                        ? (decimal)(1.0 - CosineSimilarity(x.Embedding.Embedding, centroids[c]))
                        : 0,
                    IsOutlier = false // Will be calculated after
                }).ToList();

            if (members.Any())
            {
                // Mark outliers (distance > mean + 2*stddev)
                var distances = members.Select(m => m.DistanceFromCentroid ?? 0).ToList();
                var mean = distances.Average();
                var stddev = Math.Sqrt(distances.Average(d => Math.Pow((double)(d - (decimal)mean), 2)));
                var threshold = (decimal)(mean + 2 * stddev);

                foreach (var member in members)
                {
                    member.IsOutlier = member.DistanceFromCentroid > threshold;
                }

                // Infer cluster name from common naming patterns
                var clusterName = InferClusterName(members) ?? $"Cluster_{c + 1}";

                clusters.Add(new SemanticCluster
                {
                    ClusterName = clusterName,
                    DomainTag = InferDomainTag(members),
                    MemberCount = members.Count,
                    DocumentedCount = members.Count(m => m.IsDocumented),
                    OutlierCount = members.Count(m => m.IsOutlier),
                    Members = members,
                    CentroidEmbedding = centroids[c]
                });
            }
        }

        _logger.LogInformation("Created {Count} clusters from {Objects} objects", clusters.Count, validEmbeddings.Count);
        return await Task.FromResult(clusters);
    }

    /// <summary>
    /// Find outliers in a specific cluster
    /// </summary>
    public async Task<List<ClusterMember>> FindOutliersAsync(int clusterId, CancellationToken ct = default)
    {
        // TODO [7]: Query ClusterMemberships table for outliers
        return await Task.FromResult(new List<ClusterMember>());
    }

    #region Private Helpers

    private int FindNearestCentroid(float[] embedding, List<float[]> centroids)
    {
        int nearest = 0;
        double maxSim = double.MinValue;
        for (int i = 0; i < centroids.Count; i++)
        {
            var sim = CosineSimilarity(embedding, centroids[i]);
            if (sim > maxSim)
            {
                maxSim = sim;
                nearest = i;
            }
        }
        return nearest;
    }

    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-10);
    }

    private float[] AverageVectors(List<float[]> vectors)
    {
        if (!vectors.Any()) return Array.Empty<float>();
        var len = vectors[0].Length;
        var result = new float[len];
        foreach (var v in vectors)
            for (int i = 0; i < len; i++) result[i] += v[i];
        for (int i = 0; i < len; i++) result[i] /= vectors.Count;
        return result;
    }

    private float[] GenerateFallbackEmbedding(string text)
    {
        // Simple hash-based embedding for when Azure OpenAI is not available
        var hash = text.GetHashCode();
        var rng = new Random(hash);
        var embedding = new float[64];
        for (int i = 0; i < 64; i++)
            embedding[i] = (float)(rng.NextDouble() * 2 - 1);
        return embedding;
    }

    private string? InferClusterName(List<ClusterMember> members)
    {
        if (!members.Any()) return null;

        // Find common prefix in object names
        var names = members.Select(m => m.ObjectName).ToList();
        var commonPrefix = GetCommonPrefix(names);

        if (!string.IsNullOrEmpty(commonPrefix) && commonPrefix.Length > 3)
            return $"{commonPrefix}* Objects";

        // Use most common schema
        var mostCommonSchema = members.GroupBy(m => m.SchemaName)
            .OrderByDescending(g => g.Count())
            .First().Key;

        return $"{mostCommonSchema} Objects";
    }

    private string? InferDomainTag(List<ClusterMember> members)
    {
        // Common domain patterns
        var patterns = new Dictionary<string, string[]>
        {
            { "Finance", new[] { "Account", "Ledger", "Invoice", "Payment", "GL", "AP", "AR" } },
            { "HR", new[] { "Employee", "Staff", "Payroll", "Leave", "Time" } },
            { "Inventory", new[] { "Stock", "Item", "Product", "Warehouse", "SKU" } },
            { "Sales", new[] { "Customer", "Order", "Quote", "Sale" } },
            { "Audit", new[] { "Log", "Audit", "History", "Track" } }
        };

        foreach (var (domain, keywords) in patterns)
        {
            var matches = members.Count(m => keywords.Any(k =>
                m.ObjectName.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (matches > members.Count / 2)
                return domain;
        }

        return null;
    }

    private string GetCommonPrefix(List<string> strings)
    {
        if (!strings.Any()) return string.Empty;

        var prefix = strings[0];
        foreach (var s in strings.Skip(1))
        {
            while (!s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && prefix.Length > 0)
                prefix = prefix[..^1];
        }
        return prefix;
    }

    #endregion
}
