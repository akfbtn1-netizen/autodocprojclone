-- Create DocumentationQueue table for DocGeneratorQueueProcessor
-- This table holds items to be processed by the document generation workflow

USE [IRFS1]
GO

-- Create DocumentationQueue table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentationQueue' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.DocumentationQueue (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DocId NVARCHAR(50) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        Priority INT NOT NULL DEFAULT 5,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        ErrorMessage NVARCHAR(MAX) NULL,
        RetryCount INT NOT NULL DEFAULT 0,
        ProcessedBy NVARCHAR(255) NULL,
        ProcessedAt DATETIME2 NULL
    );

    PRINT 'Created table DaQa.DocumentationQueue';
END
ELSE
BEGIN
    PRINT 'Table DaQa.DocumentationQueue already exists';
END
GO

-- Create index for efficient queue processing (Status + CreatedAt)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentationQueue_Processing' AND object_id = OBJECT_ID('DaQa.DocumentationQueue'))
BEGIN
    CREATE INDEX IX_DocumentationQueue_Processing 
    ON DaQa.DocumentationQueue(Status, CreatedAt) 
    WHERE Status IN ('Pending', 'Processing');
    
    PRINT 'Created index IX_DocumentationQueue_Processing';
END

-- Create index for DocId lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentationQueue_DocId' AND object_id = OBJECT_ID('DaQa.DocumentationQueue'))
BEGIN
    CREATE INDEX IX_DocumentationQueue_DocId 
    ON DaQa.DocumentationQueue(DocId);
    
    PRINT 'Created index IX_DocumentationQueue_DocId';
END

-- Add foreign key to DocumentChanges table
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_DocumentationQueue_DocumentChanges')
BEGIN
    ALTER TABLE DaQa.DocumentationQueue
    ADD CONSTRAINT FK_DocumentationQueue_DocumentChanges
    FOREIGN KEY (DocId) REFERENCES DaQa.DocumentChanges(DocId);
    
    PRINT 'Added foreign key FK_DocumentationQueue_DocumentChanges';
END

-- Add check constraint for valid statuses
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_DocumentationQueue_Status')
BEGIN
    ALTER TABLE DaQa.DocumentationQueue
    ADD CONSTRAINT CK_DocumentationQueue_Status 
    CHECK (Status IN ('Pending', 'Processing', 'Completed', 'Failed', 'Retry'));
    
    PRINT 'Added check constraint CK_DocumentationQueue_Status';
END

-- Add check constraint for valid priority (1-10)
IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_DocumentationQueue_Priority')
BEGIN
    ALTER TABLE DaQa.DocumentationQueue
    ADD CONSTRAINT CK_DocumentationQueue_Priority 
    CHECK (Priority BETWEEN 1 AND 10);
    
    PRINT 'Added check constraint CK_DocumentationQueue_Priority';
END

PRINT 'DocumentationQueue table setup completed';