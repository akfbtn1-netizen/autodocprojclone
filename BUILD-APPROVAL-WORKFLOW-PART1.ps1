# ============================================================================
# COMPREHENSIVE APPROVAL WORKFLOW BUILD SCRIPT
# ============================================================================
# This script generates ALL files needed for the document approval workflow
# Run from: C:\Projects\EnterpriseDocumentationPlatform.V2
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$srcPath = Join-Path $projectRoot "src"
$corePath = Join-Path $srcPath "Core"
$apiPath = Join-Path $srcPath "Api"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  BUILDING APPROVAL WORKFLOW SYSTEM" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# PART 1: CREATE DIRECTORY STRUCTURE
# ============================================================================

Write-Host "[1/10] Creating directory structure..." -ForegroundColor Yellow

$directories = @(
    (Join-Path $corePath "Domain\Models\Approval"),
    (Join-Path $corePath "Application\Services\Approval"),
    (Join-Path $corePath "Application\Interfaces\Approval"),
    (Join-Path $corePath "Infrastructure\Persistence\Repositories\Approval"),
    (Join-Path $apiPath "Controllers\Approval"),
    (Join-Path $projectRoot "sql\approval")
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Green
    }
}

Write-Host "  Done" -ForegroundColor Green
Write-Host ""

# ============================================================================
# PART 2: SQL SCRIPTS FOR DATABASE TABLES
# ============================================================================

Write-Host "[2/10] Creating SQL scripts..." -ForegroundColor Yellow

$sqlScript = @'
-- ============================================================================
-- APPROVAL WORKFLOW TABLES - DaQa Schema
-- ============================================================================
-- Run this script against your database to create approval workflow tables
-- ============================================================================

-- Ensure DaQa schema exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DaQa')
BEGIN
    EXEC('CREATE SCHEMA DaQa')
END
GO

-- ============================================================================
-- Table: Approvers - Who can approve documents
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.Approvers') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.Approvers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Email NVARCHAR(255) NOT NULL,
        DisplayName NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        NotificationPreference NVARCHAR(50) NOT NULL DEFAULT 'Teams', -- 'Teams', 'Email', 'Both'
        TeamsWebhookUrl NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ModifiedAt DATETIME2 NULL
    )

    CREATE UNIQUE INDEX IX_Approvers_Email ON DaQa.Approvers(Email)

    PRINT 'Created table: DaQa.Approvers'
END
GO

-- ============================================================================
-- Table: DocumentApprovals - Main approval queue
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.DocumentApprovals') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.DocumentApprovals (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DocumentId NVARCHAR(50) NOT NULL,           -- e.g., SP-20251118-684F6AE2
        MasterIndexId INT NULL,                      -- Link to MasterIndex

        -- Document Info
        ObjectName NVARCHAR(255) NOT NULL,
        SchemaName NVARCHAR(128) NOT NULL,
        DatabaseName NVARCHAR(128) NOT NULL,
        DocumentType NVARCHAR(50) NOT NULL,          -- 'StoredProcedure', 'Table', 'DefectFix', 'BusinessRequest'
        TemplateUsed NVARCHAR(100) NULL,             -- 'Tier1', 'Tier2', 'Tier3'
        CABNumber NVARCHAR(50) NULL,                 -- CAB # if applicable

        -- File Info
        GeneratedFilePath NVARCHAR(500) NOT NULL,    -- Where the .docx was generated
        DestinationPath NVARCHAR(500) NULL,          -- Where it should go after approval
        FileSizeBytes BIGINT NULL,

        -- Status
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected', 'Regenerating', 'Edited'
        Priority NVARCHAR(20) NOT NULL DEFAULT 'Normal', -- 'Low', 'Normal', 'High', 'Urgent'

        -- Workflow
        RequestedBy NVARCHAR(100) NOT NULL,
        RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        AssignedTo NVARCHAR(255) NULL,               -- Email of assigned approver
        DueDate DATETIME2 NULL,

        -- Resolution
        ResolvedBy NVARCHAR(100) NULL,
        ResolvedAt DATETIME2 NULL,
        ResolutionNotes NVARCHAR(MAX) NULL,

        -- Versioning
        Version INT NOT NULL DEFAULT 1,
        PreviousVersionId INT NULL,

        -- Metadata
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ModifiedAt DATETIME2 NULL
    )

    CREATE INDEX IX_DocumentApprovals_Status ON DaQa.DocumentApprovals(Status)
    CREATE INDEX IX_DocumentApprovals_DocumentId ON DaQa.DocumentApprovals(DocumentId)
    CREATE INDEX IX_DocumentApprovals_RequestedAt ON DaQa.DocumentApprovals(RequestedAt DESC)

    PRINT 'Created table: DaQa.DocumentApprovals'
