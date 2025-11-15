using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Models;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;

namespace Enterprise.Documentation.Core.Application.Services;

/// <summary>
/// Orchestrates document generation pipeline:
/// MasterIndex → Azure OpenAI → Template Data → Node.js → .docx
/// </summary>
public interface IDocGeneratorService
{
    Task<DocumentGenerationResult> GenerateDocumentAsync(
        DocumentGenerationRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentGenerationResult> GenerateFromMasterIndexAsync(
        int masterIndexId,
        CancellationToken cancellationToken = default);
}

public class DocGeneratorService : IDocGeneratorService
{
    private readonly IMasterIndexRepository _masterIndexRepo;
    private readonly ITemplateSelector _templateSelector;
    private readonly INodeJsTemplateExecutor _templateExecutor;
    private readonly ILogger<DocGeneratorService> _logger;
    private readonly OpenAIClient _openAIClient;
    private readonly string _deploymentName;

    public DocGeneratorService(
        IMasterIndexRepository masterIndexRepo,
        ITemplateSelector templateSelector,
        INodeJsTemplateExecutor templateExecutor,
        IConfiguration configuration,
        ILogger<DocGeneratorService> logger)
    {
        _masterIndexRepo = masterIndexRepo;
        _templateSelector = templateSelector;
        _templateExecutor = templateExecutor;
        _logger = logger;

        // Initialize Azure OpenAI client (v2.0 API)
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var apiKey = configuration["AzureOpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
        _deploymentName = configuration["AzureOpenAI:DeploymentName"]
            ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName not configured");

        _openAIClient = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

        _logger.LogInformation("DocGeneratorService initialized with deployment: {Deployment}", _deploymentName);
    }

    /// <summary>
    /// Generates document from MasterIndex entry.
    /// </summary>
    public async Task<DocumentGenerationResult> GenerateFromMasterIndexAsync(
        int masterIndexId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting document generation for MasterIndex ID: {Id}", masterIndexId);

        // Step 1: Get MasterIndex data
        var masterIndex = await _masterIndexRepo.GetByIdAsync(masterIndexId, cancellationToken);
        if (masterIndex == null)
        {
            return new DocumentGenerationResult
            {
                Success = false,
                ErrorMessage = $"MasterIndex entry {masterIndexId} not found"
            };
        }

        // Step 2: Convert to generation request
        var request = new DocumentGenerationRequest
        {
            MasterIndexId = masterIndexId,
            DocumentType = DetermineDocumentType(masterIndex),
            ObjectName = masterIndex.TableName ?? masterIndex.DocumentTitle ?? string.Empty,
            Schema = masterIndex.SchemaName ?? "dbo",
            DatabaseName = masterIndex.DatabaseName ?? "Unknown",
            Author = masterIndex.CreatedBy ?? "System",
            TicketNumber = masterIndex.SourceDocumentID ?? "AUTO"
        };

        return await GenerateDocumentAsync(request, cancellationToken);
    }

    /// <summary>
    /// Generates document from generation request.
    /// Complete pipeline: MasterIndex → OpenAI → Template → Node.js
    /// </summary>
    public async Task<DocumentGenerationResult> GenerateDocumentAsync(
        DocumentGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation(
                "Generating {DocType} for {ObjectName}",
                request.DocumentType,
                request.ObjectName);

            // Step 1: Get MasterIndex metadata
            MasterIndex? masterIndex = null;
            if (request.MasterIndexId.HasValue)
            {
                masterIndex = await _masterIndexRepo.GetByIdAsync(
                    request.MasterIndexId.Value,
                    cancellationToken);
            }

            // Step 2: Determine template complexity
            var complexity = masterIndex != null
                ? _templateSelector.DetermineComplexity(masterIndex)
                : TemplateComplexity.Tier2_Standard;

            // Step 3: Build template data using Azure OpenAI
            object templateData = request.DocumentType switch
            {
                DocumentType.StoredProcedure => await BuildStoredProcedureDataAsync(
                    masterIndex, request, complexity, cancellationToken),
                DocumentType.BusinessRequest => await BuildBusinessRequestDataAsync(
                    masterIndex, request, cancellationToken),
                DocumentType.DefectFix => await BuildDefectFixDataAsync(
                    masterIndex, request, cancellationToken),
                _ => throw new ArgumentException($"Unsupported document type: {request.DocumentType}")
            };

            // Step 4: Select template file
            var templateFileName = _templateSelector.GetTemplateFileName(
                request.DocumentType,
                complexity);

            // Step 5: Generate unique output filename
            var outputFileName = GenerateOutputFileName(request);

            // Step 6: Execute Node.js template
            var result = await _templateExecutor.ExecuteTemplateAsync(
                templateFileName,
                templateData,
                outputFileName,
                cancellationToken);

            // Step 7: Enhance result with metadata
            result.TemplateUsed = complexity;
            result.GenerationTime = DateTime.UtcNow - startTime;
            result.DocumentId = GenerateDocumentId(request);

            if (result.Success)
            {
                _logger.LogInformation(
                    "✅ Document generated successfully: {DocumentId} in {Duration}ms",
                    result.DocumentId,
                    result.GenerationTime.TotalMilliseconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate document for {ObjectName}", request.ObjectName);

            return new DocumentGenerationResult
            {
                Success = false,
                ErrorMessage = $"Document generation failed: {ex.Message}",
                GenerationTime = DateTime.UtcNow - startTime
            };
        }
    }

    // ===== TEMPLATE DATA BUILDERS =====

    /// <summary>
    /// Builds stored procedure template data using MasterIndex + Azure OpenAI.
    /// </summary>
    private async Task<StoredProcedureTemplateData> BuildStoredProcedureDataAsync(
        MasterIndex? masterIndex,
        DocumentGenerationRequest request,
        TemplateComplexity complexity,
        CancellationToken cancellationToken)
    {
        var data = new StoredProcedureTemplateData
        {
            Schema = request.Schema,
            ProcedureName = request.ObjectName,
            Author = request.Author,
            Ticket = request.TicketNumber,
            Created = DateTime.Now.ToString("MM/dd/yyyy"),
            Type = "Stored Procedure"
        };

        // If we have MasterIndex metadata, use it
        if (masterIndex != null)
        {
            data.Purpose = masterIndex.Description ?? "Purpose to be determined";
            data.Type = masterIndex.DocumentType ?? "Data Processing";

            // Parse dependencies
            if (!string.IsNullOrEmpty(masterIndex.UpstreamSources))
            {
                data.Dependencies.SourceTables = masterIndex.UpstreamSources
                    .Split(',')
                    .Select(t => t.Trim())
                    .ToList();
            }

            if (!string.IsNullOrEmpty(masterIndex.DownstreamTargets))
            {
                data.Dependencies.TargetTables = masterIndex.DownstreamTargets
                    .Split(',')
                    .Select(t => t.Trim())
                    .ToList();
            }

            // Use AI-generated insights if available
            if (!string.IsNullOrEmpty(masterIndex.OptimizationSuggestions))
            {
                data.ExecutionLogic.Add($"Optimization: {masterIndex.OptimizationSuggestions}");
            }
        }

        // Use Azure OpenAI to fill gaps and enhance
        await EnhanceWithAIAsync(data, masterIndex, cancellationToken);

        return data;
    }

    /// <summary>
    /// Enhances template data using Azure OpenAI.
    /// </summary>
    private async Task EnhanceWithAIAsync(
        StoredProcedureTemplateData data,
        MasterIndex? masterIndex,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = BuildEnhancementPrompt(data, masterIndex);

            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = _deploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage("You are a database documentation expert. Generate concise, professional documentation content."),
                    new ChatRequestUserMessage(prompt)
                },
                MaxTokens = 2000,
                Temperature = 0.7f
            };

            var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions, cancellationToken);
            var completion = response.Value.Choices[0].Message.Content;

