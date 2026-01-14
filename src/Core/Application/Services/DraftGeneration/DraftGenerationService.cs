using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Enterprise.Documentation.Core.Domain.Models;
using Enterprise.Documentation.Core.Application.Services.CodeExtraction;
using Enterprise.Documentation.Core.Application.Services.Quality;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration.Templates;
using Enterprise.Documentation.Core.Application.Services.SqlAnalysis;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using System.Linq;

using Enterprise.Documentation.Core.Application.Services.Metadata;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Extensions;



namespace Enterprise.Documentation.Core.Application.Services.DraftGeneration;

/// <summary>
/// Service that generates draft documentation by combining change data, code extraction, and quality analysis.
/// Integrates with SQL analysis and AI enhancement for high-quality documentation.
/// </summary>

public class DraftGenerationService : IDraftGenerationService
{
    private readonly ILogger<DraftGenerationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ITemplateSelector _templateSelector;
    private readonly ISqlAnalysisService _sqlAnalysisService;
    private readonly IMetadataExtractionService _metadataExtractionService;
    private readonly IMasterIndexPersistenceService _masterIndexPersistence;
    private readonly IDocumentGenerationService _documentGenerationService;
    private readonly OpenAIClient _openAIClient;
    private readonly string _deploymentName;



    public DraftGenerationService(
        ILogger<DraftGenerationService> logger,
        IConfiguration configuration,
        ITemplateSelector templateSelector,
        ISqlAnalysisService sqlAnalysisService,
        IMetadataExtractionService metadataExtractionService,
        IMasterIndexPersistenceService masterIndexPersistence,
        IDocumentGenerationService documentGenerationService)
    {
        _logger = logger;
        _configuration = configuration;
        _templateSelector = templateSelector;
        _sqlAnalysisService = sqlAnalysisService;
        _metadataExtractionService = metadataExtractionService;
        _masterIndexPersistence = masterIndexPersistence;
        _documentGenerationService = documentGenerationService;

        // Initialize Azure OpenAI client
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI Endpoint not configured");
        var apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("Azure OpenAI ApiKey not configured");
        _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4.1";
        _openAIClient = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }
	
	

