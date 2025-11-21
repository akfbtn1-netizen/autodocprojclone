-- =============================================
-- Batch Processing Tables - Support for Multi-Source Batch Documentation
-- Fixed for SQL Server compatibility and DaQa schema requirement
-- =============================================

USE [IRFS1]
GO

-- Create schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DaQa')
BEGIN
    EXEC('CREATE SCHEMA [DaQa]')
END
GO

PRINT 'Starting Batch Processing Tables creation...'
GO

-- =============================================
-- DROP EXISTING OBJECTS (in correct order)
-- =============================================

-- Drop views first
IF OBJECT_ID('DaQa.vw_VectorIndexingStatus', 'V') IS NOT NULL
    DROP VIEW DaQa.vw_VectorIndexingStatus
GO

IF OBJECT_ID('DaQa.vw_ConfidenceDistribution', 'V') IS NOT NULL
    DROP VIEW DaQa.vw_ConfidenceDistribution
GO

IF OBJECT_ID('DaQa.vw_BatchProcessingMetrics', 'V') IS NOT NULL
    DROP VIEW DaQa.vw_BatchProcessingMetrics
GO

IF OBJECT_ID('DaQa.vw_ItemsRequiringReview', 'V') IS NOT NULL
    DROP VIEW DaQa.vw_ItemsRequiringReview
GO

IF OBJECT_ID('DaQa.vw_BatchJobSummary', 'V') IS NOT NULL
    DROP VIEW DaQa.vw_BatchJobSummary
GO

