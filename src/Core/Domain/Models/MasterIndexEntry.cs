using System;

namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Represents a row from the MasterIndex Excel spreadsheet.
/// This is the central tracking document for all change documentation.
/// </summary>
public class MasterIndexEntry
{
    public int Id { get; set; }

    // Core Identifiers
    public string? DocumentId { get; set; }
    public string? CABNumber { get; set; }
    public string? ChangeRequestId { get; set; }

    // Document Information
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? DocumentType { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }

    // Classification
    public string? TierClassification { get; set; }  // Tier1, Tier2, Tier3
    public string? DataClassification { get; set; }  // Public, Internal, Confidential, Restricted
    public string? SecurityClearance { get; set; }

    // Ownership & Responsibility
    public string? BusinessOwner { get; set; }
    public string? TechnicalOwner { get; set; }
    public string? Author { get; set; }
    public string? Department { get; set; }
    public string? Team { get; set; }

    // Approval Workflow
    public string? ApprovalStatus { get; set; }
    public string? CurrentApprover { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovalComments { get; set; }

    // Dates & Versioning
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Version { get; set; }
    public int? RevisionNumber { get; set; }

    // Database Objects (for stored procedures/tables)
    public string? DatabaseName { get; set; }
    public string? SchemaName { get; set; }
    public string? ObjectName { get; set; }
    public string? ObjectType { get; set; }
    public string? SourceTables { get; set; }
    public string? TargetTables { get; set; }

    // File References
    public string? FilePath { get; set; }
    public string? GeneratedDocPath { get; set; }
    public string? TemplateUsed { get; set; }

    // Status & Tracking
    public string? Status { get; set; }  // Draft, InReview, Approved, Published, Archived
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public string? Tags { get; set; }
    public string? Notes { get; set; }

    // Sync Metadata
    public int ExcelRowNumber { get; set; }
    public DateTime LastSyncedFromExcel { get; set; }
    public string? SyncStatus { get; set; }
    public string? SyncErrors { get; set; }
}
