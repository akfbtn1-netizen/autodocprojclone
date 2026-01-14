-- ============================================================================
-- GAP INTELLIGENCE AGENT (AGENT #7) - DATABASE SCHEMA
-- Creates tables for ML-style gap detection, RLHF learning, and semantic clustering
-- ============================================================================
USE gwpc;
GO

PRINT 'Creating Gap Intelligence Agent tables...';
GO

-- 1. GAP PATTERNS - Learned detection patterns with RLHF metrics
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GapPatterns' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[GapPatterns] (
    [PatternId] INT IDENTITY(1,1) PRIMARY KEY,
    [PatternName] NVARCHAR(100) NOT NULL,
    [PatternType] NVARCHAR(50) NOT NULL, -- STRUCTURAL, USAGE, TEMPORAL, LINEAGE, COMPLIANCE, SEMANTIC
    [PatternDescription] NVARCHAR(500) NULL,
    [DetectionRules] NVARCHAR(MAX) NOT NULL, -- JSON rules
    [TruePositives] INT NOT NULL DEFAULT 0,
    [FalsePositives] INT NOT NULL DEFAULT 0,
    [Precision] AS CAST(CASE WHEN (TruePositives + FalsePositives) > 0
        THEN TruePositives * 1.0 / (TruePositives + FalsePositives) ELSE 0 END AS DECIMAL(5,4)) PERSISTED,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [ConfidenceThreshold] DECIMAL(5,4) NOT NULL DEFAULT 0.7,
    [LastTriggered] DATETIME2 NULL,
    [TriggerCount] INT NOT NULL DEFAULT 0,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    INDEX [IX_GapPatterns_Type] NONCLUSTERED ([PatternType]),
    INDEX [IX_GapPatterns_Active] NONCLUSTERED ([IsActive]) WHERE [IsActive] = 1
);
GO

-- Seed initial detection patterns
IF NOT EXISTS (SELECT 1 FROM DaQa.GapPatterns)
INSERT INTO [DaQa].[GapPatterns] ([PatternName], [PatternType], [PatternDescription], [DetectionRules]) VALUES
('Undocumented High-Use Table', 'USAGE', 'Tables queried frequently but lacking documentation', '{"min_query_count": 100, "lookback_days": 30}'),
('Orphaned Stored Procedure', 'STRUCTURAL', 'Procedures with no MasterIndex entry', '{"object_type": "P", "require_masterindex": true}'),
('Stale Documentation', 'TEMPORAL', 'Documentation older than schema change', '{"max_drift_days": 90}'),
('Cluster Outlier', 'SEMANTIC', 'Undocumented object in documented cluster', '{"min_cluster_coverage": 0.8}'),
('High-Impact Undocumented', 'LINEAGE', 'Many dependents but no documentation', '{"min_dependents": 10}'),
('PII Column Undocumented', 'COMPLIANCE', 'PII columns without classification', '{"pii_patterns": ["SSN", "email", "phone"]}'),
('Dark Database Zone', 'LINEAGE', 'Tables with no lineage connections', '{"max_connections": 0, "min_columns": 5}');
GO

PRINT 'Created DaQa.GapPatterns with seed data';
GO

-- 2. OBJECT IMPORTANCE SCORES - ML-derived rankings
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ObjectImportanceScores' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[ObjectImportanceScores] (
    [ScoreId] INT IDENTITY(1,1) PRIMARY KEY,
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [ObjectType] NVARCHAR(20) NOT NULL,
    [UsageScore] INT NOT NULL DEFAULT 0,           -- 25% weight
    [DependencyScore] INT NOT NULL DEFAULT 0,      -- 20% weight
    [DataVolumeScore] INT NOT NULL DEFAULT 0,      -- 10% weight
    [ChangeFrequencyScore] INT NOT NULL DEFAULT 0, -- 10% weight
    [BusinessCriticalityScore] INT NOT NULL DEFAULT 0, -- 15% weight
    [PIIScore] INT NOT NULL DEFAULT 0,             -- 10% weight
    [LineageDepthScore] INT NOT NULL DEFAULT 0,    -- 10% weight
    [CompositeScore] AS ((UsageScore * 0.25) + (DependencyScore * 0.20) + (DataVolumeScore * 0.10) +
        (ChangeFrequencyScore * 0.10) + (BusinessCriticalityScore * 0.15) + (PIIScore * 0.10) + (LineageDepthScore * 0.10)) PERSISTED,
    [HasMasterIndexEntry] BIT NOT NULL DEFAULT 0,
    [GapPriority] AS (CASE WHEN HasMasterIndexEntry = 0 THEN CompositeScore * 1.5 ELSE CompositeScore * 0.5 END) PERSISTED,
    [LastCalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [UQ_ObjectImportance] UNIQUE ([SchemaName], [ObjectName], [ObjectType]),
    INDEX [IX_ObjectImportance_Priority] NONCLUSTERED ([GapPriority] DESC)
);
GO

