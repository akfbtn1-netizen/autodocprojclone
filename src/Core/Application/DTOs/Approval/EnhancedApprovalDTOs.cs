// ═══════════════════════════════════════════════════════════════════════════
// Enhanced Approval DTOs
// Matches frontend types for 17-table approval workflow schema
// ═══════════════════════════════════════════════════════════════════════════

using System.ComponentModel.DataAnnotations;

namespace Enterprise.Documentation.Core.Application.DTOs.Approval;

// ─────────────────────────────────────────────────────────────────────────────
// Core Approval DTO (matches frontend Approval interface)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Full approval record DTO matching frontend Approval type
/// </summary>
public class ApprovalDetailDto
{
    public int Id { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public int? MasterIndexId { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string TemplateUsed { get; set; } = string.Empty;
    public string CabNumber { get; set; } = string.Empty;

    // File paths
    public string GeneratedFilePath { get; set; } = string.Empty;
    public string? DestinationPath { get; set; }
    public long? FileSizeBytes { get; set; }

    // Approval state
    public string Status { get; set; } = "PendingApproval";
    public string Priority { get; set; } = "Medium";

    // Assignment
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }

    // Resolution
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }

    // Versioning
    public int Version { get; set; } = 1;
    public int? PreviousVersionId { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    // Computed fields
    public string? ChangeDescription { get; set; }
    public string? JiraNumber { get; set; }
    public string? TableName { get; set; }
    public int? QualityRating { get; set; }
    public string? SharepointLink { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Approval History DTO
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Approval history record DTO
/// </summary>
public class ApprovalHistoryDto
{
    public int Id { get; set; }
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActionBy { get; set; } = string.Empty;
    public DateTime ActionAt { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? SourcePath { get; set; }
    public string? DestinationPath { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Approval Tracking DTO (AI Learning Data)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Approval tracking record for AI learning
/// </summary>
public class ApprovalTrackingDto
{
    public int Id { get; set; }
    public string DocId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime ActionDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }

    // Content tracking for AI learning
    public string? OriginalContent { get; set; }
    public string? EditedContent { get; set; }
    public string? ContentDiff { get; set; }
    public string? ChangedFields { get; set; }

    // Feedback for continuous learning
    public string? RejectionReason { get; set; }
    public string? RerequestPrompt { get; set; }
    public string? ApproverFeedback { get; set; }
    public int? QualityRating { get; set; }

    // Metadata
    public string? DocumentType { get; set; }
    public string? ChangeType { get; set; }
    public bool WasAIEnhanced { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Document Edit DTO
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Document edit record for section-level tracking
/// </summary>
public class DocumentEditDto
{
    public int Id { get; set; }
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string EditedText { get; set; } = string.Empty;
    public string? EditReason { get; set; }
    public string EditCategory { get; set; } = "Clarification";
    public string EditedBy { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; }
    public bool ShouldTrainAI { get; set; } = true;
    public bool AIFeedbackProcessed { get; set; }
}

/// <summary>
/// Request to save document edits
/// </summary>
public class SaveEditsRequest
{
    public List<DocumentEditDto> Edits { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Regeneration Request DTO
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Request for document regeneration with feedback
/// </summary>
public class RegenerationRequestDto
{
    public int? Id { get; set; }
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public int OriginalVersion { get; set; }

    [Required]
    public string FeedbackText { get; set; } = string.Empty;
    public string? FeedbackSection { get; set; }
    public string? AdditionalContext { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime? RequestedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public int? NewVersion { get; set; }
    public int? NewApprovalId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Workflow Event DTO (Event Sourcing)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Workflow event for event sourcing
/// </summary>
public class WorkflowEventDto
{
    public int EventId { get; set; }
    public string WorkflowId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = "Success";
    public string? Message { get; set; }
    public int? DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Metadata { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Notification DTO
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Notification record DTO
/// </summary>
public class NotificationDto
{
    public int NotificationId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public string? ChangeType { get; set; }
    public string? Priority { get; set; }
    public string? JiraNumber { get; set; }
    public string? DocumentPath { get; set; }
    public bool IsRead { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentDate { get; set; }
    public DateTime? ReadDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Enhanced Statistics DTO
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Enhanced approval statistics
/// </summary>
public class EnhancedApprovalStats
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Editing { get; set; }
    public int RePromptRequested { get; set; }
    public double AvgTimeToApproval { get; set; }
    public DateTime? OldestPending { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Filter DTOs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Approval filter parameters
/// </summary>
public class ApprovalFilterParams
{
    public List<string>? Status { get; set; }
    public List<string>? DocumentType { get; set; }
    public List<string>? Priority { get; set; }
    public string? AssignedTo { get; set; }
    public string? Search { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Action Request DTOs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Request to approve a document
/// </summary>
public class ApproveDocumentRequestDto
{
    public string? Comments { get; set; }
    public int? QualityRating { get; set; }
    public string? FeedbackText { get; set; }
}

/// <summary>
/// Request to reject a document
/// </summary>
public class RejectDocumentRequestDto
{
    [Required]
    public string RejectionReason { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public int? QualityRating { get; set; }
    public string? FeedbackText { get; set; }
}

/// <summary>
/// Request for bulk approval
/// </summary>
public class BulkApproveRequest
{
    [Required]
    public List<int> ApprovalIds { get; set; } = new();
    public string? Comments { get; set; }
}

/// <summary>
/// Request for bulk rejection
/// </summary>
public class BulkRejectRequest
{
    [Required]
    public List<int> ApprovalIds { get; set; } = new();

    [Required]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Result of bulk operation
/// </summary>
public class BulkOperationResult
{
    public List<int> Succeeded { get; set; } = new();
    public List<int> Failed { get; set; } = new();
}

/// <summary>
/// Request to assign an approval
/// </summary>
public class AssignApprovalRequest
{
    [Required]
    public string AssignedTo { get; set; } = string.Empty;
}

/// <summary>
/// Request to reassign an approval
/// </summary>
public class ReassignApprovalRequest
{
    [Required]
    public string AssignedTo { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>
/// Request to escalate an approval
/// </summary>
public class EscalateApprovalRequest
{
    public string? Message { get; set; }
}

/// <summary>
/// Request to submit feedback
/// </summary>
public class SubmitFeedbackRequest
{
    [Range(1, 5)]
    public int QualityRating { get; set; }
    public string? FeedbackText { get; set; }
}

/// <summary>
/// Document content sections for inline editing
/// </summary>
public class DocumentContentDto
{
    public List<DocumentSectionDto> Sections { get; set; } = new();
}

/// <summary>
/// Single document section
/// </summary>
public class DocumentSectionDto
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Search result with pagination
/// </summary>
public class ApprovalSearchResult
{
    public List<ApprovalDetailDto> Items { get; set; } = new();
    public int Total { get; set; }
}
