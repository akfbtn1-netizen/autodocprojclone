-- ============================================================================
-- AGENT #4: SCHEMA CHANGE DETECTOR TABLES
-- Enterprise Documentation Platform V2
-- Created: January 13, 2026
-- Purpose: Real-time schema change detection, impact analysis, diff tracking
-- ============================================================================

USE [IRFS1]
GO

-- ============================================================================
-- 1. SCHEMA CHANGES (Main change log)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaChanges' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.SchemaChanges (
        ChangeId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),

        -- Change identification
        DatabaseName NVARCHAR(128) NOT NULL,
        SchemaName NVARCHAR(128) NOT NULL,
        ObjectName NVARCHAR(256) NOT NULL,
        ObjectType NVARCHAR(50) NOT NULL,  -- TABLE, VIEW, PROCEDURE, FUNCTION, INDEX, CONSTRAINT
        ChangeType NVARCHAR(20) NOT NULL,  -- CREATE, ALTER, DROP, RENAME

        -- Change details
        ChangeDescription NVARCHAR(MAX),
        ChangedColumns NVARCHAR(MAX),      -- JSON array of affected columns
        OldDefinition NVARCHAR(MAX),       -- Previous state (for ALTER/DROP)
        NewDefinition NVARCHAR(MAX),       -- New state (for CREATE/ALTER)
        DdlStatement NVARCHAR(MAX),        -- Actual DDL that caused the change

        -- Detection metadata
        DetectedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        DetectedBy NVARCHAR(128) NOT NULL, -- 'DDL_TRIGGER', 'POLLING', 'MANUAL'
        LoginName NVARCHAR(128),           -- Who executed the DDL
        HostName NVARCHAR(128),            -- Where it was executed from
        ApplicationName NVARCHAR(256),     -- What app executed it

        -- Impact assessment
        ImpactScore INT DEFAULT 0,         -- 0-100 calculated risk score
        RiskLevel NVARCHAR(20) DEFAULT 'LOW', -- LOW, MEDIUM, HIGH, CRITICAL
        AffectedProcedures INT DEFAULT 0,
        AffectedViews INT DEFAULT 0,
        AffectedFunctions INT DEFAULT 0,
        HasPiiColumns BIT DEFAULT 0,
        HasLineageDownstream BIT DEFAULT 0,

        -- Processing status
        ProcessingStatus NVARCHAR(50) DEFAULT 'Pending',  -- Pending, Analyzing, Assessed, Acknowledged, AutoProcessed
        AcknowledgedBy NVARCHAR(128),
        AcknowledgedAt DATETIME2,
        AcknowledgementNotes NVARCHAR(MAX),

        -- Workflow integration
        ApprovalRequired BIT DEFAULT 0,
        ApprovalWorkflowId UNIQUEIDENTIFIER,  -- FK to DocumentApprovals
        DocumentationTriggered BIT DEFAULT 0,
        DocumentationTriggeredAt DATETIME2,

        -- Audit
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2,

        INDEX IX_SchemaChanges_DetectedAt (DetectedAt DESC),
        INDEX IX_SchemaChanges_ObjectName (SchemaName, ObjectName),
        INDEX IX_SchemaChanges_Status (ProcessingStatus),
        INDEX IX_SchemaChanges_RiskLevel (RiskLevel, DetectedAt DESC),
        INDEX IX_SchemaChanges_ChangeType (ChangeType, DetectedAt DESC)
    );

    PRINT 'Created DaQa.SchemaChanges table';
END
GO

