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
    
    // NEW: Metadata enrichment from MasterIndex
    public string? ExistingDescription { get; set; }         // Current description from metadata
    public string? BusinessDomain { get; set; }             // Business context
    public string? DataClassification { get; set; }         // PII/Sensitive classification
    public int? DownstreamDependencies { get; set; }        // Impact scope
    public string? SemanticCategory { get; set; }           // AI-assigned category
    public double? QualityScore { get; set; }               // Current metadata quality
    public string? UsagePattern { get; set; }               // How it's typically used
}

public class EnhancedDocumentation
{
    public required string Summary { get; set; }
    public required string Enhancement { get; set; }
    public required string Benefits { get; set; }
    public required string Code { get; set; }
    public required string CodeExplanation { get; set; }
    
    // Legacy properties for backward compatibility
    public string EnhancedDescription => Summary;
    public string EnhancedImplementation => Enhancement;
    public List<string> KeyPoints => new() { Summary };
    public List<string> TechnicalDetails => new() { Enhancement };
}

public class OpenAIEnhancementService : IOpenAIEnhancementService
{
    private readonly ILogger<OpenAIEnhancementService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly string _deployment;

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
        _model = configuration["OpenAI:Model"] ?? "gpt-4.1";
        _endpoint = configuration["OpenAI:Endpoint"]
            ?? throw new InvalidOperationException("OpenAI:Endpoint not configured");
        _deployment = configuration["OpenAI:Deployment"]
            ?? throw new InvalidOperationException("OpenAI:Deployment not configured");

