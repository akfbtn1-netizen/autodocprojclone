using Enterprise.Documentation.Core.Domain.Entities;
using Core.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Core.Application.Services;

/// <summary>
/// Service for selecting appropriate document templates based on tier and content type
/// </summary>
public class TemplateSelector : Core.Application.Interfaces.ITemplateSelector
{
    private readonly ILogger<TemplateSelector> _logger;

    public TemplateSelector(ILogger<TemplateSelector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Selects the most appropriate template based on entry characteristics and tier
    /// </summary>
    public async Task<TemplateInfo> SelectTemplateAsync(ExcelChangeEntry entry, string tier, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await DetermineOptimalTemplateAsync(entry, tier);
            _logger.LogInformation("Selected template {TemplateName} for entry {JiraNumber} (Tier: {Tier})", 
                template.Name, entry.JiraNumber, tier);
            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting template for entry {JiraNumber}", entry.JiraNumber);
            throw;
        }
    }

    /// <summary>
    /// Gets all available templates for a specific tier
    /// </summary>
    public async Task<List<TemplateInfo>> GetAvailableTemplatesAsync(string tier, CancellationToken cancellationToken = default)
    {
        var templates = new List<TemplateInfo>();

        switch (tier.ToLower())
        {
            case "tier1":
                templates.AddRange(await GetTier1TemplatesAsync());
                break;
            case "tier2":
                templates.AddRange(await GetTier2Templates());
                break;
            case "tier3":
                templates.AddRange(await GetTier3Templates());
                break;
            default:
                throw new ArgumentException($"Unknown tier: {tier}");
        }

        _logger.LogInformation("Retrieved {Count} templates for {Tier}", templates.Count, tier);
        return templates;
    }

