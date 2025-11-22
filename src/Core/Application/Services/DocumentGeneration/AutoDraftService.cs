using Enterprise.Documentation.Core.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

/// <summary>
/// Orchestrates automatic draft creation for completed Excel entries
/// </summary>
public interface IAutoDraftService
{
    Task<AutoDraftResult> CreateDraftForCompletedEntryAsync(
        DocumentChangeEntry entry,
        CancellationToken cancellationToken = default);
}

public class AutoDraftResult
{
    public bool Success { get; set; }
    public string? DocId { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class AutoDraftService : IAutoDraftService
{
    private readonly ILogger<AutoDraftService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDocIdGeneratorService _docIdGenerator;
    private readonly IOpenAIEnhancementService _openAIEnhancement;
    private readonly ITemplateExecutorService _templateExecutor;
    private readonly string _baseOutputPath;

    public AutoDraftService(
        ILogger<AutoDraftService> logger,
        IConfiguration configuration,
        IDocIdGeneratorService docIdGenerator,
        IOpenAIEnhancementService openAIEnhancement,
        ITemplateExecutorService templateExecutor)
    {
        _logger = logger;
        _configuration = configuration;
        _docIdGenerator = docIdGenerator;
        _openAIEnhancement = openAIEnhancement;
        _templateExecutor = templateExecutor;

        _baseOutputPath = configuration["DocumentGeneration:BaseOutputPath"]
            ?? @"C:\Temp\Documentation-Catalog";
    }

    public async Task<AutoDraftResult> CreateDraftForCompletedEntryAsync(
        DocumentChangeEntry entry,
        CancellationToken cancellationToken = default)
    {
        var result = new AutoDraftResult();

        try
        {
            _logger.LogInformation("Creating auto-draft for CAB {CABNumber}, Jira {JiraNumber}",
                entry.CABNumber, entry.JiraNumber);

            // 1. Validate entry
            if (!ValidateEntry(entry, result))
            {
                return result;
            }

            // 2. Generate DocId
            var docIdRequest = new DocIdRequest
            {
                ChangeType = entry.ChangeType ?? "Enhancement",
                Table = entry.TableName ?? "Unknown",
                Column = entry.ColumnName,
                JiraNumber = entry.JiraNumber ?? "UNKNOWN",
                UpdatedBy = "AutoDraftService"
            };

            result.DocId = await _docIdGenerator.GenerateDocIdAsync(docIdRequest, cancellationToken);

            _logger.LogInformation("Generated DocId: {DocId}", result.DocId);

            // 3. Enhance documentation with OpenAI
            var enhancementRequest = new DocumentationEnhancementRequest
            {
                ChangeType = entry.ChangeType ?? "Enhancement",
                Description = entry.Description ?? "No description provided",
                Documentation = entry.Documentation ?? "No documentation provided",
                Table = entry.TableName,
                Column = entry.ColumnName,
                ModifiedObjects = entry.ModifiedObjects,
                CABNumber = entry.CABNumber,
                JiraNumber = entry.JiraNumber
            };

            var enhanced = await _openAIEnhancement.EnhanceDocumentationAsync(enhancementRequest, cancellationToken);

            _logger.LogInformation("Enhanced documentation with OpenAI");

            // 4. Build template data JSON
            var templateData = BuildTemplateData(entry, enhanced, result.DocId);

            // 5. Determine template type and output path
            var (templateType, outputPath) = DetermineTemplateAndPath(entry, result.DocId);

            result.FilePath = outputPath;

            // 6. Execute template to generate document
            var templateRequest = new TemplateExecutionRequest
            {
                TemplateType = templateType,
                OutputPath = outputPath,
                TemplateData = templateData
            };

            await _templateExecutor.GenerateDocumentAsync(templateRequest, cancellationToken);

            _logger.LogInformation("Document generated successfully at: {FilePath}", result.FilePath);

            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating auto-draft for CAB {CABNumber}", entry.CABNumber);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private bool ValidateEntry(DocumentChangeEntry entry, AutoDraftResult result)
    {
        var isValid = true;

        if (string.IsNullOrWhiteSpace(entry.JiraNumber))
        {
            result.Warnings.Add("Jira number is missing");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(entry.ChangeType))
        {
            result.Warnings.Add("Change type is missing");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(entry.TableName))
        {
            result.Warnings.Add("Table name is missing");
            // Don't fail - we can use "Unknown"
        }

        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            result.Warnings.Add("Description is empty");
        }

        if (string.IsNullOrWhiteSpace(entry.Documentation))
        {
            result.Warnings.Add("Documentation is empty");
        }

        return isValid;
    }

    private object BuildTemplateData(DocumentChangeEntry entry, EnhancedDocumentation enhanced, string docId)
    {
        // Parse schema and object name from TableName (e.g., "gwpc.irf_policy")
        var (schema, objectName) = ParseSchemaAndObject(entry.TableName ?? "Unknown");

        // Common fields for all templates
        var commonData = new Dictionary<string, object?>
        {
            { "docId", docId },
            { "ticket", entry.JiraNumber },
            { "cabNumber", entry.CABNumber },
            { "dateEntered", entry.Date?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd") },
            { "author", entry.AssignedTo ?? entry.ReportedBy ?? "Unknown" },
            { "status", entry.Status ?? "Completed" },
            { "schema", schema },
            { "tableName", objectName },
            { "objectName", objectName },
            { "columnName", entry.ColumnName },
            { "priority", entry.Priority },
            { "severity", entry.Severity }
        };

        // Change type specific data
        switch (entry.ChangeType)
        {
            case "Business Request":
                return BuildBusinessRequestData(entry, enhanced, commonData);

            case "Enhancement":
                return BuildEnhancementData(entry, enhanced, commonData);

            case "Defect Fix":
                return BuildDefectFixData(entry, enhanced, commonData);

            default:
                // Default to Enhancement template
                return BuildEnhancementData(entry, enhanced, commonData);
        }
    }

    private object BuildBusinessRequestData(DocumentChangeEntry entry, EnhancedDocumentation enhanced,
        Dictionary<string, object?> commonData)
    {
        return new
        {
            ticket = commonData["ticket"],
            cabNumber = commonData["cabNumber"],
            dateEntered = commonData["dateEntered"],
            author = commonData["author"],
            newTableCreated = commonData["tableName"],
            storedProcedure = entry.ModifiedObjects,
            businessPurpose = enhanced.EnhancedDescription,
            requestDescription = entry.Description,
            reasonForChange = new
            {
                businessDriver = enhanced.KeyPoints.FirstOrDefault() ?? "Not specified",
                solution = enhanced.EnhancedImplementation
            },
            implementationDetails = new
            {
                primarySource = commonData["tableName"],
                loadStrategy = entry.ChangeType
            },
            additionalNotes = string.Join("\n", enhanced.TechnicalDetails)
        };
    }

    private object BuildEnhancementData(DocumentChangeEntry entry, EnhancedDocumentation enhanced,
        Dictionary<string, object?> commonData)
    {
        var proceduresModified = string.IsNullOrWhiteSpace(entry.ModifiedObjects)
            ? new List<string>()
            : entry.ModifiedObjects.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(sp => sp.Trim())
                .ToList();

        return new
        {
            ticket = commonData["ticket"],
            cabNumber = commonData["cabNumber"],
            dateEntered = commonData["dateEntered"],
            author = commonData["author"],
            status = commonData["status"],
            schema = commonData["schema"],
            tableName = commonData["tableName"],
            columnName = commonData["columnName"],
            enhancementDescription = enhanced.EnhancedDescription,
            currentState = entry.Description,
            improvementNeeded = entry.Documentation,
            businessValue = enhanced.KeyPoints.FirstOrDefault() ?? "Improved functionality",
            changesMade = enhanced.TechnicalDetails,
            proceduresModified = proceduresModified.Select(sp => new { name = sp }).ToList(),
            implementationApproach = enhanced.EnhancedImplementation,
            testingValidation = new List<string>
            {
                "Validated changes in development environment",
                "Reviewed impact on existing queries",
                "Confirmed data integrity"
            },
            benefits = new List<object>
            {
                new { title = "Enhanced Functionality", description = enhanced.EnhancedDescription }
            },
            deploymentInfo = new
            {
                database = "IRFS1",
                schema = commonData["schema"],
                date = commonData["dateEntered"]
            }
        };
    }

    private object BuildDefectFixData(DocumentChangeEntry entry, EnhancedDocumentation enhanced,
        Dictionary<string, object?> commonData)
    {
        var proceduresModified = string.IsNullOrWhiteSpace(entry.ModifiedObjects)
            ? new List<string>()
            : entry.ModifiedObjects.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(sp => sp.Trim())
                .ToList();

        return new
        {
            ticket = commonData["ticket"],
            cabNumber = commonData["cabNumber"],
            dateEntered = commonData["dateEntered"],
            author = commonData["author"],
            status = commonData["status"],
            schema = commonData["schema"],
            tablesAffected = commonData["tableName"],
            storedProcedure = proceduresModified.FirstOrDefault(),
            tablePurpose = enhanced.EnhancedDescription,
            defectDescription = entry.Description,
            issueDiscovered = enhanced.KeyPoints.FirstOrDefault() ?? "Issue discovered during testing",
            impact = enhanced.TechnicalDetails,
            rootCause = entry.Description,
            proceduresModified = proceduresModified.Select(sp => new { name = sp }).ToList(),
            implementationApproach = enhanced.EnhancedImplementation,
            testingValidation = new List<string>
            {
                "Verified fix in development environment",
                "Tested edge cases",
                "Confirmed resolution"
            },
            benefits = new List<object>
            {
                new { title = "Resolved Defect", description = enhanced.EnhancedImplementation }
            },
            deploymentInfo = new
            {
                database = "IRFS1",
                schema = commonData["schema"],
                method = "SQL deployment",
                rollbackPlan = "Revert to previous version if issues arise"
            }
        };
    }

    private (string TemplateType, string OutputPath) DetermineTemplateAndPath(DocumentChangeEntry entry, string docId)
    {
        // Map change type to template type
        var templateType = entry.ChangeType switch
        {
            "Business Request" => "BR",
            "Enhancement" => "EN",
            "Defect Fix" => "DF",
            _ => "EN"  // Default to Enhancement
        };

        // Parse schema and object name
        var (schema, objectName) = ParseSchemaAndObject(entry.TableName ?? "Unknown");

        // Determine object type (StoredProcedures or Tables)
        var objectType = objectName.StartsWith("usp_", StringComparison.OrdinalIgnoreCase)
            ? "StoredProcedures"
            : "Tables";

        // Build path: C:\Temp\Documentation-Catalog\IRFS1\{Schema}\{ObjectType}\{ObjectName}\Change Documentation\{DocId}.docx
        var path = Path.Combine(
            _baseOutputPath,
            "IRFS1",
            schema,
            objectType,
            objectName,
            "Change Documentation",
            $"{docId}.docx"
        );

        return (templateType, path);
    }

    private (string Schema, string ObjectName) ParseSchemaAndObject(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return ("Unknown", "Unknown");
        }

        var parts = tableName.Split('.');
        if (parts.Length >= 2)
        {
            return (parts[0], parts[1]);
        }

        return ("dbo", parts[0]);
    }
}
