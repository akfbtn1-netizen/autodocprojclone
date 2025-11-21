-- =============================================
-- Approval Tracking Table
-- Stores approval actions for AI training and quality improvement
-- =============================================

USE [IRFS1]
GO

-- Create schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DaQa')
BEGIN
    EXEC('CREATE SCHEMA [DaQa]')
END
GO

-- Drop table if exists (for development)
IF OBJECT_ID('DaQa.ApprovalTracking', 'U') IS NOT NULL
    DROP TABLE DaQa.ApprovalTracking
GO

-- Create Approval Tracking table
CREATE TABLE DaQa.ApprovalTracking
(
    TrackingId INT IDENTITY(1,1) NOT NULL,
    DocId NVARCHAR(100) NOT NULL,
    Action NVARCHAR(50) NOT NULL, -- 'Approved', 'Edited', 'Rejected', 'Rerequested'

    -- Approver Info
    ApproverUserId NVARCHAR(50) NOT NULL,
    ApproverName NVARCHAR(200) NOT NULL,
    ActionDate DATETIME2 NOT NULL,

    -- Content tracking (for Edited actions)
    OriginalContent NVARCHAR(MAX) NULL,
    EditedContent NVARCHAR(MAX) NULL,
    ContentDiff NVARCHAR(MAX) NULL, -- Calculated diff
    ChangedFields NVARCHAR(MAX) NULL, -- JSON array of changed field names

    -- Rejection tracking
    RejectionReason NVARCHAR(MAX) NULL,

    -- Rerequest tracking
    RerequestPrompt NVARCHAR(MAX) NULL,

    -- Feedback for AI training
    ApproverFeedback NVARCHAR(MAX) NULL,
    QualityRating INT NULL, -- 1-5 scale

    -- Context for AI learning
    DocumentType NVARCHAR(50) NULL,
    ChangeType NVARCHAR(50) NULL,
    WasAIEnhanced BIT NULL,

    -- Audit
    CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT PK_ApprovalTracking PRIMARY KEY CLUSTERED (TrackingId)
)
GO

-- Indexes for efficient querying
CREATE NONCLUSTERED INDEX IX_ApprovalTracking_DocId
    ON DaQa.ApprovalTracking(DocId)
    INCLUDE (Action, ActionDate, ApproverName)
GO

CREATE NONCLUSTERED INDEX IX_ApprovalTracking_Action
    ON DaQa.ApprovalTracking(Action, ActionDate DESC)
    INCLUDE (DocId, DocumentType, QualityRating)
GO

CREATE NONCLUSTERED INDEX IX_ApprovalTracking_ActionDate
    ON DaQa.ApprovalTracking(ActionDate DESC)
    INCLUDE (DocId, Action, DocumentType)
GO

-- Index for AI training queries
CREATE NONCLUSTERED INDEX IX_ApprovalTracking_Training
    ON DaQa.ApprovalTracking(WasAIEnhanced, Action)
    WHERE Action IN ('Edited', 'Rejected', 'Rerequested')
    INCLUDE (DocumentType, ChangeType, QualityRating, ChangedFields)
GO

-- View for AI training insights
CREATE OR ALTER VIEW DaQa.vw_ApprovalInsights
AS
SELECT
    DocumentType,
    ChangeType,
    Action,
    COUNT(*) AS ActionCount,
    AVG(CAST(QualityRating AS FLOAT)) AS AvgQualityRating,
    COUNT(CASE WHEN WasAIEnhanced = 1 THEN 1 END) AS AIEnhancedCount,
    COUNT(CASE WHEN WasAIEnhanced = 0 THEN 1 END) AS NonAIEnhancedCount
FROM DaQa.ApprovalTracking
WHERE Action IN ('Approved', 'Edited', 'Rejected')
GROUP BY DocumentType, ChangeType, Action
GO

-- View for common edits (for AI improvement)
CREATE OR ALTER VIEW DaQa.vw_CommonEdits
AS
SELECT
    DocumentType,
    ChangeType,
    ChangedFields,
    COUNT(*) AS EditCount,
    AVG(CAST(QualityRating AS FLOAT)) AS AvgQualityRating
FROM DaQa.ApprovalTracking
WHERE Action = 'Edited'
  AND ChangedFields IS NOT NULL
GROUP BY DocumentType, ChangeType, ChangedFields
HAVING COUNT(*) > 1 -- Only show patterns that appear more than once
GO

PRINT 'Approval Tracking table and views created successfully'
PRINT 'Use DaQa.vw_ApprovalInsights for quality metrics'
PRINT 'Use DaQa.vw_CommonEdits to identify common editing patterns'
GO