-- ============================================================================
-- 2. SCHEMA SNAPSHOTS (Point-in-time schema state)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaSnapshots' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.SchemaSnapshots (
        SnapshotId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),

        -- Snapshot identification
        SnapshotName NVARCHAR(256) NOT NULL,
        SnapshotType NVARCHAR(50) NOT NULL,  -- FULL, SCHEMA, OBJECT, BASELINE
        SchemaFilter NVARCHAR(128),           -- NULL = all schemas

        -- Snapshot content (compressed JSON)
        SnapshotData VARBINARY(MAX) NOT NULL, -- Compressed schema definition
        ObjectCount INT NOT NULL DEFAULT 0,
        TableCount INT NOT NULL DEFAULT 0,
        ViewCount INT NOT NULL DEFAULT 0,
        ProcedureCount INT NOT NULL DEFAULT 0,
        FunctionCount INT NOT NULL DEFAULT 0,

        -- Metadata
        TakenAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        TakenBy NVARCHAR(128) NOT NULL,
        DatabaseVersion NVARCHAR(50),

        -- Comparison tracking
        IsBaseline BIT DEFAULT 0,
        PreviousSnapshotId UNIQUEIDENTIFIER,
        DiffFromPrevious NVARCHAR(MAX),  -- JSON diff summary

        -- Retention
        ExpiresAt DATETIME2,
        IsArchived BIT DEFAULT 0,

        INDEX IX_SchemaSnapshots_TakenAt (TakenAt DESC),
        INDEX IX_SchemaSnapshots_Baseline (IsBaseline) WHERE IsBaseline = 1
    );

    PRINT 'Created DaQa.SchemaSnapshots table';
END
GO

-- ============================================================================
-- 3. SCHEMA DETECTION RUNS (Detection job history)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaDetectionRuns' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.SchemaDetectionRuns (
        RunId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),

        -- Run configuration
        RunType NVARCHAR(50) NOT NULL,       -- SCHEDULED, MANUAL, DDL_TRIGGER, STARTUP
        ScanScope NVARCHAR(50) NOT NULL,     -- FULL, SCHEMA, OBJECT
        SchemaFilter NVARCHAR(128),
        ObjectFilter NVARCHAR(256),

        -- Execution state (Saga pattern)
        CurrentState NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        -- States: Pending, Snapshotting, Comparing, Analyzing, Notifying, Complete, Failed, Cancelled

        -- Progress tracking
        TotalObjects INT DEFAULT 0,
        ProcessedObjects INT DEFAULT 0,
        ChangesDetected INT DEFAULT 0,
        HighRiskChanges INT DEFAULT 0,

        -- Timing
        StartedAt DATETIME2,
        SnapshotCompletedAt DATETIME2,
        ComparisonCompletedAt DATETIME2,
        AnalysisCompletedAt DATETIME2,
        CompletedAt DATETIME2,
        DurationMs BIGINT,

        -- Error handling
        ErrorMessage NVARCHAR(MAX),
        RetryCount INT DEFAULT 0,

        -- Initiator
        TriggeredBy NVARCHAR(128) NOT NULL,

        -- Results
        SnapshotId UNIQUEIDENTIFIER,  -- FK to SchemaSnapshots
        ResultSummary NVARCHAR(MAX),  -- JSON summary

        INDEX IX_DetectionRuns_State (CurrentState, StartedAt DESC),
        INDEX IX_DetectionRuns_StartedAt (StartedAt DESC)
    );

    PRINT 'Created DaQa.SchemaDetectionRuns table';
END
GO

-- ============================================================================
-- 4. CHANGE IMPACT ANALYSIS (Detailed impact per change)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChangeImpactAnalysis' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.ChangeImpactAnalysis (
        ImpactId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ChangeId UNIQUEIDENTIFIER NOT NULL,  -- FK to SchemaChanges

        -- Affected object
        AffectedSchema NVARCHAR(128) NOT NULL,
        AffectedObject NVARCHAR(256) NOT NULL,
        AffectedObjectType NVARCHAR(50) NOT NULL,  -- PROCEDURE, VIEW, FUNCTION, TABLE

        -- Impact details
        ImpactType NVARCHAR(50) NOT NULL,  -- BREAKS, INVALIDATES, MODIFIES, PERFORMANCE
        ImpactSeverity INT NOT NULL,       -- 1-5 (1=minimal, 5=critical)
        ImpactDescription NVARCHAR(MAX),

        -- Operation type (from lineage)
        OperationType NVARCHAR(20),        -- READ, UPDATE, INSERT, DELETE
        AffectedColumn NVARCHAR(256),      -- Specific column if applicable

        -- Code reference
        LineNumber INT,
        SqlFragment NVARCHAR(MAX),         -- Relevant SQL snippet

        -- Resolution
        SuggestedAction NVARCHAR(MAX),
        RequiresManualReview BIT DEFAULT 0,

        -- Timestamps
        AnalyzedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_ChangeImpact_Change FOREIGN KEY (ChangeId)
            REFERENCES DaQa.SchemaChanges(ChangeId) ON DELETE CASCADE,

        INDEX IX_ChangeImpact_ChangeId (ChangeId),
        INDEX IX_ChangeImpact_Severity (ImpactSeverity DESC, ChangeId)
    );

    PRINT 'Created DaQa.ChangeImpactAnalysis table';
