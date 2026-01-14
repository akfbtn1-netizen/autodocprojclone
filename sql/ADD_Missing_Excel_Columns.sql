USE [HelpDesk_TEST]
GO

-- Add missing columns to DocumentChanges table
ALTER TABLE daqa.DocumentChanges 
ADD ChangeApplied NVARCHAR(255) NULL;

ALTER TABLE daqa.DocumentChanges 
ADD LocationOfCodeChange NVARCHAR(500) NULL;

PRINT 'Added ChangeApplied and LocationOfCodeChange columns to DocumentChanges table'
GO