
namespace Enterprise.Documentation.Shared.Contracts.Events;

/// <summary>
/// Event published when a new document is created in the system.
/// Triggers workflows for indexing, validation, and governance checks.
/// </summary>
public class DocumentCreatedEvent : BaseEvent
{
    public DocumentCreatedEvent(string sourceAgent, string documentId, string documentType, string correlationId)
        : base(sourceAgent, correlationId)
    {
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
        DocumentType = documentType ?? throw new ArgumentNullException(nameof(documentType));
    }

    /// <summary>Unique identifier of the created document</summary>
    public string DocumentId { get; }

    /// <summary>Type/category of the document (Template, Report, Schema, etc.)</summary>
    public string DocumentType { get; }

    /// <summary>Size of the document in bytes</summary>
    public long? DocumentSize { get; init; }

    /// <summary>MIME type of the document</summary>
    public string? ContentType { get; init; }

    /// <summary>ID of the user who created the document</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Title or name of the document</summary>
    public string? Title { get; init; }

    /// <summary>Tags associated with the document</summary>
    public List<string>? Tags { get; init; }
}

/// <summary>
/// Event published when a document is updated.
/// Triggers re-indexing and validation workflows.
/// </summary>
public class DocumentUpdatedEvent : BaseEvent
{
    public DocumentUpdatedEvent(string sourceAgent, string documentId, string correlationId)
        : base(sourceAgent, correlationId)
    {
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
    }

    /// <summary>Unique identifier of the updated document</summary>
    public string DocumentId { get; }

    /// <summary>Fields that were changed in the update</summary>
    public List<string>? ChangedFields { get; init; }

    /// <summary>Version number after the update</summary>
    public int? NewVersion { get; init; }

    /// <summary>ID of the user who updated the document</summary>
    public string? UpdatedBy { get; init; }

    /// <summary>Reason for the update</summary>
    public string? UpdateReason { get; init; }
}

/// <summary>
/// Event published when a document is deleted.
/// Triggers cleanup workflows and audit logging.
/// </summary>
public class DocumentDeletedEvent : BaseEvent
{
    public DocumentDeletedEvent(string sourceAgent, string documentId, string correlationId)
        : base(sourceAgent, correlationId)
    {
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
    }

    /// <summary>Unique identifier of the deleted document</summary>
    public string DocumentId { get; }

    /// <summary>Type of deletion (Soft, Hard, Archived)</summary>
    public DocumentDeletionType DeletionType { get; init; } = DocumentDeletionType.Soft;

    /// <summary>ID of the user who deleted the document</summary>
    public string? DeletedBy { get; init; }

    /// <summary>Reason for deletion</summary>
    public string? DeletionReason { get; init; }

    /// <summary>Whether the document can be restored</summary>
    public bool CanRestore { get; init; } = true;
}

/// <summary>
/// Type of document deletion operation.
/// </summary>
public enum DocumentDeletionType
{
    /// <summary>Document is marked as deleted but data remains</summary>
    Soft,
    
    /// <summary>Document data is permanently removed</summary>
    Hard,
    
    /// <summary>Document is moved to archive storage</summary>
    Archived
}

/// <summary>
/// Event published when document processing is completed.
/// Indicates the document is ready for use by other agents.
/// </summary>
public class DocumentProcessedEvent : BaseEvent
{
    public DocumentProcessedEvent(string sourceAgent, string documentId, bool isSuccessful, string correlationId)
        : base(sourceAgent, correlationId)
    {
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
        IsSuccessful = isSuccessful;
    }

    /// <summary>Unique identifier of the processed document</summary>
    public string DocumentId { get; }

    /// <summary>Whether the processing was successful</summary>
    public bool IsSuccessful { get; }

    /// <summary>Type of processing that was performed</summary>
    public string? ProcessingType { get; init; }

    /// <summary>Duration of the processing operation</summary>
    public TimeSpan? ProcessingDuration { get; init; }

    /// <summary>Error message if processing failed</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Processing results or metadata</summary>
    public Dictionary<string, object>? ProcessingResults { get; init; }
}