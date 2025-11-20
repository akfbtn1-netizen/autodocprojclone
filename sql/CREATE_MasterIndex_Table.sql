-- ============================================================================
-- MasterIndex Table for Excel-to-SQL Sync
-- ============================================================================
-- This table mirrors the Excel MasterIndex spreadsheet
-- Data is synced via the ExcelToSqlSyncService
-- ============================================================================

-- Create schema if not exists
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'daqa')
BEGIN
    EXEC('CREATE SCHEMA daqa')
END
GO

-- Create MasterIndex table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MasterIndex' AND schema_id = SCHEMA_ID('daqa'))
BEGIN
    CREATE TABLE daqa.MasterIndex (
        -- Primary Key
        Id INT IDENTITY(1,1) PRIMARY KEY,

        -- Core Identifiers
        DocumentId NVARCHAR(50) NOT NULL,
        CABNumber NVARCHAR(50) NULL,
        ChangeRequestId NVARCHAR(50) NULL,

        -- Document Information
        Title NVARCHAR(500) NULL,
        Description NVARCHAR(MAX) NULL,
        DocumentType NVARCHAR(100) NULL,
        Category NVARCHAR(100) NULL,
        SubCategory NVARCHAR(100) NULL,

        -- Classification
        TierClassification NVARCHAR(50) NULL,
        DataClassification NVARCHAR(50) NULL,
        SecurityClearance NVARCHAR(50) NULL,

        -- Ownership & Responsibility
        BusinessOwner NVARCHAR(200) NULL,
        TechnicalOwner NVARCHAR(200) NULL,
        Author NVARCHAR(200) NULL,
        Department NVARCHAR(100) NULL,
        Team NVARCHAR(100) NULL,

        -- Approval Workflow
        ApprovalStatus NVARCHAR(50) NULL,
        CurrentApprover NVARCHAR(200) NULL,
        SubmittedDate DATETIME2 NULL,
        ApprovedDate DATETIME2 NULL,
        ApprovalComments NVARCHAR(MAX) NULL,

        -- Dates & Versioning
        CreatedDate DATETIME2 NULL,
        ModifiedDate DATETIME2 NULL,
        EffectiveDate DATETIME2 NULL,
        ExpirationDate DATETIME2 NULL,
        Version NVARCHAR(50) NULL,
        RevisionNumber INT NULL,

        -- Database Objects
        DatabaseName NVARCHAR(128) NULL,
        SchemaName NVARCHAR(128) NULL,
        ObjectName NVARCHAR(128) NULL,
        ObjectType NVARCHAR(50) NULL,
        SourceTables NVARCHAR(MAX) NULL,
        TargetTables NVARCHAR(MAX) NULL,

        -- File References
        FilePath NVARCHAR(500) NULL,
        GeneratedDocPath NVARCHAR(500) NULL,
        TemplateUsed NVARCHAR(200) NULL,

        -- Status & Tracking
        Status NVARCHAR(50) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        IsDeleted BIT NOT NULL DEFAULT 0,
        Tags NVARCHAR(500) NULL,
        Notes NVARCHAR(MAX) NULL,

        -- Sync Metadata
        ExcelRowNumber INT NULL,
        LastSyncedFromExcel DATETIME2 NULL,
        SyncStatus NVARCHAR(50) NULL,
        SyncErrors NVARCHAR(MAX) NULL,

        -- Audit
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL
    )

    -- Create indexes
    CREATE UNIQUE INDEX IX_MasterIndex_DocumentId ON daqa.MasterIndex(DocumentId)
    CREATE INDEX IX_MasterIndex_CABNumber ON daqa.MasterIndex(CABNumber) WHERE CABNumber IS NOT NULL
    CREATE INDEX IX_MasterIndex_Status ON daqa.MasterIndex(Status) WHERE Status IS NOT NULL
    CREATE INDEX IX_MasterIndex_ApprovalStatus ON daqa.MasterIndex(ApprovalStatus) WHERE ApprovalStatus IS NOT NULL
    CREATE INDEX IX_MasterIndex_BusinessOwner ON daqa.MasterIndex(BusinessOwner) WHERE BusinessOwner IS NOT NULL
    CREATE INDEX IX_MasterIndex_DocumentType ON daqa.MasterIndex(DocumentType) WHERE DocumentType IS NOT NULL

    PRINT 'Created daqa.MasterIndex table with indexes'
END
ELSE
BEGIN
    PRINT 'daqa.MasterIndex table already exists'
END
GO

-- Create trigger for UpdatedAt
IF NOT EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_MasterIndex_UpdatedAt')
BEGIN
    EXEC('
    CREATE TRIGGER daqa.TR_MasterIndex_UpdatedAt
    ON daqa.MasterIndex
    AFTER UPDATE
    AS
    BEGIN
        SET NOCOUNT ON;
        UPDATE daqa.MasterIndex
        SET UpdatedAt = GETUTCDATE()
        FROM daqa.MasterIndex mi
        INNER JOIN inserted i ON mi.Id = i.Id
    END
    ')
    PRINT 'Created UpdatedAt trigger'
END
GO

-- View for recent changes
IF NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_RecentMasterIndexChanges' AND schema_id = SCHEMA_ID('daqa'))
BEGIN
    EXEC('
    CREATE VIEW daqa.vw_RecentMasterIndexChanges AS
    SELECT
        Id,
        DocumentId,
        CABNumber,
        Title,
        DocumentType,
        ApprovalStatus,
        BusinessOwner,
        LastSyncedFromExcel,
        SyncStatus
    FROM daqa.MasterIndex
    WHERE LastSyncedFromExcel >= DATEADD(DAY, -7, GETUTCDATE())
    ')
    PRINT 'Created vw_RecentMasterIndexChanges view'
END
GO

PRINT 'MasterIndex setup complete'
