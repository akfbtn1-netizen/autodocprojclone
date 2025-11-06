using Core.Domain.ValueObjects;

namespace Core.Domain.Entities;

/// <summary>
/// Document entity representing a single document in the documentation platform.
/// Contains document metadata, content references, and approval workflow information.
/// </summary>
public class Document : BaseEntity
{
    /// <summary>
    /// Document title (required).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Document description or summary.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Document category for organization and filtering.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Document tags for search and categorization.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Physical name/identifier for the document.
    /// Used for file system storage and external references.
    /// </summary>
    public PhysicalName PhysicalName { get; set; } = PhysicalName.Empty;

    /// <summary>
    /// Current approval status of the document.
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    /// <summary>
    /// Document version number.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Document content type (markdown, html, pdf, etc.).
    /// </summary>
    public string ContentType { get; set; } = "markdown";

    /// <summary>
    /// Document content or reference to external storage.
    /// For large documents, this might be a storage path.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Document size in bytes.
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Document language code (en, es, fr, etc.).
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// When the document was last published.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Who published the document.
    /// </summary>
    public string? PublishedBy { get; set; }

    /// <summary>
    /// Document expiration date (if applicable).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether this document is publicly accessible.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Security classification level.
    /// </summary>
    public SecurityClassification SecurityLevel { get; set; } = SecurityClassification.Internal;

    /// <summary>
    /// Parent document ID for hierarchical organization.
    /// </summary>
    public Guid? ParentDocumentId { get; set; }

    /// <summary>
    /// Navigation property to parent document.
    /// </summary>
    public Document? ParentDocument { get; set; }

    /// <summary>
    /// Navigation property to child documents.
    /// </summary>
    public List<Document> ChildDocuments { get; set; } = new();

    /// <summary>
    /// Document access permissions.
    /// </summary>
    public List<DocumentPermission> Permissions { get; set; } = new();

    /// <summary>
    /// Document change history.
    /// </summary>
    public List<DocumentChange> Changes { get; set; } = new();

    /// <summary>
    /// Publishes the document, updating status and timestamps.
    /// </summary>
    /// <param name="publishedBy">Who is publishing the document</param>
    public void Publish(string publishedBy)
    {
        ApprovalStatus = Core.Domain.ValueObjects.ApprovalStatus.Approved(publishedBy);
        PublishedAt = DateTime.UtcNow;
        PublishedBy = publishedBy;
        UpdateAuditInfo(publishedBy);
    }

    /// <summary>
    /// Archives the document, removing it from active use.
    /// </summary>
    /// <param name="archivedBy">Who is archiving the document</param>
    public void Archive(string archivedBy)
    {
        ApprovalStatus = Core.Domain.ValueObjects.ApprovalStatus.Archived(archivedBy);
        UpdateAuditInfo(archivedBy);
    }

    /// <summary>
    /// Updates the document content and increments version.
    /// </summary>
    /// <param name="newContent">New document content</param>
    /// <param name="updatedBy">Who is updating the document</param>
    /// <param name="changeDescription">Description of the changes made</param>
    public void UpdateContent(string newContent, string updatedBy, string? changeDescription = null)
    {
        var oldContent = Content;
        Content = newContent;
        SizeInBytes = System.Text.Encoding.UTF8.GetByteCount(newContent ?? string.Empty);
        
        // Increment version (simple versioning - could be more sophisticated)
        if (Version.Contains('.'))
        {
            var parts = Version.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
            {
                Version = $"{parts[0]}.{minor + 1}";
            }
        }

        // Record the change
        Changes.Add(new DocumentChange
        {
            Id = Guid.NewGuid(),
            DocumentId = Id,
            ChangeType = DocumentChangeType.ContentUpdate,
            Description = changeDescription ?? "Content updated",
            OldValue = oldContent,
            NewValue = newContent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = updatedBy
        });

        // Reset approval status for review
        if (ApprovalStatus.Status == ApprovalStatusType.Approved)
        {
            ApprovalStatus = Core.Domain.ValueObjects.ApprovalStatus.PendingReview(updatedBy, "Content updated - requires re-approval");
        }

        UpdateAuditInfo(updatedBy);
    }

    /// <summary>
    /// Adds a permission for the document.
    /// </summary>
    /// <param name="userId">User or role ID</param>
    /// <param name="permissionType">Type of permission to grant</param>
    /// <param name="grantedBy">Who is granting the permission</param>
    public void GrantPermission(string userId, DocumentPermissionType permissionType, string grantedBy)
    {
        var existingPermission = Permissions.FirstOrDefault(p => p.UserId == userId);
        if (existingPermission != null)
        {
            existingPermission.PermissionType = permissionType;
            existingPermission.UpdateAuditInfo(grantedBy);
        }
        else
        {
            Permissions.Add(new DocumentPermission
            {
                Id = Guid.NewGuid(),
                DocumentId = Id,
                UserId = userId,
                PermissionType = permissionType,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = grantedBy,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = grantedBy
            });
        }

        UpdateAuditInfo(grantedBy);
    }

    /// <summary>
    /// Checks if a user has a specific permission for this document.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="permissionType">Permission type to check</param>
    /// <returns>True if the user has the permission</returns>
    public bool HasPermission(string userId, DocumentPermissionType permissionType)
    {
        return Permissions.Any(p => p.UserId == userId && 
                                  (p.PermissionType == permissionType || 
                                   p.PermissionType == DocumentPermissionType.FullControl));
    }
}