using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Dapper;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// Generates dual-format embeddings (natural language + structured) for MasterIndex entities.
/// Uses Azure OpenAI text-embedding-3-large (3072 dimensions).
/// </summary>
public class DualEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly AzureOpenAIClient _openAiClient;
    private readonly string _connectionString;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<DualEmbeddingGenerator> _logger;

    public DualEmbeddingGenerator(
        AzureOpenAIClient openAiClient,
        IOptions<EmbeddingOptions> options,
        IConfiguration configuration,
        ILogger<DualEmbeddingGenerator> logger)
    {
        _openAiClient = openAiClient;
        _options = options.Value;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string required");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DualEmbeddingResult> GenerateDualEmbeddingsAsync(
        MasterIndexEmbeddingInput input,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cached = await GetCachedEmbeddingAsync(input.DocId, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for document {DocId}", input.DocId);
            return cached;
        }

        // Generate natural language text
        var nlText = GenerateNaturalLanguageText(input);
        var nlVector = await GenerateEmbeddingAsync(nlText, cancellationToken);

        // Generate structured text
        var structuredText = GenerateStructuredText(input);
        var structuredVector = await GenerateEmbeddingAsync(structuredText, cancellationToken);

        // Create point IDs
        var nlPointId = $"nl_{input.DocId}";
        var structuredPointId = $"struct_{input.DocId}";

        var result = new DualEmbeddingResult(
            DocumentId: input.DocId,
            NaturalLanguageText: nlText,
            NaturalLanguageVector: nlVector,
            NaturalLanguagePointId: nlPointId,
            StructuredText: structuredText,
            StructuredVector: structuredVector,
            StructuredPointId: structuredPointId,
            WasCached: false);

        // Cache the result
        await CacheEmbeddingAsync(result, cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<List<DualEmbeddingResult>> GenerateBatchAsync(
        List<MasterIndexEmbeddingInput> inputs,
        int batchSize = 20,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DualEmbeddingResult>();

        foreach (var batch in inputs.Chunk(batchSize))
        {
            var tasks = batch.Select(input => GenerateDualEmbeddingsAsync(input, cancellationToken));
            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults);

            _logger.LogDebug("Processed batch of {Count} embeddings", batch.Length);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<float[]> GenerateQueryEmbeddingAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return await GenerateEmbeddingAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkStaleAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        await conn.ExecuteAsync(
            @"UPDATE DaQa.EmbeddingCache
              SET IsStale = 1, LastModified = GETUTCDATE()
              WHERE DocumentId = @DocumentId",
            new { DocumentId = documentId });

        _logger.LogDebug("Marked embedding as stale for {DocId}", documentId);
    }

    /// <inheritdoc />
    public async Task<int> RefreshStaleEmbeddingsAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);

        // Get stale documents from MasterIndex
        var staleDocIds = await conn.QueryAsync<string>(
            @"SELECT TOP (@BatchSize) ec.DocumentId
              FROM DaQa.EmbeddingCache ec
              WHERE ec.IsStale = 1
              ORDER BY ec.LastModified",
            new { BatchSize = batchSize });

        var docIdList = staleDocIds.ToList();
        if (docIdList.Count == 0)
        {
            return 0;
        }

        // Get MasterIndex data for these documents
        var inputs = await conn.QueryAsync<MasterIndexEmbeddingInput>(
            @"SELECT
                CAST(IndexId AS VARCHAR(50)) AS DocId,
                ObjectType,
                COALESCE(TableName, ColumnName, LogicalName) AS ObjectName,
                SchemaName,
                DatabaseName,
                BusinessPurpose,
                TechnicalSummary AS TechnicalDescription,
                DataClassification,
                CASE WHEN PIIIndicator = 1 THEN 'PII' ELSE NULL END AS PiiType,
                Category,
                BusinessDomain,
                Tags,
                NULL AS SemanticCategory,
                0 AS DependencyCount,
                0 AS QualityScore,
                LastModifiedDate AS ModifiedDate,
                NULL AS Owner,
                NULL AS Steward
              FROM DaQa.MasterIndex
              WHERE CAST(IndexId AS VARCHAR(50)) IN @DocIds",
            new { DocIds = docIdList });

        // Regenerate embeddings
        var results = await GenerateBatchAsync(inputs.ToList(), batchSize, cancellationToken);

        _logger.LogInformation("Refreshed {Count} stale embeddings", results.Count);
        return results.Count;
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var embeddingClient = _openAiClient.GetEmbeddingClient(_options.DeploymentName);
            var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text of length {Length}", text.Length);
            throw;
        }
    }

    private static string GenerateNaturalLanguageText(MasterIndexEmbeddingInput input)
    {
        var sb = new StringBuilder();

        // Object identification
        sb.Append($"This is a {input.ObjectType?.ToLowerInvariant() ?? "database object"} ");
        sb.Append($"named {input.ObjectName}");

        if (!string.IsNullOrEmpty(input.SchemaName))
            sb.Append($" in schema {input.SchemaName}");

        if (!string.IsNullOrEmpty(input.DatabaseName))
            sb.Append($" in database {input.DatabaseName}");

        sb.Append(". ");

        // Business context
        if (!string.IsNullOrEmpty(input.BusinessPurpose))
            sb.Append($"{input.BusinessPurpose} ");

        if (!string.IsNullOrEmpty(input.TechnicalDescription))
            sb.Append($"{input.TechnicalDescription} ");

        // Classification
        if (!string.IsNullOrEmpty(input.Category))
            sb.Append($"It is categorized as {input.Category}. ");

        if (!string.IsNullOrEmpty(input.BusinessDomain))
            sb.Append($"It belongs to the {input.BusinessDomain} domain. ");

        // Sensitivity
        if (!string.IsNullOrEmpty(input.DataClassification))
            sb.Append($"Data classification: {input.DataClassification}. ");

        if (!string.IsNullOrEmpty(input.PiiType))
            sb.Append($"Contains PII of type: {input.PiiType}. ");

        // Dependencies
        if (input.DependencyCount > 0)
            sb.Append($"It has {input.DependencyCount} dependencies. ");

        // Tags
        if (!string.IsNullOrEmpty(input.Tags))
            sb.Append($"Tags: {input.Tags}. ");

        return sb.ToString().Trim();
    }

    private static string GenerateStructuredText(MasterIndexEmbeddingInput input)
    {
        var obj = new Dictionary<string, object?>
        {
            ["type"] = input.ObjectType,
            ["name"] = input.ObjectName,
            ["schema"] = input.SchemaName,
            ["database"] = input.DatabaseName,
            ["category"] = input.Category,
            ["domain"] = input.BusinessDomain,
            ["classification"] = input.DataClassification,
            ["pii"] = input.PiiType,
            ["tags"] = input.Tags?.Split(',').Select(t => t.Trim()).ToArray(),
            ["dependencies"] = input.DependencyCount,
            ["quality"] = input.QualityScore
        };

        // Remove null values
        var filtered = obj.Where(kvp => kvp.Value != null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = false });
    }

    private async Task<DualEmbeddingResult?> GetCachedEmbeddingAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_connectionString);

        var cached = await conn.QuerySingleOrDefaultAsync<EmbeddingCacheRecord>(
            @"SELECT DocumentId, NaturalLanguageText, NaturalLanguageVector,
                     StructuredText, StructuredVector
              FROM DaQa.EmbeddingCache
              WHERE DocumentId = @DocumentId AND IsStale = 0",
            new { DocumentId = documentId });

        if (cached == null) return null;

        return new DualEmbeddingResult(
            DocumentId: cached.DocumentId,
            NaturalLanguageText: cached.NaturalLanguageText,
            NaturalLanguageVector: DeserializeVector(cached.NaturalLanguageVector),
            NaturalLanguagePointId: $"nl_{cached.DocumentId}",
            StructuredText: cached.StructuredText,
            StructuredVector: DeserializeVector(cached.StructuredVector),
            StructuredPointId: $"struct_{cached.DocumentId}",
            WasCached: true);
    }

    private async Task CacheEmbeddingAsync(DualEmbeddingResult result, CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_connectionString);

        await conn.ExecuteAsync(
            @"MERGE DaQa.EmbeddingCache AS target
              USING (SELECT @DocumentId AS DocumentId) AS source
              ON target.DocumentId = source.DocumentId
              WHEN MATCHED THEN
                  UPDATE SET
                      NaturalLanguageText = @NaturalLanguageText,
                      NaturalLanguageVector = @NaturalLanguageVector,
                      StructuredText = @StructuredText,
                      StructuredVector = @StructuredVector,
                      IsStale = 0,
                      LastModified = GETUTCDATE()
              WHEN NOT MATCHED THEN
                  INSERT (DocumentId, NaturalLanguageText, NaturalLanguageVector,
                          StructuredText, StructuredVector, IsStale, LastModified)
                  VALUES (@DocumentId, @NaturalLanguageText, @NaturalLanguageVector,
                          @StructuredText, @StructuredVector, 0, GETUTCDATE());",
            new
            {
                result.DocumentId,
                result.NaturalLanguageText,
                NaturalLanguageVector = SerializeVector(result.NaturalLanguageVector),
                result.StructuredText,
                StructuredVector = SerializeVector(result.StructuredVector)
            });
    }

    private static string SerializeVector(float[] vector) =>
        Convert.ToBase64String(vector.SelectMany(BitConverter.GetBytes).ToArray());

    private static float[] DeserializeVector(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var floats = new float[bytes.Length / 4];
        for (int i = 0; i < floats.Length; i++)
        {
            floats[i] = BitConverter.ToSingle(bytes, i * 4);
        }
        return floats;
    }

    private class EmbeddingCacheRecord
    {
        public string DocumentId { get; set; } = "";
        public string NaturalLanguageText { get; set; } = "";
        public string NaturalLanguageVector { get; set; } = "";
        public string StructuredText { get; set; } = "";
        public string StructuredVector { get; set; } = "";
    }
}

public class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string DeploymentName { get; set; } = "text-embedding-3-large";
    public int VectorDimensions { get; set; } = 3072;
    public int MaxBatchSize { get; set; } = 20;
    public int CacheExpirationHours { get; set; } = 168; // 7 days
}
