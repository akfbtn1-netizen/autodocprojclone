-- CREATE WorkflowEvents table for Step 4 workflow tracking
-- This table tracks workflow events for the documentation automation process

USE IRFS1
GO

CREATE TABLE DaQa.WorkflowEvents (
    EventId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    WorkflowId NVARCHAR(50) NOT NULL,
    EventType NVARCHAR(50) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    Message NVARCHAR(MAX) NULL,
    DurationMs INT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Metadata NVARCHAR(MAX) NULL
);

-- Create indexes for performance
CREATE INDEX IX_WorkflowEvents_WorkflowId ON DaQa.WorkflowEvents (WorkflowId);
CREATE INDEX IX_WorkflowEvents_Timestamp ON DaQa.WorkflowEvents (Timestamp);
CREATE INDEX IX_WorkflowEvents_EventType ON DaQa.WorkflowEvents (EventType);

PRINT 'WorkflowEvents table created successfully';