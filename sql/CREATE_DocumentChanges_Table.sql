-- ============================================================================
-- DocumentChanges Table for Excel-to-SQL Sync
-- ============================================================================
-- This table stores change document entries from the BI Analytics Change Spreadsheet
-- Excel Columns: Date, JIRA #, CAB #, Sprint #, Status, Priority, Severity,
--                Table, Column, Change Type, Description, Reported By,
--                Assigned to, Documentation, Documentation Link, DocId
-- Data is synced via the ExcelToSqlSyncService
-- ============================================================================

-- Create schema if not exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'daqa')
BEGIN
    EXEC('CREATE SCHEMA daqa')
END
GO

-- Create DocumentChanges table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentChanges' AND schema_id = SCHEMA_ID('daqa'))
BEGIN
    CREATE TABLE daqa.DocumentChanges (
        Id INT IDENTITY(1,1) PRIMARY KEY,

        -- Excel Columns - Direct Mapping
        Date DATETIME NULL,                         -- Date
        JiraNumber NVARCHAR(50) NULL,               -- JIRA #
        CABNumber NVARCHAR(50) NULL,                -- CAB #
        SprintNumber NVARCHAR(50) NULL,             -- Sprint #
        Status NVARCHAR(100) NULL,                  -- Status
        Priority NVARCHAR(50) NULL,                 -- Priority
        Severity NVARCHAR(50) NULL,                 -- Severity
        TableName NVARCHAR(255) NULL,               -- Table
        ColumnName NVARCHAR(255) NULL,              -- Column
        ChangeType NVARCHAR(100) NULL,              -- Change Type
        Description NVARCHAR(MAX) NULL,             -- Description
        ReportedBy NVARCHAR(255) NULL,              -- Reported By
        AssignedTo NVARCHAR(255) NULL,              -- Assigned to
        Documentation NVARCHAR(500) NULL,           -- Documentation
        DocumentationLink NVARCHAR(500) NULL,       -- Documentation Link
        DocId NVARCHAR(50) NULL,                    -- DocId (populated after approval)

        -- Sync Metadata
        ExcelRowNumber INT NULL,
        LastSyncedFromExcel DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        SyncStatus NVARCHAR(50) NULL,
        SyncErrors NVARCHAR(MAX) NULL,

        -- Deduplication
        UniqueKey NVARCHAR(500) NULL,               -- CAB|Table|Column
        ContentHash NVARCHAR(64) NULL,              -- SHA256 hash for change detection

        -- Timestamps
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL
    )

    PRINT 'Created table daqa.DocumentChanges'
END
GO

-- Create indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_UniqueKey' AND object_id = OBJECT_ID('daqa.DocumentChanges'))
BEGIN
    CREATE UNIQUE INDEX IX_DocumentChanges_UniqueKey ON daqa.DocumentChanges(UniqueKey) WHERE UniqueKey IS NOT NULL
    PRINT 'Created index IX_DocumentChanges_UniqueKey'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_CABNumber' AND object_id = OBJECT_ID('daqa.DocumentChanges'))
BEGIN
    CREATE INDEX IX_DocumentChanges_CABNumber ON daqa.DocumentChanges(CABNumber) WHERE CABNumber IS NOT NULL
    PRINT 'Created index IX_DocumentChanges_CABNumber'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_JiraNumber' AND object_id = OBJECT_ID('daqa.DocumentChanges'))
BEGIN
    CREATE INDEX IX_DocumentChanges_JiraNumber ON daqa.DocumentChanges(JiraNumber) WHERE JiraNumber IS NOT NULL
    PRINT 'Created index IX_DocumentChanges_JiraNumber'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_Date' AND object_id = OBJECT_ID('daqa.DocumentChanges'))
BEGIN
    CREATE INDEX IX_DocumentChanges_Date ON daqa.DocumentChanges(Date) WHERE Date IS NOT NULL
    PRINT 'Created index IX_DocumentChanges_Date'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_DocId' AND object_id = OBJECT_ID('daqa.DocumentChanges'))
BEGIN
    CREATE INDEX IX_DocumentChanges_DocId ON daqa.DocumentChanges(DocId) WHERE DocId IS NOT NULL
    PRINT 'Created index IX_DocumentChanges_DocId'
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_Status' AND object_id = OBJECT_ID('daqa.DocumentChanges'))
BEGIN
    CREATE INDEX IX_DocumentChanges_Status ON daqa.DocumentChanges(Status)
    PRINT 'Created index IX_DocumentChanges_Status'
END
GO

-- Create useful views
IF OBJECT_ID('daqa.vw_RecentDocumentChanges', 'V') IS NOT NULL
    DROP VIEW daqa.vw_RecentDocumentChanges
GO

CREATE VIEW daqa.vw_RecentDocumentChanges AS
SELECT TOP 100
    Id, Date, JiraNumber, CABNumber, SprintNumber, Status, Priority, Severity,
    TableName, ColumnName, ChangeType, Description, ReportedBy, AssignedTo,
    Documentation, DocumentationLink, DocId, LastSyncedFromExcel
FROM daqa.DocumentChanges
ORDER BY Date DESC, LastSyncedFromExcel DESC
GO

-- View for changes pending documentation
IF OBJECT_ID('daqa.vw_PendingDocumentation', 'V') IS NOT NULL
    DROP VIEW daqa.vw_PendingDocumentation
GO

CREATE VIEW daqa.vw_PendingDocumentation AS
SELECT
    Id, Date, JiraNumber, CABNumber, TableName, ColumnName, ChangeType,
    Description, ReportedBy, AssignedTo, Status
FROM daqa.DocumentChanges
WHERE DocId IS NULL
  AND Status NOT IN ('Cancelled', 'Duplicate', 'Rejected')
GO

PRINT 'DocumentChanges table setup complete!'
GO