    /// <summary>
    /// Validates template compatibility with entry requirements
    /// </summary>
    public async Task<bool> ValidateTemplateAsync(TemplateInfo template, ExcelChangeEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if template supports the document type
            if (!template.SupportedDocumentTypes.Contains(entry.DocumentType, StringComparer.OrdinalIgnoreCase))
                return false;

            // Check schema compatibility
            if (!string.IsNullOrEmpty(template.RequiredSchema) && 
                !template.RequiredSchema.Equals(entry.SchemaName, StringComparison.OrdinalIgnoreCase))
                return false;

            // Validate object type compatibility
            var objectType = DetermineObjectType(entry.ObjectName);
            if (!template.SupportedObjectTypes.Contains(objectType, StringComparer.OrdinalIgnoreCase))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template {TemplateName} for entry {JiraNumber}", 
                template.Name, entry.JiraNumber);
            return false;
        }
    }

    public async Task<TemplateInfo> DetermineOptimalTemplateAsync(ExcelChangeEntry entry, string tier)
    {
        var availableTemplates = await GetAvailableTemplatesAsync(tier);
        
        // Score templates based on compatibility
        var scoredTemplates = new List<(TemplateInfo Template, int Score)>();
        
        foreach (var template in availableTemplates)
        {
            var score = CalculateTemplateScore(template, entry);
            scoredTemplates.Add((template, score));
        }
        
        // Return the highest scoring template
        var bestMatch = scoredTemplates.OrderByDescending(x => x.Score).First();
        
        if (bestMatch.Score == 0)
        {
            throw new InvalidOperationException($"No suitable template found for {entry.DocumentType} in {tier}");
        }
        
        return bestMatch.Template;
    }

    private async Task<int> CalculateTemplateScoreAsync(TemplateInfo template, ExcelChangeEntry entry)
    {
        var score = 0;

        // Document type match (highest priority)
        if (template.SupportedDocumentTypes.Contains(entry.DocumentType, StringComparer.OrdinalIgnoreCase))
            score += 10;

        // Object type match
        var objectType = DetermineObjectType(entry.ObjectName);
        if (template.SupportedObjectTypes.Contains(objectType, StringComparer.OrdinalIgnoreCase))
            score += 8;

        // Schema preference
        if (!string.IsNullOrEmpty(template.RequiredSchema) && 
            template.RequiredSchema.Equals(entry.SchemaName, StringComparison.OrdinalIgnoreCase))
            score += 5;

        // Complexity match
        var entryComplexity = DetermineComplexity(entry);
        if (template.Complexity.Equals(entryComplexity, StringComparison.OrdinalIgnoreCase))
            score += 3;

        return score;
    }

    public async Task<List<TemplateInfo>> GetTier1TemplatesAsync()
    {
        return new List<TemplateInfo>
        {
            new TemplateInfo
            {
                Id = "T1_SIMPLE_TABLE",
                Name = "Simple Table Documentation",
                Description = "Basic table documentation template",
                FilePath = "templates/tier1/simple_table.docx",
                Tier = "Tier1",
                Complexity = "Simple",
                SupportedDocumentTypes = new List<string> { "Table", "Simple View" },
                SupportedObjectTypes = new List<string> { "TABLE", "VIEW" },
                EstimatedTime = TimeSpan.FromMinutes(15)
            },
            new TemplateInfo
            {
                Id = "T1_BASIC_VIEW",
                Name = "Basic View Documentation",
                Description = "Standard view documentation template",
                FilePath = "templates/tier1/basic_view.docx",
                Tier = "Tier1",
                Complexity = "Simple",
                SupportedDocumentTypes = new List<string> { "View", "Simple View" },
                SupportedObjectTypes = new List<string> { "VIEW" },
                EstimatedTime = TimeSpan.FromMinutes(20)
            },
            new TemplateInfo
            {
                Id = "T1_SIMPLE_FUNCTION",
                Name = "Simple Function Documentation",
                Description = "Basic function documentation template",
                FilePath = "templates/tier1/simple_function.docx",
                Tier = "Tier1",
                Complexity = "Simple",
                SupportedDocumentTypes = new List<string> { "Function", "Scalar Function" },
                SupportedObjectTypes = new List<string> { "FUNCTION" },
                EstimatedTime = TimeSpan.FromMinutes(25)
            }
        };
    }

    private async Task<List<TemplateInfo>> GetTier2TemplatesAsync()
    {
        return new List<TemplateInfo>
        {
            new TemplateInfo
            {
                Id = "T2_COMPLEX_TABLE",
                Name = "Complex Table Documentation",
                Description = "Comprehensive table documentation with relationships",
                FilePath = "templates/tier2/complex_table.docx",
                Tier = "Tier2",
                Complexity = "Moderate",
                SupportedDocumentTypes = new List<string> { "Table", "Complex Table" },
                SupportedObjectTypes = new List<string> { "TABLE" },
                EstimatedTime = TimeSpan.FromMinutes(45)
            },
            new TemplateInfo
            {
                Id = "T2_BUSINESS_VIEW",
                Name = "Business View Documentation",
                Description = "Business-focused view documentation",
                FilePath = "templates/tier2/business_view.docx",
                Tier = "Tier2",
                Complexity = "Moderate",
                SupportedDocumentTypes = new List<string> { "View", "Business View" },
                SupportedObjectTypes = new List<string> { "VIEW" },
                EstimatedTime = TimeSpan.FromMinutes(40)
            },
            new TemplateInfo
            {
                Id = "T2_STORED_PROCEDURE",
                Name = "Stored Procedure Documentation",
                Description = "Standard stored procedure documentation",
                FilePath = "templates/tier2/stored_procedure.docx",
                Tier = "Tier2",
                Complexity = "Moderate",
                SupportedDocumentTypes = new List<string> { "Stored Procedure", "Procedure" },
                SupportedObjectTypes = new List<string> { "PROCEDURE" },
                EstimatedTime = TimeSpan.FromHours(1)
            }
        };
    }

    private async Task<List<TemplateInfo>> GetTier3TemplatesAsync()
    {
        return new List<TemplateInfo>
        {
            new TemplateInfo
            {
                Id = "T3_ENTERPRISE_PROCEDURE",
                Name = "Enterprise Stored Procedure Documentation",
                Description = "Comprehensive enterprise-grade procedure documentation",
                FilePath = "templates/tier3/enterprise_procedure.docx",
                Tier = "Tier3",
                Complexity = "Complex",
                SupportedDocumentTypes = new List<string> { "Stored Procedure", "Complex Procedure", "Enterprise Procedure" },
                SupportedObjectTypes = new List<string> { "PROCEDURE" },
                EstimatedTime = TimeSpan.FromHours(2)
            },
            new TemplateInfo
            {
                Id = "T3_CRITICAL_SYSTEM",
                Name = "Critical System Component Documentation",
                Description = "Mission-critical system component documentation",
                FilePath = "templates/tier3/critical_system.docx",
                Tier = "Tier3",
                Complexity = "Complex",
                SupportedDocumentTypes = new List<string> { "System Function", "Critical Table", "Security Object" },
                SupportedObjectTypes = new List<string> { "TABLE", "FUNCTION", "PROCEDURE", "VIEW" },
                EstimatedTime = TimeSpan.FromHours(3)
            },
            new TemplateInfo
            {
                Id = "T3_INTEGRATION_COMPONENT",
                Name = "Integration Component Documentation",
                Description = "Complex integration and ETL component documentation",
                FilePath = "templates/tier3/integration_component.docx",
                Tier = "Tier3",
                Complexity = "Complex",
                SupportedDocumentTypes = new List<string> { "Integration Function", "ETL Procedure", "Data Pipeline" },
                SupportedObjectTypes = new List<string> { "PROCEDURE", "FUNCTION" },
                EstimatedTime = TimeSpan.FromHours(2.5)
            }
        };
    }

    private string DetermineObjectType(string objectName)
    {
        var lowerName = objectName.ToLower();
        
        if (lowerName.StartsWith("sp_") || lowerName.StartsWith("proc_") || lowerName.Contains("procedure"))
            return "PROCEDURE";
        if (lowerName.StartsWith("fn_") || lowerName.StartsWith("func_") || lowerName.Contains("function"))
            return "FUNCTION";
        if (lowerName.StartsWith("vw_") || lowerName.StartsWith("view_") || lowerName.Contains("view"))
            return "VIEW";
        if (lowerName.StartsWith("tbl_") || lowerName.StartsWith("table_"))
            return "TABLE";
        
        return "TABLE"; // Default assumption
    }

    private string DetermineComplexity(ExcelChangeEntry entry)
    {
        var description = entry.Description?.ToLower() ?? string.Empty;
        
        if (description.Contains("complex") || description.Contains("enterprise") || 
            description.Contains("critical") || description.Contains("integration"))
            return "Complex";
            
        if (description.Contains("business") || description.Contains("moderate") ||
            description.Contains("advanced"))
            return "Moderate";
            
        return "Simple";
    }

    public async Task<List<TemplateInfo>> GetTier2Templates()
    {
        return await GetTier2TemplatesAsync();
    }

    public async Task<List<TemplateInfo>> GetTier3Templates()
    {
        return await GetTier3TemplatesAsync();
    }

    public int CalculateTemplateScore(TemplateInfo template, ExcelChangeEntry entry)
    {
        return CalculateTemplateScoreAsync(template, entry).Result;
    }
}