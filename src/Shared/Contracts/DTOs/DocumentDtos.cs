
namespace Enterprise.Documentation.Shared.Contracts.DTOs;

/// <summary>
/// Data transfer object representing a document in the Enterprise Documentation Platform.
/// Used for transferring document information between agents.
/// </summary>
public class DocumentDto : BaseDto
{
    /// <summary>Title or name of the document</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Document type (Template, Report, Schema, Manual, etc.)</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Current status of the document</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    /// <summary>Content of the document (may be truncated for large documents)</summary>
    public string? Content { get; set; }

    /// <summary>MIME type of the document</summary>
    public string? ContentType { get; set; }

    /// <summary>Size of the document in bytes</summary>
    public long? Size { get; set; }

    /// <summary>Tags associated with the document for categorization</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>User ID who created the document</summary>
    public string? CreatedBy { get; set; }

    /// <summary>User ID who last modified the document</summary>
    public string? ModifiedBy { get; set; }

    /// <summary>Timestamp when document was last modified</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Current version number of the document</summary>
    public int DocumentVersion { get; set; } = 1;

    /// <summary>Whether the document contains PII (determined by governance scan)</summary>
    public bool ContainsPII { get; set; }

    /// <summary>Security classification level</summary>
    public SecurityClassification SecurityLevel { get; set; } = SecurityClassification.Internal;

    /// <summary>Approval workflow status</summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.NotRequired;

    /// <summary>Related document IDs</summary>
    public List<string> RelatedDocuments { get; set; } = new();

    /// <summary>Template ID if this document was generated from a template</summary>
    public string? TemplateId { get; set; }

    /// <summary>Full path or URL to the document storage location</summary>
    public string? StoragePath { get; set; }
}

/// <summary>
/// Request DTO for creating a new document.
/// </summary>
public class CreateDocumentRequest : BaseDto
{
    /// <summary>Title of the document to create</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Type of document to create</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Initial content of the document</summary>
    public string? Content { get; set; }

    /// <summary>MIME type of the content</summary>
    public string? ContentType { get; set; }

    /// <summary>Tags to associate with the document</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Template ID to use for document generation</summary>
    public string? TemplateId { get; set; }

    /// <summary>Template variables if using a template</summary>
    public Dictionary<string, object>? TemplateVariables { get; set; }

    /// <summary>Security classification for the document</summary>
    public SecurityClassification SecurityLevel { get; set; } = SecurityClassification.Internal;

    /// <summary>Whether approval workflow is required</summary>
    public bool RequiresApproval { get; set; }
}

/// <summary>
/// Request DTO for updating an existing document.
/// </summary>
public class UpdateDocumentRequest : BaseDto
{
    /// <summary>ID of the document to update</summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>New title (optional)</summary>
    public string? Title { get; set; }

    /// <summary>New content (optional)</summary>
    public string? Content { get; set; }

    /// <summary>New tags (optional)</summary>
    public List<string>? Tags { get; set; }

    /// <summary>New status (optional)</summary>
    public DocumentStatus? Status { get; set; }

    /// <summary>Reason for the update</summary>
    public string? UpdateReason { get; set; }

    /// <summary>Whether to increment the version number</summary>
    public bool IncrementVersion { get; set; } = true;
}

/// <summary>
/// Response DTO for document operations.
/// </summary>
public class DocumentOperationResponse : BaseDto
{
    /// <summary>Whether the operation was successful</summary>
    public bool IsSuccessful { get; set; }

    /// <summary>ID of the affected document</summary>
    public string? DocumentId { get; set; }

    /// <summary>Operation that was performed</summary>
    public string? Operation { get; set; }

    /// <summary>Success or error message</summary>
    public string? Message { get; set; }

    /// <summary>Validation errors if operation failed</summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>Processing duration in milliseconds</summary>
    public long? ProcessingDurationMs { get; set; }

    /// <summary>Updated document data (if applicable)</summary>
    public DocumentDto? Document { get; set; }
}

/// <summary>
/// Document status enumeration.
/// </summary>
public enum DocumentStatus
{
    Draft,
    UnderReview,
    Approved,
    Published,
    Archived,
    Deleted
}

/// <summary>
/// Security classification levels.
/// </summary>
public enum SecurityClassification
{
    Public,
    Internal,
    Confidential,
    Restricted
}

/// <summary>
/// Approval workflow status.
/// </summary>
public enum ApprovalStatus
{
    NotRequired,
    Pending,
    Approved,
    Rejected,
    Expired
}