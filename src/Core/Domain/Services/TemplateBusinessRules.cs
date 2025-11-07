using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Services;

/// <summary>
/// Domain service containing template business rules and logic
/// Centralizes complex business rules for template operations
/// </summary>
public static class TemplateBusinessRules
{
    /// <summary>
    /// Determines if a user can modify a template based on business rules
    /// </summary>
    /// <param name="template">Template to check</param>
    /// <param name="userId">User attempting modification</param>
    /// <param name="userRole">User's role</param>
    /// <returns>True if user can modify the template</returns>
    public static bool CanUserModifyTemplate(Template template, UserId userId, UserRole userRole)
    {
        // Creator can always modify their templates
        if (template.CreatedBy.Equals(userId))
            return true;

        // Admins and contributors can modify templates
        return userRole is UserRole.Administrator or UserRole.Contributor;
    }

    /// <summary>
    /// Determines if a template can be used for document generation
    /// </summary>
    /// <param name="template">Template to check</param>
    /// <returns>True if template can be used</returns>
    public static bool CanUseTemplate(Template template)
    {
        return template.IsActive;
    }

    /// <summary>
    /// Determines if a template can be deleted based on usage
    /// </summary>
    /// <param name="template">Template to check</param>
    /// <returns>True if template can be deleted</returns>
    public static bool CanDeleteTemplate(Template template)
    {
        // Templates that have been used should not be deleted to maintain referential integrity
        return template.UsageCount == 0;
    }

    /// <summary>
    /// Calculates the priority level of a template based on usage
    /// </summary>
    /// <param name="usageCount">Number of times template has been used</param>
    /// <returns>Priority level (High, Medium, Low)</returns>
    public static string CalculateTemplatePriority(int usageCount)
    {
        return usageCount switch
        {
            >= 100 => "High",
            >= 20 => "Medium",
            _ => "Low"
        };
    }

    /// <summary>
    /// Determines if a template needs review based on business rules
    /// </summary>
    /// <param name="template">Template to check</param>
    /// <param name="daysSinceLastModified">Days since last modification</param>
    /// <returns>True if template needs review</returns>
    public static bool NeedsReview(Template template, int daysSinceLastModified)
    {
        // High usage templates should be reviewed more frequently
        var reviewThreshold = template.UsageCount switch
        {
            >= 100 => 90,  // Every 3 months for high usage
            >= 20 => 180,  // Every 6 months for medium usage
            _ => 365       // Yearly for low usage
        };

        return daysSinceLastModified > reviewThreshold;
    }

    /// <summary>
    /// Validates template variable consistency
    /// </summary>
    /// <param name="content">Template content</param>
    /// <param name="variables">Defined variables</param>
    /// <returns>List of issues found</returns>
    public static List<string> ValidateVariableConsistency(string content, List<TemplateVariable> variables)
    {
        var issues = new List<string>();
        var definedVariables = variables.Select(v => v.Name).ToHashSet();
        var usedVariables = ExtractVariablesFromContent(content);

        // Check for undefined variables used in content
        foreach (var usedVar in usedVariables)
        {
            if (!definedVariables.Contains(usedVar))
            {
                issues.Add($"Variable '{usedVar}' used in content but not defined");
            }
        }

        // Check for defined variables not used in content
        foreach (var definedVar in definedVariables)
        {
            if (!usedVariables.Contains(definedVar))
            {
                issues.Add($"Variable '{definedVar}' defined but not used in content");
            }
        }

        return issues;
    }

    /// <summary>
    /// Extracts variable names from template content
    /// Assumes variables are in {{variableName}} format
    /// </summary>
    /// <param name="content">Template content</param>
    /// <returns>Set of variable names found</returns>
    private static HashSet<string> ExtractVariablesFromContent(string content)
    {
        var variables = new HashSet<string>();
        var pattern = @"\{\{([^}]+)\}\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                variables.Add(match.Groups[1].Value.Trim());
            }
        }

        return variables;
    }
}