END
GO

-- ============================================================================
-- Table: ApprovalHistory - Audit trail of all actions
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.ApprovalHistory') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.ApprovalHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApprovalId INT NOT NULL,
        DocumentId NVARCHAR(50) NOT NULL,

        -- Action
        Action NVARCHAR(50) NOT NULL,                -- 'Created', 'Viewed', 'Edited', 'Approved', 'Rejected', 'Regenerated', 'Moved'
        ActionBy NVARCHAR(100) NOT NULL,
        ActionAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- Details
        PreviousStatus NVARCHAR(50) NULL,
        NewStatus NVARCHAR(50) NULL,
        Notes NVARCHAR(MAX) NULL,

        -- For tracking file movements
        SourcePath NVARCHAR(500) NULL,
        DestinationPath NVARCHAR(500) NULL,

        CONSTRAINT FK_ApprovalHistory_Approval FOREIGN KEY (ApprovalId)
            REFERENCES DaQa.DocumentApprovals(Id)
    )

    CREATE INDEX IX_ApprovalHistory_ApprovalId ON DaQa.ApprovalHistory(ApprovalId)
    CREATE INDEX IX_ApprovalHistory_DocumentId ON DaQa.ApprovalHistory(DocumentId)
    CREATE INDEX IX_ApprovalHistory_ActionAt ON DaQa.ApprovalHistory(ActionAt DESC)

    PRINT 'Created table: DaQa.ApprovalHistory'
END
GO

-- ============================================================================
-- Table: DocumentEdits - Track changes made during review
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.DocumentEdits') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.DocumentEdits (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApprovalId INT NOT NULL,
        DocumentId NVARCHAR(50) NOT NULL,

        -- What was changed
        SectionName NVARCHAR(100) NOT NULL,          -- 'Purpose', 'Parameters', 'ExecutionLogic', etc.
        OriginalText NVARCHAR(MAX) NULL,
        EditedText NVARCHAR(MAX) NULL,

        -- Why it was changed (for AI learning)
        EditReason NVARCHAR(500) NULL,
        EditCategory NVARCHAR(100) NULL,             -- 'Accuracy', 'Clarity', 'Completeness', 'Formatting'

        -- Who/When
        EditedBy NVARCHAR(100) NOT NULL,
        EditedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- For AI feedback loop
        ShouldTrainAI BIT NOT NULL DEFAULT 1,
        AIFeedbackProcessed BIT NOT NULL DEFAULT 0,

        CONSTRAINT FK_DocumentEdits_Approval FOREIGN KEY (ApprovalId)
            REFERENCES DaQa.DocumentApprovals(Id)
    )

    CREATE INDEX IX_DocumentEdits_ApprovalId ON DaQa.DocumentEdits(ApprovalId)
    CREATE INDEX IX_DocumentEdits_DocumentId ON DaQa.DocumentEdits(DocumentId)
    CREATE INDEX IX_DocumentEdits_ShouldTrainAI ON DaQa.DocumentEdits(ShouldTrainAI) WHERE ShouldTrainAI = 1

    PRINT 'Created table: DaQa.DocumentEdits'
END
GO

-- ============================================================================
-- Table: RegenerationRequests - Feedback for regeneration
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.RegenerationRequests') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.RegenerationRequests (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ApprovalId INT NOT NULL,
        DocumentId NVARCHAR(50) NOT NULL,
        OriginalVersion INT NOT NULL,

        -- Feedback
        FeedbackText NVARCHAR(MAX) NOT NULL,
        FeedbackSection NVARCHAR(100) NULL,          -- Which section needs work
        AdditionalContext NVARCHAR(MAX) NULL,        -- Extra info for AI

        -- Tracking
        RequestedBy NVARCHAR(100) NOT NULL,
        RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- Result
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Processing', 'Completed', 'Failed'
        NewVersion INT NULL,
        NewApprovalId INT NULL,
        CompletedAt DATETIME2 NULL,
        ErrorMessage NVARCHAR(MAX) NULL,

        CONSTRAINT FK_RegenerationRequests_Approval FOREIGN KEY (ApprovalId)
            REFERENCES DaQa.DocumentApprovals(Id)
    )

    CREATE INDEX IX_RegenerationRequests_ApprovalId ON DaQa.RegenerationRequests(ApprovalId)
    CREATE INDEX IX_RegenerationRequests_Status ON DaQa.RegenerationRequests(Status)

    PRINT 'Created table: DaQa.RegenerationRequests'
