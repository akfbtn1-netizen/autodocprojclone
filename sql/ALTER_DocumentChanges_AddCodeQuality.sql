-- Add CodeQualityScore and CodeQualityGrade columns to DocumentChanges table
-- These columns store the results from Step 4: Code Quality Audit

USE [IRFS1]
GO

-- Check if columns already exist before adding them
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityScore')
BEGIN
    ALTER TABLE DaQa.DocumentChanges
    ADD CodeQualityScore INT NULL;
    
    PRINT 'Added CodeQualityScore column to DaQa.DocumentChanges';
END
ELSE
BEGIN
    PRINT 'CodeQualityScore column already exists in DaQa.DocumentChanges';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityGrade')
BEGIN
    ALTER TABLE DaQa.DocumentChanges
    ADD CodeQualityGrade NVARCHAR(10) NULL;
    
    PRINT 'Added CodeQualityGrade column to DaQa.DocumentChanges';
END
ELSE
BEGIN
    PRINT 'CodeQualityGrade column already exists in DaQa.DocumentChanges';
END

-- Add check constraint for valid grades (only if column exists)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityGrade')
   AND NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_DocumentChanges_CodeQualityGrade')
BEGIN
    ALTER TABLE DaQa.DocumentChanges
    ADD CONSTRAINT CK_DocumentChanges_CodeQualityGrade 
    CHECK (CodeQualityGrade IN ('A+', 'A', 'B+', 'B', 'C+', 'C', 'D', 'F') OR CodeQualityGrade IS NULL);
    
    PRINT 'Added check constraint CK_DocumentChanges_CodeQualityGrade';
END

-- Add check constraint for valid scores (only if column exists)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityScore')
   AND NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_DocumentChanges_CodeQualityScore')
BEGIN
    ALTER TABLE DaQa.DocumentChanges
    ADD CONSTRAINT CK_DocumentChanges_CodeQualityScore 
    CHECK (CodeQualityScore BETWEEN 0 AND 100 OR CodeQualityScore IS NULL);
    
    PRINT 'Added check constraint CK_DocumentChanges_CodeQualityScore';
END

-- Create index for code quality queries (only if columns exist)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityScore')
   AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_CodeQuality' AND object_id = OBJECT_ID('DaQa.DocumentChanges'))
BEGIN
    CREATE INDEX IX_DocumentChanges_CodeQuality 
    ON DaQa.DocumentChanges(CodeQualityScore, CodeQualityGrade) 
    WHERE CodeQualityScore IS NOT NULL;
    
    PRINT 'Created index IX_DocumentChanges_CodeQuality';
END

PRINT 'DocumentChanges table updated with code quality columns';