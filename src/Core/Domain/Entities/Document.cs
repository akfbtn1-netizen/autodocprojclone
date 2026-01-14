
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Events;
using Enterprise.Documentation.Core.Domain.Services;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Document entity representing a document in the enterprise documentation platform.
/// Contains document metadata, content, versioning, and approval workflow information.
/// Implements domain business rules and raises domain events for state changes.
/// </summary>
public class Document : BaseEntity<DocumentId>
{
    /// <summary>
    /// Document title (required).
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Document description or summary.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Document category for organization and filtering.
    /// </summary>
    public string Category { get; private set; }

    /// <summary>
    /// Document tags for search and categorization.
    /// </summary>
    public List<string> Tags { get; private set; }

    /// <summary>
    /// Template ID if this document was generated from a template.
    /// </summary>
    public TemplateId? TemplateId { get; private set; }

    /// <summary>
    /// Current approval status of the document.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; private set; }

    /// <summary>
    /// Security classification of the document.
    /// </summary>
    public SecurityClassification SecurityClassification { get; private set; }

    /// <summary>
    /// Document version number.
    /// </summary>
    public string DocumentVersion { get; private set; }

    /// <summary>
    /// Document type (BR, EN, DF, SP, etc.).
    /// </summary>
    public string DocumentType { get; private set; } = string.Empty;

    /// <summary>
    /// Document content type (markdown, html, pdf, etc.).
    /// </summary>
    public string ContentType { get; private set; }

    /// <summary>
    /// Document content (may be truncated for large documents).
    /// </summary>
    public string? Content { get; private set; }

    /// <summary>
    /// Size of the document in bytes.
    /// </summary>
    public long? SizeBytes { get; private set; }

    /// <summary>
    /// Full path or URL to the document storage location.
    /// </summary>
    public string? StoragePath { get; private set; }

    /// <summary>
    /// Whether the document contains PII (determined by governance scan).
    /// </summary>
    public bool ContainsPII { get; private set; }

    /// <summary>
    /// Document status (Draft, Published, Archived, etc.).
    /// </summary>
    public DocumentStatus Status { get; private set; }

    /// <summary>
    /// When the document was published (if applicable).
    /// </summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>
    /// Related document IDs.
    /// </summary>
    public List<DocumentId> RelatedDocuments { get; private set; }

    // Private constructor for EF Core
    private Document() : base() 
    {
        Title = string.Empty;
        Category = string.Empty;
        Tags = new List<string>();
        ApprovalStatus = ApprovalStatus.NotRequired();
        SecurityClassification = SecurityClassification.Internal(UserId.ForTesting());
        DocumentVersion = "1.0";
        DocumentType = "BR"; // Default to Business Request
        ContentType = "markdown";
        Status = DocumentStatus.Draft;
        RelatedDocuments = new List<DocumentId>();
    }

    /// <summary>
    /// Creates a new document.
    /// </summary>
    public Document(
        DocumentId id,
        string title,
        string category,
        SecurityClassification securityClassification,
        UserId createdBy,
        string? description = null,
        List<string>? tags = null,
        TemplateId? templateId = null,
        string contentType = "markdown") : base(id, createdBy)
    {
        // Validate all parameters using domain service
        DocumentValidationService.ValidateDocumentCreation(title, category, securityClassification, createdBy);
        DocumentValidationService.ValidateContentType(contentType);
        
        Title = title;
        Category = category;
        Description = description;
        Tags = tags ?? new List<string>();
        TemplateId = templateId;
        SecurityClassification = securityClassification;
        ContentType = contentType;
        
        // Default values
        ApprovalStatus = ApprovalStatus.NotRequired();
        DocumentVersion = "1.0";
        Status = DocumentStatus.Draft;
        RelatedDocuments = new List<DocumentId>();
        ContainsPII = false;

        // Raise domain event
        AddDomainEvent(new DocumentCreatedEvent(id, title, category, createdBy));
    }

