using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.VariantTypes;

using Enterprise.Documentation.Core.Application.Services.Metadata;
using System.Diagnostics;
using System.Text.Json;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

public interface IDocumentGenerationService
{
    Task<DocumentGenerationResult> GenerateDocumentAsync(
        Dictionary<string, object> templateData,
        MasterIndexMetadata metadata,
        CancellationToken ct = default);
}

public class DocumentGenerationService : IDocumentGenerationService
{
    private readonly ILogger<DocumentGenerationService> _logger;
    private readonly string _pythonPath;
    private readonly string _templatePath;
    private readonly string _outputPath;

    public DocumentGenerationService(
        ILogger<DocumentGenerationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        // Read from correct config section
        _pythonPath = configuration["DocumentGeneration:PythonExecutable"]
            ?? configuration["Python:ExecutablePath"]
            ?? "py";

        _templatePath = configuration["DocumentGeneration:TemplatesPath"]
            ?? configuration["Templates:BasePath"]
            ?? @"C:\Projects\EnterpriseDocumentationPlatform.V2\templates";

        _outputPath = configuration["DocumentGeneration:BaseOutputPath"]
            ?? configuration["Documents:OutputPath"]
            ?? @"C:\Temp\GeneratedDocs";

        Directory.CreateDirectory(_outputPath);
    }

