-- ============================================
-- MIGRATION 007: Data Lineage Tracer (Agent #3)
-- Created: 2026-01-13
-- Purpose: Column-level lineage tracking via ScriptDom AST parsing
-- Tables: 8 new tables for comprehensive lineage analysis
-- ============================================

USE [IRFS1];
GO

PRINT '=== Migration 007: Creating Lineage Tables ===';
PRINT '';

-- ===== TABLE 1: ObjectDependencies =====
-- Table-level dependencies from sys.sql_expression_dependencies
-- Foundation for impact analysis and lineage graphs

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ObjectDependencies' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[ObjectDependencies] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [ReferencingSchema] NVARCHAR(128) NOT NULL,
        [ReferencingObject] NVARCHAR(128) NOT NULL,
        [ReferencingType] NVARCHAR(50) NOT NULL, -- 'Table', 'View', 'Procedure', 'Function'
        [ReferencedSchema] NVARCHAR(128) NOT NULL,
        [ReferencedObject] NVARCHAR(128) NOT NULL,
        [ReferencedType] NVARCHAR(50) NOT NULL,
        [DependencyType] NVARCHAR(50) NOT NULL, -- 'REFERENCES', 'SELECTS_FROM', 'INSERTS_INTO', 'UPDATES', 'DELETES'
        [IsAmbiguous] BIT NOT NULL DEFAULT 0,
        [IsCrossDatabaseReference] BIT NOT NULL DEFAULT 0,
        [ReferencedDatabase] NVARCHAR(128) NULL,
        [DiscoveredAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [LastVerifiedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [SourceScanId] UNIQUEIDENTIFIER NULL,

        INDEX IX_ObjectDeps_Referencing (ReferencingSchema, ReferencingObject),
        INDEX IX_ObjectDeps_Referenced (ReferencedSchema, ReferencedObject),
        INDEX IX_ObjectDeps_Type (DependencyType),
        INDEX IX_ObjectDeps_ScanId (SourceScanId)
    );
    PRINT '  Created table [DaQa].[ObjectDependencies]';
END
GO

-- ===== TABLE 2: ColumnLineage =====
-- Column-level operations tracking (READ, UPDATE, INSERT, DELETE)
-- Core table for fine-grained lineage analysis

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColumnLineage' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[ColumnLineage] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [LineageId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [ProcedureSchema] NVARCHAR(128) NOT NULL,
        [ProcedureName] NVARCHAR(128) NOT NULL,
        [SourceSchema] NVARCHAR(128) NULL,
        [SourceTable] NVARCHAR(128) NULL,
        [SourceColumn] NVARCHAR(128) NULL,
        [TargetSchema] NVARCHAR(128) NULL,
        [TargetTable] NVARCHAR(128) NULL,
        [TargetColumn] NVARCHAR(128) NULL,
        [OperationType] NVARCHAR(20) NOT NULL, -- 'READ', 'INSERT', 'UPDATE', 'DELETE', 'MERGE_UPDATE', 'MERGE_INSERT'
        [TransformationExpression] NVARCHAR(MAX) NULL, -- SQL expression if not direct mapping
        [StatementIndex] INT NOT NULL DEFAULT 0, -- Order within procedure
        [LineNumber] INT NULL, -- Source line in procedure
        [IsPiiColumn] BIT NOT NULL DEFAULT 0,
        [PiiType] NVARCHAR(50) NULL, -- 'SSN', 'Email', 'Phone', 'Address', 'DOB', 'FinancialAccount'
        [RiskWeight] INT NOT NULL DEFAULT 1, -- READ=1, INSERT=2, UPDATE=3, DELETE=5
        [DiscoveredAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [SourceScanId] UNIQUEIDENTIFIER NULL,

        INDEX IX_ColumnLineage_LineageId UNIQUE (LineageId),
        INDEX IX_ColumnLineage_Procedure (ProcedureSchema, ProcedureName),
        INDEX IX_ColumnLineage_Source (SourceSchema, SourceTable, SourceColumn),
        INDEX IX_ColumnLineage_Target (TargetSchema, TargetTable, TargetColumn),
        INDEX IX_ColumnLineage_Operation (OperationType),
        INDEX IX_ColumnLineage_Pii (IsPiiColumn) WHERE IsPiiColumn = 1,
        INDEX IX_ColumnLineage_ScanId (SourceScanId)
    );
    PRINT '  Created table [DaQa].[ColumnLineage]';
END
GO

-- ===== TABLE 3: LineageScanHistory =====
-- Saga state and progress tracking for lineage scans

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LineageScanHistory' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[LineageScanHistory] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [ScanId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [ScanType] NVARCHAR(50) NOT NULL, -- 'Full', 'Incremental', 'Schema', 'Object'
        [ScanStatus] NVARCHAR(50) NOT NULL, -- 'Pending', 'Parsing', 'Building', 'Indexing', 'Completed', 'Failed', 'Cancelled'
        [SchemaFilter] NVARCHAR(128) NULL,
        [ObjectFilter] NVARCHAR(128) NULL,
        [TotalObjects] INT NOT NULL DEFAULT 0,
        [ProcessedObjects] INT NOT NULL DEFAULT 0,
        [CurrentObject] NVARCHAR(256) NULL,
        [NodesCreated] INT NOT NULL DEFAULT 0,
        [EdgesCreated] INT NOT NULL DEFAULT 0,
        [PiiColumnsFound] INT NOT NULL DEFAULT 0,
        [DynamicSqlCount] INT NOT NULL DEFAULT 0,
        [ErrorCount] INT NOT NULL DEFAULT 0,
        [StartedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt] DATETIME2 NULL,
        [StartedBy] NVARCHAR(100) NOT NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [SagaState] NVARCHAR(MAX) NULL, -- JSON serialized state
        [CorrelationId] UNIQUEIDENTIFIER NULL,
        [ParentScanId] UNIQUEIDENTIFIER NULL, -- For child scans

        INDEX IX_ScanHistory_ScanId UNIQUE (ScanId),
        INDEX IX_ScanHistory_Status (ScanStatus),
        INDEX IX_ScanHistory_StartedAt (StartedAt DESC),
        INDEX IX_ScanHistory_Correlation (CorrelationId)
    );
    PRINT '  Created table [DaQa].[LineageScanHistory]';
END
GO

-- ===== TABLE 4: DynamicSqlProcedures =====
-- Track procedures with dynamic SQL that need manual analysis

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DynamicSqlProcedures' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[DynamicSqlProcedures] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SchemaName] NVARCHAR(128) NOT NULL,
        [ProcedureName] NVARCHAR(128) NOT NULL,
        [DynamicSqlType] NVARCHAR(50) NOT NULL, -- 'sp_executesql', 'EXEC_string', 'EXEC_variable', 'OPENQUERY'
        [DetectedPattern] NVARCHAR(500) NULL,
        [LineNumber] INT NULL,
        [RiskLevel] NVARCHAR(20) NOT NULL, -- 'Low', 'Medium', 'High', 'Critical'
        [ManuallyReviewed] BIT NOT NULL DEFAULT 0,
        [ReviewedBy] NVARCHAR(100) NULL,
        [ReviewedAt] DATETIME2 NULL,
        [ReviewNotes] NVARCHAR(MAX) NULL,
        [KnownTargets] NVARCHAR(MAX) NULL, -- JSON: known tables accessed dynamically
        [DetectedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [SourceScanId] UNIQUEIDENTIFIER NULL,

        INDEX IX_DynamicSql_Schema_Proc UNIQUE (SchemaName, ProcedureName, DynamicSqlType, LineNumber),
        INDEX IX_DynamicSql_Risk (RiskLevel),
        INDEX IX_DynamicSql_Unreviewed (ManuallyReviewed) WHERE ManuallyReviewed = 0
    );
    PRINT '  Created table [DaQa].[DynamicSqlProcedures]';
END
GO

-- ===== TABLE 5: LineageGraphNodes =====
-- Graph nodes specifically for lineage visualization (extends GraphNodes)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LineageGraphNodes' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[LineageGraphNodes] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [NodeId] NVARCHAR(500) NOT NULL, -- Fully qualified: [db].[schema].[object].[column]
        [NodeType] NVARCHAR(50) NOT NULL, -- 'Table', 'Column', 'Procedure', 'View', 'Function'
        [DatabaseName] NVARCHAR(128) NULL,
        [SchemaName] NVARCHAR(128) NOT NULL,
        [ObjectName] NVARCHAR(128) NOT NULL,
        [ColumnName] NVARCHAR(128) NULL, -- NULL for table/proc level nodes
        [DisplayName] NVARCHAR(256) NOT NULL,
        [IsPiiNode] BIT NOT NULL DEFAULT 0,
        [PiiType] NVARCHAR(50) NULL,
        [DataClassification] NVARCHAR(50) NULL, -- 'Public', 'Internal', 'Confidential', 'Restricted'
        [Properties] NVARCHAR(MAX) NULL, -- JSON: {dataType, maxLength, isNullable, defaultValue}
        [RiskScore] INT NOT NULL DEFAULT 0, -- Pre-computed from lineage edges
        [InDegree] INT NOT NULL DEFAULT 0, -- Count of incoming edges
        [OutDegree] INT NOT NULL DEFAULT 0, -- Count of outgoing edges
        [ClusterGroup] NVARCHAR(100) NULL, -- For graph layout clustering
        [GraphNodeId] INT NULL, -- FK to DaQa.GraphNodes for GraphRAG sync
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_LineageNode_NodeId UNIQUE (NodeId),
        INDEX IX_LineageNode_Type (NodeType),
        INDEX IX_LineageNode_Object (SchemaName, ObjectName),
        INDEX IX_LineageNode_Column (SchemaName, ObjectName, ColumnName) WHERE ColumnName IS NOT NULL,
        INDEX IX_LineageNode_Pii (IsPiiNode) WHERE IsPiiNode = 1,
        INDEX IX_LineageNode_Risk (RiskScore DESC),
        INDEX IX_LineageNode_GraphSync (GraphNodeId)
    );
    PRINT '  Created table [DaQa].[LineageGraphNodes]';
