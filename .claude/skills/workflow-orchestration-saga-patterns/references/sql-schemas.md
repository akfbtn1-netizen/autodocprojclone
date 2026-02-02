# SQL Schemas for Workflow Orchestration

## Event Store Table

```sql
CREATE TABLE DocumentEvents (
    EventId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AggregateId UNIQUEIDENTIFIER NOT NULL,
    EventType NVARCHAR(100) NOT NULL,
    EventData NVARCHAR(MAX) NOT NULL,
    Version INT NOT NULL,
    OccurredAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    -- Ensure no duplicate versions per aggregate
    CONSTRAINT UQ_Aggregate_Version UNIQUE (AggregateId, Version)
);

-- Indexes for common queries
CREATE INDEX IX_DocumentEvents_AggregateId ON DocumentEvents(AggregateId);
CREATE INDEX IX_DocumentEvents_EventType ON DocumentEvents(EventType);
CREATE INDEX IX_DocumentEvents_OccurredAt ON DocumentEvents(OccurredAt);

-- Partition by month for large-scale deployments (optional)
-- CREATE PARTITION FUNCTION PF_EventDate (DATETIME2) AS RANGE RIGHT FOR VALUES (...)
```

## Saga State Table (MassTransit)

```sql
CREATE TABLE DocumentApprovalState (
    CorrelationId UNIQUEIDENTIFIER PRIMARY KEY,
    CurrentState NVARCHAR(64) NOT NULL,
    
    -- Document metadata
    DocumentType NVARCHAR(100),
    PhysicalName NVARCHAR(500),
    DatabaseName NVARCHAR(128),
    SchemaName NVARCHAR(128),
    ObjectName NVARCHAR(128),
    
    -- Workflow tracking
    SubmittedAt DATETIME2,
    ApprovedAt DATETIME2,
    CompletedAt DATETIME2,
    SubmittedBy NVARCHAR(100),
    ApprovedBy NVARCHAR(500),
    
    -- Approval chain (JSON arrays)
    RequiredApprovers NVARCHAR(MAX),
    CompletedApprovers NVARCHAR(MAX),
    ApprovalTier INT NOT NULL DEFAULT 1,
    
    -- Document paths
    DraftPath NVARCHAR(500),
    FinalPath NVARCHAR(500),
    SharePointUrl NVARCHAR(500),
    
    -- Timeout scheduling
    ExpirationTokenId UNIQUEIDENTIFIER,
    
    -- Optimistic concurrency
    RowVersion ROWVERSION NOT NULL,
    
    -- Audit
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Indexes for common queries
CREATE INDEX IX_DocumentApprovalState_CurrentState ON DocumentApprovalState(CurrentState);
CREATE INDEX IX_DocumentApprovalState_SubmittedAt ON DocumentApprovalState(SubmittedAt);
CREATE INDEX IX_DocumentApprovalState_ApprovalTier ON DocumentApprovalState(ApprovalTier);

-- Trigger for ModifiedAt
CREATE TRIGGER TR_DocumentApprovalState_ModifiedAt
ON DocumentApprovalState
AFTER UPDATE
AS
BEGIN
    UPDATE DocumentApprovalState
    SET ModifiedAt = GETUTCDATE()
    FROM DocumentApprovalState s
    INNER JOIN inserted i ON s.CorrelationId = i.CorrelationId;
END;
```

## Approval Tier Configuration Table

```sql
CREATE TABLE ApprovalTierConfiguration (
    TierId INT PRIMARY KEY,
    TierName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500),
    
    -- Criteria (JSON for flexibility)
    CriteriaJson NVARCHAR(MAX) NOT NULL,
    
    -- Approvers (JSON array of role names)
    ApproversJson NVARCHAR(MAX) NOT NULL,
    
    -- Routing type
    RoutingType NVARCHAR(50) NOT NULL DEFAULT 'parallel', -- parallel, sequential, staged
    
    -- SLA in hours
    SlaHours INT NOT NULL,
    
    -- Status
    IsActive BIT NOT NULL DEFAULT 1,
    
    -- Audit
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedBy NVARCHAR(100)
);

-- Default tier data
INSERT INTO ApprovalTierConfiguration (TierId, TierName, Description, CriteriaJson, ApproversJson, RoutingType, SlaHours)
VALUES
(1, 'Standard', 'Routine schema changes with no PII', 
   '{"object_types":["Column","Index"],"contains_pii":false}',
   '["data-steward"]', 
   'single', 24),
(2, 'Tables/Views', 'Standard tables, views, stored procedures',
   '{"object_types":["Table","View","StoredProcedure"],"contains_pii":false}',
   '["data-steward","dba"]',
   'parallel', 48),
(3, 'Sensitive', 'Objects containing PII or financial data',
   '{"contains_pii":true,"data_classifications":["Confidential","Restricted"]}',
   '["data-steward","dba","compliance-officer"]',
   'sequential_then_parallel', 72),
(4, 'Critical', 'Core financial/regulatory systems',
   '{"database_criticality":"critical","databases":["IRFS1","PolicyAdmin","ClaimsCore"]}',
   '["data-steward","dba","compliance-officer","data-governance-lead"]',
   'staged', 120);
```

