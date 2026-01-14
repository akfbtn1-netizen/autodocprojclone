using Microsoft.Extensions.Logging;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Enterprise.Documentation.Core.Application.Services.Metadata;

public interface IMetadataExtractionService
{
    Task<MasterIndexMetadata> ExtractMetadataAsync(
        Dictionary<string, object> templateData,
        CancellationToken ct = default);
}

public class MetadataExtractionService : IMetadataExtractionService
{
    private readonly ILogger<MetadataExtractionService> _logger;
    private readonly OpenAIClient _openAIClient;
    private readonly string _embeddingDeployment;

    public MetadataExtractionService(
        ILogger<MetadataExtractionService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        
        var endpoint = configuration["AzureOpenAI:Endpoint"] 
            ?? throw new InvalidOperationException("Azure OpenAI Endpoint not configured");
        var apiKey = configuration["AzureOpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("Azure OpenAI ApiKey not configured");
        _embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-ada-002";
        
        _openAIClient = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<MasterIndexMetadata> ExtractMetadataAsync(
        Dictionary<string, object> templateData,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Extracting metadata for {DocId}", 
                templateData.GetValueOrDefault("doc_id", "UNKNOWN"));

            var metadata = new MasterIndexMetadata
            {
                // Core Identifiers
                DocId = GetString(templateData, "doc_id"),
                JiraNumber = GetString(templateData, "jira"),
                Version = GetString(templateData, "version"),
                Author = GetString(templateData, "author"),
                DocumentDate = ParseDate(GetString(templateData, "date")),
                
                // Database Object Info
                SchemaName = GetString(templateData, "schema"),
                ObjectName = GetString(templateData, "sp_name"),
                ObjectType = "Stored Procedure",
                DatabaseName = GetString(templateData, "database"),
                
                // Change Info
                TableName = GetString(templateData, "table"),
                ColumnName = GetString(templateData, "column"),
                ChangeType = GetString(templateData, "change_type"),
                
                // Descriptions
                Purpose = GetString(templateData, "purpose"),
                BusinessImpact = GetString(templateData, "business_impact") 
                    ?? GetString(templateData, "overview"),
                TechnicalSummary = GetString(templateData, "technical_summary"),
                WhatsNew = GetString(templateData, "whats_new"),
                
                // Complexity
                LinesOfCode = GetComplexityValue(templateData, "Lines of Code"),
                JoinCount = GetComplexityValue(templateData, "Joins"),
                ComplexityLevel = GetComplexityValue(templateData, "Logic Complexity"),
                BusinessImpactLevel = GetComplexityValue(templateData, "Business Impact"),
                
                // Dependencies (JSON)
                DependentTables = GetDependentTables(templateData),
                DependentProcedures = GetDependentProcedures(templateData),
                TempTables = GetTempTables(templateData),
                ControlTables = GetControlTables(templateData),
                
                // Search/Classification
                Keywords = GetKeywords(templateData),
                ConfidenceScore = GetDouble(templateData, "confidence_score"),
                DocumentType = GetString(templateData, "doc_type"),
                
                // Metadata about metadata
                ExtractedAt = DateTime.UtcNow,
                ExtractionVersion = GetString(templateData, "extraction_version") ?? "1.0"
            };

            // Generate semantic embedding for search
            metadata.SemanticEmbedding = await GenerateEmbeddingAsync(metadata, ct);
            
            // Generate domain tags and business process (AI-powered)
            var enrichedClassification = await EnrichClassificationAsync(metadata, ct);
            metadata.DomainTags = enrichedClassification.DomainTags;
            metadata.BusinessProcess = enrichedClassification.BusinessProcess;
            metadata.ImpactScope = enrichedClassification.ImpactScope;
            metadata.DataFlow = enrichedClassification.DataFlow;

            _logger.LogInformation("Metadata extraction complete for {DocId}", metadata.DocId);
            
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata extraction failed");
            throw;
        }
    }

    private async Task<float[]?> GenerateEmbeddingAsync(MasterIndexMetadata metadata, CancellationToken ct)
    {
        try
        {
            // Create searchable text from key fields
            var searchableText = $@"
{metadata.Purpose}
{metadata.BusinessImpact}
{metadata.TechnicalSummary}
{metadata.SchemaName}.{metadata.ObjectName}
{string.Join(" ", metadata.Keywords ?? new List<string>())}
{metadata.WhatsNew}
".Trim();

            var embeddingOptions = new EmbeddingsOptions(_embeddingDeployment, new[] { searchableText });
            var response = await _openAIClient.GetEmbeddingsAsync(embeddingOptions, ct);
            
            return response.Value.Data[0].Embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding, continuing without it");
            return null;
        }
    }

