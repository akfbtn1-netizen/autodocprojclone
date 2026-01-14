// =============================================================================
// Agent #5: Post-Approval Pipeline - Metadata Finalization Service
// Generates embeddings and AI classifications at approval time (deferred processing)
// =============================================================================

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure;
using Azure.AI.OpenAI;
using Polly;
using Polly.Retry;

namespace Enterprise.Documentation.Core.Application.Services.PostApproval;

/// <summary>
/// Finalizes metadata at approval time - generates embeddings and AI classifications.
/// This service is called ONLY when a document is approved, not during draft creation.
/// Uses Polly for resilience against Azure OpenAI transient failures.
/// </summary>
public class MetadataFinalizationService : IMetadataFinalizationService
{
    private readonly ILogger<MetadataFinalizationService> _logger;
    private readonly OpenAIClient? _openAIClient;
    private readonly string _embeddingDeployment;
    private readonly string _chatDeployment;
    private readonly AsyncRetryPolicy _retryPolicy;

    public MetadataFinalizationService(
        ILogger<MetadataFinalizationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-ada-002";
        _chatDeployment = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4";

        var endpoint = configuration["AzureOpenAI:Endpoint"];
        var apiKey = configuration["AzureOpenAI:ApiKey"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey))
        {
            _openAIClient = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }

