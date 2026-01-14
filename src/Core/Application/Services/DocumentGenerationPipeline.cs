using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.DTOs;
using Models = Enterprise.Documentation.Core.Application.Models;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Application.Services;
using Core.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Enterprise.Documentation.Core.Application.Services;

/// <summary>
/// Orchestrates complete document generation from Excel entry to approval queue.
/// </summary>
public class DocumentGenerationPipeline : Enterprise.Documentation.Core.Application.Interfaces.IDocumentGenerationPipeline
{
    private readonly IExcelSyncService _excelSync;
    private readonly Enterprise.Documentation.Core.Application.Interfaces.ISchemaMetadataService _schemaMetadata;
    private readonly IAzureOpenAIService _aiService;
    private readonly Core.Application.Interfaces.ITierClassifierService _tierClassifier;
    private readonly Core.Application.Interfaces.ITemplateSelector _templateSelector;
    private readonly Core.Application.Interfaces.INodeJsTemplateExecutor _templateExecutor;
    private readonly IDocxCustomPropertiesService _customProperties;
    private readonly Core.Application.Interfaces.IApprovalService _approvalService;
    private readonly IMasterIndexRepository _masterIndex;
    private readonly ILogger<DocumentGenerationPipeline> _logger;

    public DocumentGenerationPipeline(
        IExcelSyncService excelSync,
        Enterprise.Documentation.Core.Application.Interfaces.ISchemaMetadataService schemaMetadata,
        IAzureOpenAIService aiService,
        Core.Application.Interfaces.ITierClassifierService tierClassifier,
        Core.Application.Interfaces.ITemplateSelector templateSelector,
        Core.Application.Interfaces.INodeJsTemplateExecutor templateExecutor,
        IDocxCustomPropertiesService customProperties,
        Core.Application.Interfaces.IApprovalService approvalService,
        IMasterIndexRepository masterIndex,
        ILogger<DocumentGenerationPipeline> logger)
    {
        _excelSync = excelSync;
        _schemaMetadata = schemaMetadata;
        _aiService = aiService;
        _tierClassifier = tierClassifier;
        _templateSelector = templateSelector;
        _templateExecutor = templateExecutor;
        _customProperties = customProperties;
        _approvalService = approvalService;
        _masterIndex = masterIndex;
        _logger = logger;
    }