    public async Task<DraftGenerationResult> GenerateDraftAsync(
        string docId,
        DocumentChangeEntry changeEntry,
        CodeExtractionResult? codeResult,
        CodeQualityResult? qualityResult,
        CancellationToken ct = default)
    {
        var result = new DraftGenerationResult();
        try
        {
            _logger.LogInformation("Starting draft generation for DocId: {DocId}", docId);

            // Map to internal DTO for backward compatibility
            var changeData = MapToChangeData(changeEntry);

            var documentType = DetermineDocumentType(changeData);
            _logger.LogDebug("Determined document type: {DocumentType}", documentType);
            result.DocumentType = documentType;

            var template = await SelectTemplateAsync(documentType, changeData, ct);
            if (template == null)
            {
                result.ErrorMessage = $"No suitable template found for document type: {documentType}";
                _logger.LogWarning("Template selection failed for DocId: {DocId}, Type: {DocumentType}", docId, documentType);
                return result;
            }

            result.TemplateUsed = template.GetType().Name;
            _logger.LogDebug("Selected template: {TemplateName}", result.TemplateUsed);

            // Build base template data
            var templateData = PrepareTemplateData(changeData, codeResult, qualityResult);

            // Analyze SQL if code is present
            SqlAnalysisResult? sqlAnalysis = null;
            if (!string.IsNullOrEmpty(codeResult?.ExtractedCode))
            {
                _logger.LogInformation("Analyzing SQL structure");
            sqlAnalysis = _sqlAnalysisService.AnalyzeSql(
			codeResult.ExtractedCode, 
			codeResult.JiraNumber);
            }

            // AI Enhancement (placeholder for now - will be implemented)
            var aiEnhanced = await EnrichWithAIAsync(templateData, sqlAnalysis, ct);


            // Merge all data sources
            templateData = new Dictionary<string, object>
            {
                ["ChangeEntry"] = changeEntry,
                ["SqlAnalysis"] = sqlAnalysis ?? new SqlAnalysisResult(),
                ["AiEnhanced"] = aiEnhanced,
                // Add template-expected fields (matching Python template keys)
                ["doc_id"] = changeEntry.DocId ?? docId ?? "UNKNOWN",
                ["jira"] = changeEntry.JiraNumber ?? "", // Template expects 'jira' not 'jira_number'
                ["jira_number"] = changeEntry.JiraNumber ?? "", // Keep both for compatibility
                ["table"] = changeEntry.TableName ?? "", // Template expects 'table'
                ["table_name"] = changeEntry.TableName ?? "", // Keep both for compatibility
                ["column"] = changeEntry.ColumnName ?? "", // Template expects 'column'
                ["column_name"] = changeEntry.ColumnName ?? "", // Keep both for compatibility
                ["description"] = changeEntry.Description ?? "",
                ["change_type"] = changeEntry.ChangeType ?? "",
                ["priority"] = changeEntry.Priority ?? "",
                ["status"] = changeEntry.Status ?? "",
                ["assigned_to"] = changeEntry.AssignedTo ?? "",
                ["reported_by"] = changeEntry.ReportedBy ?? changeEntry.AssignedTo ?? "", // Template expects this
                ["date"] = changeEntry.Date?.ToString("yyyy-MM-dd") ?? "",
                // Add fields that templates expect but may be missing
                ["summary"] = GetAiValue(aiEnhanced, "Purpose") ?? changeEntry.Description ?? "",
                ["data_type"] = "VARCHAR(50)", // Default, could be enhanced
                ["values"] = "Various", // Default, could be enhanced
                ["purpose"] = GetAiValue(aiEnhanced, "Purpose") ?? "",
                ["enhancement"] = GetAiValue(aiEnhanced, "WhatsNew") ?? changeEntry.Description ?? "",
                ["benefits"] = GetAiValue(aiEnhanced, "BusinessImpact") ?? "",
                ["code"] = sqlAnalysis?.BracketedChange?.Code ?? "",
                ["code_explain"] = GetAiValue(aiEnhanced, "TechnicalSummary") ?? "",
                // Add fields for StoredProcedure template (use best available data)
                ["schema"] = "dbo", // Default since SchemaName not available
                ["sp_name"] = changeEntry.ModifiedStoredProcedures?.Split(',').FirstOrDefault()?.Trim() ?? "unknown_procedure"
            };

            // ✅ EXTRACT METADATA FOR MASTER INDEX
            MasterIndexMetadata? metadata = null;
            try
            {
                _logger.LogInformation("Extracting metadata for {DocId}", docId);
                metadata = await _metadataExtractionService.ExtractMetadataAsync(templateData, ct);
                // Store metadata in result for later use
                result.Metadata["MasterIndexMetadata"] = metadata;
                _logger.LogInformation("Metadata extracted successfully: {DomainTags} domain tags, {Keywords} keywords",
                    metadata.DomainTags?.Count ?? 0, metadata.Keywords?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Metadata extraction failed, continuing without it");
            }



            // ✅ SAVE METADATA TO DATABASE
            if (metadata != null)
            {
                try
                {
                    _logger.LogInformation("Saving metadata to MasterIndex database for {DocId}", docId);
                    var indexId = await _masterIndexPersistence.SaveMetadataAsync(metadata, ct);
                    result.Metadata["IndexID"] = indexId;
                    _logger.LogInformation("Metadata saved to MasterIndex with IndexID: {IndexId}", indexId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save metadata to database, continuing without persistence");
                }
            }

            // ✅ GENERATE ACTUAL .DOCX DOCUMENT
            if (metadata != null)
            {
                try
                {
                    _logger.LogInformation("Generating .docx document for {DocId}", docId);
                    var docResult = await _documentGenerationService.GenerateDocumentAsync(templateData, metadata, ct);
                    if (docResult.Success)
                    {
                        result.Metadata["GeneratedFilePath"] = docResult.GeneratedFilePath ?? "";
                        result.Metadata["FileName"] = docResult.FileName ?? "";
                        result.Metadata["FileSize"] = docResult.FileSize;
                        _logger.LogInformation("Document generated successfully: {FileName} ({FileSize} bytes)", docResult.FileName, docResult.FileSize);
                        // Update database with document path
                        await _masterIndexPersistence.UpdateDocumentPathAsync(
                            docId ?? "UNKNOWN",
                            docResult.GeneratedFilePath!,
                            docResult.GeneratedFilePath!, // URL same as path for local files
                            ct);
                    }
                    else
                    {
                        _logger.LogWarning("Document generation failed: {Error}", docResult.ErrorMessage);
                        result.Warnings.Add($"Document generation failed: {docResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate document, continuing without it");
                    result.Warnings.Add($"Document generation error: {ex.Message}");
                }
            }

            // Generate draft content
            var draftContent = await GenerateContentAsync(template, templateData, ct);

            // Post-process and validate
            result.DraftContent = PostProcessContent(draftContent, changeEntry, qualityResult);
            result.DocumentUrl = GenerateDocumentUrl(docId ?? "UNKNOWN", documentType);

            // Add metadata
            PopulateMetadata(result, changeEntry, codeResult, qualityResult);

            // Quality warnings
            AddQualityWarnings(result, qualityResult);

            result.Success = true;
            _logger.LogInformation("Draft generation completed successfully for DocId: {DocId}", docId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Draft generation failed for DocId: {DocId}", docId);
            result.ErrorMessage = $"Draft generation failed: {ex.Message}";
            return result;
        }
    }

    private ChangeData MapToChangeData(DocumentChangeEntry entry)
    {
        return new ChangeData
        {
            DocId = entry.DocId ?? "",
            JiraNumber = entry.JiraNumber ?? "",
            TableName = entry.TableName,
            ColumnName = entry.ColumnName,
            StoredProcedureName = entry.ModifiedStoredProcedures,
            Description = entry.Description,
            AssignedTo = entry.AssignedTo,
            ChangeType = entry.ChangeType,
            Priority = entry.Priority,
            Severity = entry.Severity
        };
    }

private string DetermineColumnDataType(DocumentChangeEntry changeEntry, SqlAnalysisResult? sqlAnalysis)
{
    if (!string.IsNullOrEmpty(changeEntry.ColumnName))
    {
        var columnName = changeEntry.ColumnName;
        
        var matchingParam = sqlAnalysis?.Parameters?
            .FirstOrDefault(p => p.Name.Contains(columnName, StringComparison.OrdinalIgnoreCase));
        
        if (matchingParam != null && !string.IsNullOrEmpty(matchingParam.Type))
            return matchingParam.Type;
        
        if (columnName.Contains("_ind", StringComparison.OrdinalIgnoreCase))
            return "CHAR(1)";
        if (columnName.Contains("_cd", StringComparison.OrdinalIgnoreCase))
            return "VARCHAR(20)";
        if (columnName.Contains("_dt", StringComparison.OrdinalIgnoreCase) || 
            columnName.Contains("_date", StringComparison.OrdinalIgnoreCase))
            return "DATE";
        if (columnName.Contains("_ts", StringComparison.OrdinalIgnoreCase))
            return "DATETIME";
        if (columnName.Contains("_amt", StringComparison.OrdinalIgnoreCase))
            return "DECIMAL(18,2)";
        if (columnName.Contains("_id", StringComparison.OrdinalIgnoreCase))
            return "INT";
    }
    
    return "VARCHAR(255)";
}

private string ExtractPossibleValues(DocumentChangeEntry changeEntry, SqlAnalysisResult? sqlAnalysis)
{
    var columnName = changeEntry.ColumnName ?? "";
    
    if (columnName.Contains("_ind", StringComparison.OrdinalIgnoreCase))
    {
        return "Y = Yes/True/Active\nN = No/False/Inactive";
    }
    
    return "See code implementation for valid values";
}

private string BuildExecutiveSummary(
    DocumentChangeEntry changeEntry, 
    SqlAnalysisResult? sqlAnalysis, 
    Dictionary<string, object> aiEnhanced)
{
    var aiPurpose = aiEnhanced.GetValueOrDefault("Purpose", "") as string;
    
    if (!string.IsNullOrEmpty(aiPurpose) && aiPurpose.Length > 100)
        return aiPurpose;
    
    var parts = new List<string>();
    var changeType = changeEntry.ChangeType ?? "update";
    var objectType = string.IsNullOrEmpty(changeEntry.ColumnName) ? "stored procedure" : "column";
    var objectName = !string.IsNullOrEmpty(changeEntry.ColumnName) 
        ? $"{changeEntry.TableName}.{changeEntry.ColumnName}"
        : changeEntry.ModifiedStoredProcedures ?? "database object";
    
    parts.Add($"This {changeType.ToLower()} modifies the {objectName} {objectType} to improve system functionality.");
    
    if (sqlAnalysis?.Complexity != null)
    {
        var complexity = sqlAnalysis.Complexity?.ComplexityLevel ?? "";
        if (!string.IsNullOrEmpty(complexity))
        {
            var metrics = GetComplexityMetrics(sqlAnalysis);
            parts.Add($"The implementation involves {complexity.ToLower()} complexity processing with {metrics}.");
        }
    }
    
    return string.Join(" ", parts);
}

private string BuildEnhancementDescription(
    DocumentChangeEntry changeEntry,
    SqlAnalysisResult? sqlAnalysis,
    Dictionary<string, object> aiEnhanced)
{
    var whatsNew = aiEnhanced.GetValueOrDefault("WhatsNew", "") as string;
    
    if (!string.IsNullOrEmpty(whatsNew) && whatsNew.Length > 50)
        return whatsNew;
    
    var desc = new System.Text.StringBuilder();
    desc.AppendLine("**Primary Enhancement:**");
    desc.AppendLine(changeEntry.Description ?? "Enhancement to improve system functionality.");
    desc.AppendLine();
    
    if (sqlAnalysis?.BracketedChange != null)
    {
        desc.AppendLine("**Key Changes:**");
        desc.AppendLine($"- Modified {sqlAnalysis.BracketedChange.EndLine - sqlAnalysis.BracketedChange.StartLine + 1} lines of code");
        desc.AppendLine($"- JIRA Reference: {changeEntry.JiraNumber}");
    }
    
    return desc.ToString();
}

private string BuildBusinessBenefits(
    DocumentChangeEntry changeEntry,
    SqlAnalysisResult? sqlAnalysis,
    Dictionary<string, object> aiEnhanced)
{
    var aiImpact = aiEnhanced.GetValueOrDefault("BusinessImpact", "") as string;
    
    if (!string.IsNullOrEmpty(aiImpact) && aiImpact.Length > 50)
        return aiImpact;
    
    var benefits = new System.Text.StringBuilder();
    benefits.AppendLine("**Operational Benefits:**");
    benefits.AppendLine("- Improved data accuracy and reliability");
    benefits.AppendLine("- Enhanced system performance");
    benefits.AppendLine();
    
    benefits.AppendLine("**Business Benefits:**");
    benefits.AppendLine("- More accurate reporting and analytics");
    benefits.AppendLine("- Better compliance with business rules");
    
    return benefits.ToString();
}

private string BuildCodeExplanation(
    DocumentChangeEntry changeEntry,
    SqlAnalysisResult? sqlAnalysis,
    Dictionary<string, object> aiEnhanced)
{
    var aiTechnical = aiEnhanced.GetValueOrDefault("TechnicalSummary", "") as string;
    
    if (!string.IsNullOrEmpty(aiTechnical) && aiTechnical.Length > 100)
        return aiTechnical;
    
    var explanation = new System.Text.StringBuilder();
    
    if (sqlAnalysis?.LogicSteps != null && sqlAnalysis.LogicSteps.Any())
    {
        explanation.AppendLine("**Implementation Steps:**");
        explanation.AppendLine();
        
        for (int i = 0; i < sqlAnalysis.LogicSteps.Count; i++)
        {
            explanation.AppendLine($"**Step {i + 1}:** {sqlAnalysis.LogicSteps[i]}");
        }
    }
    
    return explanation.ToString();
}

private string ExtractRootCause(DocumentChangeEntry changeEntry, Dictionary<string, object> aiEnhanced)
{
    var description = changeEntry.Description ?? "";
    
    if (description.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
        description.Contains("bug", StringComparison.OrdinalIgnoreCase))
    {
        return $"Root cause identified in {changeEntry.JiraNumber}: {description}";
    }
    
    return aiEnhanced.GetValueOrDefault("ErrorHandling", "Technical analysis in progress") as string ?? "Analysis pending";
}

private string FormatCodeBlock(SqlAnalysisResult? sqlAnalysis)
{
    if (sqlAnalysis?.BracketedChange != null)
    {
        var code = sqlAnalysis.BracketedChange.Code;
        
        if (!code.Contains("BEGIN") && !code.Contains("--"))
        {
            var header = $"-- Extracted from {sqlAnalysis.Schema}.{sqlAnalysis.ProcedureName}\n";
            header += $"-- {sqlAnalysis.BracketedChange.EndLine - sqlAnalysis.BracketedChange.StartLine + 1} lines of code\n\n";
            return header + code;
        }
        
        return code;
    }
    
    return "-- No code changes detected";
}

private string FormatParameters(List<ParameterInfo>? parameters)
{
    if (parameters == null || !parameters.Any())
        return "No parameters";
    
    return string.Join("\n", parameters.Select(p => $"{p.Name} ({p.Type})"));
}

private string FormatLogicFlow(List<string>? logicSteps)
{
    if (logicSteps == null || !logicSteps.Any())
        return "See code for details";
    
    return string.Join("\n", logicSteps.Select((step, i) => $"{i + 1}. {step}"));
}

private string FormatPerformanceNotes(List<string>? notes)
{
    if (notes == null || !notes.Any())
        return "Standard performance";
    
    return string.Join("\n", notes.Select(note => $"- {note}"));
}

private string FormatComplexity(Dictionary<string, string>? complexity)
{
    if (complexity == null || !complexity.Any())
        return "Standard complexity";
    
    return string.Join("\n", complexity.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
}

private string GetComplexityMetrics(SqlAnalysisResult sqlAnalysis)
{
    var metrics = new List<string>();
    
    if (sqlAnalysis.Complexity?.LineCount > 0)
        metrics.Add($"{sqlAnalysis.Complexity.LineCount} lines");
    
    if (sqlAnalysis.Complexity?.JoinCount > 0)
        metrics.Add($"{sqlAnalysis.Complexity.JoinCount} joins");
    
    return metrics.Any() ? string.Join(", ", metrics) : "standard processing";
}








    private string DetermineVersion(DocumentChangeEntry changeEntry, BracketedChange? bracketedChange)
    {
        // If bracketed change detected, it's a version update
        if (bracketedChange != null)
            return "2.0"; // TODO: Implement proper version incrementing
        
        return "1.0";
    }

    private double CalculateConfidence(SqlAnalysisResult? sqlAnalysis, Dictionary<string, object> aiEnhanced)
    {
        int total = 0, filled = 0;
        var keys = new[] { "purpose", "parameters", "logic", "dependencies", "complexity", "performance", "error_handling" };
        
        foreach (var k in keys)
        {
            total++;
            if (aiEnhanced.ContainsKey(k) && aiEnhanced[k] != null) filled++;
        }
        
        if (sqlAnalysis != null)
        {
            total += 4;
            if (sqlAnalysis.Parameters?.Count > 0) filled++;
            if (sqlAnalysis.Dependencies?.Tables?.Count > 0) filled++;
            if (sqlAnalysis.Complexity != null) filled++;
            if (sqlAnalysis.LogicSteps?.Count > 0) filled++;
        }
        
        return total == 0 ? 0.0 : Math.Round((double)filled / total, 2);
    }

    private List<string> ExtractKeywords(DocumentChangeEntry changeEntry, SqlAnalysisResult? sqlAnalysis)
    {
        var keywords = new List<string>();
        
        if (!string.IsNullOrEmpty(changeEntry.Description))
            keywords.AddRange(changeEntry.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        
        if (sqlAnalysis?.Dependencies?.Tables != null)
            keywords.AddRange(sqlAnalysis.Dependencies.Tables);
        
        if (!string.IsNullOrEmpty(changeEntry.TableName))
            keywords.Add(changeEntry.TableName);
        
        if (!string.IsNullOrEmpty(changeEntry.ColumnName))
            keywords.Add(changeEntry.ColumnName);
        
        return keywords.Distinct().ToList();
    }

    private string DetermineDocType(DocumentChangeEntry changeEntry)
    {
        if (!string.IsNullOrEmpty(changeEntry.ChangeType))
        {
            if (changeEntry.ChangeType.Contains("QA", StringComparison.OrdinalIgnoreCase)) return "QA";
            if (changeEntry.ChangeType.Contains("Defect", StringComparison.OrdinalIgnoreCase)) return "Defect";
            if (changeEntry.ChangeType.Contains("Enhancement", StringComparison.OrdinalIgnoreCase)) return "Enhancement";
            if (changeEntry.ChangeType.Contains("Business", StringComparison.OrdinalIgnoreCase)) return "BusinessRequest";
        }
        
        if (!string.IsNullOrEmpty(changeEntry.ModifiedStoredProcedures)) return "SP";
        
        return "Unknown";
    }

    private string ExtractSchemaFromProcName(string? procName)
    {
        if (string.IsNullOrEmpty(procName)) return "";
        var parts = procName.Split('.');
        return parts.Length > 1 ? parts[0] : "";
    }

    private string ExtractProcNameOnly(string? procName)
    {
        if (string.IsNullOrEmpty(procName)) return "";
        var parts = procName.Split('.');
        return parts.Length > 1 ? parts[1] : parts[0];
    }

    private string GenerateUsageExample(DocumentChangeEntry changeEntry, SqlAnalysisResult? sqlAnalysis)
    {
        if (sqlAnalysis == null || sqlAnalysis.Parameters == null || !sqlAnalysis.Parameters.Any())
            return $"EXEC {changeEntry.ModifiedStoredProcedures};";
        
        var paramExamples = sqlAnalysis.Parameters
            .Select(p => $"{p.Name} = {GetExampleValue(p.Type)}")
            .ToList();
        
        return $"EXEC {changeEntry.ModifiedStoredProcedures}\n    {string.Join(",\n    ", paramExamples)};";
    }

    private string GetExampleValue(string type)
    {
        if (type.Contains("INT", StringComparison.OrdinalIgnoreCase)) return "12345";
        if (type.Contains("VARCHAR", StringComparison.OrdinalIgnoreCase) || type.Contains("CHAR", StringComparison.OrdinalIgnoreCase)) return "'ExampleValue'";
        if (type.Contains("DATE", StringComparison.OrdinalIgnoreCase)) return "'2025-12-19'";
        if (type.Contains("BIT", StringComparison.OrdinalIgnoreCase)) return "1";
        if (type.Contains("DECIMAL", StringComparison.OrdinalIgnoreCase)) return "100.00";
        return "NULL";
    }

    private List<Dictionary<string, object>> BuildRecentChanges(DocumentChangeEntry changeEntry, BracketedChange? bracketedChange)
    {
        var changes = new List<Dictionary<string, object>>();
        
        if (bracketedChange != null)
        {
            changes.Add(new Dictionary<string, object>
            {
                ["version"] = "2.0",
                ["date"] = changeEntry.Date?.ToString("MM/dd/yyyy") ?? DateTime.Now.ToString("MM/dd/yyyy"),
                ["author"] = changeEntry.ReportedBy ?? "Unknown",
                ["summary"] = changeEntry.Description ?? "Update",
                ["ref"] = bracketedChange.Ticket,
                ["details"] = $"Modified code block (lines {bracketedChange.StartLine}-{bracketedChange.EndLine})"
            });
        }
        else
        {
            changes.Add(new Dictionary<string, object>
            {
                ["version"] = "1.0",
                ["date"] = changeEntry.Date?.ToString("MM/dd/yyyy") ?? DateTime.Now.ToString("MM/dd/yyyy"),
                ["author"] = changeEntry.ReportedBy ?? "Unknown",
                ["summary"] = changeEntry.Description ?? "Initial version",
                ["ref"] = changeEntry.JiraNumber ?? "Initial Release",
                ["details"] = ""
            });
        }
        
        return changes;
    }

    private string DetermineDocumentType(ChangeData changeData)
    {
        if (!string.IsNullOrEmpty(changeData.ChangeType))
        {
            if (changeData.ChangeType.Contains("Enhancement", StringComparison.OrdinalIgnoreCase)) return "EN";
            if (changeData.ChangeType.Contains("Defect", StringComparison.OrdinalIgnoreCase)) return "DF";
            if (changeData.ChangeType.Contains("Bug", StringComparison.OrdinalIgnoreCase)) return "DF";
            return "BR";
        }
        
        if (!string.IsNullOrEmpty(changeData.StoredProcedureName))
        {
            _logger.LogInformation("Using SP template for {SPName}", changeData.StoredProcedureName);
            return "SP";
        }
        
        if (!string.IsNullOrEmpty(changeData.TableName))
        {
            return "TB";
        }
        
        return "BR";
    }

    private async Task<IDocumentTemplate?> SelectTemplateAsync(
        string documentType,
        ChangeData changeData,
        CancellationToken ct)
    {
        try
        {
            var templateName = documentType switch
            {
                "SP" => "StoredProcedureTemplate",
                "EN" => "EnhancementTemplate",
                "DF" => "DefectTemplate",
                _ => "BusinessRuleTemplate"
            };

            _logger.LogInformation("Using {TemplateName} template for document type: {DocumentType}", templateName, documentType);
            return await _templateSelector.SelectTemplateAsync(templateName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Template selection failed, using fallback logic");
            return documentType switch
            {
                "SP" => new StoredProcedureTemplate(),
                _ => null
            };
        }
    }

    private Dictionary<string, object> PrepareTemplateData(
        ChangeData changeData,
        CodeExtractionResult? codeResult,
        CodeQualityResult? qualityResult)
    {
        var templateData = new Dictionary<string, object>
        {
            ["DocId"] = changeData.DocId,
            ["JiraNumber"] = changeData.JiraNumber ?? "N/A",
            ["Description"] = changeData.Description ?? "No description provided",
            ["ChangeType"] = changeData.ChangeType ?? "Unknown",
            ["Priority"] = changeData.Priority ?? "Medium",
            ["Severity"] = changeData.Severity ?? "Low",
            ["AssignedTo"] = changeData.AssignedTo ?? "Unassigned",
            ["TableName"] = changeData.TableName ?? "N/A",
            ["ColumnName"] = changeData.ColumnName ?? "N/A",
            ["StoredProcedureName"] = changeData.StoredProcedureName ?? "N/A",
            ["GeneratedDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            ["DocumentVersion"] = "1.0"
        };

        if (codeResult != null)
        {
            templateData["HasCodeExtraction"] = true;
            templateData["ExtractedCode"] = codeResult.ExtractedCode ?? "No code extracted";
            templateData["ExtractionSuccess"] = !string.IsNullOrEmpty(codeResult.ExtractedCode);
            if (!string.IsNullOrEmpty(codeResult.Warnings))
            {
                templateData["CodeWarnings"] = codeResult.Warnings;
            }
        }
        else
        {
            templateData["HasCodeExtraction"] = false;
            templateData["ExtractedCode"] = "No code extraction performed";
        }

        if (qualityResult != null)
        {
            templateData["HasQualityAnalysis"] = true;
            templateData["QualityScore"] = qualityResult.Score;
            templateData["QualityGrade"] = qualityResult.Grade;
            templateData["QualityCategory"] = qualityResult.Category;
            if (qualityResult.Issues?.Any() == true)
            {
                templateData["QualityIssues"] = string.Join("; ", qualityResult.Issues);
            }
        }
        else
        {
            templateData["HasQualityAnalysis"] = false;
            templateData["QualityScore"] = "N/A";
            templateData["QualityGrade"] = "Not Analyzed";
        }

        return templateData;
    }


    private async Task<string> GenerateContentAsync(
        IDocumentTemplate template,
        Dictionary<string, object> templateData,
        CancellationToken ct)
    {
        try
        {
            var content = await template.GenerateAsync(templateData, ct);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Template generation failed, creating fallback content");
            return GenerateFallbackContent(templateData);
        }
    }

    private string GenerateFallbackContent(Dictionary<string, object> templateData)
    {
        var content = $@"# Documentation Draft - {templateData["DocId"]}

## Change Information
- **JIRA Number**: {templateData["JiraNumber"]}
- **Description**: {templateData["Description"]}
- **Change Type**: {templateData["ChangeType"]}
- **Priority**: {templateData["Priority"]}
- **Assigned To**: {templateData["AssignedTo"]}

## Technical Details
- **Table**: {templateData["TableName"]}
- **Column**: {templateData["ColumnName"]}
- **Stored Procedure**: {templateData["StoredProcedureName"]}

";

        if ((bool)templateData.GetValueOrDefault("HasCodeExtraction", false))
        {
            content += $@"## Code Analysis
**Extracted Code:**
```sql
{templateData["ExtractedCode"]}
```

";
        }

        if ((bool)templateData.GetValueOrDefault("HasQualityAnalysis", false))
        {
            content += $@"## Quality Assessment
- **Score**: {templateData["QualityScore"]}/100
- **Grade**: {templateData["QualityGrade"]}
- **Category**: {templateData["QualityCategory"]}
";
            
            if (templateData.ContainsKey("QualityIssues"))
            {
                content += $@"
**Quality Issues:**
{templateData["QualityIssues"]}
";
            }
        }

        content += $@"

## Metadata
- **Generated**: {templateData["GeneratedDate"]}
- **Version**: {templateData["DocumentVersion"]}

---
*This document was automatically generated by the Enterprise Documentation Platform.*
";

        return content;
    }

    private string PostProcessContent(string content, DocumentChangeEntry changeEntry, CodeQualityResult? qualityResult)
    {
        if (qualityResult != null && (qualityResult.Grade == "D" || qualityResult.Grade == "F"))
        {
            var warning = $@"
⚠️ **QUALITY WARNING**: This change has received a poor quality grade ({qualityResult.Grade}). Please review the code quality issues before proceeding.

";
            content = warning + content;
        }

        if (changeEntry.Priority == "High" || changeEntry.Severity == "High")
        {
            var highPriorityNote = @"
🚨 **HIGH PRIORITY CHANGE** - This change requires immediate attention and thorough review.

";
            content = highPriorityNote + content;
        }

        return content;
    }

    private string GenerateDocumentUrl(string docId, string documentType)
    {
        var baseUrl = _configuration["DocumentStorage:BaseUrl"] ?? "https://docs.company.com";
        return $"{baseUrl}/drafts/{documentType}/{docId}.md";
    }

    private void PopulateMetadata(
        DraftGenerationResult result,
        DocumentChangeEntry changeEntry,
        CodeExtractionResult? codeResult,
        CodeQualityResult? qualityResult)
    {
        result.Metadata["DocumentType"] = DetermineDocType(changeEntry);
        result.Metadata["GenerationTimestamp"] = DateTime.UtcNow;
        result.Metadata["ChangeData"] = JsonSerializer.Serialize(changeEntry);
        
        if (codeResult != null)
        {
            result.Metadata["CodeExtractionSuccess"] = !string.IsNullOrEmpty(codeResult.ExtractedCode);
            result.Metadata["ExtractionMethod"] = codeResult.ExtractionMethod;
            result.Metadata["MarkerCount"] = codeResult.MarkerCount;
        }
        
        if (qualityResult != null)
        {
            result.Metadata["QualityScore"] = qualityResult.Score;
            result.Metadata["QualityGrade"] = qualityResult.Grade;
        }
    }

    private void AddQualityWarnings(DraftGenerationResult result, CodeQualityResult? qualityResult)
    {
        if (qualityResult == null) return;

        if (qualityResult.Grade == "F")
        {
            result.Warnings.Add("CRITICAL: Code quality grade F detected. Immediate refactoring required.");
        }
        else if (qualityResult.Grade == "D")
        {
            result.Warnings.Add("WARNING: Code quality grade D detected. Significant improvements needed.");
        }
        
        if (qualityResult.Score < 50)
        {
            result.Warnings.Add($"Quality score ({qualityResult.Score}/100) is below acceptable threshold.");
        }
    }
	private async Task<Dictionary<string, object>> EnrichWithAIAsync(
    Dictionary<string, object> templateData,
    SqlAnalysisResult? sqlAnalysis,
    CancellationToken ct)
{
    // If no SQL analysis, return as-is
    if (sqlAnalysis == null)
    {
        _logger.LogInformation("No SQL analysis available, skipping AI enhancement");
        return templateData;
    }

    try
    {
        _logger.LogInformation("Starting AI enhancement for {Schema}.{Procedure}", 
            sqlAnalysis.Schema, sqlAnalysis.ProcedureName);

        // Build comprehensive prompt
        var prompt = BuildEnhancementPrompt(templateData, sqlAnalysis);
        
        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = _deploymentName,
            Messages =
            {
                new ChatRequestSystemMessage("You are a technical documentation expert specializing in SQL Server stored procedures. Analyze code structure and provide comprehensive, accurate documentation based on ACTUAL CODE ANALYSIS provided to you."),
                new ChatRequestUserMessage(prompt)
            },
            Temperature = 0.3f, // Lower temperature for more deterministic output
            MaxTokens = 2000,
            ResponseFormat = ChatCompletionsResponseFormat.JsonObject
        };

        var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions, ct);
        var content = response.Value.Choices[0].Message.Content;

        _logger.LogInformation("AI enhancement completed, parsing response");

        // Parse JSON response
        var enhanced = ParseAIResponse(content);
        
        // Log what was enhanced
        _logger.LogInformation("AI enhanced {FieldCount} fields", enhanced.Count);
        
        return enhanced;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "AI enhancement failed, returning original data");
        
        // Return original data if AI fails - don't break the pipeline
        return new Dictionary<string, object>
        {
            ["AI_Enhancement_Failed"] = true,
            ["AI_Error"] = ex.Message
        };
    }
}

private string BuildEnhancementPrompt(
    Dictionary<string, object> templateData,
    SqlAnalysisResult sqlAnalysis)
{
    var prompt = $@"SQL STRUCTURE ANALYSIS (extracted from code):

Schema: {sqlAnalysis.Schema}
Procedure: {sqlAnalysis.ProcedureName}

Parameters ({sqlAnalysis.Parameters.Count}):
{string.Join("\n", sqlAnalysis.Parameters.Select(p => $"  - {p.Name} {p.Type} ({p.Direction})"))}

Tables Referenced ({sqlAnalysis.Dependencies.Tables.Count}):
{string.Join("\n", sqlAnalysis.Dependencies.Tables.Select(t => $"  - {t}"))}

Called Procedures ({sqlAnalysis.Dependencies.Procedures.Count}):
{string.Join("\n", sqlAnalysis.Dependencies.Procedures.Select(p => $"  - {p}"))}

Temp Tables ({sqlAnalysis.Dependencies.TempTables.Count}):
{string.Join("\n", sqlAnalysis.Dependencies.TempTables.Select(t => $"  - #{t}"))}

Control Tables ({sqlAnalysis.Dependencies.ControlTables.Count}):
{string.Join("\n", sqlAnalysis.Dependencies.ControlTables.Select(t => $"  - {t}"))}

Complexity: {sqlAnalysis.Complexity.ComplexityLevel} - {sqlAnalysis.Complexity.LineCount} lines, {sqlAnalysis.Complexity.JoinCount} joins, {sqlAnalysis.Complexity.TempTableCount} temp tables, {sqlAnalysis.Complexity.CteCount} CTEs

Logic Steps ({sqlAnalysis.LogicSteps.Count}):
{string.Join("\n", sqlAnalysis.LogicSteps.Select((s, i) => $"  {i + 1}. {s}"))}";

    if (sqlAnalysis.BracketedChange != null)
    {
        prompt += $@"

Bracketed Change Detected:
  Ticket: {sqlAnalysis.BracketedChange.Ticket}
  Lines: {sqlAnalysis.BracketedChange.StartLine}-{sqlAnalysis.BracketedChange.EndLine}
  This is a MODIFICATION to an existing procedure (not a new one).";
    }

    if (sqlAnalysis.ValidationRules.Count > 0)
    {
        prompt += $@"

Validation Rules ({sqlAnalysis.ValidationRules.Count}):
{string.Join("\n", sqlAnalysis.ValidationRules.Select(r => $"  - {r.RuleText}"))}";
    }

    prompt += $@"

Original Description from Change Request:
{templateData.GetValueOrDefault("Description", "No description provided")}

TASK: Analyze the SQL structure above and create comprehensive documentation that explains:
1. WHAT the code does (based on structure, dependencies, logic steps)
2. WHY this change matters (business impact, financial/operational/compliance implications)
3. HOW it works (technical implementation details from parameters, temp tables, joins)

Return ONLY a JSON object with this EXACT structure (no markdown, no code blocks):
{{
  ""purpose"": ""2-3 sentences explaining core function based on structure"",
  ""business_impact"": ""Why this matters: financial accuracy, operational efficiency, compliance, reporting"",
  ""technical_summary"": ""How it works: key technical approach using temp tables, CTEs, joins, validation logic"",
  ""parameters"": [
    {{""name"": ""@ParamName"", ""type"": ""TYPE"", ""direction"": ""IN/OUT"", ""description"": ""what this parameter controls""}}
  ],
  ""logic_steps"": [""Step 1: Initialize and validate inputs..."", ""Step 2: Build temp tables for...""],
  ""dependencies"": {{
    ""Source Tables"": ""comma-separated list from analysis"",
    ""Target Tables"": ""tables being updated"",
    ""Procedures"": ""called procedures from analysis"",
    ""Temp Tables"": ""temp tables from analysis""
  }},
  ""complexity"": {{
    ""Lines of Code"": ""{sqlAnalysis.Complexity.LineCount}"",
    ""Logic Complexity"": ""{sqlAnalysis.Complexity.ComplexityLevel}"",
    ""Business Impact"": ""HIGH/MEDIUM/LOW based on what tables/processes this affects""
  }},
  ""performance_notes"": [""List any performance considerations based on temp tables, joins, CTEs""],
  ""whats_new"": ""This version addresses {(sqlAnalysis.BracketedChange != null ? sqlAnalysis.BracketedChange.Ticket : "initial release")}: [describe what changed based on bracketed code]"",
  ""error_handling"": ""Describe validation rules, error checks, transaction management visible in code""
}}

CRITICAL: Base your response on the ACTUAL CODE STRUCTURE analyzed above. Don't guess - extract facts from parameters, tables, joins, logic steps provided.";

    return prompt;
}

private Dictionary<string, object> ParseAIResponse(string jsonContent)
{
    var enhanced = new Dictionary<string, object>();
    
    try
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // Simple strings
        if (root.TryGetProperty("purpose", out var purpose))
            enhanced["Purpose"] = purpose.GetString() ?? "";
        
        if (root.TryGetProperty("business_impact", out var impact))
            enhanced["BusinessImpact"] = impact.GetString() ?? "";
        
        if (root.TryGetProperty("technical_summary", out var summary))
            enhanced["TechnicalSummary"] = summary.GetString() ?? "";
        
        if (root.TryGetProperty("whats_new", out var whatsNew))
            enhanced["WhatsNew"] = whatsNew.GetString() ?? "";
        
        if (root.TryGetProperty("error_handling", out var errorHandling))
            enhanced["ErrorHandling"] = errorHandling.GetString() ?? "";

        // Arrays → List<Dictionary<string, string>>
        if (root.TryGetProperty("parameters", out var paramsArray))
        {
            var paramsList = new List<Dictionary<string, string>>();
            foreach (var param in paramsArray.EnumerateArray())
            {
                paramsList.Add(new Dictionary<string, string>
                {
                    ["name"] = param.GetProperty("name").GetString() ?? "",
                    ["type"] = param.GetProperty("type").GetString() ?? "",
                    ["direction"] = param.GetProperty("direction").GetString() ?? "",
                    ["description"] = param.GetProperty("description").GetString() ?? ""
                });
            }
            enhanced["Parameters"] = paramsList;
        }

        // Arrays → List<string>
        if (root.TryGetProperty("logic_steps", out var logicArray))
        {
            var logicList = new List<string>();
            foreach (var step in logicArray.EnumerateArray())
                logicList.Add(step.GetString() ?? "");
            enhanced["LogicSteps"] = logicList;
        }
        
        if (root.TryGetProperty("performance_notes", out var perfArray))
        {
            var perfList = new List<string>();
            foreach (var note in perfArray.EnumerateArray())
                perfList.Add(note.GetString() ?? "");
            enhanced["PerformanceNotes"] = perfList;
        }

        // Objects → Dictionary<string, string>
        if (root.TryGetProperty("dependencies", out var depsObj))
        {
            var depsDict = new Dictionary<string, string>();
            foreach (var prop in depsObj.EnumerateObject())
                depsDict[prop.Name] = prop.Value.GetString() ?? "";
            enhanced["Dependencies"] = depsDict;
        }
        
        if (root.TryGetProperty("complexity", out var complexObj))
        {
            var complexDict = new Dictionary<string, string>();
            foreach (var prop in complexObj.EnumerateObject())
                complexDict[prop.Name] = prop.Value.GetString() ?? "";
            enhanced["Complexity"] = complexDict;
        }

        _logger.LogInformation("Successfully parsed AI response with {Count} fields", enhanced.Count);
    }
    catch (JsonException ex)
    {
        _logger.LogError(ex, "Failed to parse AI JSON response");
        throw;
    }

    return enhanced;
}

    private static string? GetAiValue(Dictionary<string, object> aiEnhanced, string key)
    {
        if (aiEnhanced.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }
        return null;
    }
    
    public class ParameterInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Description { get; set; } = "";
    }
}