END
GO

-- ============================================================================
-- Insert default approvers (update with your actual info)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM DaQa.Approvers WHERE Email = 'your.email@company.com')
BEGIN
    INSERT INTO DaQa.Approvers (Email, DisplayName, NotificationPreference)
    VALUES
        ('your.email@company.com', 'Your Name', 'Teams'),
        ('alex@company.com', 'Alex', 'Teams')

    PRINT 'Inserted default approvers'
END
GO

-- ============================================================================
-- Add CAB Number to MasterIndex (if table exists)
-- ============================================================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.MasterIndex') AND type = 'U')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MasterIndex') AND name = 'CABNumber')
    BEGIN
        ALTER TABLE dbo.MasterIndex ADD CABNumber NVARCHAR(50) NULL
        PRINT 'Added CABNumber column to MasterIndex'
    END
END
GO

PRINT ''
PRINT '============================================================================'
PRINT 'Approval workflow tables created successfully!'
PRINT '============================================================================'
GO
'@

$sqlPath = Join-Path $projectRoot "sql\approval\create-approval-tables.sql"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($sqlPath, $sqlScript, $utf8NoBom)
Write-Host "  Created: $sqlPath" -ForegroundColor Green
Write-Host ""

# ============================================================================
# PART 3: C# MODELS
# ============================================================================

Write-Host "[3/10] Creating C# models..." -ForegroundColor Yellow

# DocumentApproval model
$documentApprovalModel = @'
using System;
using System.Collections.Generic;

namespace Enterprise.Documentation.Core.Domain.Models.Approval;

/// <summary>
/// Represents a document in the approval queue.
/// </summary>
public class DocumentApproval
{
    public int Id { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public int? MasterIndexId { get; set; }

    // Document Info
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? TemplateUsed { get; set; }
    public string? CABNumber { get; set; }

    // File Info
    public string GeneratedFilePath { get; set; } = string.Empty;
    public string? DestinationPath { get; set; }
    public long? FileSizeBytes { get; set; }

    // Status
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public ApprovalPriority Priority { get; set; } = ApprovalPriority.Normal;

    // Workflow
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }

    // Resolution
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }

    // Versioning
    public int Version { get; set; } = 1;
    public int? PreviousVersionId { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    // Navigation
    public ICollection<ApprovalHistoryEntry> History { get; set; } = new List<ApprovalHistoryEntry>();
    public ICollection<DocumentEdit> Edits { get; set; } = new List<DocumentEdit>();
    public ICollection<RegenerationRequest> RegenerationRequests { get; set; } = new List<RegenerationRequest>();
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Regenerating,
    Edited
}

public enum ApprovalPriority
{
    Low,
    Normal,
    High,
    Urgent
}
'@

$modelPath = Join-Path $corePath "Domain\Models\Approval\DocumentApproval.cs"
[System.IO.File]::WriteAllText($modelPath, $documentApprovalModel, $utf8NoBom)
Write-Host "  Created: DocumentApproval.cs" -ForegroundColor Green

# ApprovalHistoryEntry model
$approvalHistoryModel = @'
using System;

namespace Enterprise.Documentation.Core.Domain.Models.Approval;

/// <summary>
/// Audit trail entry for approval actions.
/// </summary>
public class ApprovalHistoryEntry
{
    public int Id { get; set; }
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;

    // Action
    public string Action { get; set; } = string.Empty;
    public string ActionBy { get; set; } = string.Empty;
    public DateTime ActionAt { get; set; } = DateTime.UtcNow;

    // Details
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Notes { get; set; }

    // File tracking
    public string? SourcePath { get; set; }
    public string? DestinationPath { get; set; }

    // Navigation
    public DocumentApproval? Approval { get; set; }
}

