-- =============================================
-- MasterIndex Table - Complete Document Metadata Repository
-- Stores comprehensive metadata for all documentation with 115+ columns
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
IF OBJECT_ID('DaQa.MasterIndex', 'U') IS NOT NULL
    DROP TABLE DaQa.MasterIndex
GO

-- Create MasterIndex table with all 115 columns
CREATE TABLE DaQa.MasterIndex
(
    -- Primary Key
    IndexID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MasterIndex PRIMARY KEY CLUSTERED,

    -- Core Identity
    SourceSystem NVARCHAR(50) NULL,
    SourceDocumentID NVARCHAR(200) NULL, -- DocId: EN-0001-irf_policy-PolicyNumber-BAS-123
    SourceFilePath NVARCHAR(500) NULL,
    DocumentTitle NVARCHAR(500) NULL,
    DocumentType NVARCHAR(100) NULL,
    Description NVARCHAR(MAX) NULL,

    -- Business Context
    BusinessDomain NVARCHAR(200) NULL,
    BusinessProcess NVARCHAR(200) NULL,
    BusinessOwner NVARCHAR(200) NULL,
    TechnicalOwner NVARCHAR(200) NULL,

    -- System and Database Lineage
    SystemName NVARCHAR(100) NULL,
    DatabaseName NVARCHAR(100) NULL,
    SchemaName NVARCHAR(100) NULL,
    TableName NVARCHAR(200) NULL,
    ColumnName NVARCHAR(200) NULL,

    -- Data Type Information
    DataType NVARCHAR(100) NULL,
    DataClassification NVARCHAR(50) NULL,
    Sensitivity NVARCHAR(50) NULL,
    ComplianceFlags NVARCHAR(MAX) NULL, -- JSON array

    -- Quality Metrics
    QualityScore INT NULL, -- 0-100
    CompletenessScore INT NULL, -- 0-100
    LastValidated DATETIME2 NULL,
    ValidationStatus NVARCHAR(50) NULL,

    -- Document Metadata
    GeneratedDocPath NVARCHAR(500) NULL,
    GeneratedDocURL NVARCHAR(1000) NULL, -- SharePoint URL
    FileSize BIGINT NULL,
    FileHash NVARCHAR(100) NULL, -- SHA256

    -- Versioning
    VersionNumber NVARCHAR(20) NULL,
    IsLatestVersion BIT NULL,
    PreviousVersionID INT NULL,

    -- Status and Workflow
    Status NVARCHAR(50) NULL,
    WorkflowStatus NVARCHAR(50) NULL,
    ApprovalStatus NVARCHAR(50) NULL,
    ApprovedBy NVARCHAR(200) NULL,
    ApprovedDate DATETIME2 NULL,

    -- Search and Categorization
    Keywords NVARCHAR(MAX) NULL, -- JSON array
    Tags NVARCHAR(MAX) NULL, -- JSON array
    RelatedDocuments NVARCHAR(MAX) NULL, -- JSON array

    -- Data Lineage
    UpstreamSystems NVARCHAR(MAX) NULL, -- JSON array
    DownstreamSystems NVARCHAR(MAX) NULL, -- JSON array

    -- Usage Tracking
    AccessCount INT NULL DEFAULT 0,
    LastAccessedDate DATETIME2 NULL,
    LastAccessedBy NVARCHAR(200) NULL,

    -- Audit Trail
    CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy NVARCHAR(200) NULL,
    ModifiedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedBy NVARCHAR(200) NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedDate DATETIME2 NULL,
    DeletedBy NVARCHAR(200) NULL,

    -- Custom/Excel Fields
    CustomField1 NVARCHAR(500) NULL, -- Priority
    CustomField2 NVARCHAR(500) NULL, -- Severity
    CustomField3 NVARCHAR(500) NULL, -- Sprint

    -- Column-Specific Metadata
    MaxLength INT NULL,
    IsNullable BIT NULL,
    DefaultValue NVARCHAR(500) NULL,

    -- Business Rules and Validation
    BusinessRules NVARCHAR(MAX) NULL,
    ValidationRules NVARCHAR(MAX) NULL,
    Constraints NVARCHAR(MAX) NULL,
    AllowedValues NVARCHAR(MAX) NULL, -- JSON array

    -- Relationships
    RelatedTables NVARCHAR(MAX) NULL, -- JSON array
    RelatedColumns NVARCHAR(MAX) NULL, -- JSON array
    ForeignKeys NVARCHAR(MAX) NULL, -- JSON array
    Dependencies NVARCHAR(MAX) NULL, -- JSON array

    -- Data Flow
    UpstreamSources NVARCHAR(MAX) NULL, -- JSON array
    DownstreamTargets NVARCHAR(MAX) NULL, -- JSON array

    -- Usage Information
    UsagePurpose NVARCHAR(MAX) NULL,
    CommonQueries NVARCHAR(MAX) NULL,
    SampleData NVARCHAR(MAX) NULL,

    -- Data Quality
    DataQualityScore INT NULL,
    AccuracyNotes NVARCHAR(MAX) NULL,
    KnownIssues NVARCHAR(MAX) NULL,

    -- Compliance and Security
    SensitivityLevel NVARCHAR(50) NULL,
    PIIIndicator BIT NULL,
    ContainsPII BIT NULL,
    PIITypes NVARCHAR(MAX) NULL, -- JSON array
    RetentionPolicy NVARCHAR(200) NULL,
    ComplianceTags NVARCHAR(MAX) NULL, -- JSON array

    -- Ownership
    DataOwner NVARCHAR(200) NULL,
    DataSteward NVARCHAR(200) NULL,
    BusinessContactEmail NVARCHAR(200) NULL,
    TechnicalContactEmail NVARCHAR(200) NULL,

    -- Change Management
    ChangeFrequency NVARCHAR(50) NULL,
    LastSchemaChange DATETIME2 NULL,
    ChangeImpactAssessment NVARCHAR(MAX) NULL,

    -- Performance Metrics
    EstimatedRowCount BIGINT NULL,
    DataSizeMB DECIMAL(18,2) NULL,
    IndexCount INT NULL,
    QueryFrequency NVARCHAR(50) NULL,
    PerformanceNotes NVARCHAR(MAX) NULL,

    -- AI-Enhanced Metadata
    AIGeneratedTags NVARCHAR(MAX) NULL, -- JSON array
    SemanticCategory NVARCHAR(200) NULL,
    RecommendedIndexes NVARCHAR(MAX) NULL,
    OptimizationSuggestions NVARCHAR(MAX) NULL,
    MetadataCompleteness INT NULL, -- 0-100

    -- Access and Requirements
    UsageExamples NVARCHAR(MAX) NULL,
    AccessRequirements NVARCHAR(MAX) NULL,
    UpdateFrequency NVARCHAR(50) NULL,
    DataRetentionPolicy NVARCHAR(500) NULL,
    RelatedSystems NVARCHAR(MAX) NULL, -- JSON array

    -- Quality Rules
    DataQualityRules NVARCHAR(MAX) NULL,
    SampleValues NVARCHAR(MAX) NULL,

    -- Join Information
    CommonJoins NVARCHAR(MAX) NULL,
    PerformanceConsiderations NVARCHAR(MAX) NULL,

    -- Table Relationships
    ParentTables NVARCHAR(MAX) NULL, -- JSON array
    ChildTables NVARCHAR(MAX) NULL, -- JSON array

    -- Database Objects
    Indexes NVARCHAR(MAX) NULL, -- JSON array
    Triggers NVARCHAR(MAX) NULL, -- JSON array
    StoredProcedures NVARCHAR(MAX) NULL, -- JSON array

    -- Status Flags
    DeprecationStatus NVARCHAR(50) NULL,
    MigrationNotes NVARCHAR(MAX) NULL,

    -- Business Glossary
    BusinessGlossaryTerms NVARCHAR(MAX) NULL, -- JSON array
    TechnicalComplexity NVARCHAR(50) NULL,
    CriticalityLevel NVARCHAR(50) NULL,

    -- Change Tracking (from Excel)
    CABNumber NVARCHAR(50) NULL
)
GO