END
GO

-- ===== TABLE 6: LineageGraphEdges =====
-- Graph edges for lineage relationships

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LineageGraphEdges' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[LineageGraphEdges] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EdgeId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [SourceNodeId] NVARCHAR(500) NOT NULL,
        [TargetNodeId] NVARCHAR(500) NOT NULL,
        [EdgeType] NVARCHAR(50) NOT NULL, -- 'USES', 'PRODUCES', 'TRANSFORMS', 'READS', 'WRITES', 'PII_FLOW'
        [OperationType] NVARCHAR(20) NULL, -- 'READ', 'INSERT', 'UPDATE', 'DELETE'
        [TransformationHint] NVARCHAR(500) NULL, -- Brief description of transformation
        [SourceProcedure] NVARCHAR(256) NULL, -- Which procedure creates this edge
        [Weight] DECIMAL(5,2) NOT NULL DEFAULT 1.0, -- For PageRank/importance
        [IsPiiFlow] BIT NOT NULL DEFAULT 0,
        [Properties] NVARCHAR(MAX) NULL, -- JSON metadata
        [GraphEdgeId] INT NULL, -- FK to DaQa.GraphEdges for GraphRAG sync
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_LineageEdge_EdgeId UNIQUE (EdgeId),
        INDEX IX_LineageEdge_Source (SourceNodeId),
        INDEX IX_LineageEdge_Target (TargetNodeId),
        INDEX IX_LineageEdge_Type (EdgeType),
        INDEX IX_LineageEdge_PiiFlow (IsPiiFlow) WHERE IsPiiFlow = 1,
        INDEX IX_LineageEdge_Proc (SourceProcedure)
    );
    PRINT '  Created table [DaQa].[LineageGraphEdges]';
