
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Events;
using Enterprise.Documentation.Core.Domain.Services;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Template entity representing a reusable document template.
/// Contains template structure, variables, and generation logic.
/// </summary>
public class Template : BaseEntity<TemplateId>
{
    /// <summary>
    /// Template name (required).
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Template description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Template category for organization.
    /// </summary>
    public string Category { get; private set; }

    /// <summary>
    /// Template content with variable placeholders.
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// Content type of the template (markdown, html, etc.).
    /// </summary>
    public string ContentType { get; private set; }

    /// <summary>
    /// Template variables and their definitions.
    /// </summary>
    public List<TemplateVariable> Variables { get; private set; }

    /// <summary>
    /// Security classification for documents generated from this template.
    /// </summary>
    public SecurityClassification DefaultSecurityClassification { get; private set; }

    /// <summary>
    /// Whether approval is required for documents generated from this template.
    /// </summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>
    /// Template version number.
    /// </summary>
    public string TemplateVersion { get; private set; }

    /// <summary>
    /// Document type this template supports (BR, EN, DF, SP, etc.).
    /// </summary>
    public string DocumentType { get; private set; }

    /// <summary>
    /// Supported document types for this template.
    /// </summary>
    public List<string> SupportedDocumentTypes { get; private set; }

    /// <summary>
    /// Whether this template is currently active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Number of times this template has been used.
    /// </summary>
    public int UsageCount { get; private set; }

    // Private constructor for EF Core
    private Template() : base()
    {
        Name = string.Empty;
        Category = string.Empty;
        Content = string.Empty;
        ContentType = "markdown";
        Variables = new List<TemplateVariable>();
        DefaultSecurityClassification = SecurityClassification.Internal(UserId.ForTesting());
        TemplateVersion = "1.0";
        DocumentType = "BR";
        SupportedDocumentTypes = new List<string>();
        IsActive = true;
        UsageCount = 0;
    }

    /// <summary>
    /// Creates a new template.
    /// </summary>
    public Template(
        TemplateId id,
        string name,
        string category,
        string content,
        SecurityClassification defaultSecurityClassification,
        UserId createdBy,
        string? description = null,
        List<TemplateVariable>? variables = null,
        bool requiresApproval = false,
        string contentType = "markdown",
        string documentType = "BR",
        List<string>? supportedDocumentTypes = null) : base(id, createdBy)
    {
        // Validate all parameters using domain service
        TemplateValidationService.ValidateTemplateCreation(name, category, content, defaultSecurityClassification, createdBy);
        TemplateValidationService.ValidateContentType(contentType);
        
        Name = name;
        Category = category;
        Content = content;
        Description = description;
        Variables = variables ?? new List<TemplateVariable>();
        ContentType = contentType;
        DefaultSecurityClassification = defaultSecurityClassification;
        RequiresApproval = requiresApproval;
        TemplateVersion = "1.0";
        DocumentType = documentType;
        SupportedDocumentTypes = supportedDocumentTypes ?? new List<string> { documentType };
        IsActive = true;
        UsageCount = 0;

        AddDomainEvent(new TemplateCreatedEvent(id, name, category, createdBy));
    }

    /// <summary>
    /// Updates the template content and structure.
    /// </summary>
    public void UpdateContent(
        string content,
        List<TemplateVariable>? variables,
        UserId updatedBy)
    {
        TemplateValidationService.ValidateContent(content);

        Content = content;
        if (variables != null)
            Variables = new List<TemplateVariable>(variables);

        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new TemplateContentUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates template metadata.
    /// </summary>
    public void UpdateMetadata(
        string? name = null,
        string? description = null,
        string? category = null,
        bool? requiresApproval = null,
        UserId? updatedBy = null)
    {
        if (updatedBy == null)
            throw new ArgumentNullException(nameof(updatedBy));

        if (!string.IsNullOrWhiteSpace(name))
            Name = name;

        if (description != null)
            Description = description;

        if (!string.IsNullOrWhiteSpace(category))
            Category = category;

        if (requiresApproval.HasValue)
            RequiresApproval = requiresApproval.Value;

        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new TemplateMetadataUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Activates the template for use.
    /// </summary>
    public void Activate(UserId activatedBy)
    {
        TemplateValidationService.ValidateCanActivate(this);

        IsActive = true;
        UpdateModificationTracking(activatedBy);
        AddDomainEvent(new TemplateActivatedEvent(Id, activatedBy));
    }

    /// <summary>
    /// Deactivates the template to prevent further use.
    /// </summary>
    public void Deactivate(UserId deactivatedBy)
    {
        TemplateValidationService.ValidateCanDeactivate(this);

        IsActive = false;
        UpdateModificationTracking(deactivatedBy);
        AddDomainEvent(new TemplateDeactivatedEvent(Id, deactivatedBy));
    }

    /// <summary>
    /// Increments the usage count when a document is generated from this template.
    /// </summary>
    public void RecordUsage(UserId usedBy)
    {
        UsageCount++;
        UpdateModificationTracking(usedBy);
        AddDomainEvent(new TemplateUsedEvent(Id, usedBy, UsageCount));
    }

    /// <summary>
    /// Validates that all required variables are provided for document generation.
    /// </summary>
    public List<string> ValidateVariables(Dictionary<string, object> providedVariables)
    {
        return TemplateValidationService.ValidateProvidedVariables(Variables, providedVariables);
    }
}

/// <summary>
/// Represents a variable within a template.
/// </summary>
public class TemplateVariable : BaseValueObject
{
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TemplateVariableType Type { get; private set; }
    public bool IsRequired { get; private set; }
    public string? DefaultValue { get; private set; }
    public List<string>? AllowedValues { get; private set; }

    // Parameterless constructor for EF Core
    private TemplateVariable()
    {
    }

    public TemplateVariable(
        string name,
        string displayName,
        TemplateVariableType type,
        bool isRequired = false,
        string? description = null,
        string? defaultValue = null,
        List<string>? allowedValues = null)
    {
        TemplateValidationService.ValidateTemplateVariable(name, displayName);
            
        Name = name;
        DisplayName = displayName;
        Type = type;
        IsRequired = isRequired;
        Description = description;
        DefaultValue = defaultValue;
        AllowedValues = allowedValues;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return DisplayName;
        yield return Description;
        yield return Type;
        yield return IsRequired;
        yield return DefaultValue;
        yield return AllowedValues != null ? string.Join(",", AllowedValues) : null;
    }
}

/// <summary>
/// Template variable types.
/// </summary>
public enum TemplateVariableType
{
    Text,
    Number,
    Date,
    Boolean,
    Selection,
    MultiSelection
}

