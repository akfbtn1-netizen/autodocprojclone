-- <copyright file="Add_DocumentVersionHistory_Table.sql" company="Enterprise Documentation Platform">
-- Copyright (c) Enterprise Documentation Platform. All rights reserved.
-- This software is proprietary and confidential.
-- </copyright>

-- =============================================================================
-- Script Name: Add DocumentVersionHistory Table
-- Description: Creates the DocumentVersionHistory table for tracking document 
--              version changes in the adaptive stored procedure documentation system.
--              This table supports comprehensive version tracking with change
--              descriptions and reference document linking.
-- Author: Enterprise Documentation Platform
-- Created: 2024-12-03
-- Version: 1.0
-- =============================================================================

USE IRFS1;
GO

-- Check if DaQa schema exists, create if not
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DaQa')
BEGIN
    EXEC('CREATE SCHEMA DaQa');
    PRINT 'DaQa schema created successfully';
END
GO

-- Create DocumentVersionHistory table for comprehensive version tracking
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.DocumentVersionHistory') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.DocumentVersionHistory (
        -- Primary key for unique identification of each version record
        HistoryId INT IDENTITY(1,1) PRIMARY KEY,
        
        -- Foreign key reference to MasterIndex table
        IndexID NVARCHAR(50) NOT NULL,
        
        -- Version number (e.g., '1.0', '1.1', '2.0')
        Version NVARCHAR(10) NOT NULL,
        
        -- Timestamp when this version was created
        ChangeDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        
        -- User or system that made the change
        ChangedBy NVARCHAR(100) NOT NULL,
        
        -- Description of what changed in this version
        ChangeDescription NVARCHAR(500) NOT NULL,
        
        -- Optional reference to the change document (BR, EN, DF, etc.)
        ReferenceDocId NVARCHAR(50) NULL,
        
        -- Performance indexes for common query patterns
        INDEX IX_DocumentVersionHistory_IndexID (IndexID),
        INDEX IX_DocumentVersionHistory_ChangeDate (ChangeDate DESC),
        INDEX IX_DocumentVersionHistory_ReferenceDoc (ReferenceDocId)
    );
    
    PRINT 'DaQa.DocumentVersionHistory table created successfully';
    PRINT 'Table supports comprehensive version tracking for stored procedure documentation';
END
ELSE
BEGIN
    PRINT 'DaQa.DocumentVersionHistory table already exists';
END
GO

-- Add foreign key constraint to MasterIndex if it exists
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.MasterIndex') AND type = 'U')
   AND NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_DocumentVersionHistory_MasterIndex')
BEGIN
    ALTER TABLE DaQa.DocumentVersionHistory
    ADD CONSTRAINT FK_DocumentVersionHistory_MasterIndex
    FOREIGN KEY (IndexID) REFERENCES DaQa.MasterIndex(IndexID)
    ON DELETE CASCADE;
    
    PRINT 'Foreign key constraint added to MasterIndex table';
END
GO