        // Configure Polly retry policy for Azure OpenAI resilience
        _retryPolicy = Policy
            .Handle<RequestFailedException>(ex => ex.Status == 429 || ex.Status >= 500)
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Azure OpenAI retry {RetryCount} after {Delay}s",
                        retryCount, timeSpan.TotalSeconds);
                });
    }

    public async Task<FinalizedMetadata> FinalizeMetadataAsync(
        int approvalId,
        string documentPath,
        ExtractedMetadata draftMetadata,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Finalizing metadata for approval {ApprovalId}, document {DocId}",
            approvalId, draftMetadata.DocumentId);

        var finalized = new FinalizedMetadata
        {
            // Copy all base properties
            DocumentId = draftMetadata.DocumentId,
            DocumentType = draftMetadata.DocumentType,
            SchemaName = draftMetadata.SchemaName,
            ObjectName = draftMetadata.ObjectName,
            ColumnName = draftMetadata.ColumnName,
            ObjectType = draftMetadata.ObjectType,
            Description = draftMetadata.Description,
            Purpose = draftMetadata.Purpose,
            Parameters = draftMetadata.Parameters,
            TablesAccessed = draftMetadata.TablesAccessed,
            ColumnsModified = draftMetadata.ColumnsModified,
            JiraNumber = draftMetadata.JiraNumber,
            CABNumber = draftMetadata.CABNumber,
            ChangeDescription = draftMetadata.ChangeDescription,
            BracketedCode = draftMetadata.BracketedCode,
            ComplexityScore = draftMetadata.ComplexityScore,
            ComplexityTier = draftMetadata.ComplexityTier,
            HasDynamicSql = draftMetadata.HasDynamicSql,
            HasCursors = draftMetadata.HasCursors,
            HasTransactions = draftMetadata.HasTransactions,
            HasErrorHandling = draftMetadata.HasErrorHandling,
            ExtractedAt = draftMetadata.ExtractedAt,
            ContentHash = draftMetadata.ContentHash,
            ApprovalId = approvalId
        };

        var totalTokens = 0;
        decimal totalCost = 0;

        try
        {
            // Read approved document content for embedding
            var documentContent = await ReadDocumentContentAsync(documentPath);

            // Generate embedding (1536 dimensions for ada-002)
            _logger.LogInformation("Generating semantic embedding for {DocId}", draftMetadata.DocumentId);
            var embeddingResult = await GenerateEmbeddingWithMetricsAsync(documentContent, ct);
            finalized.SemanticEmbedding = embeddingResult.Embedding;
            finalized.EmbeddingGeneratedAt = DateTime.UtcNow;
            totalTokens += embeddingResult.TokensUsed;
            totalCost += embeddingResult.Cost;

            // Enrich classification with AI
            _logger.LogInformation("Enriching classification for {DocId}", draftMetadata.DocumentId);
            var classificationResult = await EnrichClassificationWithMetricsAsync(documentContent, draftMetadata, ct);
            finalized.Classification = classificationResult.Classification;
            finalized.ClassificationEnrichedAt = DateTime.UtcNow;
            totalTokens += classificationResult.TokensUsed;
            totalCost += classificationResult.Cost;

            finalized.TokensUsed = totalTokens;
            finalized.GenerationCostUSD = totalCost;
            finalized.AIModel = _chatDeployment;

            _logger.LogInformation(
                "Metadata finalized for {DocId}: {Tokens} tokens, ${Cost:F4}",
                draftMetadata.DocumentId, totalTokens, totalCost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing metadata for {DocId}", draftMetadata.DocumentId);
            // Continue with partial metadata - embedding/classification are optional
            finalized.Classification = CreateDefaultClassification(draftMetadata);
        }

        return finalized;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string documentContent, CancellationToken ct = default)
    {
        if (_openAIClient == null)
        {
            _logger.LogWarning("OpenAI client not configured - returning empty embedding");
            return Array.Empty<float>();
        }

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            // Truncate if too long (ada-002 has 8191 token limit)
            var truncatedContent = TruncateForEmbedding(documentContent, 6000);

            var response = await _openAIClient.GetEmbeddingsAsync(
                new EmbeddingsOptions(_embeddingDeployment, new[] { truncatedContent }),
                ct);

            return response.Value.Data[0].Embedding.ToArray();
        });
    }

    public async Task<EnrichedClassification> EnrichClassificationAsync(
        string documentContent,
        ExtractedMetadata baseMetadata,
        CancellationToken ct = default)
    {
        if (_openAIClient == null)
        {
            return CreateDefaultClassification(baseMetadata);
        }

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var prompt = BuildClassificationPrompt(documentContent, baseMetadata);

            var chatOptions = new ChatCompletionsOptions(_chatDeployment, new[]
            {
                new ChatRequestSystemMessage(ClassificationSystemPrompt),
                new ChatRequestUserMessage(prompt)
            })
            {
                Temperature = 0.3f,
                MaxTokens = 1000,
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            var response = await _openAIClient.GetChatCompletionsAsync(chatOptions, ct);
            var content = response.Value.Choices[0].Message.Content;

            try
            {
                return JsonSerializer.Deserialize<EnrichedClassification>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? CreateDefaultClassification(baseMetadata);
            }
            catch
            {
                _logger.LogWarning("Failed to parse classification response, using defaults");
                return CreateDefaultClassification(baseMetadata);
            }
        });
    }

    #region Private Methods

    private async Task<(float[] Embedding, int TokensUsed, decimal Cost)> GenerateEmbeddingWithMetricsAsync(
        string content, CancellationToken ct)
    {
        if (_openAIClient == null)
            return (Array.Empty<float>(), 0, 0);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var truncated = TruncateForEmbedding(content, 6000);
            var response = await _openAIClient.GetEmbeddingsAsync(
                new EmbeddingsOptions(_embeddingDeployment, new[] { truncated }), ct);

            var tokens = response.Value.Usage.TotalTokens;
            // ada-002 pricing: $0.0001 per 1K tokens
            var cost = tokens * 0.0001m / 1000;

            return (response.Value.Data[0].Embedding.ToArray(), tokens, cost);
        });
    }

    private async Task<(EnrichedClassification Classification, int TokensUsed, decimal Cost)>
        EnrichClassificationWithMetricsAsync(
            string content,
            ExtractedMetadata metadata,
            CancellationToken ct)
    {
        if (_openAIClient == null)
            return (CreateDefaultClassification(metadata), 0, 0);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var prompt = BuildClassificationPrompt(content, metadata);
            var chatOptions = new ChatCompletionsOptions(_chatDeployment, new[]
            {
                new ChatRequestSystemMessage(ClassificationSystemPrompt),
                new ChatRequestUserMessage(prompt)
            })
            {
                Temperature = 0.3f,
                MaxTokens = 1000,
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            var response = await _openAIClient.GetChatCompletionsAsync(chatOptions, ct);
            var usage = response.Value.Usage;

            // GPT-4 pricing: $0.03 input, $0.06 output per 1K tokens
            var cost = (usage.PromptTokens * 0.03m + usage.CompletionTokens * 0.06m) / 1000;

            var classification = JsonSerializer.Deserialize<EnrichedClassification>(
                response.Value.Choices[0].Message.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? CreateDefaultClassification(metadata);

            return (classification, usage.TotalTokens, cost);
        });
    }

    private async Task<string> ReadDocumentContentAsync(string documentPath)
    {
        if (!File.Exists(documentPath))
        {
            _logger.LogWarning("Document not found: {Path}", documentPath);
            return string.Empty;
        }

        try
        {
            // Extract text from docx using Open XML
            var content = new StringBuilder();
            using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(documentPath, false);
            var body = doc.MainDocumentPart?.Document.Body;
            if (body != null)
            {
                foreach (var para in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                {
                    content.AppendLine(para.InnerText);
                }
            }
            return content.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading document content from {Path}", documentPath);
            return string.Empty;
        }
    }

    private string TruncateForEmbedding(string content, int maxTokens)
    {
        // Rough estimate: 1 token ~ 4 characters
        var maxChars = maxTokens * 4;
        return content.Length > maxChars ? content[..maxChars] : content;
    }

    private string BuildClassificationPrompt(string content, ExtractedMetadata metadata)
    {
        return $@"Analyze this database documentation and provide classification:

Document ID: {metadata.DocumentId}
Object: {metadata.SchemaName}.{metadata.ObjectName}
Type: {metadata.ObjectType}
Description: {metadata.Description}

Document Content:
{TruncateForEmbedding(content, 2000)}

Provide JSON classification with:
- businessDomain: Primary business area (e.g., Claims, Policy, Billing, Reporting)
- domainTags: Array of relevant tags
- dataClassification: Public/Internal/Confidential/Restricted
- semanticCategory: Technical category
- businessProcesses: Array of affected business processes
- complianceCategory: HIPAA/PCI/SOX/None
- containsPII: boolean
- piiTypes: Array of PII types if found (SSN, EMAIL, PHONE, DOB, ADDRESS, etc.)
- confidenceScore: 0.0-1.0
- classificationRationale: Brief explanation";
    }

    private EnrichedClassification CreateDefaultClassification(ExtractedMetadata metadata)
    {
        return new EnrichedClassification
        {
            BusinessDomain = InferDomainFromSchema(metadata.SchemaName),
            DomainTags = new List<string> { metadata.ObjectType, metadata.SchemaName },
            DataClassification = "Internal",
            SemanticCategory = metadata.ObjectType,
            BusinessProcesses = new List<string>(),
            ContainsPII = false,
            PIITypes = new List<string>(),
            ConfidenceScore = 0.5m,
            ClassificationRationale = "Default classification - AI enrichment not available"
        };
    }

    private string InferDomainFromSchema(string schemaName)
    {
        return schemaName.ToLower() switch
        {
            "gwpc" => "Policy Management",
            "claims" => "Claims Processing",
            "billing" => "Billing",
            "daqa" => "Data Quality",
            "rpt" => "Reporting",
            "hr" => "Human Resources",
            "fin" => "Finance",
            _ => "General"
        };
    }

    private const string ClassificationSystemPrompt = @"You are a database documentation classifier.
Analyze the provided documentation and output a JSON object with classification metadata.
Focus on:
1. Business domain identification
2. Data sensitivity classification
3. PII detection
4. Compliance requirements
5. Business process mapping

Always respond with valid JSON matching the requested schema.";

    #endregion
}