END
GO

-- ===== TABLE 7: OpenLineageEvents =====
-- OpenLineage standard compatibility for external tool integration

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OpenLineageEvents' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[OpenLineageEvents] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EventId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [EventType] NVARCHAR(50) NOT NULL, -- 'START', 'RUNNING', 'COMPLETE', 'FAIL', 'ABORT'
        [EventTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Producer] NVARCHAR(256) NOT NULL DEFAULT 'enterprise-documentation-platform',
        [SchemaURL] NVARCHAR(500) NOT NULL DEFAULT 'https://openlineage.io/spec/1-0-5/OpenLineage.json',
        [JobNamespace] NVARCHAR(256) NOT NULL,
        [JobName] NVARCHAR(256) NOT NULL,
        [RunId] UNIQUEIDENTIFIER NOT NULL,
        [InputDatasets] NVARCHAR(MAX) NULL, -- JSON array of input datasets
        [OutputDatasets] NVARCHAR(MAX) NULL, -- JSON array of output datasets
        [ColumnLineage] NVARCHAR(MAX) NULL, -- JSON facet for column lineage
        [CustomFacets] NVARCHAR(MAX) NULL, -- Additional facets
        [PublishedAt] DATETIME2 NULL, -- When sent to external system
        [PublishStatus] NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Published', 'Failed'
        [PublishError] NVARCHAR(MAX) NULL,

        INDEX IX_OpenLineage_EventId UNIQUE (EventId),
        INDEX IX_OpenLineage_RunId (RunId),
        INDEX IX_OpenLineage_Job (JobNamespace, JobName),
        INDEX IX_OpenLineage_EventTime (EventTime DESC),
        INDEX IX_OpenLineage_PublishStatus (PublishStatus) WHERE PublishStatus != 'Published'
    );
    PRINT '  Created table [DaQa].[OpenLineageEvents]';
