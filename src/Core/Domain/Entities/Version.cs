
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Version entity representing a specific version of a document.
/// Contains version-specific content, approval status, and change tracking.
/// </summary>
public class Version : BaseEntity<VersionId>
{
    /// <summary>
    /// The document this version belongs to.
    /// </summary>
    public DocumentId DocumentId { get; private set; }

    /// <summary>
    /// Version number (e.g., "1.0", "1.1", "2.0").
    /// </summary>
    public string VersionNumber { get; private set; }

    /// <summary>
    /// Version content.
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// Size of the version content in bytes.
    /// </summary>
    public long SizeBytes { get; private set; }

    /// <summary>
    /// Storage path for this version's content.
    /// </summary>
    public string? StoragePath { get; private set; }

    /// <summary>
    /// Version status.
    /// </summary>
    public VersionStatus Status { get; private set; }

    /// <summary>
    /// Change summary for this version.
    /// </summary>
    public string? ChangesSummary { get; private set; }

    /// <summary>
    /// Whether this is the current/active version.
    /// </summary>
    public bool IsCurrent { get; private set; }

    /// <summary>
    /// When this version was published.
    /// </summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>
    /// Who published this version.
    /// </summary>
    public UserId? PublishedBy { get; private set; }

    /// <summary>
    /// Approval information for this version.
    /// </summary>
    public List<VersionApproval> Approvals { get; private set; }

    // Private constructor for EF Core
    private Version() : base()
    {
        DocumentId = DocumentId.ForTesting();
        VersionNumber = string.Empty;
        Content = string.Empty;
        Status = VersionStatus.Draft;
        Approvals = new List<VersionApproval>();
    }

    /// <summary>
    /// Creates a new version.
    /// </summary>
    public Version(
        VersionId id,
        DocumentId documentId,
        string versionNumber,
        string content,
        UserId createdBy,
        string? changesSummary = null,
        bool isCurrent = false) : base(id, createdBy)
    {
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
        VersionNumber = !string.IsNullOrWhiteSpace(versionNumber) 
            ? versionNumber 
            : throw new ArgumentException("Version number cannot be empty", nameof(versionNumber));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ChangesSummary = changesSummary;
        IsCurrent = isCurrent;
        Status = VersionStatus.Draft;
        SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        Approvals = new List<VersionApproval>();

        AddDomainEvent(new VersionCreatedEvent(id, documentId, versionNumber, createdBy));
    }

    /// <summary>
    /// Updates the version content.
    /// </summary>
    public void UpdateContent(string content, UserId updatedBy, string? changesSummary = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (Status != VersionStatus.Draft)
            throw new InvalidOperationException("Cannot update content of non-draft version");

        Content = content;
        SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        ChangesSummary = changesSummary;
        
        UpdateModificationTracking(updatedBy);
        AddDomainEvent(new VersionContentUpdatedEvent(Id, DocumentId, updatedBy));
    }

    /// <summary>
    /// Submits the version for review.
    /// </summary>
    public void SubmitForReview(UserId submittedBy)
    {
        if (Status != VersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be submitted for review");

        Status = VersionStatus.UnderReview;
        UpdateModificationTracking(submittedBy);
        AddDomainEvent(new VersionSubmittedForReviewEvent(Id, DocumentId, submittedBy));
    }

    /// <summary>
    /// Approves the version.
    /// </summary>
    public void Approve(UserId approvedBy, string? comments = null)
    {
        if (Status != VersionStatus.UnderReview)
            throw new InvalidOperationException("Only versions under review can be approved");

        var approval = new VersionApproval(
            VersionApprovalId.New<VersionApprovalId>(),
            Id,
            approvedBy,
            comments);

        Approvals.Add(approval);
        Status = VersionStatus.Approved;
        
        UpdateModificationTracking(approvedBy);
        AddDomainEvent(new VersionApprovedEvent(Id, DocumentId, approvedBy, comments));
    }

    /// <summary>
    /// Rejects the version.
    /// </summary>
    public void Reject(UserId rejectedBy, string rejectionReason)
    {
        if (Status != VersionStatus.UnderReview)
            throw new InvalidOperationException("Only versions under review can be rejected");

        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason is required", nameof(rejectionReason));

        Status = VersionStatus.Rejected;
        UpdateModificationTracking(rejectedBy);
        AddDomainEvent(new VersionRejectedEvent(Id, DocumentId, rejectedBy, rejectionReason));
    }

    /// <summary>
    /// Publishes the version.
    /// </summary>
    public void Publish(UserId publishedBy)
    {
        if (Status != VersionStatus.Approved)
            throw new InvalidOperationException("Only approved versions can be published");

        Status = VersionStatus.Published;
        PublishedAt = DateTime.UtcNow;
        PublishedBy = publishedBy;
        IsCurrent = true;

        UpdateModificationTracking(publishedBy);
        AddDomainEvent(new VersionPublishedEvent(Id, DocumentId, VersionNumber, publishedBy));
    }

    /// <summary>
    /// Marks this version as current.
    /// </summary>
    public void SetAsCurrent(UserId updatedBy)
    {
        if (Status != VersionStatus.Published)
            throw new InvalidOperationException("Only published versions can be set as current");

        IsCurrent = true;
        UpdateModificationTracking(updatedBy);
    }

    /// <summary>
    /// Marks this version as not current.
    /// </summary>
    public void SetAsNotCurrent(UserId updatedBy)
    {
        IsCurrent = false;
        UpdateModificationTracking(updatedBy);
    }
}

/// <summary>
/// Version status enumeration.
/// </summary>
public enum VersionStatus
{
    Draft,
    UnderReview,
    Approved,
    Rejected,
    Published,
    Archived
}

/// <summary>
/// Version approval entity.
/// </summary>
public class VersionApproval : BaseEntity<VersionApprovalId>
{
    public VersionId VersionId { get; private set; }
    public string? Comments { get; private set; }

    private VersionApproval() : base()
    {
        VersionId = VersionId.ForTesting();
    }

    public VersionApproval(
        VersionApprovalId id,
        VersionId versionId,
        UserId approvedBy,
        string? comments = null) : base(id, approvedBy)
    {
        VersionId = versionId ?? throw new ArgumentNullException(nameof(versionId));
        Comments = comments;
    }
}

// Domain Events
public record VersionCreatedEvent(VersionId VersionId, DocumentId DocumentId, string VersionNumber, UserId CreatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(VersionCreatedEvent);
}

public record VersionContentUpdatedEvent(VersionId VersionId, DocumentId DocumentId, UserId UpdatedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(VersionContentUpdatedEvent);
}

public record VersionSubmittedForReviewEvent(VersionId VersionId, DocumentId DocumentId, UserId SubmittedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(VersionSubmittedForReviewEvent);
}

public record VersionApprovedEvent(VersionId VersionId, DocumentId DocumentId, UserId ApprovedBy, string? Comments) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(VersionApprovedEvent);
}

public record VersionRejectedEvent(VersionId VersionId, DocumentId DocumentId, UserId RejectedBy, string RejectionReason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(VersionRejectedEvent);
}

public record VersionPublishedEvent(VersionId VersionId, DocumentId DocumentId, string VersionNumber, UserId PublishedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType { get; } = nameof(VersionPublishedEvent);
}