    /// <summary>
    /// Updates the document content and metadata.
    /// </summary>
    public void UpdateContent(
        string? content, 
        long? sizeBytes, 
        string? storagePath,
        bool containsPII,
        UserId updatedBy)
    {
        Content = content;
        SizeBytes = sizeBytes;
        StoragePath = storagePath;
        ContainsPII = containsPII;
        
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentContentUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates document metadata (title, description, tags, etc.).
    /// </summary>
    public void UpdateMetadata(
        string? title = null,
        string? description = null,
        string? category = null,
        List<string>? tags = null,
        UserId? updatedBy = null)
    {
        if (updatedBy == null)
            throw new ArgumentNullException(nameof(updatedBy));

        if (!string.IsNullOrWhiteSpace(title))
            Title = title;

        if (description != null)
            Description = description;

        if (!string.IsNullOrWhiteSpace(category))
            Category = category;

        if (tags != null)
            Tags = new List<string>(tags);

        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentMetadataUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates the approval status of the document.
    /// Implements business rules for approval workflow transitions.
    /// </summary>
    public void UpdateApprovalStatus(ApprovalStatus newStatus, UserId updatedBy)
    {
        DocumentValidationService.ValidateApprovalTransition(ApprovalStatus, newStatus);

        var previousStatus = ApprovalStatus;
        ApprovalStatus = newStatus;
        
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentApprovalStatusChangedEvent(Id, previousStatus.Status, newStatus.Status, updatedBy));
    }

    /// <summary>
    /// Publishes the document.
    /// Business rule: Document must be approved before publishing.
    /// </summary>
    public void Publish(UserId publishedBy)
    {
        DocumentValidationService.ValidateCanPublish(this);

        Status = DocumentStatus.Published;
        PublishedAt = DateTime.UtcNow;
        
        UpdateModificationTracking(publishedBy);
        AddDomainEvent(new DocumentPublishedEvent(Id, publishedBy));
    }

    /// <summary>
    /// Archives the document.
    /// </summary>
    public void Archive(UserId archivedBy)
    {
        DocumentValidationService.ValidateCanArchive(this);

        Status = DocumentStatus.Archived;
        
        UpdateModificationTracking(archivedBy);
        AddDomainEvent(new DocumentArchivedEvent(Id, archivedBy));
    }

    /// <summary>
    /// Updates the document title.
    /// </summary>
    public void UpdateTitle(string title, UserId updatedBy)
    {
        DocumentValidationService.ValidateNotArchived(this);
        DocumentValidationService.ValidateTitle(title);

        Title = title;
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentMetadataUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates the document category.
    /// </summary>
    public void UpdateCategory(string category, UserId updatedBy)
    {
        DocumentValidationService.ValidateNotArchived(this);
        DocumentValidationService.ValidateCategory(category);

        Category = category;
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentMetadataUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates the document description.
    /// </summary>
    public void UpdateDescription(string? description, UserId updatedBy)
    {
        DocumentValidationService.ValidateNotArchived(this);

        Description = description;
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentMetadataUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates the document tags.
    /// </summary>
    public void UpdateTags(List<string> tags, UserId updatedBy)
    {
        DocumentValidationService.ValidateNotArchived(this);

        Tags = tags ?? new List<string>();
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentMetadataUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Updates the document content type.
    /// </summary>
    public void UpdateContentType(string contentType, UserId updatedBy)
    {
        DocumentValidationService.ValidateNotArchived(this);
        DocumentValidationService.ValidateContentType(contentType);

        ContentType = contentType;
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentMetadataUpdatedEvent(Id, updatedBy));
    }

    /// <summary>
    /// Checks if a user can modify this document.
    /// Business rules: Only creators, admins, or users with appropriate roles can modify.
    /// </summary>
    public bool CanUserModifyDocument(UserId userId, UserRole userRole)
    {
        return DocumentBusinessRules.CanUserModifyDocument(this, userId, userRole);
    }

    /// <summary>
    /// Checks if a user can approve this document.
    /// Business rules: Only users with approve permissions and sufficient security clearance.
    /// </summary>
    public bool CanUserApproveDocument(UserId userId, UserRole userRole)
    {
        return DocumentBusinessRules.CanUserApproveDocument(this, userId, userRole);
    }

    /// <summary>
    /// Approves the document.
    /// </summary>
    public void Approve(UserId approvedBy, string? comments = null)
    {
        if (Status != DocumentStatus.UnderReview)
            throw new InvalidOperationException("Document must be under review to be approved");

        Status = DocumentStatus.Published;
        PublishedAt = DateTime.UtcNow;
        ApprovalStatus = ApprovalStatus.Approved(approvedBy, comments);
        
        UpdateModificationTracking(approvedBy);
        AddDomainEvent(new DocumentApprovalStatusChangedEvent(Id, "UnderReview", "Approved", approvedBy));
        AddDomainEvent(new DocumentPublishedEvent(Id, approvedBy));
    }

    /// <summary>
    /// Adds a related document.
    /// </summary>
    public void AddRelatedDocument(DocumentId relatedDocumentId, UserId updatedBy)
    {
        DocumentValidationService.ValidateRelatedDocument(Id, relatedDocumentId);

        if (!RelatedDocuments.Contains(relatedDocumentId))
        {
            RelatedDocuments.Add(relatedDocumentId);
            UpdateModificationTracking(updatedBy);
            AddDomainEvent(new DocumentRelationAddedEvent(Id, relatedDocumentId, updatedBy));
        }
    }

    /// <summary>
    /// Removes a related document.
    /// </summary>
    public void RemoveRelatedDocument(DocumentId relatedDocumentId, UserId updatedBy)
    {
        if (RelatedDocuments.Remove(relatedDocumentId))
        {
            UpdateModificationTracking(updatedBy);
            AddDomainEvent(new DocumentRelationRemovedEvent(Id, relatedDocumentId, updatedBy));
        }
    }

    /// <summary>
    /// Updates the security classification of the document.
    /// Business rule: Can only downgrade classification levels.
    /// </summary>
    public void UpdateSecurityClassification(SecurityClassification newClassification, UserId updatedBy)
    {
        DocumentValidationService.ValidateSecurityClassificationChange(SecurityClassification, newClassification);

        var previousClassification = SecurityClassification;
        SecurityClassification = newClassification;
        
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new DocumentSecurityClassificationChangedEvent(Id, previousClassification.Level, newClassification.Level, updatedBy));
    }
}

/// <summary>
/// Document status value object with business rules.
/// </summary>
public record DocumentStatus
{
    public string Value { get; }

    public static readonly DocumentStatus Draft = new("Draft");
    public static readonly DocumentStatus UnderReview = new("UnderReview");
    public static readonly DocumentStatus Published = new("Published");
    public static readonly DocumentStatus Archived = new("Archived");

    private DocumentStatus(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public static DocumentStatus FromString(string value)
    {
        return value?.ToLowerInvariant() switch
        {
            "draft" => Draft,
            "underreview" => UnderReview,
            "published" => Published,
            "archived" => Archived,
            _ => throw new ArgumentException($"Invalid document status: {value}", nameof(value))
        };
    }

    public static implicit operator string(DocumentStatus status) => status.Value;
    public override string ToString() => Value;
}

