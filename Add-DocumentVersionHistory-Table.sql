-- Add-DocumentVersionHistory-Table.sql

USE IRFS1;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.DocumentVersionHistory') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.DocumentVersionHistory (
        HistoryId INT IDENTITY(1,1) PRIMARY KEY,
        MasterIndexId UNIQUEIDENTIFIER NOT NULL,
        Version NVARCHAR(10) NOT NULL,
        ChangeDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ChangedBy NVARCHAR(100) NOT NULL,
        ChangeDescription NVARCHAR(500) NOT NULL,
        ReferenceDocId NVARCHAR(50) NULL,
        
        CONSTRAINT FK_DocVersionHistory_MasterIndex 
            FOREIGN KEY (MasterIndexId) REFERENCES DaQa.MasterIndex(MasterIndexId),
        
        INDEX IX_DocumentVersionHistory_MasterIndexId (MasterIndexId),
        INDEX IX_DocumentVersionHistory_ChangeDate (ChangeDate DESC)
    );
    
    PRINT 'DaQa.DocumentVersionHistory table created successfully';
END
ELSE
BEGIN
    PRINT 'DaQa.DocumentVersionHistory table already exists';
END
GO
