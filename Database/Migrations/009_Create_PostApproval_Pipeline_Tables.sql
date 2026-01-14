-- ============================================================================
-- POST-APPROVAL PIPELINE - DATABASE SCHEMA
-- Migration: 009_Create_PostApproval_Pipeline_Tables.sql
-- Date: 2026-01-13
-- Description: Creates tables for deferred embedding, Shadow Metadata, and lineage
-- ============================================================================
USE gwpc;
GO

PRINT 'Creating Post-Approval Pipeline schema additions...';
GO

-- Add columns to DocumentApprovals for deferred processing
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentApprovals') AND name = 'ExtractedMetadata')
BEGIN
    ALTER TABLE DaQa.DocumentApprovals ADD ExtractedMetadata NVARCHAR(MAX) NULL;
    PRINT 'Added ExtractedMetadata column to DocumentApprovals';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentApprovals') AND name = 'FinalizedMetadata')
BEGIN
    ALTER TABLE DaQa.DocumentApprovals ADD FinalizedMetadata NVARCHAR(MAX) NULL;
    PRINT 'Added FinalizedMetadata column to DocumentApprovals';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentApprovals') AND name = 'EmbeddingGeneratedAt')
BEGIN
    ALTER TABLE DaQa.DocumentApprovals ADD EmbeddingGeneratedAt DATETIME2 NULL;
    PRINT 'Added EmbeddingGeneratedAt column to DocumentApprovals';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentApprovals') AND name = 'ClassificationEnrichedAt')
BEGIN
    ALTER TABLE DaQa.DocumentApprovals ADD ClassificationEnrichedAt DATETIME2 NULL;
    PRINT 'Added ClassificationEnrichedAt column to DocumentApprovals';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentApprovals') AND name = 'MasterIndexPopulatedAt')
BEGIN
    ALTER TABLE DaQa.DocumentApprovals ADD MasterIndexPopulatedAt DATETIME2 NULL;
    PRINT 'Added MasterIndexPopulatedAt column to DocumentApprovals';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentApprovals') AND name = 'DocxStampedAt')
BEGIN
    ALTER TABLE DaQa.DocumentApprovals ADD DocxStampedAt DATETIME2 NULL;
    PRINT 'Added DocxStampedAt column to DocumentApprovals';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentApprovals') AND name = 'LineageExtractedAt')
BEGIN
    ALTER TABLE DaQa.DocumentApprovals ADD LineageExtractedAt DATETIME2 NULL;
    PRINT 'Added LineageExtractedAt column to DocumentApprovals';
END
GO

-- ============================================================================
-- Document Processing Queue for async post-approval tasks
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentProcessingQueue' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[DocumentProcessingQueue] (
        [QueueId] INT IDENTITY(1,1) PRIMARY KEY,
        [ApprovalId] INT NOT NULL,
        [DocumentId] NVARCHAR(50) NOT NULL,
        [ProcessingType] NVARCHAR(50) NOT NULL, -- FINALIZE_METADATA, STAMP_DOCX, POPULATE_MASTERINDEX, EXTRACT_LINEAGE
        [Priority] INT NOT NULL DEFAULT 5,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'PENDING', -- PENDING, PROCESSING, COMPLETED, FAILED, CANCELLED
        [QueuedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [StartedAt] DATETIME2 NULL,
        [CompletedAt] DATETIME2 NULL,
        [RetryCount] INT NOT NULL DEFAULT 0,
        [MaxRetries] INT NOT NULL DEFAULT 3,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [ProcessedBy] NVARCHAR(100) NULL,
        [CorrelationId] UNIQUEIDENTIFIER NULL,
        INDEX [IX_ProcessingQueue_Status] NONCLUSTERED ([Status], [Priority] DESC, [QueuedAt]),
        INDEX [IX_ProcessingQueue_ApprovalId] NONCLUSTERED ([ApprovalId]),
        INDEX [IX_ProcessingQueue_DocumentId] NONCLUSTERED ([DocumentId])
    );
    PRINT 'Created DocumentProcessingQueue table';
END
GO