    public async Task<DocumentGenerationResult> GenerateDocumentAsync(
        Dictionary<string, object> templateData,
        MasterIndexMetadata metadata,
        CancellationToken ct = default)
    {
        var result = new DocumentGenerationResult { Success = false };
        
        try
        {
            _logger.LogInformation("Generating document for {DocId}", metadata.DocId);
            
            // Step 1: Determine template type
            var templateType = DetermineTemplateType(metadata.DocumentType);
            var templateScript = Path.Combine(_templatePath, $"TEMPLATE_{templateType}.py");
            
            if (!File.Exists(templateScript))
            {
                throw new FileNotFoundException($"Template not found: {templateScript}");
            }
            
            // Step 2: Generate filename
            var fileName = GenerateFileName(metadata);
            var docPath = Path.Combine(_outputPath, fileName);
            
            // Step 3: Transform data to flat structure for Python templates
            var flatData = TransformToFlatStructure(templateData, metadata);
            
            // Step 4: Prepare template data JSON
            var dataJson = JsonSerializer.Serialize(flatData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            var dataPath = Path.Combine(_outputPath, $"{metadata.DocId}_data.json");
            await File.WriteAllTextAsync(dataPath, dataJson, ct);
            
            _logger.LogInformation("Created data file: {DataPath}", dataPath);
            _logger.LogDebug("Template data: {Data}", dataJson);
            
            // Step 5: Call Python template
            var pythonSuccess = await CallPythonTemplateAsync(templateScript, dataPath, docPath, ct);
            
            if (!pythonSuccess)
            {
                result.ErrorMessage = "Python template execution failed";
                return result;
            }
            
            // Step 6: Embed custom properties
            EmbedCustomProperties(docPath, metadata, templateData);
            
            // Step 7: Cleanup temp files (optional - keep for debugging)
            // File.Delete(dataPath);
            
            result.Success = true;
            result.GeneratedFilePath = docPath;
            result.FileName = fileName;
            result.FileSize = new FileInfo(docPath).Length;
            
            _logger.LogInformation("Document generated successfully: {FilePath}", docPath);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document generation failed for {DocId}", metadata.DocId);
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private async Task<bool> CallPythonTemplateAsync(
        string scriptPath, 
        string dataPath, 
        string outputPath,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Calling Python: {Script} {DataPath} {OutputPath}", scriptPath, dataPath, outputPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{scriptPath}\" \"{dataPath}\" \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError("Python template failed: {Error}", error);
                return false;
            }

            _logger.LogDebug("Python template output: {Output}", output);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Python template");
            return false;
        }
    }

    /// <summary>
    /// Transforms structured template data to the flat format expected by Python templates
    /// This preserves all your sophisticated stored procedure analysis and version control logic
    /// </summary>
    private Dictionary<string, object> TransformToFlatStructure(
        Dictionary<string, object> templateData, 
        MasterIndexMetadata metadata)
    {
        var flatData = new Dictionary<string, object>();

        // Core document identifiers (required by all templates)
        flatData["doc_id"] = metadata.DocId;
        flatData["jira"] = templateData.GetValueOrDefault("JiraNumber", metadata.JiraNumber) ?? "N/A";
        flatData["status"] = "Completed"; // Since we're generating the document
        flatData["date"] = DateTime.Now.ToString("MM/dd/yyyy");
        
        // People
        flatData["reported_by"] = ExtractFromNested(templateData, "change_request", "reported_by")?.ToString() 
            ?? templateData.GetValueOrDefault("AssignedTo")?.ToString() 
            ?? "System";
        flatData["assigned_to"] = templateData.GetValueOrDefault("AssignedTo")?.ToString() ?? "System";

        // Database objects
        flatData["schema"] = metadata.SchemaName ?? ExtractFromNested(templateData, "database_context", "schema")?.ToString() ?? "dbo";
        flatData["table"] = metadata.TableName ?? templateData.GetValueOrDefault("TableName")?.ToString() ?? "N/A";
        flatData["column"] = metadata.ColumnName ?? templateData.GetValueOrDefault("ColumnName")?.ToString() ?? "N/A";
        
        // Data type and values (for Business Request templates)
        flatData["data_type"] = ExtractFromNested(templateData, "technical_analysis", "data_type")?.ToString() ?? "VARCHAR(255)";
        flatData["values"] = BuildValidValuesText(templateData);

        // Content fields
        flatData["purpose"] = ExtractBusinessPurpose(templateData, metadata);
        flatData["summary"] = ExtractBusinessSummary(templateData, metadata);
        flatData["rule_def"] = ExtractBusinessRules(templateData);
        
        // Code and analysis
        flatData["code"] = ExtractCodeWithJiraComments(templateData, metadata);
        flatData["code_explain"] = ExtractCodeExplanation(templateData);
        
        // Control table information
        flatData["control_table"] = ExtractControlTable(templateData);
        flatData["table_desc"] = ExtractTableDescription(templateData);
        flatData["key_columns"] = ExtractKeyColumns(templateData);

        // Additional stored procedure analysis data
        if (templateData.ContainsKey("stored_procedure_analysis"))
        {
            var spAnalysis = templateData["stored_procedure_analysis"] as Dictionary<string, object>;
            if (spAnalysis != null)
            {
                // Add SP-specific data for your sophisticated templates
                flatData["sp_analysis"] = spAnalysis;
                flatData["jira_references"] = ExtractJiraReferences(spAnalysis);
                flatData["parameters"] = ExtractStoredProcParameters(spAnalysis);
                flatData["tables_accessed"] = ExtractTablesAccessed(spAnalysis);
                flatData["business_logic"] = ExtractBusinessLogic(spAnalysis);
            }
        }

        // Version history for living document approach
        flatData["version_history"] = BuildVersionHistory(templateData, metadata);

        return flatData;
    }

    private object? ExtractFromNested(Dictionary<string, object> data, string section, string field)
    {
        if (data.TryGetValue(section, out var sectionObj) && sectionObj is Dictionary<string, object> sectionDict)
        {
            return sectionDict.GetValueOrDefault(field);
        }
        return null;
    }

    private string BuildValidValuesText(Dictionary<string, object> templateData)
    {
        var aiEnhanced = ExtractFromNested(templateData, "ai_enhancement", "business_rules");
        if (aiEnhanced != null)
        {
            return aiEnhanced.ToString()!;
        }
        
        // Fallback to description
        var description = templateData.GetValueOrDefault("Description")?.ToString();
        if (!string.IsNullOrEmpty(description))
        {
            return $"**Valid Values** = As defined in business requirements\\n**Description** = {description}";
        }
        
        return "**Valid Values** = To be defined\\n**Invalid Values** = All others";
    }

    private string ExtractBusinessPurpose(Dictionary<string, object> templateData, MasterIndexMetadata metadata)
    {
        // Try AI enhancement first
        var aiPurpose = ExtractFromNested(templateData, "ai_enhancement", "business_impact");
        if (aiPurpose != null)
        {
            return aiPurpose.ToString()!;
        }

        // Fallback to metadata or description
        return metadata.Purpose ?? templateData.GetValueOrDefault("Description")?.ToString() ?? 
               "Enhances data quality and business process efficiency through improved data management.";
    }

    private string ExtractBusinessSummary(Dictionary<string, object> templateData, MasterIndexMetadata metadata)
    {
        // Try AI enhancement
        var aiSummary = ExtractFromNested(templateData, "ai_enhancement", "technical_summary");
        if (aiSummary != null)
        {
            return aiSummary.ToString()!;
        }

        var description = templateData.GetValueOrDefault("Description")?.ToString();
        var jira = templateData.GetValueOrDefault("JiraNumber")?.ToString() ?? metadata.JiraNumber;
        
        return $"This {metadata.DocumentType?.ToLower() ?? "change"} implements {description} " +
               $"as tracked in {jira}. The implementation follows best practices for data integrity and system maintainability.";
    }

    private string ExtractBusinessRules(Dictionary<string, object> templateData)
    {
        var aiRules = ExtractFromNested(templateData, "ai_enhancement", "business_rules");
        if (aiRules != null)
        {
            return $"**Business Logic:**\\n{aiRules}\\n\\n**Implementation Approach:**\\nThe system uses a data-driven approach for maintainable business rules.";
        }

        return "**Business Logic:**\\nImplemented according to business requirements\\n\\n**Implementation Approach:**\\nFollows enterprise standards for data validation and integrity.";
    }

    private string ExtractCodeWithJiraComments(Dictionary<string, object> templateData, MasterIndexMetadata metadata)
    {
        var extractedCode = templateData.GetValueOrDefault("ExtractedCode")?.ToString();
        var jira = templateData.GetValueOrDefault("JiraNumber")?.ToString() ?? metadata.JiraNumber ?? "CHANGE-001";
        
        if (!string.IsNullOrEmpty(extractedCode))
        {
            // Add JIRA comments to code for your bracketed JIRA detection logic
            return $"-- BEGIN {jira}: {metadata.Purpose ?? "Code Enhancement"}\\n{extractedCode}\\n-- END {jira}";
        }

        // Generate sample code structure
        var schema = metadata.SchemaName ?? "dbo";
        var table = metadata.TableName ?? "TableName";
        var column = metadata.ColumnName ?? "ColumnName";
        
        return $"-- BEGIN {jira}: Add {column} Enhancement\\n" +
               $"ALTER TABLE {schema}.{table}\\n" +
               $"ADD {column} VARCHAR(255) NULL;\\n\\n" +
               $"-- Update logic for {column}\\n" +
               $"UPDATE {schema}.{table}\\n" +
               $"SET {column} = 'DefaultValue'\\n" +
               $"WHERE {column} IS NULL;\\n" +
               $"-- END {jira}";
    }

    private string ExtractCodeExplanation(Dictionary<string, object> templateData)
    {
        var aiExplanation = ExtractFromNested(templateData, "ai_enhancement", "technical_details");
        if (aiExplanation != null)
        {
            return $"**Implementation Details:**\\n\\n{aiExplanation}\\n\\n**Performance Considerations:**\\nOptimized for minimal impact on existing operations.";
        }

        return "**Implementation Details:**\\n\\nCode implements the required functionality following enterprise standards.\\n\\n**Performance Considerations:**\\nMinimal performance impact through optimized implementation.";
    }

    private string ExtractControlTable(Dictionary<string, object> templateData)
    {
        // Check if there's a control table mentioned in AI enhancement or analysis
        var controlTable = ExtractFromNested(templateData, "database_context", "control_tables");
        if (controlTable != null)
        {
            return controlTable.ToString()!;
        }
        return "N/A - Direct implementation";
    }

    private string ExtractTableDescription(Dictionary<string, object> templateData)
    {
        var tableDesc = ExtractFromNested(templateData, "database_context", "table_description");
        if (tableDesc != null)
        {
            return tableDesc.ToString()!;
        }
        return "Core business table supporting operational requirements and data integrity.";
    }

    private List<string> ExtractKeyColumns(Dictionary<string, object> templateData)
    {
        var columns = new List<string>();
        
        // Try to extract from database context
        var dbContext = ExtractFromNested(templateData, "database_context", "key_columns");
        if (dbContext is List<object> columnList)
        {
            columns.AddRange(columnList.Select(c => c.ToString()!));
        }
        
        // Add basic columns if none found
        if (!columns.Any())
        {
            var tableName = templateData.GetValueOrDefault("TableName")?.ToString();
            var columnName = templateData.GetValueOrDefault("ColumnName")?.ToString();
            
            columns.Add($"ID - Primary key identifier");
            if (!string.IsNullOrEmpty(columnName))
            {
                columns.Add($"{columnName} - Enhanced column with new functionality");
            }
            columns.Add($"ModifiedDate - Audit trail for changes");
        }
        
        return columns;
    }

    private List<string> ExtractJiraReferences(Dictionary<string, object> spAnalysis)
    {
        // Your sophisticated JIRA detection logic goes here
        var jiraRefs = new List<string>();
        
        // Extract from stored procedure analysis if available
        if (spAnalysis.TryGetValue("jira_references", out var refs) && refs is List<object> refList)
        {
            jiraRefs.AddRange(refList.Select(r => r.ToString()!));
        }
        
        return jiraRefs;
    }

    private List<Dictionary<string, object>> ExtractStoredProcParameters(Dictionary<string, object> spAnalysis)
    {
        var parameters = new List<Dictionary<string, object>>();
        
        if (spAnalysis.TryGetValue("parameters", out var params_) && params_ is List<object> paramList)
        {
            parameters.AddRange(paramList.Cast<Dictionary<string, object>>());
        }
        
        return parameters;
    }

    private List<Dictionary<string, object>> ExtractTablesAccessed(Dictionary<string, object> spAnalysis)
    {
        var tables = new List<Dictionary<string, object>>();
        
        if (spAnalysis.TryGetValue("tables_accessed", out var tables_) && tables_ is List<object> tableList)
        {
            tables.AddRange(tableList.Cast<Dictionary<string, object>>());
        }
        
        return tables;
    }

    private string ExtractBusinessLogic(Dictionary<string, object> spAnalysis)
    {
        return spAnalysis.GetValueOrDefault("business_logic")?.ToString() ?? 
               "Implements core business logic for data processing and validation.";
    }

    private List<Dictionary<string, object>> BuildVersionHistory(Dictionary<string, object> templateData, MasterIndexMetadata metadata)
    {
        var history = new List<Dictionary<string, object>>();
        
        // Add current change as the latest version (living document approach)
        history.Add(new Dictionary<string, object>
        {
            ["date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["jira"] = templateData.GetValueOrDefault("JiraNumber", metadata.JiraNumber) ?? "CHANGE-001",
            ["change"] = templateData.GetValueOrDefault("Description", "Initial implementation") ?? "System enhancement",
            ["author"] = templateData.GetValueOrDefault("AssignedTo", "System") ?? "System"
        });
        
        // Add any existing version history if available
        if (templateData.TryGetValue("version_history", out var existing) && existing is List<object> existingList)
        {
            history.AddRange(existingList.Cast<Dictionary<string, object>>());
        }
        
        return history;
    }

    private void EmbedCustomProperties(
        string docPath, 
        MasterIndexMetadata metadata, 
        Dictionary<string, object> templateData)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(docPath, true);
            
            // Get or create custom properties part
            var customPropsPart = doc.CustomFilePropertiesPart 
                ?? doc.AddCustomFilePropertiesPart();
            
            var props = customPropsPart.Properties 
                ?? new Properties();
            
            // Clear existing custom properties
            props.RemoveAllChildren();
            
            int propId = 2; // Start at 2 (1 is reserved)
            
            // Core identifiers
            AddCustomProperty(props, ref propId, "DocId", metadata.DocId);
            AddCustomProperty(props, ref propId, "JiraNumber", metadata.JiraNumber);
            AddCustomProperty(props, ref propId, "Version", metadata.Version);
            AddCustomProperty(props, ref propId, "Author", metadata.Author);
            
            // Database object info
            AddCustomProperty(props, ref propId, "Schema", metadata.SchemaName);
            AddCustomProperty(props, ref propId, "StoredProcedure", metadata.ObjectName);
            AddCustomProperty(props, ref propId, "DatabaseName", metadata.DatabaseName);
            AddCustomProperty(props, ref propId, "TableName", metadata.TableName);
            AddCustomProperty(props, ref propId, "ColumnName", metadata.ColumnName);
            
            // Classification
            AddCustomProperty(props, ref propId, "DocumentType", metadata.DocumentType);
            AddCustomProperty(props, ref propId, "BusinessProcess", metadata.BusinessProcess);
            AddCustomProperty(props, ref propId, "DomainTags", 
                metadata.DomainTags != null ? string.Join(", ", metadata.DomainTags) : "");
            
            // Complexity & Quality
            AddCustomProperty(props, ref propId, "TechnicalComplexity", metadata.ComplexityLevel);
            AddCustomProperty(props, ref propId, "BusinessImpact", metadata.BusinessImpactLevel);
            AddCustomProperty(props, ref propId, "ConfidenceScore", metadata.ConfidenceScore.ToString("F2"));
            
            // Dependencies (JSON)
            AddCustomProperty(props, ref propId, "DependentTables", 
                metadata.DependentTables != null ? JsonSerializer.Serialize(metadata.DependentTables) : "[]");
            AddCustomProperty(props, ref propId, "TempTables", 
                metadata.TempTables != null ? JsonSerializer.Serialize(metadata.TempTables) : "[]");
            
            // Metadata tracking
            AddCustomProperty(props, ref propId, "ExtractedAt", metadata.ExtractedAt.ToString("o"));
            AddCustomProperty(props, ref propId, "ExtractionVersion", metadata.ExtractionVersion);
            
            // Update core properties
            var coreProps = doc.PackageProperties;
            coreProps.Title = $"{metadata.SchemaName}.{metadata.ObjectName} - {metadata.JiraNumber}";
            coreProps.Subject = metadata.Purpose?.Length > 255 
                ? metadata.Purpose.Substring(0, 255) 
                : metadata.Purpose;
            coreProps.Keywords = metadata.Keywords != null 
                ? string.Join(", ", metadata.Keywords.Take(20)) : "";
            coreProps.Category = metadata.DocumentType;
            coreProps.ContentStatus = metadata.Version;
            coreProps.Creator = metadata.Author;
            coreProps.LastModifiedBy = metadata.Author;
            
            customPropsPart.Properties = props;
            props.Save();
            
            _logger.LogInformation("Embedded {Count} custom properties in document", propId - 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to embed custom properties, document still usable");
        }
    }

    private void AddCustomProperty(Properties props, ref int propId, string name, string value)
    {
        if (string.IsNullOrEmpty(value)) value = "";
        var prop = new CustomDocumentProperty
        {
            FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
            PropertyId = propId++,
            Name = name
        };
        prop.AppendChild(new VTLPWSTR(value));
        props.AppendChild(prop);
    }

    private string DetermineTemplateType(string documentType)
    {
        return documentType switch
        {
            "Enhancement" => "Enhancement",
            "Defect" => "Defect",
            "Business Request" => "BusinessRequest",
            _ => "StoredProcedure"
        };
    }

    private string GenerateFileName(MasterIndexMetadata metadata)
    {
        // Format: {DocType}-{Number}_{Schema}.{Table}_{ColumnName}.docx
        // Example: DF-0016_gwpcDaily.irf_property_MortgageIndicator.docx
        var schemaTable = $"{metadata.SchemaName}.{metadata.TableName}";
        var column = string.IsNullOrWhiteSpace(metadata.ColumnName) ? metadata.ObjectName : metadata.ColumnName;
        var safeName = $"{metadata.DocId}_{schemaTable}_{column}.docx"
            .Replace(" ", "_")
            .Replace("/", "-")
            .Replace("\\", "-");
        return safeName;
    }
}

public class DocumentGenerationResult
{
    public bool Success { get; set; }
    public string? GeneratedFilePath { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public string? ErrorMessage { get; set; }
}