END
GO

-- ===== TABLE 8: ColumnRiskScores =====
-- Pre-computed impact analysis scores per column

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColumnRiskScores' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[ColumnRiskScores] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SchemaName] NVARCHAR(128) NOT NULL,
        [TableName] NVARCHAR(128) NOT NULL,
        [ColumnName] NVARCHAR(128) NOT NULL,
        [DirectDependentCount] INT NOT NULL DEFAULT 0,
        [TransitiveDependentCount] INT NOT NULL DEFAULT 0,
        [ReadOperations] INT NOT NULL DEFAULT 0,
        [WriteOperations] INT NOT NULL DEFAULT 0,
        [DeleteOperations] INT NOT NULL DEFAULT 0,
        [AffectedProcedures] INT NOT NULL DEFAULT 0,
        [AffectedViews] INT NOT NULL DEFAULT 0,
        [PiiExposureCount] INT NOT NULL DEFAULT 0, -- How many PII paths involve this column
        [RiskScore] INT NOT NULL DEFAULT 0, -- Composite score
        [ImpactLevel] AS (
            CASE
                WHEN RiskScore >= 100 THEN 'Critical'
                WHEN RiskScore >= 50 THEN 'High'
                WHEN RiskScore >= 20 THEN 'Medium'
                ELSE 'Low'
            END
        ) PERSISTED,
        [LastCalculatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [SourceScanId] UNIQUEIDENTIFIER NULL,

        INDEX IX_RiskScores_Column UNIQUE (SchemaName, TableName, ColumnName),
        INDEX IX_RiskScores_Score (RiskScore DESC),
        INDEX IX_RiskScores_Impact (ImpactLevel)
    );
    PRINT '  Created table [DaQa].[ColumnRiskScores]';
END
GO

-- ===== Stored Procedures for Lineage Operations =====

