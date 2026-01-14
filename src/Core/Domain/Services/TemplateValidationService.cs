using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Services;

/// <summary>
/// Domain service for template validation operations
/// Centralizes validation logic for template entities
/// </summary>
public static class TemplateValidationService
{
    /// <summary>
    /// Validates parameters for template creation
    /// </summary>
    /// <param name="name">Template name</param>
    /// <param name="category">Template category</param>
    /// <param name="content">Template content</param>
    /// <param name="defaultSecurityClassification">Default security classification</param>
    /// <param name="createdBy">User creating the template</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    public static void ValidateTemplateCreation(
        string name,
        string category,
        string content,
        SecurityClassification defaultSecurityClassification,
        UserId createdBy)
    {
        ValidateName(name);
        ValidateCategory(category);
        ValidateContent(content);
        ValidateNotNull(defaultSecurityClassification, nameof(defaultSecurityClassification));
        ValidateNotNull(createdBy, nameof(createdBy));
    }

    /// <summary>
    /// Validates template name
    /// </summary>
    /// <param name="name">Name to validate</param>
    /// <exception cref="ArgumentException">Thrown when name is invalid</exception>
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        if (name.Length > 150)
            throw new ArgumentException("Name cannot exceed 150 characters", nameof(name));
    }

    /// <summary>
    /// Validates template category
    /// </summary>
    /// <param name="category">Category to validate</param>
    /// <exception cref="ArgumentException">Thrown when category is invalid</exception>
    public static void ValidateCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));

        if (category.Length > 100)
            throw new ArgumentException("Category cannot exceed 100 characters", nameof(category));
    }

    /// <summary>
    /// Validates template content
    /// </summary>
    /// <param name="content">Content to validate</param>
    /// <exception cref="ArgumentException">Thrown when content is invalid</exception>
    public static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));
    }

    /// <summary>
    /// Validates content type
    /// </summary>
    /// <param name="contentType">Content type to validate</param>
    /// <exception cref="ArgumentException">Thrown when content type is invalid</exception>
    public static void ValidateContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty", nameof(contentType));
    }

    /// <summary>
    /// Validates that template is active before operations that require active status
    /// </summary>
    /// <param name="template">Template to check</param>
    /// <exception cref="InvalidOperationException">Thrown when template is inactive</exception>
    public static void ValidateTemplateIsActive(Template template)
    {
        if (!template.IsActive)
            throw new InvalidOperationException("Template must be active for this operation");
    }

    /// <summary>
    /// Validates that template can be activated
    /// </summary>
    /// <param name="template">Template to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when template cannot be activated</exception>
    public static void ValidateCanActivate(Template template)
    {
        if (template.IsActive)
            throw new InvalidOperationException("Template is already active");
    }

    /// <summary>
    /// Validates that template can be deactivated
    /// </summary>
    /// <param name="template">Template to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when template cannot be deactivated</exception>
    public static void ValidateCanDeactivate(Template template)
    {
        if (!template.IsActive)
            throw new InvalidOperationException("Template is already inactive");
    }

    /// <summary>
    /// Validates template variable creation parameters
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <param name="displayName">Variable display name</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    public static void ValidateTemplateVariable(string name, string displayName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Variable display name cannot be empty", nameof(displayName));

        if (name.Length > 50)
            throw new ArgumentException("Variable name cannot exceed 50 characters", nameof(name));

        if (displayName.Length > 100)
            throw new ArgumentException("Variable display name cannot exceed 100 characters", nameof(displayName));
    }

    /// <summary>
    /// Validates that provided variables meet template requirements
    /// </summary>
    /// <param name="templateVariables">Template variable definitions</param>
    /// <param name="providedVariables">Variables provided for generation</param>
    /// <returns>List of missing required variables</returns>
    public static List<string> ValidateProvidedVariables(
        IEnumerable<TemplateVariable> templateVariables, 
        Dictionary<string, object> providedVariables)
    {
        var missingVariables = new List<string>();

        foreach (var variable in templateVariables.Where(v => v.IsRequired))
        {
            if (!providedVariables.ContainsKey(variable.Name) ||
                providedVariables[variable.Name] == null)
            {
                missingVariables.Add(variable.Name);
            }
        }

        return missingVariables;
    }

    /// <summary>
    /// Generic null validation helper
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <param name="parameterName">Parameter name for exception</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
    private static void ValidateNotNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}