-- ============================================================================
-- Document Metadata Snapshots (for audit trail)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentMetadataSnapshots' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[DocumentMetadataSnapshots] (
        [SnapshotId] INT IDENTITY(1,1) PRIMARY KEY,
        [DocumentId] NVARCHAR(50) NOT NULL,
        [SnapshotType] NVARCHAR(20) NOT NULL, -- DRAFT, APPROVED, MODIFIED, REGENERATED
        [MetadataJson] NVARCHAR(MAX) NOT NULL,
        [SemanticEmbedding] VARBINARY(MAX) NULL,
        [ContentHash] NVARCHAR(64) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        INDEX [IX_MetadataSnapshots_DocId] NONCLUSTERED ([DocumentId], [SnapshotType]),
        INDEX [IX_MetadataSnapshots_CreatedAt] NONCLUSTERED ([CreatedAt] DESC)
    );
    PRINT 'Created DocumentMetadataSnapshots table';
END
GO

-- ============================================================================
-- Shadow Metadata tracking for documents
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentShadowMetadata' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[DocumentShadowMetadata] (
        [ShadowId] INT IDENTITY(1,1) PRIMARY KEY,
        [DocumentId] NVARCHAR(50) NOT NULL UNIQUE,
        [FilePath] NVARCHAR(500) NOT NULL,
        [SyncStatus] NVARCHAR(20) NOT NULL DEFAULT 'DRAFT', -- DRAFT, CURRENT, STALE, CONFLICT, ORPHANED, PENDING
        [ContentHash] NVARCHAR(64) NOT NULL,
        [SchemaHash] NVARCHAR(64) NULL,
        [MasterIndexId] INT NULL,
        [LastModified] DATETIME2 NOT NULL,
        [LastSynced] DATETIME2 NULL,
        [StaleReason] NVARCHAR(200) NULL,
        [TokensUsed] INT NULL,
        [GenerationCostUSD] DECIMAL(10,4) NULL,
        [AIModel] NVARCHAR(50) NULL,
        [GeneratedAt] DATETIME2 NULL,
        [ApprovedAt] DATETIME2 NULL,
        [ApprovedBy] NVARCHAR(100) NULL,
        INDEX [IX_ShadowMetadata_Status] NONCLUSTERED ([SyncStatus]),
        INDEX [IX_ShadowMetadata_MasterIndex] NONCLUSTERED ([MasterIndexId]),
        INDEX [IX_ShadowMetadata_FilePath] NONCLUSTERED ([FilePath])
    );
    PRINT 'Created DocumentShadowMetadata table';
END
GO

-- ============================================================================
-- Column-level Lineage Tracking
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColumnLineage' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[ColumnLineage] (
        [LineageId] INT IDENTITY(1,1) PRIMARY KEY,
        [SourceSchema] NVARCHAR(128) NOT NULL,
        [SourceName] NVARCHAR(128) NOT NULL,
        [SourceType] NVARCHAR(50) NOT NULL, -- PROCEDURE, VIEW, FUNCTION, TRIGGER
        [TargetSchema] NVARCHAR(128) NOT NULL,
        [TargetTable] NVARCHAR(128) NOT NULL,
        [TargetColumn] NVARCHAR(128) NOT NULL,
        [OperationType] NVARCHAR(20) NOT NULL, -- READ, INSERT, UPDATE, DELETE, MERGE_INSERT, MERGE_UPDATE, MERGE_DELETE
        [TransformationExpression] NVARCHAR(MAX) NULL,
        [IsPiiColumn] BIT NOT NULL DEFAULT 0,
        [PiiType] NVARCHAR(50) NULL, -- SSN, EMAIL, PHONE, DOB, ADDRESS, etc.
        [RiskWeight] INT NOT NULL DEFAULT 1,
        [StartLine] INT NULL,
        [EndLine] INT NULL,
        [LastAnalyzed] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [AnalyzedBy] NVARCHAR(100) NULL,
        INDEX [IX_ColumnLineage_Source] NONCLUSTERED ([SourceSchema], [SourceName]),
        INDEX [IX_ColumnLineage_Target] NONCLUSTERED ([TargetSchema], [TargetTable], [TargetColumn]),
        INDEX [IX_ColumnLineage_PII] NONCLUSTERED ([IsPiiColumn]) WHERE [IsPiiColumn] = 1
    );
    PRINT 'Created ColumnLineage table';