    public async Task<GenerationResult> GenerateDocumentAsync(
        ExcelChangeEntry entry,
        CancellationToken cancellationToken = default)
    {
        var result = new GenerationResult { EntryId = entry.Id };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Step 1: Extract schema metadata
            _logger.LogInformation("Step 1: Extracting schema metadata for {Object}", entry.ObjectName);
            var metadata = await _schemaMetadata.ExtractMetadataAsync(
                entry.SchemaName ?? "gwpc",
                entry.ObjectName);

            // Step 2: Classify complexity tier
            _logger.LogInformation("Step 2: Classifying complexity tier");
            var tier = await _tierClassifier.ClassifyAsync(
                entry.ChangeType,
                entry.ObjectName,
                new Dictionary<string, object>
                {
                    ["ObjectType"] = metadata.ObjectType ?? string.Empty,
                    ["LineCount"] = metadata.Definition?.Split('\n').Length ?? 0,
                    ["TablesAccessed"] = metadata.ReferencedTables?.Count ?? 0,
                    ["ParameterCount"] = metadata.Parameters?.Count ?? 0,
                    ["HasDynamicSQL"] = metadata.HasDynamicSQL,
                    ["HasCursors"] = metadata.HasCursors,
                    ["HasTransactions"] = metadata.HasExplicitTransactions
                });

            result.Tier = tier;
            result.Model = tier == 1 ? "gpt-4.1" : tier == 2 ? "gpt-4" : "gpt-4-turbo";

            // Step 3: AI Enhancement
            _logger.LogInformation("Step 3: Enhancing with Azure OpenAI ({Model})", result.Model);
            var aiResponse = await _aiService.GenerateDocumentationAsync(
                BuildPrompt(entry, metadata, tier),
                new Dictionary<string, object>
                {
                    ["SystemPrompt"] = GetSystemPrompt(metadata.ObjectType ?? ""),
                    ["JsonMode"] = true,
                    ["Temperature"] = 0.3m,
                    ["MaxTokens"] = tier == 1 ? 4000 : tier == 2 ? 2000 : 1000,
                    ["Model"] = result.Model
                });

            result.TokensUsed = tier == 1 ? 3000 : tier == 2 ? 1500 : 800; // Mock token usage
            result.ConfidenceScore = aiResponse.Length > 500 ? 0.9m : 0.7m; // Mock confidence based on response length

            // Step 4: Select template
            _logger.LogInformation("Step 4: Selecting template");
            var template = await _templateSelector.SelectTemplateAsync(
                entry.DocumentType,
                tier,
                new Dictionary<string, object>
                {
                    ["entry"] = entry,
                    ["tier"] = tier,
                    ["aiResponse"] = aiResponse
                });

            // Step 5: Generate document
            _logger.LogInformation("Step 5: Generating document with Node.js template");
            var docResult = await _templateExecutor.ExecuteAsync(
                template,
                new Dictionary<string, object>
                {
                    ["entry"] = entry,
                    ["metadata"] = metadata,
                    ["ObjectName"] = metadata.ObjectName ?? string.Empty,
                    ["ObjectType"] = metadata.ObjectType ?? string.Empty,
                    ["SchemaName"] = metadata.SchemaName ?? string.Empty,
                    ["Description"] = metadata.Description ?? string.Empty,
                    ["OutputPath"] = GetOutputPath(entry),
                    ["DocumentType"] = entry.ChangeType,
                    ["Tier"] = tier
                });

            if (string.IsNullOrEmpty(docResult))
            {
                throw new InvalidOperationException("Document generation failed: empty result");
            }

            result.DocumentPath = GetOutputPath(entry);

            // Step 6: Add custom properties
            _logger.LogInformation("Step 6: Adding custom properties to document");
            if (result.DocumentPath != null)
            {
                await _customProperties.SetPropertiesAsync(result.DocumentPath, new DocumentCustomProperties
                {
                    MasterIndexId = entry.MasterIndexId,
                    DocumentType = entry.ChangeType,
                    ObjectName = entry.ObjectName,
                    SchemaName = entry.SchemaName,
                    DatabaseName = entry.DatabaseName ?? "IRFS1",
                    GeneratedAt = DateTime.UtcNow,
                    AIModel = result.Model,
                    TokensUsed = result.TokensUsed,
                    ConfidenceScore = result.ConfidenceScore,
                    Tier = tier,
                    SyncStatus = "DRAFT",
                    ContentHash = ComputeContentHash(aiResponse),
                    PIIIndicator = metadata.ContainsPII,
                    DataClassification = metadata.DataClassification,
                    BusinessDomain = metadata.BusinessDomain
                }, cancellationToken);
            }

            // Step 7: Queue for approval
            _logger.LogInformation("Step 7: Queuing for approval");
            var approvalId = await _approvalService.CreateApprovalRequestAsync(
                new CreateApprovalRequest
                {
                    DocumentPath = result.DocumentPath ?? string.Empty,
                    DocumentType = entry.ChangeType,
                    ObjectName = entry.ObjectName,
                    RequestedBy = entry.CreatedBy ?? "system",
                    Priority = DeterminePriority(metadata, tier),
                    DueDate = DateTime.UtcNow.AddHours(GetSLAHours(tier)),
                    Metadata = new Models.ApprovalMetadata
                    {
                        Tier = tier,
                        ConfidenceScore = result.ConfidenceScore,
                        AIModel = result.Model,
                        PIIDetected = metadata.ContainsPII,
                        TablesAffected = metadata.ReferencedTables?.Count ?? 0
                    }
                });

            result.ApprovalId = approvalId;
            result.Success = true;
            result.Status = "PendingApproval";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document generation failed for entry {EntryId}", entry.Id);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Status = "Failed";
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task<bool> ValidateRequestAsync(ExcelChangeEntry entry, CancellationToken cancellationToken = default)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(entry.ObjectName))
            return false;

        if (string.IsNullOrWhiteSpace(entry.ChangeType))
            return false;

