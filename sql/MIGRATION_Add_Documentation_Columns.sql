-- Migration script to add missing Documentation columns to DocumentChanges table
-- This addresses the SQL error: "Invalid column name 'Documentation'" and "Invalid column name 'DocumentationLink'"

USE IRFS1
GO

-- Check if Documentation column exists, if not add it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('daqa.DocumentChanges') AND name = 'Documentation')
BEGIN
    ALTER TABLE daqa.DocumentChanges ADD Documentation NVARCHAR(500) NULL
    PRINT 'Added Documentation column to daqa.DocumentChanges'
END
ELSE
BEGIN
    PRINT 'Documentation column already exists in daqa.DocumentChanges'
END

-- Check if DocumentationLink column exists, if not add it  
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('daqa.DocumentChanges') AND name = 'DocumentationLink')
BEGIN
    ALTER TABLE daqa.DocumentChanges ADD DocumentationLink NVARCHAR(500) NULL
    PRINT 'Added DocumentationLink column to daqa.DocumentChanges'
END
ELSE
BEGIN
    PRINT 'DocumentationLink column already exists in daqa.DocumentChanges'
END

-- Check if DocId column exists, if not add it
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('daqa.DocumentChanges') AND name = 'DocId')
BEGIN
    ALTER TABLE daqa.DocumentChanges ADD DocId NVARCHAR(50) NULL
    PRINT 'Added DocId column to daqa.DocumentChanges'
END
ELSE
BEGIN
    PRINT 'DocId column already exists in daqa.DocumentChanges'
END

PRINT 'Migration completed - Documentation columns added to daqa.DocumentChanges table'