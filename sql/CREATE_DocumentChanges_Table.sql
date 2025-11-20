-- ============================================================================
-- DocumentChanges Table for Excel-to-SQL Sync
-- ============================================================================
-- This table stores change document entries from the Excel tracking spreadsheet
-- Data is synced via the ExcelToSqlSyncService
-- Supports both local Excel files and SharePoint
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

        -- Deduplication
        UniqueKey NVARCHAR(500) NULL,   -- CABNumber|ObjectName|Version
        ContentHash NVARCHAR(64) NULL,   -- SHA256 hash of key fields

        -- Audit
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL
    )

    -- Create indexes
    CREATE UNIQUE INDEX IX_DocumentChanges_DocumentId ON daqa.DocumentChanges(DocumentId)
    CREATE INDEX IX_DocumentChanges_UniqueKey ON daqa.DocumentChanges(UniqueKey) WHERE UniqueKey IS NOT NULL
    CREATE INDEX IX_DocumentChanges_ContentHash ON daqa.DocumentChanges(ContentHash) WHERE ContentHash IS NOT NULL
    CREATE INDEX IX_DocumentChanges_CABNumber ON daqa.DocumentChanges(CABNumber) WHERE CABNumber IS NOT NULL
    CREATE INDEX IX_DocumentChanges_Status ON daqa.DocumentChanges(Status) WHERE Status IS NOT NULL
    CREATE INDEX IX_DocumentChanges_ApprovalStatus ON daqa.DocumentChanges(ApprovalStatus) WHERE ApprovalStatus IS NOT NULL
    CREATE INDEX IX_DocumentChanges_BusinessOwner ON daqa.DocumentChanges(BusinessOwner) WHERE BusinessOwner IS NOT NULL
    CREATE INDEX IX_DocumentChanges_DocumentType ON daqa.DocumentChanges(DocumentType) WHERE DocumentType IS NOT NULL
    CREATE INDEX IX_DocumentChanges_CAB_Object_Version ON daqa.DocumentChanges(CABNumber, ObjectName, Version)

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
        DocumentId,
        CABNumber,
        Title,
        DocumentType,
        ApprovalStatus,
        BusinessOwner,
        LastSyncedFromExcel,
        SyncStatus,
        UniqueKey
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
        ObjectName,
        COUNT(*) AS DuplicateCount,
        STRING_AGG(DocumentId, '', '') AS DocumentIds
    FROM daqa.DocumentChanges
    WHERE CABNumber IS NOT NULL AND ObjectName IS NOT NULL
    GROUP BY CABNumber, ObjectName
    HAVING COUNT(*) > 1
    ')
    PRINT 'Created vw_PotentialDuplicates view'
END
GO

PRINT 'DocumentChanges setup complete'
