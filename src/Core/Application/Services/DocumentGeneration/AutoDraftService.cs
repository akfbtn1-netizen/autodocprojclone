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
            try
            {
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
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("DocId generation was canceled for CAB {CAB}, using fallback", entry.CABNumber);
                result.DocId = $"DRAFT-{DateTime.UtcNow:yyyyMMdd}-{entry.CABNumber ?? "UNK"}-{Guid.NewGuid().ToString()[..8]}";
                result.Warnings.Add("Used fallback DocId due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate DocId for CAB {CAB}, using fallback", entry.CABNumber);
                result.DocId = $"DRAFT-{DateTime.UtcNow:yyyyMMdd}-{entry.CABNumber ?? "UNK"}-{Guid.NewGuid().ToString()[..8]}";
                result.Warnings.Add($"Used fallback DocId due to error: {ex.Message}");
            }

            // 3. Enhance documentation with OpenAI
            var enhancementRequest = new DocumentationEnhancementRequest
            {
                ChangeType = entry.ChangeType ?? "Enhancement",
                Description = entry.Description ?? "No description provided",
                Documentation = entry.ChangeApplied ?? "No documentation provided",
                Table = entry.TableName,
                Column = entry.ColumnName,
                ModifiedStoredProcedures = entry.ModifiedStoredProcedures,
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

            // Create a more generous timeout for template generation
            using var templateCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            templateCts.CancelAfter(TimeSpan.FromMinutes(3)); // 3 minutes for template generation
            
            await _templateExecutor.GenerateDocumentAsync(templateRequest, templateCts.Token);

            _logger.LogInformation("Document generated successfully at: {FilePath}", result.FilePath);

            result.Success = true;

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Auto-draft creation was cancelled for CAB {CABNumber}", entry.CABNumber);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Auto-draft creation timed out for CAB {CABNumber}", entry.CABNumber);
            result.ErrorMessage = "Document generation timed out. This may be due to system load or network issues.";
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

        if (string.IsNullOrWhiteSpace(entry.ChangeApplied))
        {
            result.Warnings.Add("Change Applied documentation is empty");
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
            Title = $"Business Request - {entry.JiraNumber}",
            DocumentId = commonData["docId"],
            Jira = commonData["ticket"],
            Status = commonData["status"],
            DateRequested = commonData["dateEntered"],
            ReportedBy = entry.ReportedBy ?? "Unknown",
            AssignedTo = commonData["author"],
            ExecutiveSummary = enhanced.EnhancedDescription,
            BusinessJustification = enhanced.KeyPoints.FirstOrDefault() ?? "Business requirement for system enhancement",
            InScope = new List<string>
            {
                $"Database table: {commonData["tableName"]}",
                entry.ModifiedStoredProcedures != null ? $"Stored procedures: {entry.ModifiedStoredProcedures}" : "Database modifications",
                "Data structure changes"
            },
            OutOfScope = new List<string>
            {
                "User interface changes",
                "Third-party integrations", 
                "Legacy data migration"
            },
            SuccessCriteria = enhanced.KeyPoints.Take(3).ToList(),
            FunctionalRequirements = new List<object>
            {
                new 
                {
                    Id = "FR-001",
                    Title = "Database Enhancement",
                    Description = enhanced.EnhancedImplementation,
                    AcceptanceCriteria = string.Join("; ", enhanced.TechnicalDetails.Take(2))
                }
            },
            NonFunctionalRequirements = new List<object>
            {
                new 
                {
                    Category = "Performance",
                    Description = "System response time should not be negatively impacted"
                },
                new 
                {
                    Category = "Reliability", 
                    Description = "Data integrity must be maintained throughout the process"
                }
            },
            Assumptions = new List<string>
            {
                "Database backup and recovery procedures are in place",
                "Required database permissions are available",
                "Change can be implemented during maintenance window"
            },
            Dependencies = new List<string>
            {
                "Database administrator approval",
                "Change advisory board approval",
                "Testing environment availability"
            },
            Risks = new List<object>
            {
                new 
                {
                    Description = "Data inconsistency during implementation",
                    Likelihood = "Low",
                    Impact = "Medium",
                    Mitigation = "Comprehensive backup and testing strategy"
                }
            },
            Timeline = new List<object>
            {
                new 
                {
                    Phase = "Analysis and Design",
                    StartDate = commonData["dateEntered"],
                    Duration = "1-2 days",
                    Deliverables = new List<string> { "Technical specification", "Impact analysis" }
                },
                new 
                {
                    Phase = "Implementation", 
                    StartDate = "TBD",
                    Duration = "2-4 hours",
                    Deliverables = new List<string> { "Database changes", "Documentation update" }
                }
            },
            Budget = new 
            {
                EstimatedCost = "Low",
                CostBreakdown = new List<object>
                {
                    new { Item = "Development time", Cost = "4-8 hours" },
                    new { Item = "Testing time", Cost = "2-4 hours" }
                }
            }
        };
    }

    private object BuildEnhancementData(DocumentChangeEntry entry, EnhancedDocumentation enhanced,
        Dictionary<string, object?> commonData)
    {
        var proceduresModified = string.IsNullOrWhiteSpace(entry.ModifiedStoredProcedures)
            ? new List<string>()
            : entry.ModifiedStoredProcedures.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(sp => sp.Trim())
                .ToList();

        // Create proper EnhancementData structure matching the template expectations
        return new
        {
            Title = $"Enhancement Request - {entry.JiraNumber}",
            DocumentId = commonData["docId"],
            Jira = commonData["ticket"],
            Status = commonData["status"],
            DateRequested = commonData["dateEntered"],
            ReportedBy = entry.ReportedBy ?? "Unknown",
            AssignedTo = commonData["author"],
            CurrentState = entry.Description ?? "No current state description provided",
            ProposedEnhancement = enhanced.EnhancedDescription,
            BusinessValue = enhanced.KeyPoints.FirstOrDefault() ?? "Improved functionality and system performance",
            UserStories = new List<object>
            {
                new 
                {
                    Id = "US-1",
                    Title = "Database Enhancement",
                    AsA = "Database User",
                    IWant = enhanced.EnhancedDescription,
                    SoThat = enhanced.KeyPoints.FirstOrDefault() ?? "the system functions more effectively",
                    AcceptanceCriteria = enhanced.TechnicalDetails.Take(3).ToList()
                }
            },
            TechnicalApproach = enhanced.EnhancedImplementation,
            CodeExamples = !string.IsNullOrEmpty(entry.ModifiedStoredProcedures) 
                ? $"Modified Stored Procedures: {entry.ModifiedStoredProcedures}" 
                : "",
            ImplementationSteps = new List<object>
            {
                new 
                {
                    Phase = 1,
                    Title = "Database Changes",
                    Description = entry.ChangeApplied ?? "Apply database modifications",
                    EstimatedEffort = "2-4 hours",
                    Dependencies = new List<string> { "Database access", "Change approval" }
                }
            },
            TestingStrategy = new 
            {
                UnitTesting = "Validated individual stored procedure changes",
                IntegrationTesting = "Tested integration with existing database queries and applications",
                UserAcceptanceTesting = "Confirmed functionality meets business requirements"
            },
            PerformanceImpact = enhanced.TechnicalDetails.FirstOrDefault() ?? "Minimal performance impact expected",
            SecurityConsiderations = "Database access permissions reviewed and maintained",
            RollbackPlan = "Database backup created before changes; rollback scripts prepared",
            SuccessMetrics = enhanced.KeyPoints.Take(2).ToList(),
            Risks = new List<object>
            {
                new 
                {
                    Description = "Potential data inconsistency",
                    Impact = "Medium",
                    Probability = "Low", 
                    Mitigation = "Comprehensive testing and backup strategy"
                }
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
        var proceduresModified = string.IsNullOrWhiteSpace(entry.ModifiedStoredProcedures)
            ? new List<string>()
            : entry.ModifiedStoredProcedures.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(sp => sp.Trim())
                .ToList();

        return new
        {
            Title = $"Defect Fix - {entry.JiraNumber}",
            DocumentId = commonData["docId"],
            Jira = commonData["ticket"],
            Status = commonData["status"],
            DateRequested = commonData["dateEntered"],
            ReportedBy = entry.ReportedBy ?? "Unknown",
            AssignedTo = commonData["author"],
            ProblemDescription = entry.Description ?? "No problem description provided",
            StepsToReproduce = new List<string>
            {
                "Access the database system",
                $"Query table: {commonData["tableName"]}",
                "Observe the reported issue"
            },
            ExpectedResult = "System should function correctly without errors",
            ActualResult = enhanced.EnhancedDescription,
            Environment = new 
            {
                OperatingSystem = "Database Server",
                BrowserVersion = "N/A - Database Issue", 
                ApplicationVersion = "Current Production",
                AdditionalInfo = $"Database: {commonData["schema"]}, Table: {commonData["tableName"]}"
            },
            Severity = commonData["severity"] ?? "Medium",
            Priority = commonData["priority"] ?? "Medium", 
            Impact = enhanced.KeyPoints.FirstOrDefault() ?? "System functionality affected",
            Screenshots = new List<string>(), // Database issues typically don't have screenshots
            Workaround = "Manual intervention required until fix is deployed",
            RelatedDefects = new List<string>(),
            RootCause = enhanced.TechnicalDetails.FirstOrDefault() ?? "Database configuration or data issue",
            Resolution = enhanced.EnhancedImplementation,
            CodeChanges = proceduresModified.Any() 
                ? $"Modified stored procedures: {string.Join(", ", proceduresModified)}"
                : "Database schema or data corrections applied",
            TestingNotes = "Verified fix in development environment and confirmed resolution",
            TestCases = new List<object>
            {
                new 
                {
                    Name = "Defect Verification", 
                    Description = "Verify the reported issue is resolved",
                    ExpectedResult = "System functions correctly",
                    Status = "Passed"
                }
            },
            PreventionMeasures = "Enhanced validation and monitoring implemented"
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