    private async Task<ClassificationEnrichment> EnrichClassificationAsync(
        MasterIndexMetadata metadata, 
        CancellationToken ct)
    {
        try
        {
                        var prompt =
                                "Analyze this database procedure and classify it:\n\n" +
                                $"Object: {metadata.SchemaName}.{metadata.ObjectName}\n" +
                                $"Purpose: {metadata.Purpose}\n" +
                                $"Tables: {string.Join(", ", metadata.DependentTables ?? new List<string>())}\n" +
                                $"Change: {metadata.WhatsNew}\n\n" +
                                "Provide classification in JSON format:\n" +
                                "{\n" +
                                "  \"domain_tags\": [\"tag1\", \"tag2\"],\n" +
                                "  \"business_process\": \"specific business process name\",\n" +
                                "  \"impact_scope\": \"what systems/reports/processes this affects\",\n" +
                                "  \"data_flow\": \"brief source -> target description\"\n" +
                                "}\n\n" +
                                "Domain tags should be from: Policy Management, Financial Reporting, Compliance, Claims Processing, Premium Calculation, Agent Management, Commission Processing, Reinsurance, Underwriting, Billing, Data Quality, ETL Processing, Reconciliation\n\n" +
                                "Business process examples: Month-End Close, Policy Lapse Processing, Premium Posting, Agent Commission Calculation, Financial Statement Preparation";

            var chatOptions = new ChatCompletionsOptions
            {
                DeploymentName = "gpt-4.1",
                Messages =
                {
                    new ChatRequestSystemMessage("You are a business analyst classifying database procedures."),
                    new ChatRequestUserMessage(prompt)
                },
                Temperature = 0.3f,
                MaxTokens = 500,
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            var response = await _openAIClient.GetChatCompletionsAsync(chatOptions, ct);
            var content = response.Value.Choices[0].Message.Content;
            
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            
            return new ClassificationEnrichment
            {
                DomainTags = root.GetProperty("domain_tags").EnumerateArray()
                    .Select(e => e.GetString() ?? "").ToList(),
                BusinessProcess = root.GetProperty("business_process").GetString() ?? "",
                ImpactScope = root.GetProperty("impact_scope").GetString() ?? "",
                DataFlow = root.GetProperty("data_flow").GetString() ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Classification enrichment failed, using defaults");
            return new ClassificationEnrichment();
        }
    }

    // Helper methods
    private string GetString(Dictionary<string, object> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";
    }

    private double GetDouble(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue(key, out var value) && value != null)
        {
            if (value is double d) return d;
            if (double.TryParse(value.ToString(), out var parsed)) return parsed;
        }
        return 0.0;
    }

    private DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return null;
        return DateTime.TryParse(dateStr, out var date) ? date : null;
    }

    private string GetComplexityValue(Dictionary<string, object> data, string key)
    {
        if (data.TryGetValue("complexity", out var complexityObj) && 
            complexityObj is Dictionary<string, string> complexDict)
        {
            return complexDict.GetValueOrDefault(key, "");
        }
        return "";
    }

    private List<string> GetDependentTables(Dictionary<string, object> data)
    {
        return data.TryGetValue("related_tables", out var tables) && tables is List<string> list
            ? list : new List<string>();
    }

    private List<string> GetDependentProcedures(Dictionary<string, object> data)
    {
        if (data.TryGetValue("dependencies", out var deps) && 
            deps is Dictionary<string, string> depsDict &&
            depsDict.TryGetValue("Procedures", out var procs))
        {
            return procs.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).ToList();
        }
        return new List<string>();
    }

    private List<string> GetTempTables(Dictionary<string, object> data)
    {
        if (data.TryGetValue("dependencies", out var deps) && 
            deps is Dictionary<string, string> depsDict &&
            depsDict.TryGetValue("Temp Tables", out var temps))
        {
            return temps.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();
        }
        return new List<string>();
    }

    private List<string> GetControlTables(Dictionary<string, object> data)
    {
        return data.TryGetValue("related_tables", out var tables) && tables is List<string> list
            ? list.Where(t => t.Contains("Control", StringComparison.OrdinalIgnoreCase) || 
                             t.Contains("ctl", StringComparison.OrdinalIgnoreCase)).ToList()
            : new List<string>();
    }

    private List<string> GetKeywords(Dictionary<string, object> data)
    {
        return data.TryGetValue("keywords", out var keywords) && keywords is List<string> list
            ? list : new List<string>();
    }
}

// Model classes
public class MasterIndexMetadata
{
    // Core Identifiers
    public string DocId { get; set; } = string.Empty;
    public string JiraNumber { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime? DocumentDate { get; set; }
    
    // Database Object Info
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    
    // Change Info
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    
    // Descriptions
    public string Purpose { get; set; } = string.Empty;
    public string BusinessImpact { get; set; } = string.Empty;
    public string TechnicalSummary { get; set; } = string.Empty;
    public string WhatsNew { get; set; } = string.Empty;
    
    // Complexity
    public string LinesOfCode { get; set; } = string.Empty;
    public string JoinCount { get; set; } = string.Empty;
    public string ComplexityLevel { get; set; } = string.Empty;
    public string BusinessImpactLevel { get; set; } = string.Empty;
    
    // Dependencies
    public List<string>? DependentTables { get; set; }
    public List<string>? DependentProcedures { get; set; }
    public List<string>? TempTables { get; set; }
    public List<string>? ControlTables { get; set; }
    
    // Search/Classification
    public List<string>? Keywords { get; set; }
    public double ConfidenceScore { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    
    // AI-Enhanced Classification
    public List<string>? DomainTags { get; set; }
    public string BusinessProcess { get; set; } = string.Empty;
    public string ImpactScope { get; set; } = string.Empty;
    public string DataFlow { get; set; } = string.Empty;
    
    // Semantic Search
    public float[]? SemanticEmbedding { get; set; }
    
    // Metadata about metadata
    public DateTime ExtractedAt { get; set; }
    public string ExtractionVersion { get; set; } = string.Empty;
}

public class ClassificationEnrichment
{
    public List<string> DomainTags { get; set; } = new();
    public string BusinessProcess { get; set; } = string.Empty;
    public string ImpactScope { get; set; } = string.Empty;
    public string DataFlow { get; set; } = string.Empty;
}