-- =============================================
-- INDEXES for Efficient Retrieval
-- =============================================

-- Primary search by DocId
CREATE NONCLUSTERED INDEX IX_MasterIndex_SourceDocumentID
    ON DaQa.MasterIndex(SourceDocumentID)
    INCLUDE (DocumentTitle, DocumentType, Status, GeneratedDocURL)
GO

-- Search by CAB number
CREATE NONCLUSTERED INDEX IX_MasterIndex_CABNumber
    ON DaQa.MasterIndex(CABNumber)
    INCLUDE (SourceDocumentID, DocumentTitle, DocumentType)
GO

-- Database object lookups
CREATE NONCLUSTERED INDEX IX_MasterIndex_Schema_Table
    ON DaQa.MasterIndex(SchemaName, TableName)
    INCLUDE (SourceDocumentID, ColumnName, DocumentType)
GO

CREATE NONCLUSTERED INDEX IX_MasterIndex_Table_Column
    ON DaQa.MasterIndex(TableName, ColumnName)
    WHERE ColumnName IS NOT NULL
    INCLUDE (SourceDocumentID, DocumentTitle)
GO

-- Status and workflow
CREATE NONCLUSTERED INDEX IX_MasterIndex_Status
    ON DaQa.MasterIndex(Status, ApprovalStatus)
    INCLUDE (SourceDocumentID, WorkflowStatus)
