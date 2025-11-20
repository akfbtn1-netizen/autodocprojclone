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
        -- Primary Key
        Id INT IDENTITY(1,1) PRIMARY KEY,

        -- Excel Column Mapping (Direct 1:1 with spreadsheet)
        Date DATETIME2 NULL,                        -- Date
        JiraNumber NVARCHAR(50) NULL,               -- JIRA #
        CABNumber NVARCHAR(50) NULL,                -- CAB #
        SprintNumber NVARCHAR(50) NULL,             -- Sprint #
        Status NVARCHAR(50) NULL,                   -- Status
        Priority NVARCHAR(50) NULL,                 -- Priority
        Severity NVARCHAR(50) NULL,                 -- Severity
        TableName NVARCHAR(128) NULL,               -- Table
        ColumnName NVARCHAR(128) NULL,              -- Column
        ChangeType NVARCHAR(100) NULL,              -- Change Type
        Description NVARCHAR(MAX) NULL,             -- Description
        ReportedBy NVARCHAR(200) NULL,              -- Reported By
        AssignedTo NVARCHAR(200) NULL,              -- Assigned to
        Documentation NVARCHAR(500) NULL,           -- Documentation
        DocumentationLink NVARCHAR(500) NULL,       -- Documentation Link
        DocId NVARCHAR(50) NULL,                    -- DocId (populated after approval)

        -- Sync Metadata
        ExcelRowNumber INT NULL,
        LastSyncedFromExcel DATETIME2 NULL,
        SyncStatus NVARCHAR(50) NULL,
        SyncErrors NVARCHAR(MAX) NULL,

        -- Deduplication
        UniqueKey NVARCHAR(500) NULL,               -- CABNumber|TableName|ColumnName
        ContentHash NVARCHAR(64) NULL,              -- SHA256 hash for change detection

        -- Audit
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL
    )

    -- Create indexes for common queries
    CREATE INDEX IX_DocumentChanges_UniqueKey ON daqa.DocumentChanges(UniqueKey) WHERE UniqueKey IS NOT NULL
    CREATE INDEX IX_DocumentChanges_ContentHash ON daqa.DocumentChanges(ContentHash) WHERE ContentHash IS NOT NULL
    CREATE INDEX IX_DocumentChanges_CABNumber ON daqa.DocumentChanges(CABNumber) WHERE CABNumber IS NOT NULL
    CREATE INDEX IX_DocumentChanges_JiraNumber ON daqa.DocumentChanges(JiraNumber) WHERE JiraNumber IS NOT NULL
    CREATE INDEX IX_DocumentChanges_Status ON daqa.DocumentChanges(Status) WHERE Status IS NOT NULL
    CREATE INDEX IX_DocumentChanges_TableName ON daqa.DocumentChanges(TableName) WHERE TableName IS NOT NULL
    CREATE INDEX IX_DocumentChanges_Date ON daqa.DocumentChanges(Date) WHERE Date IS NOT NULL
    CREATE INDEX IX_DocumentChanges_DocId ON daqa.DocumentChanges(DocId) WHERE DocId IS NOT NULL
    CREATE INDEX IX_DocumentChanges_CAB_Table_Column ON daqa.DocumentChanges(CABNumber, TableName, ColumnName)

    PRINT 'Created daqa.DocumentChanges table with indexes'
END
ELSE
BEGIN
    PRINT 'daqa.DocumentChanges table already exists'
END
GO

-- Create trigger for UpdatedAt
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_DocumentChanges_UpdatedAt')
BEGIN
    EXEC('
    CREATE TRIGGER daqa.TR_DocumentChanges_UpdatedAt
    ON daqa.DocumentChanges
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE daqa.DocumentChanges
        SET UpdatedAt = GETUTCDATE()
        FROM daqa.DocumentChanges dc
        INNER JOIN inserted i ON dc.Id = i.Id
    END
    ')
    PRINT 'Created UpdatedAt trigger'
END
GO

-- View for recent changes
IF NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_RecentDocumentChanges' AND schema_id = SCHEMA_ID('daqa'))
BEGIN
    EXEC('
    CREATE VIEW daqa.vw_RecentDocumentChanges AS
    SELECT
        Id,
        Date,
        JiraNumber,
        CABNumber,
        SprintNumber,
        Status,
        Priority,
        Severity,
        TableName,
        ColumnName,
        ChangeType,
        Description,
        ReportedBy,
        AssignedTo,
        LastSyncedFromExcel,
        SyncStatus
    FROM daqa.DocumentChanges
    WHERE LastSyncedFromExcel >= DATEADD(DAY, -7, GETUTCDATE())
    ')
    PRINT 'Created vw_RecentDocumentChanges view'
END
GO

-- View for duplicate detection
IF NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_PotentialDuplicates' AND schema_id = SCHEMA_ID('daqa'))
BEGIN
    EXEC('
    CREATE VIEW daqa.vw_PotentialDuplicates AS
    SELECT
        CABNumber,
        TableName,
        ColumnName,
        COUNT(*) AS DuplicateCount,
        STRING_AGG(CAST(Id AS NVARCHAR(10)), '','') AS DocumentIds
    FROM daqa.DocumentChanges
    WHERE CABNumber IS NOT NULL AND TableName IS NOT NULL
    GROUP BY CABNumber, TableName, ColumnName
    HAVING COUNT(*) > 1
    ')
    PRINT 'Created vw_PotentialDuplicates view'
END
GO

-- View for sync status summary
IF NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_SyncStatusSummary' AND schema_id = SCHEMA_ID('daqa'))
BEGIN
    EXEC('
    CREATE VIEW daqa.vw_SyncStatusSummary AS
    SELECT
        SyncStatus,
        COUNT(*) AS EntryCount,
        MIN(LastSyncedFromExcel) AS OldestSync,
        MAX(LastSyncedFromExcel) AS LatestSync
    FROM daqa.DocumentChanges
    GROUP BY SyncStatus
    ')
    PRINT 'Created vw_SyncStatusSummary view'
END
GO

PRINT 'DocumentChanges setup complete'
