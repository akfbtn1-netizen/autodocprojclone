using System.ComponentModel.DataAnnotations;

namespace Enterprise.Documentation.Core.Domain.Entities;

/// <summary>
/// Represents an approval request for document generation in the approval workflow.
/// </summary>
public class ApprovalEntity
{
    /// <summary>
    /// Unique identifier for the approval request.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the related document.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// ID of the MasterIndex record being documented.
    /// </summary>
    public int MasterIndexId { get; set; }

    /// <summary>
    /// Type of document being generated.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Current approval status.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// Database name for the object being documented.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Object name being documented.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Schema name for the object.
    /// </summary>
    [MaxLength(255)]
    public string SchemaName { get; set; } = string.Empty;

    /// <summary>
    /// AI model used for generation.
    /// </summary>
    [MaxLength(100)]
    public string AIModel { get; set; } = string.Empty;

    /// <summary>
    /// AI confidence score for the generated content.
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Document tier classification.
    /// </summary>
    public int Tier { get; set; }

    /// <summary>
    /// Number of AI tokens used in generation.
    /// </summary>
    public int TokensUsed { get; set; }

    /// <summary>
    /// When the approval request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Who created the approval request.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// When the approval was completed (if applicable).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Who approved/rejected the request.
    /// </summary>
    [MaxLength(255)]
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Comments from the approver.
    /// </summary>
    public string? Comments { get; set; }

    // Additional properties needed by ApprovalService
    /// <summary>
    /// JIRA ticket number for tracking.
    /// </summary>
    [MaxLength(50)]
    public string? JiraNumber { get; set; }

    /// <summary>
    /// Path to the generated document.
    /// </summary>
    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    /// <summary>
    /// SLA hours for completion.
    /// </summary>
    public int SLAHours { get; set; } = 48;

    /// <summary>
    /// Creation date (alias for CreatedAt for compatibility).
    /// </summary>
    public DateTime CreatedDate 
    { 
        get => CreatedAt; 
        set => CreatedAt = value; 
    }

    /// <summary>
    /// Due date for the approval.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Email of the requester.
    /// </summary>
    [MaxLength(255)]
    public string? RequesterEmail { get; set; }

    /// <summary>
    /// Metadata identifier for additional tracking.
    /// </summary>
    public int? MetadataId { get; set; }

    /// <summary>
    /// Priority level for the approval.
    /// </summary>
    [MaxLength(50)]
    public string Priority { get; set; } = "NORMAL";

    /// <summary>
    /// Business domain classification.
    /// </summary>
    [MaxLength(100)]
    public string BusinessDomain { get; set; } = string.Empty;

    /// <summary>
    /// Data classification level.
    /// </summary>
    [MaxLength(50)]
    public string DataClassification { get; set; } = string.Empty;

    /// <summary>
    /// Whether the object contains PII data.
    /// </summary>
    public bool PIIIndicator { get; set; }

    /// <summary>
    /// Business criticality level.
    /// </summary>
    [MaxLength(50)]
    public string BusinessCriticality { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to the related MasterIndex record.
    /// </summary>
    public virtual MasterIndex? MasterIndex { get; set; }
}