GO

CREATE NONCLUSTERED INDEX IX_MasterIndex_WorkflowStatus
    ON DaQa.MasterIndex(WorkflowStatus)
    INCLUDE (SourceDocumentID, Status)
GO

-- Recent documents
CREATE NONCLUSTERED INDEX IX_MasterIndex_CreatedDate
    ON DaQa.MasterIndex(CreatedDate DESC)
    INCLUDE (SourceDocumentID, DocumentType, Status)
GO

-- Modified documents
CREATE NONCLUSTERED INDEX IX_MasterIndex_ModifiedDate
    ON DaQa.MasterIndex(ModifiedDate DESC)
    WHERE IsDeleted = 0
    INCLUDE (SourceDocumentID, ModifiedBy)
GO

-- Versioning
CREATE NONCLUSTERED INDEX IX_MasterIndex_Versioning
    ON DaQa.MasterIndex(SourceDocumentID, VersionNumber, IsLatestVersion)
GO

-- =============================================
-- FULL-TEXT INDEX for Search
-- =============================================

-- Create full-text catalog if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'DocumentationCatalog')
BEGIN
    CREATE FULLTEXT CATALOG DocumentationCatalog AS DEFAULT
END
GO

-- Create full-text index on searchable columns
CREATE FULLTEXT INDEX ON DaQa.MasterIndex
(
    DocumentTitle LANGUAGE 1033,
    Description LANGUAGE 1033,
    Keywords LANGUAGE 1033,
    Tags LANGUAGE 1033,
    AIGeneratedTags LANGUAGE 1033,
    BusinessRules LANGUAGE 1033,
    UsagePurpose LANGUAGE 1033
)
KEY INDEX PK_MasterIndex
GO

-- =============================================
-- VIEWS for Common Queries
-- =============================================

-- View for recent approved documents
CREATE OR ALTER VIEW DaQa.vw_RecentApprovedDocuments
AS
SELECT TOP 100
    IndexID,
    SourceDocumentID AS DocId,
    DocumentTitle,
    DocumentType,
    TableName,
    ColumnName,
    ApprovedBy,
    ApprovedDate,
    GeneratedDocURL,
    CreatedDate
FROM DaQa.MasterIndex
WHERE ApprovalStatus = 'Approved'
  AND IsDeleted = 0
ORDER BY ApprovedDate DESC
GO

-- View for document statistics
CREATE OR ALTER VIEW DaQa.vw_DocumentStatistics
AS
SELECT
    DocumentType,
    COUNT(*) AS TotalDocuments,
    COUNT(CASE WHEN ApprovalStatus = 'Approved' THEN 1 END) AS ApprovedCount,
    COUNT(CASE WHEN Status = 'Published' THEN 1 END) AS PublishedCount,
    AVG(QualityScore) AS AvgQualityScore,
    AVG(CompletenessScore) AS AvgCompletenessScore,
    AVG(MetadataCompleteness) AS AvgMetadataCompleteness
FROM DaQa.MasterIndex
WHERE IsDeleted = 0
GROUP BY DocumentType
GO

-- View for column-specific changes
CREATE OR ALTER VIEW DaQa.vw_ColumnChanges
AS
SELECT
    TableName,
    ColumnName,
    SourceDocumentID AS DocId,
    DocumentType,
    DocumentTitle,
    ApprovedDate,
    GeneratedDocURL
FROM DaQa.MasterIndex
WHERE ColumnName IS NOT NULL
  AND IsDeleted = 0
GO

-- View for quality metrics
CREATE OR ALTER VIEW DaQa.vw_QualityMetrics
AS
SELECT
    IndexID,
    SourceDocumentID AS DocId,
    DocumentTitle,
    QualityScore,
    CompletenessScore,
    MetadataCompleteness,
    LastValidated,
    ValidationStatus,
    CASE
        WHEN QualityScore >= 90 THEN 'Excellent'
        WHEN QualityScore >= 75 THEN 'Good'
        WHEN QualityScore >= 60 THEN 'Fair'
        ELSE 'Needs Improvement'
    END AS QualityRating
FROM DaQa.MasterIndex
WHERE IsDeleted = 0
GO

PRINT 'MasterIndex table created successfully with 115+ columns'
PRINT 'Indexes created for efficient querying'
PRINT 'Full-text search enabled on key columns'
PRINT 'Views created for common queries:'
PRINT '  - DaQa.vw_RecentApprovedDocuments'
PRINT '  - DaQa.vw_DocumentStatistics'
PRINT '  - DaQa.vw_ColumnChanges'
PRINT '  - DaQa.vw_QualityMetrics'
GO