END
GO

-- ============================================================================
-- Post-Approval Pipeline Execution Log
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PostApprovalPipelineLog' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[PostApprovalPipelineLog] (
        [LogId] INT IDENTITY(1,1) PRIMARY KEY,
        [ApprovalId] INT NOT NULL,
        [DocumentId] NVARCHAR(50) NOT NULL,
        [PipelineStatus] NVARCHAR(20) NOT NULL, -- STARTED, COMPLETED, FAILED, PARTIAL
        [StartedAt] DATETIME2 NOT NULL,
        [CompletedAt] DATETIME2 NULL,
        [TotalDurationMs] BIGINT NULL,
        [StepsJson] NVARCHAR(MAX) NULL, -- JSON array of step results
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [TokensUsed] INT NULL,
        [GenerationCostUSD] DECIMAL(10,4) NULL,
        [MasterIndexId] INT NULL,
        [ExecutedBy] NVARCHAR(100) NULL,
        INDEX [IX_PipelineLog_ApprovalId] NONCLUSTERED ([ApprovalId]),
        INDEX [IX_PipelineLog_Status] NONCLUSTERED ([PipelineStatus], [StartedAt] DESC)
    );
    PRINT 'Created PostApprovalPipelineLog table';
END
GO

-- ============================================================================
-- Stored Procedure: Get pending queue items
-- ============================================================================
IF OBJECT_ID('DaQa.usp_GetPendingQueueItems', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_GetPendingQueueItems;
GO

CREATE PROCEDURE DaQa.usp_GetPendingQueueItems
    @ProcessingType NVARCHAR(50) = NULL,
    @MaxItems INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@MaxItems)
        q.QueueId,
        q.ApprovalId,
        q.DocumentId,
        q.ProcessingType,
        q.Priority,
        q.Status,
        q.QueuedAt,
        q.RetryCount,
        q.MaxRetries,
        a.DocumentPath,
        a.ExtractedMetadata
    FROM DaQa.DocumentProcessingQueue q
    INNER JOIN DaQa.DocumentApprovals a ON q.ApprovalId = a.ApprovalID
    WHERE q.Status = 'PENDING'
      AND (@ProcessingType IS NULL OR q.ProcessingType = @ProcessingType)
      AND q.RetryCount < q.MaxRetries
    ORDER BY q.Priority DESC, q.QueuedAt ASC;
END
GO

PRINT 'Created usp_GetPendingQueueItems procedure';
GO

-- ============================================================================
-- Stored Procedure: Update queue item status
-- ============================================================================
IF OBJECT_ID('DaQa.usp_UpdateQueueItemStatus', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_UpdateQueueItemStatus;
GO

CREATE PROCEDURE DaQa.usp_UpdateQueueItemStatus
    @QueueId INT,
    @Status NVARCHAR(20),
    @ProcessedBy NVARCHAR(100) = NULL,
    @ErrorMessage NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE DaQa.DocumentProcessingQueue
    SET Status = @Status,
        ProcessedBy = @ProcessedBy,
        ErrorMessage = @ErrorMessage,
        StartedAt = CASE WHEN @Status = 'PROCESSING' THEN GETUTCDATE() ELSE StartedAt END,
        CompletedAt = CASE WHEN @Status IN ('COMPLETED', 'FAILED', 'CANCELLED') THEN GETUTCDATE() ELSE CompletedAt END,
        RetryCount = CASE WHEN @Status = 'FAILED' THEN RetryCount + 1 ELSE RetryCount END
    WHERE QueueId = @QueueId;
END
GO

PRINT 'Created usp_UpdateQueueItemStatus procedure';
GO

PRINT 'Post-Approval Pipeline schema created successfully';
GO