PRINT 'Created DaQa.ObjectImportanceScores';
GO

-- 3. DOCUMENTATION VELOCITY - Tracking doc creation rates by group
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentationVelocity' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[DocumentationVelocity] (
    [VelocityId] INT IDENTITY(1,1) PRIMARY KEY,
    [GroupType] NVARCHAR(20) NOT NULL, -- SCHEMA, TEAM, DOMAIN
    [GroupName] NVARCHAR(128) NOT NULL,
    [DocsCreated30d] INT NOT NULL DEFAULT 0,
    [DocsApproved30d] INT NOT NULL DEFAULT 0,
    [AvgApprovalTimeHours] DECIMAL(10,2) NULL,
    [TotalObjects] INT NOT NULL DEFAULT 0,
    [DocumentedObjects] INT NOT NULL DEFAULT 0,
    [CoveragePercent] AS (CASE WHEN TotalObjects > 0 THEN CAST(DocumentedObjects * 100.0 / TotalObjects AS DECIMAL(5,2)) ELSE 0 END) PERSISTED,
    [VelocityTrend] NVARCHAR(10) NULL, -- INCREASING, STABLE, DECLINING
    [DaysToFullCoverage] INT NULL,
    [LastCalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [UQ_DocVelocity] UNIQUE ([GroupType], [GroupName])
);
GO

PRINT 'Created DaQa.DocumentationVelocity';
GO

-- 4. SEMANTIC CLUSTERS - Grouping similar objects by embeddings
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SemanticClusters' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[SemanticClusters] (
    [ClusterId] INT IDENTITY(1,1) PRIMARY KEY,
    [ClusterName] NVARCHAR(200) NOT NULL,
    [DomainTag] NVARCHAR(100) NULL,
    [MemberCount] INT NOT NULL DEFAULT 0,
    [DocumentedCount] INT NOT NULL DEFAULT 0,
    [CoveragePercent] AS (CASE WHEN MemberCount > 0 THEN CAST(DocumentedCount * 100.0 / MemberCount AS DECIMAL(5,2)) ELSE 0 END) PERSISTED,
    [CentroidEmbedding] VARBINARY(MAX) NULL,
    [OutlierCount] INT NOT NULL DEFAULT 0,
    [LastClusteredAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

PRINT 'Created DaQa.SemanticClusters';
GO

-- 5. CLUSTER MEMBERSHIPS - Object-to-cluster associations
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ClusterMemberships' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[ClusterMemberships] (
    [MembershipId] INT IDENTITY(1,1) PRIMARY KEY,
    [ClusterId] INT NOT NULL REFERENCES [DaQa].[SemanticClusters]([ClusterId]),
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [ObjectType] NVARCHAR(20) NOT NULL,
    [DistanceFromCentroid] DECIMAL(10,6) NULL,
    [IsOutlier] BIT NOT NULL DEFAULT 0,
    [IsDocumented] BIT NOT NULL DEFAULT 0,
    [ObjectEmbedding] VARBINARY(MAX) NULL,
    CONSTRAINT [UQ_ClusterMembership] UNIQUE ([ClusterId], [SchemaName], [ObjectName])
);
GO

PRINT 'Created DaQa.ClusterMemberships';
GO

-- 6. GAP FEEDBACK - RLHF learning from human feedback
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GapFeedback' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[GapFeedback] (
    [FeedbackId] INT IDENTITY(1,1) PRIMARY KEY,
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [PatternId] INT NULL REFERENCES [DaQa].[GapPatterns]([PatternId]),
    [DetectedGapType] NVARCHAR(50) NOT NULL,
    [DetectedConfidence] DECIMAL(5,4) NOT NULL,
    [FeedbackType] NVARCHAR(20) NOT NULL, -- CONFIRMED, REJECTED, DEFERRED
    [FeedbackBy] NVARCHAR(100) NOT NULL,
    [FeedbackAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [FeedbackReason] NVARCHAR(500) NULL,
    [WasCorrectPrediction] AS (CASE WHEN FeedbackType = 'CONFIRMED' THEN 1 ELSE 0 END) PERSISTED,
    [PatternAdjustmentApplied] BIT NOT NULL DEFAULT 0
);
GO

PRINT 'Created DaQa.GapFeedback';
GO

-- 7. USAGE HEATMAP - DMV-based query statistics
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UsageHeatmap' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[UsageHeatmap] (
    [HeatmapId] INT IDENTITY(1,1) PRIMARY KEY,
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [ObjectType] NVARCHAR(20) NOT NULL,
    [ExecutionCount30d] BIGINT NOT NULL DEFAULT 0,
    [AvgCpuTimeMs] DECIMAL(18,4) NULL,
    [AvgLogicalReads] BIGINT NULL,
    [UniqueUsers7d] INT NULL,
    [HeatScore] AS (LOG10(NULLIF(ExecutionCount30d, 0) + 1) * 20 + ISNULL(UniqueUsers7d, 0) * 2) PERSISTED,
    [UsageTrend] NVARCHAR(10) NULL, -- INCREASING, STABLE, DECLINING
    [LastRefreshedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [UQ_UsageHeatmap] UNIQUE ([SchemaName], [ObjectName], [ObjectType]),
    INDEX [IX_UsageHeatmap_Heat] NONCLUSTERED ([HeatScore] DESC)
);
GO