        _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        
        // Note: Timeout is configured in Program.cs via AddHttpClient
    }

    public async Task<EnhancedDocumentation> EnhanceDocumentationAsync(
        DocumentationEnhancementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Enhancing documentation for {ChangeType}: {CAB}", request.ChangeType, request.CABNumber);

            var systemPrompt = GetEnhancedSystemPrompt();
            var fewShotExamples = GetFewShotExamples();
            var cotPrompt = GetChainOfThoughtPrompt(request);
            
            // Log prompt details for debugging content filter issues (but not sensitive content)
            _logger.LogDebug("Sending OpenAI request for {ChangeType} with enhanced prompt system", request.ChangeType);

            var openAIRequest = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = fewShotExamples + "\n\n" + cotPrompt }
                },
                temperature = 0.3f,              // Optimal for consistency
                max_tokens = 2048,               // Enough for detailed documentation
                frequency_penalty = 0.3f,        // Reduce repetition
                presence_penalty = 0.2f,         // Encourage diverse vocabulary
                response_format = new { type = "json_object" }  // Force JSON structure
            };

            var azureUrl = $"{_endpoint.TrimEnd('/')}/openai/deployments/{_deployment}/chat/completions?api-version=2023-12-01-preview";
            
            // Create a timeout token that's more generous than the HTTP client timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(1)); // 1 minute for this specific request
            
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(
                    azureUrl,
                    openAIRequest,
                    timeoutCts.Token
                );
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("OpenAI API call timed out after 1 minute for {ChangeType}", request.ChangeType);
                throw new TimeoutException("OpenAI API call timed out. The service may be experiencing high load.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error during OpenAI API call for {ChangeType}", request.ChangeType);
                throw new InvalidOperationException("Network error communicating with OpenAI service. Please try again later.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("OpenAI API key is invalid or missing (401 Unauthorized). Please check OpenAI:ApiKey configuration. Response: {ErrorContent}", errorContent);
                    throw new InvalidOperationException("OpenAI API key is invalid or missing. Please check your OpenAI:ApiKey configuration.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && errorContent.Contains("content_filter"))
                {
                    _logger.LogWarning("OpenAI content filter triggered for {ChangeType}. Attempting retry with conservative prompt.", request.ChangeType);
                    return await RetryWithConservativePrompt(request, cancellationToken);
                }
                else
                {
                    _logger.LogError("OpenAI API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    response.EnsureSuccessStatusCode();
                }
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                _logger.LogWarning("OpenAI returned no choices, falling back to original text");
                return FallbackEnhancement(request);
            }

            var enhancedText = result.Choices[0].Message.Content;

            _logger.LogDebug("OpenAI enhanced documentation received, parsing structured response");

            return ParseStructuredResponse(enhancedText, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enhancing documentation with OpenAI, falling back to original text");
            return FallbackEnhancement(request);
        }
    }

    // LEVEL 1: Enhanced System Prompt
    private string GetEnhancedSystemPrompt()
    {
        return @"You are a senior database architect and technical writer with 15+ years of enterprise experience.

**Your Role:**
- Transform raw database change descriptions into professional, comprehensive documentation
- Write for technical audiences (DBAs, developers, architects)
- Focus on clarity, precision, and actionable information

**Documentation Standards:**
1. **Executive Summary:** 2-3 sentences covering WHAT changed and WHY
2. **Technical Details:** Specific table/column info, data types, constraints
3. **Business Impact:** How this affects operations, reporting, or workflows
4. **Implementation:** Clear code explanation with reasoning
5. **Benefits:** Quantifiable improvements where possible

**Writing Style:**
- Use active voice and present tense
- Be concise but comprehensive
- Include technical specifics (data types, constraints, indexes)
- Explain the 'why' not just the 'what'
- Use bullet points for lists, not paragraphs
- Bold key terms using **markdown**

**Quality Criteria:**
- Each section must provide NEW information (no repetition)
- Code explanations must explain logic, not just describe syntax
- Benefits must be specific and measurable
- Avoid generic phrases like 'improves efficiency' without specifics

**Output Format:**
Always return valid JSON with these exact fields:
{
    ""summary"": ""2-3 sentence executive summary of the change"",
    ""enhancement"": ""Detailed technical description with bullet points"",
    ""benefits"": ""Specific, quantifiable business benefits"",
    ""code"": ""SQL code implementation (if applicable)"",
    ""codeExplanation"": ""Line-by-line code explanation with reasoning""
}";
    }

    // LEVEL 2: Few-Shot Examples
    private string GetFewShotExamples()
    {
        return @"**EXAMPLE 1: Column Addition**

INPUT:
Table: gwpcDaily.irf_policy
Column: renewal_status_cd VARCHAR(20)
Description: Track renewal status

EXPECTED OUTPUT:

{
    ""summary"": ""This enhancement adds a renewal status code column to improve policy lifecycle tracking and enable better renewal forecasting. The implementation supports automated renewal processing workflows and enhances reporting capabilities for business intelligence."",
    ""enhancement"": ""• **Table:** gwpcDaily.irf_policy\n• **Column:** renewal_status_cd\n• **Data Type:** VARCHAR(20)\n• **Purpose:** Tracks the current renewal status of a policy for reporting and business intelligence\n• **Possible Values:**\n  - **ACTIVE** - Policy actively renewing\n  - **PENDING** - Renewal pending review\n  - **EXPIRED** - Policy expired without renewal\n  - **CANCELLED** - Policy cancelled before renewal"",
    ""benefits"": ""• Enables real-time renewal pipeline tracking\n• Supports automated workflow triggers based on status\n• Improves renewal forecasting accuracy by 15-20%\n• Reduces manual tracking overhead for policy administrators"",
    ""code"": ""ALTER TABLE gwpcDaily.irf_policy \nADD renewal_status_cd VARCHAR(20) NULL;\n\nCREATE INDEX IX_irf_policy_renewal_status \nON gwpcDaily.irf_policy(renewal_status_cd, effective_dt);"",
    ""codeExplanation"": ""• Column added as nullable to allow gradual population\n• Composite index created on renewal_status_cd and effective_dt for performance\n• Index optimizes queries filtering by status within date ranges (common reporting pattern)""
}

---

**EXAMPLE 2: Stored Procedure Enhancement**

INPUT:
Procedure: usp_Calculate_Premium
Change: Add risk_tier parameter
Description: Support tiered pricing

EXPECTED OUTPUT:

{
    ""summary"": ""This enhancement extends the premium calculation procedure to support risk-based tiered pricing, enabling more accurate premium quotes and improving competitive positioning. The implementation maintains backward compatibility while adding granular pricing control."",
    ""enhancement"": ""• **Procedure:** dbo.usp_Calculate_Premium\n• **New Parameter:** @risk_tier VARCHAR(10)\n• **Valid Values:** 'LOW', 'MEDIUM', 'HIGH'\n• **Impact:** Affects premium calculation logic for all new policies\n• **Backward Compatibility:** Default value maintains existing behavior"",
    ""benefits"": ""• Enables risk-based pricing strategies (estimated 8-12% revenue increase)\n• Improves quote accuracy and competitiveness\n• Supports regulatory compliance for risk-based pricing\n• Provides foundation for advanced pricing models"",
    ""code"": ""ALTER PROCEDURE dbo.usp_Calculate_Premium\n    @policy_id INT,\n    @coverage_amount DECIMAL(18,2),\n    @risk_tier VARCHAR(10) = 'MEDIUM'\nAS\nBEGIN\n    DECLARE @tier_multiplier DECIMAL(5,4);\n    \n    SET @tier_multiplier = CASE @risk_tier\n        WHEN 'LOW' THEN 0.85      -- 15% discount\n        WHEN 'MEDIUM' THEN 1.00   -- Standard rate\n        WHEN 'HIGH' THEN 1.25     -- 25% surcharge\n        ELSE 1.00\n    END;\n    \n    RETURN @coverage_amount * @base_rate * @tier_multiplier;\nEND;"",
    ""codeExplanation"": ""• Default parameter value maintains backward compatibility with existing calls\n• CASE statement applies tier-specific multipliers (LOW=0.85, MEDIUM=1.00, HIGH=1.25)\n• Multipliers align with actuarial risk assessments from underwriting department\n• Procedure returns calculated premium directly for use in quoting system""
}

---";
    }

    // LEVEL 3: Chain-of-Thought Prompting with Metadata Context
    private string GetChainOfThoughtPrompt(DocumentationEnhancementRequest request)
    {
        var metadataContext = BuildMetadataContext(request);
        
        return $@"Before generating the final documentation, think through these steps:

**STEP 1: ANALYZE CONTEXT**
- Change type: {request.ChangeType}
- Business domain: {request.BusinessDomain ?? "Unknown"}
- Impact level: {GetImpactLevel(request.DownstreamDependencies)}
- PII classification: {request.DataClassification ?? "Not classified"}
- Current quality score: {request.QualityScore?.ToString("F1") ?? "Unknown"}

**STEP 2: ASSESS IMPACT & IMPORTANCE**
- Downstream dependencies: {request.DownstreamDependencies ?? 0}
- Usage pattern: {request.UsagePattern ?? "Unknown"}
- Existing description quality: {AssessDescriptionQuality(request.ExistingDescription)}
- Business criticality: {AssessBusinessCriticality(request)}

**STEP 3: IDENTIFY KEY POINTS**
- What are the 3-5 most important things to communicate?
- What technical details are critical given the impact level?
- What benefits can be quantified based on usage patterns?
- What risks exist given the dependency count and PII classification?

**STEP 4: STRUCTURE FOR AUDIENCE**
- Primary audience: {GetPrimaryAudience(request)}
- Technical depth needed: {GetRequiredTechnicalDepth(request)}
- Compliance considerations: {GetComplianceConsiderations(request)}

{metadataContext}

**STEP 5: GENERATE DOCUMENTATION**
Now write comprehensive documentation that addresses all the above context.

---

INPUT TO ENHANCE:

**Description:**
{request.Description}

**Implementation:**
{request.Documentation}

**Project Information:**
- CAB Number: {request.CABNumber}
- Jira Ticket: {request.JiraNumber}
- Database Table: {request.Table ?? "Not specified"}
- Column: {request.Column ?? "Not specified"}
- Stored Procedures: {request.ModifiedStoredProcedures ?? "None specified"}

**Metadata Context:**
{metadataContext}

OUTPUT FORMAT:
Return ONLY valid JSON with the required fields (summary, enhancement, benefits, code, codeExplanation). Do not include thinking steps in your output.";
    }

    private string BuildMetadataContext(DocumentationEnhancementRequest request)
    {
        var context = new List<string>();
        
        if (!string.IsNullOrEmpty(request.ExistingDescription))
            context.Add($"**Existing Description:** {request.ExistingDescription}");
            
        if (request.DownstreamDependencies.HasValue)
            context.Add($"**Impact Scope:** {request.DownstreamDependencies} dependent objects");
            
        if (!string.IsNullOrEmpty(request.SemanticCategory))
            context.Add($"**Category:** {request.SemanticCategory}");
            
        if (request.QualityScore.HasValue)
            context.Add($"**Quality Score:** {request.QualityScore:F1}/100");

        return context.Any() ? string.Join("\n", context) : "**No additional metadata available**";
    }

    private string GetImpactLevel(int? dependencies)
    {
        return dependencies switch
        {
            >= 20 => "CRITICAL (20+ dependencies)",
            >= 10 => "HIGH (10+ dependencies)", 
            >= 5 => "MEDIUM (5+ dependencies)",
            _ => "LOW (few dependencies)"
        };
    }

    private string AssessDescriptionQuality(string? description)
    {
        if (string.IsNullOrEmpty(description)) return "No existing description";
        if (description.Length < 50) return "Poor (too brief)";
        if (description.Length < 100) return "Fair";
        return "Good (comprehensive)";
    }

    private string AssessBusinessCriticality(DocumentationEnhancementRequest request)
    {
        var factors = new List<string>();
        
        if (request.DataClassification?.Contains("PII") == true) factors.Add("Contains PII");
        if (request.DownstreamDependencies >= 10) factors.Add("High dependency count");
        if (request.BusinessDomain?.Contains("Finance") == true) factors.Add("Financial domain");
        
        return factors.Any() ? $"HIGH - {string.Join(", ", factors)}" : "STANDARD";
    }

    private string GetPrimaryAudience(DocumentationEnhancementRequest request)
    {
        if (request.DataClassification?.Contains("PII") == true) return "Compliance + Technical teams";
        if (request.DownstreamDependencies >= 15) return "Architecture + Development teams";
        return "Development team";
    }

    private string GetRequiredTechnicalDepth(DocumentationEnhancementRequest request)
    {
        return request.DownstreamDependencies switch
        {
            >= 15 => "DEEP - Include lineage, performance impacts, rollback procedures",
            >= 5 => "MODERATE - Include dependencies, testing requirements",
            _ => "STANDARD - Focus on functionality and immediate impacts"
        };
    }

    private string GetComplianceConsiderations(DocumentationEnhancementRequest request)
    {
        var considerations = new List<string>();
        
        if (request.DataClassification?.Contains("PII") == true)
            considerations.Add("PII handling procedures");
        if (request.DataClassification?.Contains("Sensitive") == true)
            considerations.Add("Data sensitivity protocols");
        
        return considerations.Any() ? string.Join(", ", considerations) : "Standard change management";
    }

    private async Task<EnhancedDocumentation> RetryWithConservativePrompt(
        DocumentationEnhancementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Retrying OpenAI request with conservative prompt for {ChangeType}", request.ChangeType);

            var conservativePrompt = $@"Please improve this documentation and return as JSON:

Description: {request.Description}
Implementation: {request.Documentation}

Return JSON with these fields:
{{
    ""summary"": ""Brief summary"",
    ""enhancement"": ""Improved description"",
    ""benefits"": ""Key benefits"",
    ""code"": ""SQL code if applicable"",
    ""codeExplanation"": ""Code explanation if applicable""
}}";

            var simpleSystemPrompt = "You are a technical writer. Enhance the provided documentation and return valid JSON with the requested fields.";

            var openAIRequest = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = simpleSystemPrompt },
                    new { role = "user", content = conservativePrompt }
                },
                temperature = 0.2f,
                max_tokens = 1000,
                response_format = new { type = "json_object" }
            };

            var azureUrl = $"{_endpoint.TrimEnd('/')}/openai/deployments/{_deployment}/chat/completions?api-version=2023-12-01-preview";
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // Shorter timeout for retry
            
            var response = await _httpClient.PostAsJsonAsync(azureUrl, openAIRequest, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Conservative prompt also failed, falling back to original text");
                return FallbackEnhancement(request);
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken);
            
            if (result?.Choices == null || result.Choices.Length == 0)
            {
                return FallbackEnhancement(request);
            }

            var enhancedText = result.Choices[0].Message.Content;
            return ParseStructuredResponse(enhancedText, request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Conservative prompt retry failed, falling back to original text");
            return FallbackEnhancement(request);
        }
    }

    // LEVEL 4: Structured Response Parsing
    private EnhancedDocumentation ParseStructuredResponse(string responseContent, DocumentationEnhancementRequest request)
    {
        try
        {
            // Parse the structured JSON response
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Try to parse the OpenAI structured response
            var structuredResponse = JsonSerializer.Deserialize<StructuredDocumentationResponse>(responseContent, options);

            if (structuredResponse != null && 
                !string.IsNullOrWhiteSpace(structuredResponse.Summary) &&
                !string.IsNullOrWhiteSpace(structuredResponse.Enhancement))
            {
                var result = new EnhancedDocumentation
                {
                    Summary = structuredResponse.Summary,
                    Enhancement = structuredResponse.Enhancement,
                    Benefits = structuredResponse.Benefits ?? "Enhanced functionality and improved documentation quality.",
                    Code = structuredResponse.Code ?? "-- No specific code implementation provided",
                    CodeExplanation = structuredResponse.CodeExplanation ?? "Implementation details not specified in the change request."
                };

                // Validate quality
                if (IsQualityOutput(result))
                {
                    _logger.LogInformation("High-quality structured response received for {ChangeType}", request.ChangeType);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Structured response quality below threshold, using fallback");
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse structured OpenAI response, attempting legacy parsing");
        }

        // Fallback to legacy parsing or enhancement
        return ParseLegacyResponse(responseContent, request);
    }

    // Validation for quality output
    private bool IsQualityOutput(EnhancedDocumentation doc)
    {
        return doc.Summary.Length >= 100 &&
               doc.Enhancement.Length >= 200 &&
               (doc.Benefits.Contains("%") || doc.Benefits.Contains("enable") || doc.Benefits.Contains("improve")) &&
               doc.CodeExplanation.Length >= 50;
    }

    // Legacy parsing fallback
    private EnhancedDocumentation ParseLegacyResponse(string responseContent, DocumentationEnhancementRequest request)
    {
        try
        {
            // Try legacy format
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var legacyResponse = JsonSerializer.Deserialize<LegacyDocumentationResponse>(responseContent, options);

            if (legacyResponse != null)
            {
                return new EnhancedDocumentation
                {
                    Summary = legacyResponse.EnhancedDescription ?? request.Description,
                    Enhancement = legacyResponse.EnhancedImplementation ?? request.Documentation,
                    Benefits = "Enhanced documentation provides improved clarity and professional presentation.",
                    Code = "-- Implementation details in progress",
                    CodeExplanation = "Code explanation to be added in next iteration."
                };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse legacy response format, using fallback");
        }

        return FallbackEnhancement(request);
    }

    private EnhancedDocumentation FallbackEnhancement(DocumentationEnhancementRequest request)
    {
        return new EnhancedDocumentation
        {
            Summary = $"This {request.ChangeType?.ToLower() ?? "database change"} enhances the {request.Table ?? "database structure"} to support improved functionality and business requirements.",
            Enhancement = $"**Technical Details:**\n• **Change Type:** {request.ChangeType}\n• **Table:** {request.Table ?? "Not specified"}\n• **Column:** {request.Column ?? "Not specified"}\n• **Implementation:** {request.Documentation}",
            Benefits = "• Improves data structure and organization\n• Supports enhanced business processes\n• Provides foundation for future enhancements",
            Code = !string.IsNullOrWhiteSpace(request.ModifiedStoredProcedures) 
                ? $"-- Modified Stored Procedures:\n-- {request.ModifiedStoredProcedures.Replace(",", "\n-- ")}"
                : "-- Implementation details to be provided during technical review",
            CodeExplanation = "Implementation follows standard database change procedures with appropriate testing and validation protocols."
        };
    }

    // Response models for structured parsing
    private class StructuredDocumentationResponse
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("enhancement")]
        public string? Enhancement { get; set; }

        [JsonPropertyName("benefits")]
        public string? Benefits { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("codeExplanation")]
        public string? CodeExplanation { get; set; }
    }

    private class LegacyDocumentationResponse
    {
        [JsonPropertyName("enhancedDescription")]
        public string? EnhancedDescription { get; set; }

        [JsonPropertyName("enhancedImplementation")]
        public string? EnhancedImplementation { get; set; }

        [JsonPropertyName("keyPoints")]
        public List<string>? KeyPoints { get; set; }

        [JsonPropertyName("technicalDetails")]
        public List<string>? TechnicalDetails { get; set; }
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
