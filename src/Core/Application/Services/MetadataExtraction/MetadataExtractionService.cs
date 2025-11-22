using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DocumentFormat.OpenXml.Packaging;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.MetadataExtraction;

public class MetadataExtractionService : IMetadataExtractionService
{
    private readonly IOpenAIEnhancementService _openAI;
    private readonly ILogger<MetadataExtractionService> _logger;
    private readonly string _connectionString;

    public MetadataExtractionService(
        IOpenAIEnhancementService openAI,
        IConfiguration configuration,
        ILogger<MetadataExtractionService> logger)
    {
        _openAI = openAI;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public async Task<ExtractedMetadata> ExtractFromDatabaseObjectAsync(
        string objectType,
        string schemaName,
        string objectName,
        string definition,
        CancellationToken ct)
    {
        _logger.LogInformation("Extracting metadata from {Type}: {Schema}.{Name}",
            objectType, schemaName, objectName);

        var result = new ExtractedMetadata
        {
            SchemaName = schemaName,
            TableName = objectName,
            Method = ExtractionMethod.Hybrid,
            ExtractedAt = DateTime.UtcNow,
            ExtractedBy = "MetadataExtractionService"
        };

        try
        {
            // Step 1: Extract from INFORMATION_SCHEMA (HIGH CONFIDENCE)
            await ExtractFromSchemaAsync(result, objectType, schemaName, objectName, ct);

            // Step 2: Named Entity Recognition from definition (MEDIUM CONFIDENCE)
            ExtractEntitiesFromDefinition(result, definition);

            // Step 3: OpenAI extraction (VARIABLE CONFIDENCE)
            await ExtractWithOpenAIAsync(result, definition, ct);

            // Step 4: Validate against database schema
            await ValidateAgainstSchemaAsync(result, ct);

            // Step 5: Fuzzy match to existing MasterIndex
            await FuzzyMatchToMasterIndexAsync(result, ct);

            // Step 6: Calculate overall confidence
            CalculateOverallConfidence(result);

            _logger.LogInformation(
                "Extracted metadata for {Name} with {Confidence:P0} confidence ({Warnings} warnings)",
                objectName, result.OverallConfidence, result.ValidationWarnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from {Name}", objectName);
            result.ValidationErrors.Add($"Extraction error: {ex.Message}");
            result.OverallConfidence = 0.0;
            return result;
        }
    }

    public async Task<ExtractedMetadata> ExtractFromDocumentAsync(
        string filePath,
        CancellationToken ct)
    {
        _logger.LogInformation("Extracting metadata from document: {File}", Path.GetFileName(filePath));

        var result = new ExtractedMetadata
        {
            Method = ExtractionMethod.DocumentParsing,
            ExtractedAt = DateTime.UtcNow,
            ExtractedBy = "MetadataExtractionService"
        };

        try
        {
            if (!File.Exists(filePath))
            {
                result.ValidationErrors.Add($"File not found: {filePath}");
                return result;
            }

            // Step 1: Parse .docx structure using DocumentFormat.OpenXml
            using (var doc = WordprocessingDocument.Open(filePath, false))
            {
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null)
                {
                    result.ValidationErrors.Add("Document body is empty");
                    return result;
                }

                // Extract from document properties
                var props = doc.PackageProperties;
                result.AdditionalMetadata["Title"] = props.Title ?? "";
                result.AdditionalMetadata["Author"] = props.Creator ?? "";
                result.AdditionalMetadata["Created"] = props.Created?.ToString() ?? "";

                var text = body.InnerText;

                // Extract structured data using patterns
                ExtractStructuredDataFromDocument(result, text);
            }

            // Step 2: Enhanced NER extraction
            await ExtractEntitiesWithAdvancedNERAsync(result, filePath, ct);

            // Step 3: OpenAI extraction for complex fields
            await ExtractDescriptionFromDocumentAsync(result, filePath, ct);

            // Step 4: Validate extracted data
            await ValidateAgainstSchemaAsync(result, ct);

            // Step 5: Calculate confidence
            CalculateOverallConfidence(result);

            _logger.LogInformation(
                "Extracted metadata from document with {Confidence:P0} confidence",
                result.OverallConfidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata from document {File}", filePath);
            result.ValidationErrors.Add($"Extraction error: {ex.Message}");
            result.OverallConfidence = 0.0;
            return result;
        }
    }

    public async Task<ExtractedMetadata> ExtractFromExcelRowAsync(
        ExcelRowData rowData,
        CancellationToken ct)
    {
        _logger.LogDebug("Extracting metadata from Excel row");

        var result = new ExtractedMetadata
        {
            Method = ExtractionMethod.Manual,  // Excel data is human-entered
            ExtractedAt = DateTime.UtcNow,
            ExtractedBy = "ExcelImport"
        };

        // Direct mapping from Excel columns (HIGH CONFIDENCE)
        result.JiraNumber = rowData.JiraNumber;
        result.CABNumber = rowData.CABNumber;
        result.TableName = rowData.Table;
        result.ColumnName = rowData.Column;
        result.ChangeType = rowData.ChangeType;
        result.Description = rowData.Description;
        result.Documentation = rowData.Documentation;
        result.Priority = rowData.Priority;
        result.Severity = rowData.Severity;
        result.Sprint = rowData.SprintNumber;
        result.ReportedBy = rowData.ReportedBy;
        result.AssignedTo = rowData.AssignedTo;

        // Parse modified procedures
        if (!string.IsNullOrWhiteSpace(rowData.ModifiedObjects))
        {
            result.ModifiedProcedures = rowData.ModifiedObjects
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        // Parse schema and table from "schema.table" format
        if (!string.IsNullOrWhiteSpace(rowData.Table) && rowData.Table.Contains('.'))
        {
            var parts = rowData.Table.Split('.');
            if (parts.Length == 2)
            {
                result.SchemaName = parts[0].Trim();
                result.TableName = parts[1].Trim();
                result.FieldConfidences["SchemaName"] = 1.0;
                result.FieldConfidences["TableName"] = 1.0;
            }
        }

        // Date parsing
        if (DateTime.TryParse(rowData.Date, out var dateEntered))
        {
            result.DateEntered = dateEntered;
            result.FieldConfidences["DateEntered"] = 1.0;
        }

        // Set high confidence for all directly mapped fields
        if (!string.IsNullOrWhiteSpace(result.JiraNumber))
            result.FieldConfidences["JiraNumber"] = 1.0;

        if (!string.IsNullOrWhiteSpace(result.CABNumber))
            result.FieldConfidences["CABNumber"] = 1.0;

        if (!string.IsNullOrWhiteSpace(result.ColumnName))
            result.FieldConfidences["ColumnName"] = 1.0;

        if (!string.IsNullOrWhiteSpace(result.ChangeType))
            result.FieldConfidences["ChangeType"] = 0.95;  // Validate against known types

        if (!string.IsNullOrWhiteSpace(result.Description))
            result.FieldConfidences["Description"] = 0.9;  // Human-written

        // Validate against schema
        await ValidateAgainstSchemaAsync(result, ct);

        // Calculate overall confidence
        CalculateOverallConfidence(result);

        return result;
    }

    public async Task<ValidationResult> ValidateMetadataAsync(
        ExtractedMetadata metadata,
        CancellationToken ct)
    {
        var result = new ValidationResult();

        // Required fields validation
        if (string.IsNullOrWhiteSpace(metadata.TableName))
            result.Errors.Add("TableName is required");

        if (string.IsNullOrWhiteSpace(metadata.Description))
            result.Warnings.Add("Description is missing or empty");

        // Validate against database schema
        if (!string.IsNullOrWhiteSpace(metadata.TableName))
        {
            var tableExists = await ValidateTableExistsAsync(
                metadata.SchemaName,
                metadata.TableName,
                ct);

            if (!tableExists)
            {
                result.Errors.Add($"Table {metadata.SchemaName}.{metadata.TableName} not found in database");

                // Suggest similar table names
                var suggestions = await FindSimilarTableNamesAsync(metadata.TableName, ct);
                if (suggestions.Any())
                {
                    result.SuggestedCorrections["TableName"] = string.Join(", ", suggestions);
                }
            }
            else
            {
                metadata.TableExists = true;
            }
        }

        // Validate column if specified
        if (!string.IsNullOrWhiteSpace(metadata.ColumnName) && metadata.TableExists)
        {
            var columnExists = await ValidateColumnExistsAsync(
                metadata.SchemaName,
                metadata.TableName,
                metadata.ColumnName,
                ct);

            if (!columnExists)
            {
                result.Errors.Add($"Column {metadata.ColumnName} not found in {metadata.TableName}");

                // Suggest similar column names
                var suggestions = await FindSimilarColumnNamesAsync(
                    metadata.SchemaName,
                    metadata.TableName,
                    metadata.ColumnName,
                    ct);

                if (suggestions.Any())
                {
                    result.SuggestedCorrections["ColumnName"] = string.Join(", ", suggestions);
                }
            }
            else
            {
                metadata.ColumnExists = true;
            }
        }

        // Validate change type
        var validChangeTypes = new[] { "Business Request", "Enhancement", "Defect Fix", "Anomaly", "Research" };
        if (!string.IsNullOrWhiteSpace(metadata.ChangeType) &&
            !validChangeTypes.Contains(metadata.ChangeType, StringComparer.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Unknown change type: {metadata.ChangeType}");
        }

        // Validate Jira format
        if (!string.IsNullOrWhiteSpace(metadata.JiraNumber))
        {
            var jiraPattern = @"^[A-Z]+-\d+$";
            if (!Regex.IsMatch(metadata.JiraNumber, jiraPattern))
            {
                result.Warnings.Add($"Jira number format invalid: {metadata.JiraNumber}");
            }
        }

        result.IsValid = result.Errors.Count == 0;
        result.ValidationScore = result.IsValid ? (1.0 - (result.Warnings.Count * 0.1)) : 0.0;

        return result;
    }

    public async Task<ExtractedMetadata> EnhanceWithAIAsync(
        ExtractedMetadata metadata,
        CancellationToken ct)
    {
        _logger.LogDebug("Enhancing metadata with AI for {Table}", metadata.TableName);

        try
        {
            var enhancementRequest = new DocumentationEnhancementRequest
            {
                Description = metadata.Description ?? "",
                Documentation = metadata.Documentation ?? "",
                ChangeType = metadata.ChangeType ?? "Enhancement",
                ObjectName = metadata.TableName,
                PropertyName = metadata.ColumnName,
                Context = metadata.AdditionalMetadata
            };

            var enhanced = await _openAI.EnhanceDocumentationAsync(enhancementRequest, ct);

            metadata.EnhancedDescription = enhanced.EnhancedDescription;
            metadata.EnhancedDocumentation = enhanced.Content;
            metadata.AIGeneratedTags = enhanced.KeyPoints;
            metadata.SemanticCategory = DetermineSemanticCategory(metadata.ChangeType);

            // Update confidence for AI-enhanced fields
            metadata.FieldConfidences["EnhancedDescription"] = 0.85;
            metadata.FieldConfidences["AIGeneratedTags"] = 0.80;

            _logger.LogInformation("AI enhancement completed for {Table}", metadata.TableName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI enhancement failed for {Table}", metadata.TableName);
            metadata.ValidationWarnings.Add("AI enhancement failed");
        }

        return metadata;
    }

    // Private helper methods

    private async Task ExtractFromSchemaAsync(
        ExtractedMetadata result,
        string objectType,
        string schemaName,
        string objectName,
        CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);

        if (objectType == "StoredProcedure" || objectType == "Procedure")
        {
            var sql = @"
                SELECT
                    p.name AS ProcedureName,
                    s.name AS SchemaName,
                    p.create_date AS CreatedDate,
                    p.modify_date AS LastModified,
                    (SELECT COUNT(*) FROM sys.parameters WHERE object_id = p.object_id) AS ParameterCount
                FROM sys.procedures p
                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                WHERE s.name = @SchemaName AND p.name = @ObjectName";

            var procInfo = await connection.QueryFirstOrDefaultAsync(sql, new
            {
                SchemaName = schemaName,
                ObjectName = objectName
            });

            if (procInfo != null)
            {
                result.AdditionalMetadata["CreatedDate"] = procInfo.CreatedDate;
                result.AdditionalMetadata["LastModified"] = procInfo.LastModified;
                result.AdditionalMetadata["ParameterCount"] = procInfo.ParameterCount;
                result.FieldConfidences["SchemaName"] = 1.0;
                result.FieldConfidences["TableName"] = 1.0;
            }
        }
        else if (objectType == "Table")
        {
            var sql = @"
                SELECT
                    t.name AS TableName,
                    s.name AS SchemaName,
                    (SELECT COUNT(*) FROM sys.columns WHERE object_id = t.object_id) AS ColumnCount
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                WHERE s.name = @SchemaName AND t.name = @ObjectName";

            var tableInfo = await connection.QueryFirstOrDefaultAsync(sql, new
            {
                SchemaName = schemaName,
                ObjectName = objectName
            });

            if (tableInfo != null)
            {
                result.AdditionalMetadata["ColumnCount"] = tableInfo.ColumnCount;
                result.FieldConfidences["SchemaName"] = 1.0;
                result.FieldConfidences["TableName"] = 1.0;
            }
        }
    }

    private void ExtractEntitiesFromDefinition(ExtractedMetadata result, string definition)
    {
        // Extract table references: [schema].[table]
        var tablePattern = @"\b([a-zA-Z_]+)\.([a-zA-Z_]+)\b";
        var matches = Regex.Matches(definition, tablePattern);

        var referencedTables = matches
            .Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}")
            .Distinct()
            .Where(t => !t.StartsWith("sys.") && !t.StartsWith("INFORMATION_SCHEMA."))
            .ToList();

        if (referencedTables.Any())
        {
            result.AdditionalMetadata["ReferencedTables"] = referencedTables;
            result.FieldConfidences["ReferencedTables"] = 0.75;
        }

        // Extract procedure calls: EXEC [schema].[procedure]
        var procPattern = @"EXEC(?:UTE)?\s+([a-zA-Z_]+)\.([a-zA-Z_]+)";
        var procMatches = Regex.Matches(definition, procPattern, RegexOptions.IgnoreCase);

        var calledProcs = procMatches
            .Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}")
            .Distinct()
            .ToList();

        if (calledProcs.Any())
        {
            result.ModifiedProcedures = calledProcs;
            result.FieldConfidences["ModifiedProcedures"] = 0.70;
        }
    }

    private void ExtractStructuredDataFromDocument(ExtractedMetadata result, string text)
    {
        // Look for patterns in document text
        var patterns = new Dictionary<string, string>
        {
            { "Table", @"Table:\s*([a-zA-Z_]+(?:\.[a-zA-Z_]+)?)" },
            { "Column", @"Column:\s*([a-zA-Z_]+)" },
            { "Jira", @"Jira:\s*([A-Z]+-\d+)" },
            { "CAB", @"CAB:\s*([A-Z]+-\d+)" },
            { "Schema", @"Schema:\s*([a-zA-Z_]+)" }
        };

        foreach (var (field, pattern) in patterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var value = match.Groups[1].Value;
                switch (field)
                {
                    case "Table":
                        if (value.Contains('.'))
                        {
                            var parts = value.Split('.');
                            result.SchemaName = parts[0];
                            result.TableName = parts[1];
                        }
                        else
                        {
                            result.TableName = value;
                        }
                        result.FieldConfidences["TableName"] = 0.9;
                        break;
                    case "Column":
                        result.ColumnName = value;
                        result.FieldConfidences["ColumnName"] = 0.9;
                        break;
                    case "Jira":
                        result.JiraNumber = value;
                        result.FieldConfidences["JiraNumber"] = 1.0;
                        break;
                    case "CAB":
                        result.CABNumber = value;
                        result.FieldConfidences["CABNumber"] = 1.0;
                        break;
                    case "Schema":
                        result.SchemaName = value;
                        result.FieldConfidences["SchemaName"] = 0.9;
                        break;
                }
            }
        }
    }

    private Task ExtractEntitiesWithAdvancedNERAsync(
        ExtractedMetadata result,
        string filePath,
        CancellationToken ct)
    {
        // TODO: Implement advanced NER using spaCy, Azure Cognitive Services, or similar
        // For now, use regex-based extraction
        return;
    }

    private async Task ExtractWithOpenAIAsync(
        ExtractedMetadata result,
        string definition,
        CancellationToken ct)
    {
        try
        {
            var enhancementRequest = new DocumentationEnhancementRequest
            {
                Description = definition.Length > 1000 ? definition.Substring(0, 1000) : definition,
                Documentation = "",
                ChangeType = "StoredProcedure",
                Context = new Dictionary<string, object>
                {
                    ["TableName"] = result.TableName ?? "",
                    ["SchemaName"] = result.SchemaName ?? ""
                }
            };

            var enhanced = await _openAI.EnhanceDocumentationAsync(enhancementRequest, ct);

            result.Description = enhanced.EnhancedDescription;
            result.AIGeneratedTags = enhanced.KeyPoints;
            result.FieldConfidences["Description"] = 0.80;
            result.FieldConfidences["AIGeneratedTags"] = 0.75;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI extraction failed for {Name}", result.TableName);
            result.ValidationWarnings.Add("AI extraction failed");
        }
    }

    private Task ExtractDescriptionFromDocumentAsync(
        ExtractedMetadata result,
        string filePath,
        CancellationToken ct)
    {
        // TODO: Extract description from document using OpenAI
        return;
    }

    private async Task ValidateAgainstSchemaAsync(ExtractedMetadata result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.TableName))
            return;