PRINT 'Created DaQa.UsageHeatmap';
GO

-- 8. PREDICTED GAPS - Future gap predictions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PredictedGaps' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[PredictedGaps] (
    [PredictionId] INT IDENTITY(1,1) PRIMARY KEY,
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [ObjectType] NVARCHAR(20) NOT NULL,
    [PredictedGapType] NVARCHAR(50) NOT NULL,
    [PredictionConfidence] DECIMAL(5,4) NOT NULL,
    [DaysUntilGap] INT NULL,
    [RiskFactors] NVARCHAR(MAX) NULL,
    [RecommendedPriority] NVARCHAR(10) NULL,
    [PredictedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ExpiresAt] DATETIME2 NULL
);
GO

PRINT 'Created DaQa.PredictedGaps';
GO

-- 9. GAP DETECTION RUNS - Audit log of detection runs
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GapDetectionRuns' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[GapDetectionRuns] (
    [RunId] INT IDENTITY(1,1) PRIMARY KEY,
    [RunType] NVARCHAR(20) NOT NULL, -- FULL, INCREMENTAL
    [StartedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CompletedAt] DATETIME2 NULL,
    [DurationMs] INT NULL,
    [ObjectsScanned] INT NOT NULL DEFAULT 0,
    [GapsDetected] INT NOT NULL DEFAULT 0,
    [NewGaps] INT NOT NULL DEFAULT 0,
    [ResolvedGaps] INT NOT NULL DEFAULT 0,
    [PatternResults] NVARCHAR(MAX) NULL,
    [Errors] NVARCHAR(MAX) NULL
);
GO

PRINT 'Created DaQa.GapDetectionRuns';
GO

-- 10. DETECTED GAPS - Current gap inventory
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DetectedGaps' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[DetectedGaps] (
    [GapId] INT IDENTITY(1,1) PRIMARY KEY,
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [ObjectType] NVARCHAR(20) NOT NULL,
    [ColumnName] NVARCHAR(128) NULL,
    [GapType] NVARCHAR(50) NOT NULL,
    [PatternId] INT NULL REFERENCES [DaQa].[GapPatterns]([PatternId]),
    [Severity] NVARCHAR(10) NOT NULL, -- CRITICAL, HIGH, MEDIUM, LOW
    [Priority] INT NOT NULL,
    [DetectedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [DetectionRunId] INT NULL REFERENCES [DaQa].[GapDetectionRuns]([RunId]),
    [Confidence] DECIMAL(5,4) NOT NULL,
    [Evidence] NVARCHAR(MAX) NULL,
    [Status] NVARCHAR(20) NOT NULL DEFAULT 'OPEN', -- OPEN, IN_PROGRESS, RESOLVED, REJECTED
    [AssignedTo] NVARCHAR(100) NULL,
    [ResolvedAt] DATETIME2 NULL,
    [AgeInDays] AS DATEDIFF(DAY, DetectedAt, GETUTCDATE()) PERSISTED,
    INDEX [IX_DetectedGaps_Status] NONCLUSTERED ([Status], [Priority] DESC),
    INDEX [IX_DetectedGaps_Object] NONCLUSTERED ([SchemaName], [ObjectName])
);
GO

PRINT 'Created DaQa.DetectedGaps';
GO

-- 11. GAP INTELLIGENCE QUEUE - DDL change queue for event processing
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GapIntelligenceQueue' AND schema_id = SCHEMA_ID('DaQa'))
CREATE TABLE [DaQa].[GapIntelligenceQueue] (
    [QueueId] INT IDENTITY(1,1) PRIMARY KEY,
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [ObjectType] NVARCHAR(50) NOT NULL,
    [EventType] NVARCHAR(50) NOT NULL,
    [QueuedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ProcessedAt] DATETIME2 NULL,
    [Status] NVARCHAR(20) NOT NULL DEFAULT 'PENDING',
    INDEX [IX_GapQueue_Status] NONCLUSTERED ([Status], [QueuedAt])
);
GO

PRINT 'Created DaQa.GapIntelligenceQueue';
GO

PRINT '============================================================================';
PRINT 'Gap Intelligence Agent tables created successfully!';
PRINT '11 tables: GapPatterns, ObjectImportanceScores, DocumentationVelocity,';
PRINT '           SemanticClusters, ClusterMemberships, GapFeedback, UsageHeatmap,';
PRINT '           PredictedGaps, GapDetectionRuns, DetectedGaps, GapIntelligenceQueue';
PRINT '============================================================================';
GO