END
GO

-- ============================================================================
-- 5. COLUMN CHANGE TRACKING (Column-level change history)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColumnChangeHistory' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.ColumnChangeHistory (
        ColumnChangeId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ChangeId UNIQUEIDENTIFIER NOT NULL,  -- FK to SchemaChanges

        -- Column identification
        SchemaName NVARCHAR(128) NOT NULL,
        TableName NVARCHAR(256) NOT NULL,
        ColumnName NVARCHAR(256) NOT NULL,

        -- Change type
        ColumnChangeType NVARCHAR(50) NOT NULL,  -- ADD, DROP, MODIFY, RENAME

        -- Before state (for MODIFY/DROP)
        OldDataType NVARCHAR(256),
        OldMaxLength INT,
        OldPrecision TINYINT,
        OldScale TINYINT,
        OldIsNullable BIT,
        OldDefaultValue NVARCHAR(MAX),
        OldIsIdentity BIT,
        OldIsPii BIT,
        OldPiiType NVARCHAR(50),

        -- After state (for ADD/MODIFY)
        NewDataType NVARCHAR(256),
        NewMaxLength INT,
        NewPrecision TINYINT,
        NewScale TINYINT,
        NewIsNullable BIT,
        NewDefaultValue NVARCHAR(MAX),
        NewIsIdentity BIT,
        NewIsPii BIT,
        NewPiiType NVARCHAR(50),

        -- Impact from lineage
        ReadCount INT DEFAULT 0,
        UpdateCount INT DEFAULT 0,
        InsertCount INT DEFAULT 0,
        DeleteCount INT DEFAULT 0,

        DetectedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_ColumnChange_Change FOREIGN KEY (ChangeId)
            REFERENCES DaQa.SchemaChanges(ChangeId) ON DELETE CASCADE,

        INDEX IX_ColumnChange_ChangeId (ChangeId),
        INDEX IX_ColumnChange_Column (SchemaName, TableName, ColumnName)
    );

    PRINT 'Created DaQa.ColumnChangeHistory table';
END
GO

-- ============================================================================
-- 6. CHANGE NOTIFICATION LOG
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChangeNotifications' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.ChangeNotifications (
        NotificationId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ChangeId UNIQUEIDENTIFIER NOT NULL,

        NotificationType NVARCHAR(50) NOT NULL,  -- SIGNALR, EMAIL, TEAMS, WEBHOOK
        RecipientType NVARCHAR(50) NOT NULL,     -- USER, GROUP, ROLE
        RecipientId NVARCHAR(256) NOT NULL,

        NotificationTitle NVARCHAR(500),
        NotificationBody NVARCHAR(MAX),

        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',  -- Pending, Sent, Failed, Acknowledged
        SentAt DATETIME2,
        AcknowledgedAt DATETIME2,
        FailureReason NVARCHAR(MAX),
        RetryCount INT DEFAULT 0,

        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_Notification_Change FOREIGN KEY (ChangeId)
            REFERENCES DaQa.SchemaChanges(ChangeId) ON DELETE CASCADE,

        INDEX IX_Notification_ChangeId (ChangeId),
        INDEX IX_Notification_Status (Status, CreatedAt DESC)
    );

    PRINT 'Created DaQa.ChangeNotifications table';
END
GO

-- ============================================================================
-- 7. DDL TRIGGER FOR REAL-TIME DETECTION
-- ============================================================================
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_CaptureSchemaChanges' AND parent_class = 0)
BEGIN
    DROP TRIGGER TR_CaptureSchemaChanges ON DATABASE;
    PRINT 'Dropped existing DDL trigger';
END
GO