        result.TableExists = await ValidateTableExistsAsync(
            result.SchemaName,
            result.TableName,
            ct);

        if (!result.TableExists)
        {
            result.ValidationWarnings.Add($"Table {result.SchemaName}.{result.TableName} not found");
            if (result.FieldConfidences.ContainsKey("TableName"))
                result.FieldConfidences["TableName"] *= 0.5;
        }

        if (!string.IsNullOrWhiteSpace(result.ColumnName) && result.TableExists)
        {
            result.ColumnExists = await ValidateColumnExistsAsync(
                result.SchemaName,
                result.TableName,
                result.ColumnName,
                ct);

            if (!result.ColumnExists)
            {
                result.ValidationWarnings.Add($"Column {result.ColumnName} not found");
                if (result.FieldConfidences.ContainsKey("ColumnName"))
                    result.FieldConfidences["ColumnName"] *= 0.5;
            }
        }
    }

    private async Task FuzzyMatchToMasterIndexAsync(ExtractedMetadata result, CancellationToken ct)
    {
        // TODO: Implement fuzzy matching against MasterIndex
        // Use Levenshtein distance or cosine similarity
        await Task.CompletedTask;
    }

    private void CalculateOverallConfidence(ExtractedMetadata result)
    {
        if (result.FieldConfidences.Any())
        {
            result.OverallConfidence = result.FieldConfidences.Values.Average();
        }
        else
        {
            result.OverallConfidence = 0.5;  // Default if no confidence scores
        }

        // Penalize for validation warnings
        if (result.ValidationWarnings.Any())
        {
            result.OverallConfidence *= Math.Max(0.5, 1.0 - (result.ValidationWarnings.Count * 0.1));
        }

        // Penalize for validation errors
        if (result.ValidationErrors.Any())
        {
            result.OverallConfidence *= 0.3;
        }
    }

    private async Task<bool> ValidateTableExistsAsync(
        string? schema,
        string? table,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table))
            return false;

        using var connection = new SqlConnection(_connectionString);
        var sql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table";

        var count = await connection.ExecuteScalarAsync<int>(sql, new { Schema = schema, Table = table });
        return count > 0;
    }

    private async Task<bool> ValidateColumnExistsAsync(
        string? schema,
        string? table,
        string? column,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(column))
            return false;

        using var connection = new SqlConnection(_connectionString);
        var sql = @"
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table AND COLUMN_NAME = @Column";

        var count = await connection.ExecuteScalarAsync<int>(sql, new
        {
            Schema = schema,
            Table = table,
            Column = column
        });
        return count > 0;
    }

    private async Task<List<string>> FindSimilarTableNamesAsync(string? tableName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return new List<string>();

        using var connection = new SqlConnection(_connectionString);
        var sql = @"
            SELECT TOP 5 TABLE_SCHEMA + '.' + TABLE_NAME AS FullName
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME LIKE @Pattern
            ORDER BY TABLE_NAME";

        var pattern = $"%{tableName}%";
        var results = await connection.QueryAsync<string>(sql, new { Pattern = pattern });
        return results.ToList();
    }

    private async Task<List<string>> FindSimilarColumnNamesAsync(
        string? schema,
        string? table,
        string? columnName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return new List<string>();

        using var connection = new SqlConnection(_connectionString);
        var sql = @"
            SELECT TOP 5 COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema
              AND TABLE_NAME = @Table
              AND COLUMN_NAME LIKE @Pattern
            ORDER BY COLUMN_NAME";

        var pattern = $"%{columnName}%";
        var results = await connection.QueryAsync<string>(sql, new
        {
            Schema = schema,
            Table = table,
            Pattern = pattern
        });
        return results.ToList();
    }

    private string DetermineSemanticCategory(string? changeType)
    {
        return changeType switch
        {
            "Business Request" => "New Feature",
            "Enhancement" => "Improvement",
            "Defect Fix" => "Bug Fix",
            "Anomaly" => "Data Quality Issue",
            "Research" => "Investigation",
            _ => "General Change"
        };
    }
}