        // Check if we have required services
        try
        {
            var metadata = await _schemaMetadata.ExtractMetadataAsync(
                entry.SchemaName ?? "gwpc", 
                entry.ObjectName);
            
            return !string.IsNullOrWhiteSpace(metadata.ObjectName);
        }
        catch
        {
            return false;
        }
    }

    private string BuildPrompt(ExcelChangeEntry entry, Enterprise.Documentation.Core.Domain.Entities.SchemaMetadata metadata, int tier)
    {
        // Tier-specific prompt building
        return tier switch
        {
            1 => BuildTier1Prompt(entry, metadata),  // Comprehensive
            2 => BuildTier2Prompt(entry, metadata),  // Standard
            _ => BuildTier3Prompt(entry, metadata)   // Lightweight
        };
    }

    private string BuildTier1Prompt(ExcelChangeEntry entry, Enterprise.Documentation.Core.Domain.Entities.SchemaMetadata metadata)
    {
        return $@"
Generate comprehensive documentation for this database object.

## Object Information
- Name: {metadata.SchemaName}.{metadata.ObjectName}
- Type: {metadata.ObjectType}
- Database: {metadata.DatabaseName}

## Change Context
- Change Type: {entry.ChangeType}
- Jira: {entry.JiraNumber}
- Description: {entry.Description}

## Definition
```sql
{metadata.Definition}
```

## Tables Accessed
{string.Join("\n", metadata.ReferencedTables?.Select(t => $"- {t.Schema}.{t.Name}") ?? Array.Empty<string>())}

## Parameters
{string.Join("\n", metadata.Parameters?.Select(p => $"- @{p.Name} ({p.DataType})") ?? Array.Empty<string>())}

## Required Output (JSON)
{{
  ""purpose"": ""[Detailed business purpose]"",
  ""technicalSummary"": ""[Technical implementation overview]"",
  ""parameters"": [{{""name"": """", ""purpose"": """", ""validValues"": """", ""required"": true}}],
  ""businessLogic"": [""[Step-by-step logic explanation]""],
  ""tablesAccessed"": [{{""table"": """", ""operation"": """", ""purpose"": """"}}],
  ""errorHandling"": ""[Error handling description]"",
  ""performanceNotes"": ""[Performance considerations]"",
  ""securityNotes"": ""[Security implications]"",
  ""dependencies"": [""[Upstream/downstream dependencies]""],
  ""exampleUsage"": ""[Example call with parameters]"",
  ""confidenceScore"": 0.85
}}
";
    }

    private string BuildTier2Prompt(ExcelChangeEntry entry, Enterprise.Documentation.Core.Domain.Entities.SchemaMetadata metadata)
    {
        return $@"
Generate standard documentation for this database object.

- Name: {metadata.SchemaName}.{metadata.ObjectName}
- Type: {metadata.ObjectType}
- Change: {entry.ChangeType} - {entry.Description}

Definition (summary):
{TruncateDefinition(metadata.Definition, 2000)}

Output JSON with: purpose, technicalSummary, parameters, businessLogic, tablesAccessed, confidenceScore
";
    }

    private string BuildTier3Prompt(ExcelChangeEntry entry, Enterprise.Documentation.Core.Domain.Entities.SchemaMetadata metadata)
    {
        return $@"
Generate brief documentation for {metadata.SchemaName}.{metadata.ObjectName}.
Type: {metadata.ObjectType}
Change: {entry.Description}

Output JSON: {{""purpose"": """", ""summary"": """", ""confidenceScore"": 0.85}}
";
    }

    private string GetSystemPrompt(string objectType)
    {
        return objectType?.ToLower() switch
        {
            "storedprocedure" => "You are a database documentation expert specializing in stored procedures. Focus on business logic, parameters, and data flow.",
            "function" => "You are a database documentation expert specializing in functions. Focus on calculations, return values, and usage patterns.",
            "view" => "You are a database documentation expert specializing in views. Focus on data sources, filtering logic, and intended usage.",
            "table" => "You are a database documentation expert specializing in tables. Focus on data structure, relationships, and business context.",
            _ => "You are a database documentation expert. Provide clear, accurate technical documentation."
        };
    }

    private string GetOutputPath(ExcelChangeEntry entry)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{entry.ChangeType}-{entry.ObjectName}-{timestamp}.docx";
        return Path.Combine("C:\\Temp\\Documentation-Catalog", fileName);
    }

    private int GetSLAHours(int tier) => tier switch
    {
        1 => 24,   // Complex = 24 hours
        2 => 48,   // Standard = 48 hours  
        3 => 72,   // Simple = 72 hours
        _ => 48
    };

    private string DeterminePriority(SchemaMetadata metadata, int tier)
    {
        if (metadata.ContainsPII) return "urgent";
        if (tier == 1) return "high";
        if (metadata.ReferencedTables?.Count > 10) return "high";
        return "medium";
    }

    private string TruncateDefinition(string? definition, int maxLength)
    {
        if (string.IsNullOrEmpty(definition)) return "";
        if (definition.Length <= maxLength) return definition;
        return definition.Substring(0, maxLength) + "\n-- [TRUNCATED]";
    }

    private string ComputeContentHash(object content)
    {
        var json = JsonSerializer.Serialize(content);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(bytes).Substring(0, 16);
    }
}

/// <summary>
/// Result of document generation pipeline execution.
/// </summary>
public class GenerationResult
{
    public int EntryId { get; set; }
    public bool Success { get; set; }
    public string? DocumentPath { get; set; }
    public Guid? ApprovalId { get; set; }
    public int Tier { get; set; }
    public string? Model { get; set; }
    public int TokensUsed { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public long ProcessingTimeMs { get; set; }
}