CREATE TRIGGER TR_CaptureSchemaChanges
ON DATABASE
FOR CREATE_TABLE, ALTER_TABLE, DROP_TABLE,
    CREATE_VIEW, ALTER_VIEW, DROP_VIEW,
    CREATE_PROCEDURE, ALTER_PROCEDURE, DROP_PROCEDURE,
    CREATE_FUNCTION, ALTER_FUNCTION, DROP_FUNCTION,
    CREATE_INDEX, ALTER_INDEX, DROP_INDEX
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EventData XML = EVENTDATA();
    DECLARE @SchemaName NVARCHAR(128);
    DECLARE @ObjectName NVARCHAR(256);
    DECLARE @ObjectType NVARCHAR(50);
    DECLARE @ChangeType NVARCHAR(20);
    DECLARE @DdlStatement NVARCHAR(MAX);
    DECLARE @LoginName NVARCHAR(128);
    DECLARE @HostName NVARCHAR(128);
    DECLARE @AppName NVARCHAR(256);

    SELECT
        @SchemaName = @EventData.value('(/EVENT_INSTANCE/SchemaName)[1]', 'NVARCHAR(128)'),
        @ObjectName = @EventData.value('(/EVENT_INSTANCE/ObjectName)[1]', 'NVARCHAR(256)'),
        @ObjectType = @EventData.value('(/EVENT_INSTANCE/ObjectType)[1]', 'NVARCHAR(50)'),
        @ChangeType = CASE
            WHEN @EventData.value('(/EVENT_INSTANCE/EventType)[1]', 'NVARCHAR(50)') LIKE 'CREATE%' THEN 'CREATE'
            WHEN @EventData.value('(/EVENT_INSTANCE/EventType)[1]', 'NVARCHAR(50)') LIKE 'ALTER%' THEN 'ALTER'
            WHEN @EventData.value('(/EVENT_INSTANCE/EventType)[1]', 'NVARCHAR(50)') LIKE 'DROP%' THEN 'DROP'
        END,
        @DdlStatement = @EventData.value('(/EVENT_INSTANCE/TSQLCommand/CommandText)[1]', 'NVARCHAR(MAX)'),
        @LoginName = @EventData.value('(/EVENT_INSTANCE/LoginName)[1]', 'NVARCHAR(128)'),
        @HostName = HOST_NAME(),
        @AppName = APP_NAME();

    -- Exclude system and documentation schema from self-triggering
    IF @SchemaName IN ('sys', 'INFORMATION_SCHEMA') OR
       (@SchemaName = 'DaQa' AND @ObjectName LIKE 'Schema%')
    BEGIN
        RETURN;
    END

    BEGIN TRY
        INSERT INTO DaQa.SchemaChanges (
            DatabaseName, SchemaName, ObjectName, ObjectType, ChangeType,
            DdlStatement, DetectedBy, LoginName, HostName, ApplicationName,
            ProcessingStatus
        )
        VALUES (
            DB_NAME(), @SchemaName, @ObjectName, @ObjectType, @ChangeType,
            @DdlStatement, 'DDL_TRIGGER', @LoginName, @HostName, @AppName,
            'Pending'
        );
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR('Schema change capture warning: %s', 10, 1, @ErrorMsg) WITH LOG;
    END CATCH
END
GO

PRINT 'Created DDL trigger TR_CaptureSchemaChanges';
GO

-- ============================================================================
-- 8. HELPER STORED PROCEDURES
-- ============================================================================

CREATE OR ALTER PROCEDURE DaQa.usp_GetPendingSchemaChanges
    @MaxCount INT = 100,
    @MinRiskLevel NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@MaxCount)
        ChangeId, DatabaseName, SchemaName, ObjectName, ObjectType,
        ChangeType, ChangeDescription, DdlStatement, DetectedAt,
        DetectedBy, LoginName, ImpactScore, RiskLevel, ProcessingStatus,
        AffectedProcedures, AffectedViews, HasPiiColumns, HasLineageDownstream
    FROM DaQa.SchemaChanges
    WHERE ProcessingStatus = 'Pending'
      AND (@MinRiskLevel IS NULL OR RiskLevel = @MinRiskLevel)
    ORDER BY
        CASE RiskLevel
            WHEN 'CRITICAL' THEN 1
            WHEN 'HIGH' THEN 2
            WHEN 'MEDIUM' THEN 3
            WHEN 'LOW' THEN 4
        END,
        DetectedAt ASC;
END
GO

PRINT '============================================================';
PRINT 'Agent #4 Schema Change Detector tables created successfully!';
PRINT '============================================================';
GO
