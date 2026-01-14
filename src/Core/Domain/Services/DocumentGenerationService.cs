
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Exceptions;

namespace Enterprise.Documentation.Core.Domain.Services;

/// <summary>
/// Domain service for complex document generation operations that span multiple entities.
/// Encapsulates business logic that doesn't naturally belong to a single entity.
/// </summary>
public class DocumentGenerationService
{
    /// <summary>
    /// Generates a document from a template with provided variables.
    /// Validates template requirements, security clearance, and variable completeness.
    /// </summary>
    public Document GenerateDocumentFromTemplate(
        Template template,
        Dictionary<string, object> variables,
        User requestingUser,
        string category,
        string? description = null)
    {
        // Validate template is active
        if (!template.IsActive)
            throw new InactiveTemplateException($"Template {template.Id} is not active");

        // Validate user security clearance
        if (!requestingUser.CanAccessSecurityLevel(template.DefaultSecurityClassification))
            throw new InsufficientSecurityClearanceException(
                $"User {requestingUser.Id} lacks clearance for template {template.Id}");

        // Validate required variables are provided
        var missingVariables = template.ValidateVariables(variables);
        if (missingVariables.Any())
            throw new MissingTemplateVariablesException(
                $"Missing required variables: {string.Join(", ", missingVariables)}");

        // Generate document content by replacing template variables
        var generatedContent = ReplaceTemplateVariables(template.Content, variables);

        // Create document with template security classification
        var document = new Document(
            DocumentId.New<DocumentId>(),
            $"Generated from {template.Name}",
            category,
            template.DefaultSecurityClassification,
            requestingUser.Id,
            description,
            new List<string> { "generated", "template" },
            template.Id,
            template.ContentType);

        // Set content after creation
        document.UpdateContent(
            generatedContent, 
            generatedContent?.Length, 
            null, // No storage path for generated content initially
            false, // Generated content assumed to not contain PII initially
            requestingUser.Id);

        // Record template usage
        template.RecordUsage(requestingUser.Id);

        // If template requires approval, set document to pending approval
        if (template.RequiresApproval)
        {
            document.UpdateApprovalStatus(ApprovalStatus.Pending(), requestingUser.Id);
        }

        return document;
    }

    /// <summary>
    /// Validates whether a user can generate documents from a specific template.
    /// Considers security clearance, user roles, and template requirements.
    /// </summary>
    public DocumentGenerationValidationResult ValidateDocumentGeneration(
        Template template,
        User requestingUser)
    {
        var issues = new List<string>();

        // Check template is active
        if (!template.IsActive)
            issues.Add("Template is not active");

        // Check security clearance
        if (!requestingUser.CanAccessSecurityLevel(template.DefaultSecurityClassification))
            issues.Add("Insufficient security clearance");

        // Check user is active
        if (!requestingUser.IsActive)
            issues.Add("User account is not active");

        // Check user has contributor role at minimum
        if (!requestingUser.HasAnyRole(UserRole.Contributor, UserRole.Manager, UserRole.Administrator))
            issues.Add("User lacks document creation permissions");

        return new DocumentGenerationValidationResult(issues.Count == 0, issues);
    }

    /// <summary>
    /// Determines the appropriate approval workflow for a document based on 
    /// security classification, content type, and user roles.
    /// </summary>
    public ApprovalWorkflowRecommendation RecommendApprovalWorkflow(
        Document document,
        User creatingUser)
    {
        var requiresApproval = false;
        var recommendedApprovers = new List<UserRole>();
        var reasoning = new List<string>();

        // High security documents always require approval
        if (document.SecurityClassification.Level == "Confidential" || 
            document.SecurityClassification.Level == "Restricted")
        {
            requiresApproval = true;
            recommendedApprovers.Add(UserRole.Manager);
            reasoning.Add("High security classification requires manager approval");
        }

        // Template-generated documents may require approval
        if (document.TemplateId != null)
        {
            // This would typically check the template's approval requirements
            // For now, assume template approval requirements are already handled
            reasoning.Add("Template-based generation requirements applied");
        }

        // Large documents require review
        if (document.Content?.Length > 50000) // 50KB threshold
        {
            requiresApproval = true;
            recommendedApprovers.Add(UserRole.Approver);
            reasoning.Add("Large document size requires approval");
        }

        // Contributors need approval for most documents
        if (creatingUser.HasRole(UserRole.Contributor) && 
            !creatingUser.HasAnyRole(UserRole.Manager, UserRole.Administrator))
        {
            requiresApproval = true;
            recommendedApprovers.Add(UserRole.Approver);
            reasoning.Add("Contributor role requires approval workflow");
        }

        return new ApprovalWorkflowRecommendation(
            requiresApproval,
            recommendedApprovers.Distinct().ToList(),
            reasoning);
    }

    /// <summary>
    /// Replaces template variables in content with provided values.
    /// Supports {{variableName}} syntax.
    /// </summary>
    private static string ReplaceTemplateVariables(string content, Dictionary<string, object> variables)
    {
        var result = content;
        
        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}"; // {{variableName}}
            var value = variable.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value);
        }

        return result;
    }
}

/// <summary>
/// Result of document generation validation.
/// </summary>
public record DocumentGenerationValidationResult(bool IsValid, List<string> Issues);

/// <summary>
/// Recommendation for document approval workflow.
/// </summary>
public record ApprovalWorkflowRecommendation(
    bool RequiresApproval,
    List<UserRole> RecommendedApprovers,
    List<string> Reasoning);