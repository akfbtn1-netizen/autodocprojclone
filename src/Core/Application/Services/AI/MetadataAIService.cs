using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Enterprise.Documentation.Core.Application.Services.AI;

public interface IMetadataAIService
{
    Task<SemanticClassification> ClassifySemanticCategoryAsync(
        string schema, string table, string column, string description, CancellationToken ct);
    Task<string[]> GenerateTagsAsync(
        string schema, string table, string column, string description, CancellationToken ct);
    Task<ComplianceClassification> ClassifyComplianceAsync(
        string schema, string table, string column, bool containsPII, CancellationToken ct);
}

public class MetadataAIService : IMetadataAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MetadataAIService> _logger;
    private readonly string? _openAIApiKey;
    private readonly string? _openAIEndpoint;
    private readonly string? _openAIModel;

    public MetadataAIService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MetadataAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _openAIApiKey = configuration["AzureOpenAI:ApiKey"];
        _openAIEndpoint = configuration["AzureOpenAI:Endpoint"];
        _openAIModel = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4.1";

        if (!string.IsNullOrEmpty(_openAIApiKey))
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", _openAIApiKey);
        }
    }

    public async Task<SemanticClassification> ClassifySemanticCategoryAsync(
        string schema, string table, string column, string description, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_openAIApiKey) || string.IsNullOrEmpty(_openAIEndpoint))
        {
            _logger.LogWarning("OpenAI not configured, returning default semantic category");
            return new SemanticClassification { Category = "General", Confidence = 0.0 };
        }

        var prompt = $@"Classify this database object into exactly ONE semantic category:

Schema: {schema}
Table: {table}
Column: {column ?? "N/A"}
Description: {description ?? "No description"}

Categories:
- Policy Management: Policy lifecycle, endorsements, renewals, cancellations
- Claims Processing: Claims, losses, payments, reserves
- Billing & Finance: Premiums, invoices, payments, accounting
- Customer Data: Policyholders, contacts, demographics
- Agent & Producer: Agent info, commissions, hierarchies
- Reference Data: Lookup tables, codes, configurations
- Underwriting: Risk assessment, rating, eligibility
- Reporting & Analytics: Aggregated data, metrics, KPIs
- System & Audit: Logs, timestamps, technical metadata
- Document Management: Documents, attachments, correspondence

Respond with JSON only:
{{""category"": ""selected category"", ""confidence"": 0.0-1.0}}";

        try
        {
            var response = await CallOpenAIAsync(prompt, ct);
            var classification = JsonSerializer.Deserialize<SemanticClassification>(response);
            return classification ?? new SemanticClassification { Category = "General", Confidence = 0.0 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify semantic category for {Schema}.{Table}.{Column}", schema, table, column);
            return new SemanticClassification { Category = "General", Confidence = 0.0 };
        }
    }

    public async Task<string[]> GenerateTagsAsync(
        string schema, string table, string column, string description, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_openAIApiKey) || string.IsNullOrEmpty(_openAIEndpoint))
        {
            _logger.LogWarning("OpenAI not configured, returning default tags");
            return Array.Empty<string>();
        }

        var prompt = $@"Generate searchable tags for this database object:

Schema: {schema}
Table: {table}
Column: {column ?? "N/A"}
Description: {description ?? "No description"}

Rules:
- Lowercase only
- Include business and technical terms
- Include abbreviations users might search
- Maximum 8 tags

Respond with JSON only:
{{""tags"": [""tag1"", ""tag2"", ...]}}";

        try
        {
            var response = await CallOpenAIAsync(prompt, ct);
            var tagsResult = JsonSerializer.Deserialize<TagsResult>(response);
            return tagsResult?.Tags ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate tags for {Schema}.{Table}.{Column}", schema, table, column);
            return Array.Empty<string>();
        }
    }

    public async Task<ComplianceClassification> ClassifyComplianceAsync(
        string schema, string table, string column, bool containsPII, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_openAIApiKey) || string.IsNullOrEmpty(_openAIEndpoint))
        {
            _logger.LogWarning("OpenAI not configured, returning default compliance classification");
            return new ComplianceClassification { ComplianceTags = Array.Empty<string>(), RetentionYears = 7 };
        }

        var prompt = $@"Classify compliance requirements for this database object:

Schema: {schema}
Table: {table}
Column: {column ?? "N/A"}
Contains PII: {containsPII}

Frameworks: SOX, HIPAA, PCI-DSS, GLBA, State Insurance Regulations, GDPR, CCPA

Based on the schema and PII status, determine applicable compliance frameworks and retention period.

Respond with JSON only:
{{""complianceTags"": [""SOX"", ""GLBA""], ""retentionYears"": 7}}";

        try
        {
            var response = await CallOpenAIAsync(prompt, ct);
            var classification = JsonSerializer.Deserialize<ComplianceClassification>(response);
            return classification ?? new ComplianceClassification { ComplianceTags = Array.Empty<string>(), RetentionYears = 7 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify compliance for {Schema}.{Table}.{Column}", schema, table, column);
            return new ComplianceClassification { ComplianceTags = Array.Empty<string>(), RetentionYears = 7 };
        }
    }

    private async Task<string> CallOpenAIAsync(string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            messages = new[]
            {
                new { role = "system", content = "You are a database metadata expert. Respond with valid JSON only." },
                new { role = "user", content = prompt }
            },
            model = _openAIModel,
            max_tokens = 1000,
            temperature = 0.1
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var url = $"{_openAIEndpoint?.TrimEnd('/')}/openai/deployments/{_openAIModel}/chat/completions?api-version=2024-08-01-preview";
        var response = await _httpClient.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(ct);
        var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
        
        return openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "{}";
    }

    private class OpenAIResponse
    {
        public Choice[]? Choices { get; set; }
    }

    private class Choice
    {
        public Message? Message { get; set; }
    }

    private class Message
    {
        public string? Content { get; set; }
    }
}

public class SemanticClassification
{
    public string Category { get; set; } = "General";
    public double Confidence { get; set; }
}

public class TagsResult
{
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class ComplianceClassification
{
    public string[] ComplianceTags { get; set; } = Array.Empty<string>();
    public int RetentionYears { get; set; } = 7;
}