## Approval History Table (Read Model)

```sql
CREATE TABLE ApprovalHistory (
    HistoryId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    
    -- Action details
    ActionType NVARCHAR(50) NOT NULL, -- Submitted, Approved, Rejected, Escalated, Filed
    ActionBy NVARCHAR(100) NOT NULL,
    ActionAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    -- Additional context
    Comments NVARCHAR(MAX),
    PreviousState NVARCHAR(64),
    NewState NVARCHAR(64),
    
    -- Foreign key to saga state
    CONSTRAINT FK_ApprovalHistory_Document FOREIGN KEY (DocumentId) 
        REFERENCES DocumentApprovalState(CorrelationId)
);

CREATE INDEX IX_ApprovalHistory_DocumentId ON ApprovalHistory(DocumentId);
CREATE INDEX IX_ApprovalHistory_ActionAt ON ApprovalHistory(ActionAt);
```

## Approval Queue View (Dashboard)

```sql
CREATE VIEW vw_PendingApprovals AS
SELECT 
    s.CorrelationId AS DocumentId,
    s.PhysicalName,
    s.DatabaseName,
    s.DocumentType,
    s.ApprovalTier,
    t.TierName,
    s.SubmittedAt,
    s.SubmittedBy,
    s.RequiredApprovers,
    s.CompletedApprovers,
    s.CurrentState,
    t.SlaHours,
    DATEADD(HOUR, t.SlaHours, s.SubmittedAt) AS Deadline,
    CASE 
        WHEN GETUTCDATE() > DATEADD(HOUR, t.SlaHours, s.SubmittedAt) THEN 1 
        ELSE 0 
    END AS IsOverdue,
    DATEDIFF(HOUR, s.SubmittedAt, GETUTCDATE()) AS HoursElapsed
FROM DocumentApprovalState s
INNER JOIN ApprovalTierConfiguration t ON s.ApprovalTier = t.TierId
WHERE s.CurrentState IN ('AwaitingApproval', 'Escalated', 'DraftPending');
```

## Metrics View

```sql
CREATE VIEW vw_ApprovalMetrics AS
SELECT 
    CAST(s.SubmittedAt AS DATE) AS SubmitDate,
    s.ApprovalTier,
    COUNT(*) AS TotalSubmitted,
    SUM(CASE WHEN s.CurrentState = 'Final' THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN s.CurrentState = 'Rejected' THEN 1 ELSE 0 END) AS Rejected,
    SUM(CASE WHEN s.CurrentState IN ('AwaitingApproval', 'Escalated') THEN 1 ELSE 0 END) AS Pending,
    AVG(DATEDIFF(HOUR, s.SubmittedAt, s.ApprovedAt)) AS AvgApprovalHours
FROM DocumentApprovalState s
WHERE s.SubmittedAt >= DATEADD(DAY, -30, GETUTCDATE())
GROUP BY CAST(s.SubmittedAt AS DATE), s.ApprovalTier;
```

## Stored Procedures

### Get Approval Chain

```sql
CREATE PROCEDURE sp_GetApprovalChain
    @DocumentId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT 
        h.ActionType,
        h.ActionBy,
        h.ActionAt,
        h.Comments,
        h.PreviousState,
        h.NewState
    FROM ApprovalHistory h
    WHERE h.DocumentId = @DocumentId
    ORDER BY h.ActionAt;
END;
```

### Record Approval Action

```sql
CREATE PROCEDURE sp_RecordApprovalAction
    @DocumentId UNIQUEIDENTIFIER,
    @ActionType NVARCHAR(50),
    @ActionBy NVARCHAR(100),
    @Comments NVARCHAR(MAX) = NULL,
    @PreviousState NVARCHAR(64) = NULL,
    @NewState NVARCHAR(64) = NULL
AS
BEGIN
    INSERT INTO ApprovalHistory (DocumentId, ActionType, ActionBy, Comments, PreviousState, NewState)
    VALUES (@DocumentId, @ActionType, @ActionBy, @Comments, @PreviousState, @NewState);
END;
```