-- Create helpful view for version history reporting
IF NOT EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'DaQa.vw_DocumentVersionSummary'))
BEGIN
    EXEC('
    CREATE VIEW DaQa.vw_DocumentVersionSummary
    AS
    SELECT 
        dvh.HistoryId,
        mi.DocumentTitle,
        mi.SourceDocumentID,
        dvh.Version,
        dvh.ChangeDate,
        dvh.ChangedBy,
        dvh.ChangeDescription,
        dvh.ReferenceDocId,
        mi.SourceSystem,
        mi.Status,
        ROW_NUMBER() OVER (PARTITION BY dvh.IndexID ORDER BY dvh.ChangeDate DESC) as VersionRank
    FROM DaQa.DocumentVersionHistory dvh
    INNER JOIN DaQa.MasterIndex mi ON dvh.IndexID = mi.IndexID
    ');
    
    PRINT 'DocumentVersionSummary view created for reporting';
END
GO

-- Insert sample data if this is a new installation and MasterIndex exists
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.MasterIndex') AND type = 'U')
   AND NOT EXISTS (SELECT TOP 1 * FROM DaQa.DocumentVersionHistory)
BEGIN
    -- Sample version history entries for demonstration
    INSERT INTO DaQa.DocumentVersionHistory (IndexID, Version, ChangeDate, ChangedBy, ChangeDescription, ReferenceDocId)
    SELECT 
        mi.IndexID,
        COALESCE(mi.DocumentVersion, '1.0') as Version,
        COALESCE(mi.CreatedDate, GETUTCDATE()) as ChangeDate,
        COALESCE(mi.CreatedBy, 'System') as ChangedBy,
        'Initial documentation creation' as ChangeDescription,
        mi.RelatedDocuments as ReferenceDocId
    FROM DaQa.MasterIndex mi
    WHERE mi.Status = 'Active'
    AND mi.DocumentTitle LIKE 'SP-%'  -- Only for stored procedure documents
    AND NOT EXISTS (
        SELECT 1 FROM DaQa.DocumentVersionHistory dvh 
        WHERE dvh.IndexID = mi.IndexID
    );
    
    DECLARE @RowCount INT = @@ROWCOUNT;
    IF @RowCount > 0
        PRINT CONCAT('Inserted ', @RowCount, ' initial version history records from existing MasterIndex entries');
END
GO

-- Create stored procedure for easy version history management
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.usp_AddVersionHistory') AND type = 'P')
BEGIN
    EXEC('
    CREATE PROCEDURE DaQa.usp_AddVersionHistory
        @IndexID NVARCHAR(50),
        @Version NVARCHAR(10),
        @ChangedBy NVARCHAR(100),
        @ChangeDescription NVARCHAR(500),
        @ReferenceDocId NVARCHAR(50) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        
        -- Validate that IndexID exists in MasterIndex
        IF NOT EXISTS (SELECT 1 FROM DaQa.MasterIndex WHERE IndexID = @IndexID)
        BEGIN
            RAISERROR(''IndexID %s does not exist in MasterIndex'', 16, 1, @IndexID);
            RETURN;
        END
        
        -- Insert the version history record
        INSERT INTO DaQa.DocumentVersionHistory (
            IndexID, Version, ChangedBy, ChangeDescription, ReferenceDocId
        )
        VALUES (
            @IndexID, @Version, @ChangedBy, @ChangeDescription, @ReferenceDocId
        );
        
        -- Update the MasterIndex with the latest version
        UPDATE DaQa.MasterIndex
        SET DocumentVersion = @Version,
            ModifiedDate = GETUTCDATE(),
            ModifiedBy = @ChangedBy
        WHERE IndexID = @IndexID;
        
        SELECT ''Version history added successfully'' as Result;
    END
    ');
    
    PRINT 'usp_AddVersionHistory stored procedure created for version management';
END
GO

-- Create function to get latest version for a document
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.fn_GetLatestVersion') AND type = 'FN')
BEGIN
    EXEC('
    CREATE FUNCTION DaQa.fn_GetLatestVersion(@IndexID NVARCHAR(50))
    RETURNS NVARCHAR(10)
    AS
    BEGIN
        DECLARE @LatestVersion NVARCHAR(10);
        
        SELECT TOP 1 @LatestVersion = Version
        FROM DaQa.DocumentVersionHistory
        WHERE IndexID = @IndexID
        ORDER BY ChangeDate DESC;
        
        RETURN ISNULL(@LatestVersion, ''1.0'');
    END
    ');
    
    PRINT 'fn_GetLatestVersion function created for version retrieval';
END
GO

-- Final verification and summary
SELECT 
    'DocumentVersionHistory Setup Complete' as Status,
    COUNT(*) as ExistingRecords,
    MAX(ChangeDate) as LatestChange
FROM DaQa.DocumentVersionHistory;

PRINT '=============================================================================';
PRINT 'DocumentVersionHistory table setup completed successfully';
PRINT 'Features added:';
PRINT '  - Comprehensive version tracking table';
PRINT '  - Performance indexes for common queries';
PRINT '  - Foreign key constraint to MasterIndex';
PRINT '  - Reporting view for version summaries';
PRINT '  - Management stored procedure for version operations';
PRINT '  - Helper function for latest version retrieval';
PRINT '=============================================================================';
GO