            ParseAIEnhancements(data, completion);

            _logger.LogDebug("AI enhancement completed for {ProcedureName}", data.ProcedureName);
        }
        catch (Exception)
        {
            _logger.LogWarning("AI enhancement failed, using fallback data");
        }
    }

    /// <summary>
    /// Builds AI prompt for documentation enhancement.
    /// </summary>
    private string BuildEnhancementPrompt(StoredProcedureTemplateData data, MasterIndex? masterIndex)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Generate documentation for stored procedure: {data.ProcedureName}");
        prompt.AppendLine($"Schema: {data.Schema}");

        if (masterIndex != null)
        {
            prompt.AppendLine($"Description: {masterIndex.Description}");
            prompt.AppendLine($"Business Domain: {masterIndex.BusinessDomain}");
        }

        prompt.AppendLine();
        prompt.AppendLine("Provide the following in JSON format:");
        prompt.AppendLine("1. purpose: 2-3 sentence description");
        prompt.AppendLine("2. parameters: Array of {name, type, description}");
        prompt.AppendLine("3. executionLogic: Array of 4-6 step descriptions");
        prompt.AppendLine("4. usageExamples: Array of {title, code}");

        return prompt.ToString();
    }

    /// <summary>
    /// Parses AI response and updates template data.
    /// </summary>
    private void ParseAIEnhancements(StoredProcedureTemplateData data, string aiResponse)
    {
        try
        {
            // Try to parse as JSON
            var jsonDoc = JsonDocument.Parse(aiResponse);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("purpose", out var purpose))
            {
                data.Purpose = purpose.GetString() ?? data.Purpose;
            }

            if (root.TryGetProperty("executionLogic", out var logic))
            {
                data.ExecutionLogic = logic.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            // Add more parsing as needed
        }
        catch (JsonException)
        {
            // If not JSON, extract useful text content
            _logger.LogDebug("AI response not in JSON format, using text extraction");

            // Use basic text as purpose if empty
            if (string.IsNullOrEmpty(data.Purpose) || data.Purpose == "Purpose to be determined")
            {
                var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    data.Purpose = lines[0].Trim();
                }
            }
        }
    }

    /// <summary>
    /// Builds business request template data.
    /// </summary>
    private async Task<BusinessRequestTemplateData> BuildBusinessRequestDataAsync(
        MasterIndex? masterIndex,
        DocumentGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var data = new BusinessRequestTemplateData
        {
            Ticket = request.TicketNumber,
            Author = request.Author,
            DateEntered = DateTime.Now.ToString("MM/dd/yyyy"),
            NewTableCreated = $"{request.Schema}.{request.ObjectName}"
        };

        if (masterIndex != null)
        {
            data.BusinessPurpose = masterIndex.Description ?? "Business purpose to be documented";
            data.SourceTables = masterIndex.UpstreamSources ?? "To be determined";
        }

        // Can add AI enhancement here if needed
        await Task.CompletedTask;

        return data;
    }

    /// <summary>
    /// Builds defect fix template data.
    /// </summary>
    private async Task<DefectFixTemplateData> BuildDefectFixDataAsync(
        MasterIndex? masterIndex,
        DocumentGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var data = new DefectFixTemplateData
        {
            Ticket = request.TicketNumber,
            Author = request.Author,
            DateEntered = DateTime.Now.ToString("MM/dd/yyyy"),
            Schema = request.Schema,
            TablesAffected = request.ObjectName
        };

        if (masterIndex != null)
        {
            data.DefectDescription = masterIndex.Description ?? "Defect to be documented";
            data.TablePurpose = masterIndex.UsagePurpose ?? "Purpose to be documented";
        }

        await Task.CompletedTask;

        return data;
    }

    // ===== UTILITY METHODS =====

    private DocumentType DetermineDocumentType(MasterIndex masterIndex)
    {
        var docType = masterIndex.DocumentType?.ToLower();

        return docType switch
        {
            var t when t?.Contains("stored procedure") == true => DocumentType.StoredProcedure,
            var t when t?.Contains("defect") == true => DocumentType.DefectFix,
            var t when t?.Contains("business") == true => DocumentType.BusinessRequest,
            _ => DocumentType.StoredProcedure // Default
        };
    }

    private string GenerateOutputFileName(DocumentGenerationRequest request)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var sanitizedName = SanitizeFileName(request.ObjectName);
        return $"{sanitizedName}_{timestamp}.docx";
    }

    private string GenerateDocumentId(DocumentGenerationRequest request)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd");
        var prefix = request.DocumentType switch
        {
            DocumentType.StoredProcedure => "SP",
            DocumentType.BusinessRequest => "BR",
            DocumentType.DefectFix => "DF",
            _ => "DOC"
        };

        return $"{prefix}-{timestamp}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd('.');
    }
}