-- SP: Get column lineage graph
IF OBJECT_ID('DaQa.usp_GetColumnLineage', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_GetColumnLineage;
GO

CREATE PROCEDURE DaQa.usp_GetColumnLineage
    @SchemaName NVARCHAR(128),
    @TableName NVARCHAR(128),
    @ColumnName NVARCHAR(128) = NULL,
    @MaxDepth INT = 5,
    @Direction NVARCHAR(20) = 'Both' -- 'Upstream', 'Downstream', 'Both'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartNodeId NVARCHAR(500) =
        CASE
            WHEN @ColumnName IS NOT NULL THEN CONCAT(@SchemaName, '.', @TableName, '.', @ColumnName)
            ELSE CONCAT(@SchemaName, '.', @TableName)
        END;

    -- CTE for recursive traversal
    WITH LineageCTE AS (
        -- Anchor: Start node
        SELECT
            n.NodeId,
            n.NodeType,
            n.DisplayName,
            n.IsPiiNode,
            n.PiiType,
            n.RiskScore,
            CAST(n.NodeId AS NVARCHAR(MAX)) AS Path,
            0 AS Depth,
            'Start' AS Direction
        FROM DaQa.LineageGraphNodes n
        WHERE n.NodeId = @StartNodeId

        UNION ALL

        -- Recursive: Upstream (dependencies)
        SELECT
            n.NodeId,
            n.NodeType,
            n.DisplayName,
            n.IsPiiNode,
            n.PiiType,
            n.RiskScore,
            cte.Path + ' <- ' + n.NodeId,
            cte.Depth + 1,
            'Upstream'
        FROM LineageCTE cte
        JOIN DaQa.LineageGraphEdges e ON e.TargetNodeId = cte.NodeId
        JOIN DaQa.LineageGraphNodes n ON n.NodeId = e.SourceNodeId
        WHERE cte.Depth < @MaxDepth
          AND @Direction IN ('Both', 'Upstream')
          AND CHARINDEX(n.NodeId, cte.Path) = 0 -- Prevent cycles

        UNION ALL

        -- Recursive: Downstream (dependents)
        SELECT
            n.NodeId,
            n.NodeType,
            n.DisplayName,
            n.IsPiiNode,
            n.PiiType,
            n.RiskScore,
            cte.Path + ' -> ' + n.NodeId,
            cte.Depth + 1,
            'Downstream'
        FROM LineageCTE cte
        JOIN DaQa.LineageGraphEdges e ON e.SourceNodeId = cte.NodeId
        JOIN DaQa.LineageGraphNodes n ON n.NodeId = e.TargetNodeId
        WHERE cte.Depth < @MaxDepth
          AND @Direction IN ('Both', 'Downstream')
          AND CHARINDEX(n.NodeId, cte.Path) = 0
    )
    SELECT DISTINCT
        NodeId,
        NodeType,
        DisplayName,
        IsPiiNode,
        PiiType,
        RiskScore,
        Path,
        Depth,
        Direction
    FROM LineageCTE
    ORDER BY Depth, Direction, NodeId;

    -- Return edges for visualization
    SELECT DISTINCT
        e.EdgeId,
        e.SourceNodeId,
        e.TargetNodeId,
        e.EdgeType,
        e.OperationType,
        e.IsPiiFlow,
        e.Weight
    FROM DaQa.LineageGraphEdges e
    WHERE EXISTS (
        SELECT 1 FROM LineageCTE cte
        WHERE cte.NodeId = e.SourceNodeId OR cte.NodeId = e.TargetNodeId
    );
END
GO

PRINT '  Created procedure [DaQa].[usp_GetColumnLineage]';

-- SP: Calculate column risk scores
IF OBJECT_ID('DaQa.usp_CalculateColumnRiskScores', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_CalculateColumnRiskScores;
GO

CREATE PROCEDURE DaQa.usp_CalculateColumnRiskScores
    @ScanId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Clear existing scores for this scan
    IF @ScanId IS NOT NULL
        DELETE FROM DaQa.ColumnRiskScores WHERE SourceScanId = @ScanId;

    -- Calculate scores based on column lineage data
    INSERT INTO DaQa.ColumnRiskScores (
        SchemaName, TableName, ColumnName,
        DirectDependentCount, TransitiveDependentCount,
        ReadOperations, WriteOperations, DeleteOperations,
        AffectedProcedures, AffectedViews, PiiExposureCount,
        RiskScore, SourceScanId
    )
    SELECT
        n.SchemaName,
        n.ObjectName,
        n.ColumnName,
        n.OutDegree AS DirectDependentCount,
        (SELECT COUNT(*) FROM DaQa.LineageGraphEdges e WHERE e.SourceNodeId = n.NodeId) AS TransitiveDependentCount,
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.SourceColumn = n.ColumnName AND cl.OperationType = 'READ') AS ReadOperations,
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType IN ('INSERT', 'UPDATE', 'MERGE_UPDATE', 'MERGE_INSERT')) AS WriteOperations,
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType = 'DELETE') AS DeleteOperations,
        (SELECT COUNT(DISTINCT cl.ProcedureName) FROM DaQa.ColumnLineage cl WHERE cl.SourceColumn = n.ColumnName OR cl.TargetColumn = n.ColumnName) AS AffectedProcedures,
        0 AS AffectedViews, -- TODO: Add view tracking
        (SELECT COUNT(*) FROM DaQa.LineageGraphEdges e WHERE e.SourceNodeId = n.NodeId AND e.IsPiiFlow = 1) AS PiiExposureCount,
        -- Risk Score Formula: READ*1 + INSERT*2 + UPDATE*3 + DELETE*5 + PII*10
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.SourceColumn = n.ColumnName AND cl.OperationType = 'READ') * 1 +
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType IN ('INSERT', 'MERGE_INSERT')) * 2 +
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType IN ('UPDATE', 'MERGE_UPDATE')) * 3 +
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType = 'DELETE') * 5 +
        CASE WHEN n.IsPiiNode = 1 THEN 10 ELSE 0 END AS RiskScore,
        @ScanId
    FROM DaQa.LineageGraphNodes n
    WHERE n.NodeType = 'Column' AND n.ColumnName IS NOT NULL;

    -- Update node risk scores
    UPDATE n
    SET n.RiskScore = rs.RiskScore,
        n.UpdatedAt = GETUTCDATE()
    FROM DaQa.LineageGraphNodes n
    JOIN DaQa.ColumnRiskScores rs ON rs.SchemaName = n.SchemaName
        AND rs.TableName = n.ObjectName
        AND rs.ColumnName = n.ColumnName;

    SELECT @@ROWCOUNT AS ColumnsScored;