-- Drop stored procedures
IF OBJECT_ID('DaQa.usp_CancelBatch', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_CancelBatch
GO

IF OBJECT_ID('DaQa.usp_RejectItems', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_RejectItems
GO

IF OBJECT_ID('DaQa.usp_ApproveItems', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_ApproveItems
GO

IF OBJECT_ID('DaQa.usp_GetItemsRequiringReview', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_GetItemsRequiringReview
GO

IF OBJECT_ID('DaQa.usp_GetBatchStatus', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_GetBatchStatus
GO

-- Drop tables (child first, then parent)
IF OBJECT_ID('DaQa.BatchJobItems', 'U') IS NOT NULL
    DROP TABLE DaQa.BatchJobItems
GO

IF OBJECT_ID('DaQa.BatchJobs', 'U') IS NOT NULL
    DROP TABLE DaQa.BatchJobs
GO

PRINT 'Existing objects dropped successfully'
GO

-- =============================================
-- CREATE BatchJobs TABLE
-- =============================================

CREATE TABLE DaQa.BatchJobs
(
    -- Primary Key
    BatchId UNIQUEIDENTIFIER NOT NULL,

    -- Source Information
    SourceType NVARCHAR(50) NOT NULL, -- DatabaseSchema, FolderScan, ExcelImport, ManualUpload
    DatabaseName NVARCHAR(100) NULL,
    SchemaName NVARCHAR(100) NULL,
    SourcePath NVARCHAR(500) NULL,

    -- Status and Progress
    Status NVARCHAR(50) NOT NULL, -- Pending, Processing, Completed, Failed, Cancelled
    TotalItems INT NOT NULL DEFAULT 0,
    ProcessedCount INT NOT NULL DEFAULT 0,
    SuccessCount INT NOT NULL DEFAULT 0,
    FailedCount INT NOT NULL DEFAULT 0,
    RequiresReviewCount INT NOT NULL DEFAULT 0,
    ProgressPercentage FLOAT NULL, -- Calculated: ProcessedCount / TotalItems * 100

    -- Confidence Tracking
    HighConfidenceCount INT NOT NULL DEFAULT 0,   -- >= 0.85
    MediumConfidenceCount INT NOT NULL DEFAULT 0, -- 0.70-0.84
    LowConfidenceCount INT NOT NULL DEFAULT 0,    -- < 0.70
    AverageConfidence FLOAT NULL,

    -- Vector Indexing Status
    VectorIndexedCount INT NOT NULL DEFAULT 0,
    VectorIndexFailedCount INT NOT NULL DEFAULT 0,

    -- Timing
    StartedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt DATETIME2 NULL,
    DurationSeconds FLOAT NULL,
    EstimatedTimeRemainingSeconds FLOAT NULL,

    -- Error Tracking
    ErrorMessage NVARCHAR(MAX) NULL,
    StackTrace NVARCHAR(MAX) NULL,

    -- Configuration (JSON)
    OptionsJson NVARCHAR(MAX) NULL,

    -- Audit
    CreatedBy NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedBy NVARCHAR(200) NULL,
    ModifiedAt DATETIME2 NULL,

    -- Primary Key Constraint
    CONSTRAINT PK_BatchJobs PRIMARY KEY CLUSTERED (BatchId)
)
GO

PRINT 'BatchJobs table created successfully'
GO

-- =============================================
-- CREATE BatchJobItems TABLE
-- =============================================

CREATE TABLE DaQa.BatchJobItems
(
    -- Primary Key
    ItemId UNIQUEIDENTIFIER NOT NULL,

    -- Foreign Key
    BatchId UNIQUEIDENTIFIER NOT NULL,

    -- Object Information
    ObjectName NVARCHAR(200) NOT NULL,
    ObjectType NVARCHAR(100) NULL, -- Procedure, Table, View, Function, Document, etc.

    -- Status
    Status NVARCHAR(50) NOT NULL, -- Pending, MetadataExtraction, ValidationRequired, Approved, DocumentGeneration, IndexingMetadata, VectorIndexing, Completed, Failed, Rejected

    -- Metadata Extraction
    ExtractedMetadataJson NVARCHAR(MAX) NULL,
    ConfidenceScore FLOAT NULL,
    ExtractionMethod NVARCHAR(50) NULL, -- INFORMATION_SCHEMA, NER, OpenAI, Hybrid, Document, Excel

    -- Validation
    RequiresHumanReview BIT NOT NULL DEFAULT 0,
    ValidationWarningsJson NVARCHAR(MAX) NULL, -- JSON array

    -- Generated Outputs
    GeneratedDocId NVARCHAR(200) NULL,        -- DocId from AutoDraftService
    DocumentPath NVARCHAR(500) NULL,          -- Path to generated .docx
    IsVectorIndexed BIT NOT NULL DEFAULT 0,
    VectorId NVARCHAR(200) NULL,              -- Vector database ID
    MasterIndexId INT NULL,                    -- Foreign key to MasterIndex.IndexID

    -- Human Review
    ReviewedBy NVARCHAR(200) NULL,
    ReviewedAt DATETIME2 NULL,
    ReviewNotes NVARCHAR(MAX) NULL,

    -- Processing Metrics
    ProcessedAt DATETIME2 NULL,
    ProcessingDurationSeconds FLOAT NULL,
    RetryCount INT NOT NULL DEFAULT 0,

    -- Error Tracking
    ErrorMessage NVARCHAR(MAX) NULL,
    StackTrace NVARCHAR(MAX) NULL,

    -- Audit
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedAt DATETIME2 NULL,

    -- Primary Key Constraint
    CONSTRAINT PK_BatchJobItems PRIMARY KEY CLUSTERED (ItemId)

    -- Note: Foreign key constraint removed due to REFERENCES permission restrictions
    -- Application layer will ensure referential integrity instead
)
GO

PRINT 'BatchJobItems table created successfully (without FK constraint - limited permissions)'
GO

-- =============================================
-- INDEXES for Performance
-- =============================================

-- Query by batch
CREATE NONCLUSTERED INDEX IX_BatchJobItems_BatchId
    ON DaQa.BatchJobItems(BatchId)
    INCLUDE (ObjectName, Status, ConfidenceScore, RequiresHumanReview)
GO

-- Find items requiring review (removed WHERE clause for permissions)
CREATE NONCLUSTERED INDEX IX_BatchJobItems_RequiresReview
    ON DaQa.BatchJobItems(BatchId, RequiresHumanReview, Status)
    INCLUDE (ItemId, ObjectName, ConfidenceScore)
GO

-- Query by generated DocId (removed WHERE clause for permissions)
CREATE NONCLUSTERED INDEX IX_BatchJobItems_GeneratedDocId
    ON DaQa.BatchJobItems(GeneratedDocId)
    INCLUDE (ItemId, ObjectName, Status)
GO

-- Query by MasterIndex linkage (removed WHERE clause for permissions)
CREATE NONCLUSTERED INDEX IX_BatchJobItems_MasterIndexId
    ON DaQa.BatchJobItems(MasterIndexId)
    INCLUDE (ItemId, GeneratedDocId)
GO

-- Query by vector indexing status (removed WHERE clause for permissions)
CREATE NONCLUSTERED INDEX IX_BatchJobItems_VectorIndexed
    ON DaQa.BatchJobItems(IsVectorIndexed)
    INCLUDE (VectorId, GeneratedDocId)
GO

-- Query batches by status
CREATE NONCLUSTERED INDEX IX_BatchJobs_Status
    ON DaQa.BatchJobs(Status, StartedAt DESC)
    INCLUDE (BatchId, SourceType, TotalItems, ProcessedCount)
GO

-- Query batches by source type
CREATE NONCLUSTERED INDEX IX_BatchJobs_SourceType
    ON DaQa.BatchJobs(SourceType, StartedAt DESC)
    INCLUDE (BatchId, Status, TotalItems)
GO

-- Recent batches
CREATE NONCLUSTERED INDEX IX_BatchJobs_StartedAt
    ON DaQa.BatchJobs(StartedAt DESC)
    INCLUDE (BatchId, SourceType, Status, TotalItems)
GO

PRINT 'Indexes created successfully'
GO

-- =============================================
-- VIEWS for Common Queries
-- =============================================

-- View for batch job summary
CREATE VIEW DaQa.vw_BatchJobSummary
AS
SELECT
    bj.BatchId,
    bj.SourceType,
    bj.DatabaseName,
    bj.SchemaName,
    bj.SourcePath,
    bj.Status,
    bj.TotalItems,
    bj.ProcessedCount,
    bj.SuccessCount,
    bj.FailedCount,
    bj.RequiresReviewCount,
    bj.ProgressPercentage,
    bj.HighConfidenceCount,
    bj.MediumConfidenceCount,
    bj.LowConfidenceCount,
    bj.AverageConfidence,
    bj.VectorIndexedCount,
    bj.StartedAt,
    bj.CompletedAt,
    bj.DurationSeconds,
    CASE
        WHEN bj.Status = 'Processing' AND bj.ProcessedCount > 0 AND bj.TotalItems > 0 THEN
            (bj.DurationSeconds / bj.ProcessedCount) * (bj.TotalItems - bj.ProcessedCount)
        ELSE NULL
    END AS EstimatedTimeRemainingSeconds,
    bj.ErrorMessage,
    bj.CreatedBy,
    -- Summary statistics
    CASE
        WHEN bj.TotalItems > 0 THEN CAST(bj.SuccessCount AS FLOAT) / bj.TotalItems * 100
        ELSE 0
    END AS SuccessRate,
    CASE
        WHEN bj.TotalItems > 0 THEN CAST(bj.RequiresReviewCount AS FLOAT) / bj.TotalItems * 100
        ELSE 0
    END AS ReviewRate
FROM DaQa.BatchJobs bj
GO

PRINT 'vw_BatchJobSummary created successfully'
GO

-- View for items requiring review
CREATE VIEW DaQa.vw_ItemsRequiringReview
AS
SELECT
    bi.ItemId,
    bi.BatchId,
    bi.ObjectName,
    bi.ObjectType,
    bi.Status,
    bi.ConfidenceScore,
    CASE
        WHEN bi.ConfidenceScore >= 0.85 THEN 'High'
        WHEN bi.ConfidenceScore >= 0.70 THEN 'Medium'
        ELSE 'Low'
    END AS ConfidenceLevel,
    bi.ExtractedMetadataJson,
    bi.ValidationWarningsJson,
    bi.CreatedAt,
    -- Batch context
    bj.SourceType,
    bj.DatabaseName,
    bj.SchemaName,
    bj.StartedAt AS BatchStartedAt
FROM DaQa.BatchJobItems bi
INNER JOIN DaQa.BatchJobs bj ON bi.BatchId = bj.BatchId
WHERE bi.RequiresHumanReview = 1
  AND bi.Status = 'ValidationRequired'
GO

PRINT 'vw_ItemsRequiringReview created successfully'
GO

-- View for batch processing metrics
CREATE VIEW DaQa.vw_BatchProcessingMetrics
AS
SELECT
    SourceType,
    COUNT(*) AS TotalBatches,
    SUM(TotalItems) AS TotalItemsProcessed,
    SUM(SuccessCount) AS TotalSuccessful,
    SUM(FailedCount) AS TotalFailed,
    SUM(RequiresReviewCount) AS TotalRequiringReview,
    AVG(AverageConfidence) AS OverallAverageConfidence,
    AVG(CASE WHEN Status = 'Completed' THEN DurationSeconds END) AS AvgCompletionTimeSeconds,
    COUNT(CASE WHEN Status = 'Completed' THEN 1 END) AS CompletedBatches,
    COUNT(CASE WHEN Status = 'Failed' THEN 1 END) AS FailedBatches,
    COUNT(CASE WHEN Status = 'Processing' THEN 1 END) AS InProgressBatches
FROM DaQa.BatchJobs
GROUP BY SourceType
GO

PRINT 'vw_BatchProcessingMetrics created successfully'
GO

-- View for confidence distribution
CREATE VIEW DaQa.vw_ConfidenceDistribution
AS
SELECT
    bj.BatchId,
    bj.SourceType,
    bj.Status,
    bj.TotalItems,
    bj.HighConfidenceCount,
    bj.MediumConfidenceCount,
    bj.LowConfidenceCount,
    bj.AverageConfidence,
    CASE
        WHEN bj.TotalItems > 0 THEN CAST(bj.HighConfidenceCount AS FLOAT) / bj.TotalItems * 100
        ELSE 0
    END AS HighConfidencePercentage,
    CASE
        WHEN bj.TotalItems > 0 THEN CAST(bj.MediumConfidenceCount AS FLOAT) / bj.TotalItems * 100
        ELSE 0
    END AS MediumConfidencePercentage,
    CASE
        WHEN bj.TotalItems > 0 THEN CAST(bj.LowConfidenceCount AS FLOAT) / bj.TotalItems * 100
        ELSE 0
    END AS LowConfidencePercentage,
    bj.StartedAt,
    bj.CompletedAt
FROM DaQa.BatchJobs bj
GO

PRINT 'vw_ConfidenceDistribution created successfully'
GO

-- View for vector indexing status
CREATE VIEW DaQa.vw_VectorIndexingStatus
AS
SELECT
    bj.BatchId,
    bj.SourceType,
    bj.Status,
    bj.TotalItems,
    bj.VectorIndexedCount,
    bj.VectorIndexFailedCount,
    CASE
        WHEN bj.TotalItems > 0 THEN CAST(bj.VectorIndexedCount AS FLOAT) / bj.TotalItems * 100
        ELSE 0
    END AS VectorIndexedPercentage,
    -- Item-level details
    COUNT(bi.ItemId) AS TotalProcessedItems,
    SUM(CASE WHEN bi.IsVectorIndexed = 1 THEN 1 ELSE 0 END) AS ItemsVectorIndexed,
    SUM(CASE WHEN bi.VectorId IS NOT NULL THEN 1 ELSE 0 END) AS ItemsWithVectorId
FROM DaQa.BatchJobs bj
LEFT JOIN DaQa.BatchJobItems bi ON bj.BatchId = bi.BatchId
GROUP BY
    bj.BatchId,
    bj.SourceType,
    bj.Status,
    bj.TotalItems,
    bj.VectorIndexedCount,
    bj.VectorIndexFailedCount
GO

PRINT 'vw_VectorIndexingStatus created successfully'
GO

-- =============================================
-- STORED PROCEDURES
-- =============================================

-- Get batch status with detailed progress
CREATE PROCEDURE DaQa.usp_GetBatchStatus
    @BatchId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Batch summary
    SELECT * FROM DaQa.vw_BatchJobSummary
    WHERE BatchId = @BatchId;

    -- Item breakdown by status
    SELECT
        Status,
        COUNT(*) AS Count,
        AVG(ConfidenceScore) AS AvgConfidence
    FROM DaQa.BatchJobItems
    WHERE BatchId = @BatchId
    GROUP BY Status;

    -- Recent errors
    SELECT TOP 10
        ItemId,
        ObjectName,
        ErrorMessage,
        ProcessedAt
    FROM DaQa.BatchJobItems
    WHERE BatchId = @BatchId
      AND Status = 'Failed'
    ORDER BY ProcessedAt DESC;
END
GO

PRINT 'usp_GetBatchStatus created successfully'
GO

-- Get items requiring human review
CREATE PROCEDURE DaQa.usp_GetItemsRequiringReview
    @BatchId UNIQUEIDENTIFIER = NULL,
    @TopN INT = 100
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@TopN) *
    FROM DaQa.vw_ItemsRequiringReview
    WHERE (@BatchId IS NULL OR BatchId = @BatchId)
    ORDER BY ConfidenceScore ASC, CreatedAt ASC;
END
GO

PRINT 'usp_GetItemsRequiringReview created successfully'
GO

-- Approve items for processing
CREATE PROCEDURE DaQa.usp_ApproveItems
    @ItemIds NVARCHAR(MAX), -- Comma-separated GUIDs
    @ReviewedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE DaQa.BatchJobItems
    SET RequiresHumanReview = 0,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = GETUTCDATE(),
        Status = 'Approved',
        ModifiedAt = GETUTCDATE()
    WHERE ItemId IN (
        SELECT CAST(value AS UNIQUEIDENTIFIER)
        FROM STRING_SPLIT(@ItemIds, ',')
    );

    SELECT @@ROWCOUNT AS ItemsApproved;
END
GO

PRINT 'usp_ApproveItems created successfully'
GO

-- Reject items with feedback
CREATE PROCEDURE DaQa.usp_RejectItems
    @ItemIds NVARCHAR(MAX), -- Comma-separated GUIDs
    @Reason NVARCHAR(MAX),
    @ReviewedBy NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE DaQa.BatchJobItems
    SET RequiresHumanReview = 0,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = GETUTCDATE(),
        ReviewNotes = @Reason,
        Status = 'Rejected',
        ModifiedAt = GETUTCDATE()
    WHERE ItemId IN (
        SELECT CAST(value AS UNIQUEIDENTIFIER)
        FROM STRING_SPLIT(@ItemIds, ',')
    );

    SELECT @@ROWCOUNT AS ItemsRejected;
END
GO

PRINT 'usp_RejectItems created successfully'
GO

-- Cancel batch job
CREATE PROCEDURE DaQa.usp_CancelBatch
    @BatchId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE DaQa.BatchJobs
    SET Status = 'Cancelled',
        CompletedAt = GETUTCDATE(),
        ModifiedAt = GETUTCDATE()
    WHERE BatchId = @BatchId
      AND Status IN ('Pending', 'Processing');

    -- Also cancel pending items
    UPDATE DaQa.BatchJobItems
    SET Status = 'Cancelled',
        ModifiedAt = GETUTCDATE()
    WHERE BatchId = @BatchId
      AND Status IN ('Pending', 'MetadataExtraction', 'ValidationRequired');

    SELECT @@ROWCOUNT AS ItemsCancelled;
END
GO

PRINT 'usp_CancelBatch created successfully'
GO

-- =============================================
-- SUMMARY
-- =============================================

PRINT ''
PRINT '============================================================================'
PRINT 'Batch processing tables created successfully'
PRINT '============================================================================'
PRINT ''
PRINT 'Tables:'
PRINT '  - DaQa.BatchJobs'
PRINT '  - DaQa.BatchJobItems'
PRINT ''
PRINT 'Views:'
PRINT '  - DaQa.vw_BatchJobSummary'
PRINT '  - DaQa.vw_ItemsRequiringReview'
PRINT '  - DaQa.vw_BatchProcessingMetrics'
PRINT '  - DaQa.vw_ConfidenceDistribution'
PRINT '  - DaQa.vw_VectorIndexingStatus'
PRINT ''
PRINT 'Stored Procedures:'
PRINT '  - DaQa.usp_GetBatchStatus'
PRINT '  - DaQa.usp_GetItemsRequiringReview'
PRINT '  - DaQa.usp_ApproveItems'
PRINT '  - DaQa.usp_RejectItems'
PRINT '  - DaQa.usp_CancelBatch'
PRINT ''
PRINT 'Note: Hangfire will also create its tables in the DaQa schema'
PRINT '      Configure in HangfireConfiguration.cs with SchemaName = "DaQa"'
PRINT ''
PRINT '============================================================================'
GO
