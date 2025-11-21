using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

/// <summary>
/// Uses OpenAI to enhance documentation fields before draft creation
/// </summary>
public interface IOpenAIEnhancementService
{
    Task<EnhancedDocumentation> EnhanceDocumentationAsync(
        DocumentationEnhancementRequest request,
        CancellationToken cancellationToken = default);
}

public class DocumentationEnhancementRequest
{
    public required string ChangeType { get; set; }
    public required string Description { get; set; }          // Raw description from Excel
    public required string Documentation { get; set; }        // What was done (from Excel)
    public string? Table { get; set; }
    public string? Column { get; set; }
    public string? ModifiedStoredProcedures { get; set; }    // Comma-separated list
    public string? CABNumber { get; set; }
    public string? JiraNumber { get; set; }
}

public class EnhancedDocumentation
{
    public required string EnhancedDescription { get; set; }
    public required string EnhancedImplementation { get; set; }
    public List<string> KeyPoints { get; set; } = new();
    public List<string> TechnicalDetails { get; set; } = new();
}

public class OpenAIEnhancementService : IOpenAIEnhancementService
{
    private readonly ILogger<OpenAIEnhancementService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAIEnhancementService(
        ILogger<OpenAIEnhancementService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
        _model = configuration["OpenAI:Model"] ?? "gpt-4";

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<EnhancedDocumentation> EnhanceDocumentationAsync(
        DocumentationEnhancementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enhancing documentation for {ChangeType}: {CAB}", request.ChangeType, request.CABNumber);

            var systemPrompt = BuildSystemPrompt(request.ChangeType);
            var userPrompt = BuildUserPrompt(request);

            var openAIRequest = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,  // Low temperature for strict interpretation
                max_tokens = 1500
            };

            var response = await _httpClient.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions",
                openAIRequest,
                cancellationToken
            );

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                _logger.LogWarning("OpenAI returned no choices, falling back to original text");
                return FallbackEnhancement(request);
            }

            var enhancedText = result.Choices[0].Message.Content;

            _logger.LogDebug("OpenAI enhanced documentation: {EnhancedText}", enhancedText);

            return ParseEnhancedResponse(enhancedText, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing documentation with OpenAI, falling back to original text");
            return FallbackEnhancement(request);
        }
    }

    private string BuildSystemPrompt(string changeType)
    {
        return $@"You are a technical documentation assistant for database documentation. Your role is to enhance {changeType} documentation by:

1. **Expanding technical descriptions** - Add clarity to technical explanations without making assumptions
2. **Structuring information** - Organize information logically
3. **Adding context** - Explain technical terms when relevant
4. **Maintaining accuracy** - NEVER add information not present in the source material
5. **Using strict interpretation** - Only expand on what is explicitly stated

RULES:
- Do NOT make assumptions about implementation details
- Do NOT add information not provided in the original text
- Do NOT speculate about future changes or impacts
- DO explain technical database concepts when mentioned
- DO organize information into clear sections
- DO use professional, technical language

Output format should be JSON:
{{
  ""enhancedDescription"": ""Clear, expanded description of the change request or issue"",
  ""enhancedImplementation"": ""Clear, expanded description of what was implemented or fixed"",
  ""keyPoints"": [""Key point 1"", ""Key point 2""],
  ""technicalDetails"": [""Technical detail 1"", ""Technical detail 2""]
}}";
    }

    private string BuildUserPrompt(DocumentationEnhancementRequest request)
    {
        var prompt = $@"Enhance this database {request.ChangeType} documentation:

**Original Description (Reason for Change):**
{request.Description}

**Original Implementation (What Was Done):**
{request.Documentation}

**Context:**
- CAB Number: {request.CABNumber}
- Jira Ticket: {request.JiraNumber}
- Table: {request.Table ?? "Not specified"}
- Column: {request.Column ?? "Not specified"}
- Modified Stored Procedures: {request.ModifiedStoredProcedures ?? "None specified"}

Please enhance this documentation following the rules. Return ONLY valid JSON.";

        return prompt;
    }

    private EnhancedDocumentation ParseEnhancedResponse(string enhancedText, DocumentationEnhancementRequest request)
    {
        try
        {
            // Try to parse JSON response
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<EnhancedDocumentation>(enhancedText, options);

            if (parsed != null)
            {
                return parsed;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenAI response as JSON, using fallback");
        }

        // Fallback: use original text if parsing fails
        return FallbackEnhancement(request);
    }

    private EnhancedDocumentation FallbackEnhancement(DocumentationEnhancementRequest request)
    {
        return new EnhancedDocumentation
        {
            EnhancedDescription = request.Description,
            EnhancedImplementation = request.Documentation,
            KeyPoints = new List<string> { request.Description },
            TechnicalDetails = string.IsNullOrWhiteSpace(request.ModifiedStoredProcedures)
                ? new List<string>()
                : request.ModifiedStoredProcedures.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(sp => sp.Trim())
                    .ToList()
        };
    }

    // OpenAI API response models
    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public OpenAIChoice[]? Choices { get; set; }
    }

    private class OpenAIChoice
    {
        [JsonPropertyName("message")]
        public required OpenAIMessage Message { get; set; }
    }

    private class OpenAIMessage
    {
        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }
}