END
GO

PRINT '  Created procedure [DaQa].[usp_CalculateColumnRiskScores]';

-- SP: Get PII flow paths
IF OBJECT_ID('DaQa.usp_GetPiiFlowPaths', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_GetPiiFlowPaths;
GO

CREATE PROCEDURE DaQa.usp_GetPiiFlowPaths
    @SchemaName NVARCHAR(128),
    @TableName NVARCHAR(128),
    @ColumnName NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartNodeId NVARCHAR(500) = CONCAT(@SchemaName, '.', @TableName, '.', @ColumnName);

    -- Find all PII flow paths from this column
    WITH PiiFlowCTE AS (
        SELECT
            e.SourceNodeId,
            e.TargetNodeId,
            CAST(e.SourceNodeId + ' -> ' + e.TargetNodeId AS NVARCHAR(MAX)) AS FlowPath,
            1 AS PathLength
        FROM DaQa.LineageGraphEdges e
        WHERE e.SourceNodeId = @StartNodeId
          AND e.IsPiiFlow = 1

        UNION ALL

        SELECT
            cte.SourceNodeId,
            e.TargetNodeId,
            cte.FlowPath + ' -> ' + e.TargetNodeId,
            cte.PathLength + 1
        FROM PiiFlowCTE cte
        JOIN DaQa.LineageGraphEdges e ON e.SourceNodeId = cte.TargetNodeId
        WHERE e.IsPiiFlow = 1
          AND cte.PathLength < 10
          AND CHARINDEX(e.TargetNodeId, cte.FlowPath) = 0
    )
    SELECT
        FlowPath,
        PathLength,
        SourceNodeId AS StartColumn,
        TargetNodeId AS EndColumn
    FROM PiiFlowCTE
    ORDER BY PathLength;
END
GO

PRINT '  Created procedure [DaQa].[usp_GetPiiFlowPaths]';

PRINT '';
PRINT '=== Migration 007 Complete ===';
PRINT 'Created 8 tables and 3 stored procedures for Data Lineage Tracer';
GO