public static class ApprovalActions
{
    public const string Created = "Created";
    public const string Viewed = "Viewed";
    public const string Edited = "Edited";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Regenerated = "Regenerated";
    public const string Moved = "Moved";
    public const string Assigned = "Assigned";
    public const string Commented = "Commented";
}
'@

$historyModelPath = Join-Path $corePath "Domain\Models\Approval\ApprovalHistoryEntry.cs"
[System.IO.File]::WriteAllText($historyModelPath, $approvalHistoryModel, $utf8NoBom)
Write-Host "  Created: ApprovalHistoryEntry.cs" -ForegroundColor Green

# DocumentEdit model
$documentEditModel = @'
using System;

namespace Enterprise.Documentation.Core.Domain.Models.Approval;

/// <summary>
/// Tracks changes made to documents during review.
/// Used for AI training and improvement feedback loop.
/// </summary>
public class DocumentEdit
{
    public int Id { get; set; }
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;

    // What was changed
    public string SectionName { get; set; } = string.Empty;
    public string? OriginalText { get; set; }
    public string? EditedText { get; set; }

    // Why (for AI learning)
    public string? EditReason { get; set; }
    public EditCategory Category { get; set; } = EditCategory.Other;

    // Who/When
    public string EditedBy { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;

    // AI feedback loop
    public bool ShouldTrainAI { get; set; } = true;
    public bool AIFeedbackProcessed { get; set; } = false;

    // Navigation
    public DocumentApproval? Approval { get; set; }
}

public enum EditCategory
{
    Accuracy,
    Clarity,
    Completeness,
    Formatting,
    TechnicalDetail,
    BusinessContext,
    Other
}

public static class DocumentSections
{
    public const string Purpose = "Purpose";
    public const string Parameters = "Parameters";
    public const string ExecutionLogic = "ExecutionLogic";
    public const string Dependencies = "Dependencies";
    public const string UsageExamples = "UsageExamples";
    public const string ErrorHandling = "ErrorHandling";
    public const string ChangeHistory = "ChangeHistory";
    public const string PerformanceMetrics = "PerformanceMetrics";
}
'@

$editModelPath = Join-Path $corePath "Domain\Models\Approval\DocumentEdit.cs"
[System.IO.File]::WriteAllText($editModelPath, $documentEditModel, $utf8NoBom)
Write-Host "  Created: DocumentEdit.cs" -ForegroundColor Green

# RegenerationRequest model
$regenerationModel = @'
using System;

namespace Enterprise.Documentation.Core.Domain.Models.Approval;

/// <summary>
/// Request to regenerate a document with additional feedback.
/// </summary>
public class RegenerationRequest
{
    public int Id { get; set; }
    public int ApprovalId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public int OriginalVersion { get; set; }

    // Feedback
    public string FeedbackText { get; set; } = string.Empty;
    public string? FeedbackSection { get; set; }
    public string? AdditionalContext { get; set; }

    // Tracking
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    // Result
    public RegenerationStatus Status { get; set; } = RegenerationStatus.Pending;
    public int? NewVersion { get; set; }
    public int? NewApprovalId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation
    public DocumentApproval? Approval { get; set; }
}

public enum RegenerationStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
'@

$regenModelPath = Join-Path $corePath "Domain\Models\Approval\RegenerationRequest.cs"
[System.IO.File]::WriteAllText($regenModelPath, $regenerationModel, $utf8NoBom)
Write-Host "  Created: RegenerationRequest.cs" -ForegroundColor Green

# Approver model
$approverModel = @'
using System;

namespace Enterprise.Documentation.Core.Domain.Models.Approval;

/// <summary>
/// Person who can approve documents.
/// </summary>
public class Approver
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.Teams;
    public string? TeamsWebhookUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
}

public enum NotificationPreference
{
    Teams,
    Email,
    Both,
    None
}
'@

$approverModelPath = Join-Path $corePath "Domain\Models\Approval\Approver.cs"
[System.IO.File]::WriteAllText($approverModelPath, $approverModel, $utf8NoBom)
Write-Host "  Created: Approver.cs" -ForegroundColor Green

Write-Host ""

# Continue in next part...
Write-Host "[4/10] Creating more files... (script continues)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Script Part 1 complete. Run Part 2 for services and API..." -ForegroundColor Cyan
