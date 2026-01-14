-- ============================================================================
-- MASTER MIGRATION SCRIPT: DaQa Schema Tables
-- Enterprise Documentation Platform V2
-- Generated: 2026-01-13
--
-- This script creates all missing DaQa schema tables in the correct order.
-- Run this against the IRFS1 database.
--
-- Tables Created:
--   - Search & Discovery (Agent #6): 7 tables
--   - Data Lineage (Agent #3): 8 tables + 3 stored procedures
--   - Schema Change Detection (Agent #4): 6 tables + DDL trigger + stored procedures
--   - Dependency Tracking: 1 table + 1 view
--   - Alterations to existing tables
-- ============================================================================

USE [IRFS1];
GO

SET NOCOUNT ON;
PRINT '============================================================================';
PRINT 'MASTER MIGRATION: DaQa Schema Tables';
PRINT 'Started: ' + CONVERT(VARCHAR, GETUTCDATE(), 120);
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- PREREQUISITE: Ensure DaQa schema exists
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DaQa')
BEGIN
    EXEC('CREATE SCHEMA [DaQa]');
    PRINT 'Created DaQa schema';
END
ELSE
BEGIN
    PRINT 'DaQa schema already exists';
END
GO

-- ============================================================================
-- SECTION 1: SMART SEARCH & DISCOVERY (Agent #6)
-- Migration 006: 7 tables for hybrid search, GraphRAG, continuous learning
-- ============================================================================
PRINT '';
PRINT '=== SECTION 1: Smart Search & Discovery (Agent #6) ===';
PRINT '';

-- TABLE 1: SearchQueries
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SearchQueries' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[SearchQueries] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [QueryId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserId] NVARCHAR(100) NOT NULL,
        [QueryText] NVARCHAR(2000) NOT NULL,
        [QueryType] NVARCHAR(50) NOT NULL,
        [RoutingPath] NVARCHAR(50) NOT NULL,
        [ExecutionTimeMs] INT NOT NULL,
        [ResultCount] INT NOT NULL,
        [SearchedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [SessionId] NVARCHAR(100) NULL,
        [IPAddress] NVARCHAR(50) NULL,

        INDEX IX_SearchQueries_QueryId UNIQUE (QueryId),
        INDEX IX_SearchQueries_UserId (UserId),
        INDEX IX_SearchQueries_SearchedAt (SearchedAt DESC),
        INDEX IX_SearchQueries_QueryType (QueryType)
    );
    PRINT '  Created [DaQa].[SearchQueries]';
END
ELSE PRINT '  [DaQa].[SearchQueries] already exists';
GO

-- TABLE 2: SearchResults
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SearchResults' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[SearchResults] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [QueryId] UNIQUEIDENTIFIER NOT NULL,
        [DocumentId] NVARCHAR(100) NOT NULL,
        [ObjectType] NVARCHAR(50) NOT NULL,
        [ObjectName] NVARCHAR(255) NOT NULL,
        [SchemaName] NVARCHAR(50) NULL,
        [Rank] INT NOT NULL,
        [RelevanceScore] DECIMAL(5,4) NOT NULL,
        [WasClicked] BIT NOT NULL DEFAULT 0,
        [TimeSpentSeconds] INT NULL,
        [WasExported] BIT NOT NULL DEFAULT 0,
        [WasShared] BIT NOT NULL DEFAULT 0,

        INDEX IX_SearchResults_QueryId (QueryId),
        INDEX IX_SearchResults_WasClicked (WasClicked, Rank)
    );
    PRINT '  Created [DaQa].[SearchResults]';
END
ELSE PRINT '  [DaQa].[SearchResults] already exists';
GO

-- TABLE 3: UserInteractions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserInteractions' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[UserInteractions] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [InteractionId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [QueryId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] NVARCHAR(100) NOT NULL,
        [InteractionType] NVARCHAR(50) NOT NULL,
        [DocumentId] NVARCHAR(100) NULL,
        [InteractionData] NVARCHAR(MAX) NULL,
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_UserInteractions_QueryId (QueryId),
        INDEX IX_UserInteractions_UserId (UserId),
        INDEX IX_UserInteractions_Timestamp (Timestamp DESC)
    );
    PRINT '  Created [DaQa].[UserInteractions]';
END
ELSE PRINT '  [DaQa].[UserInteractions] already exists';
GO

-- TABLE 4: EmbeddingCache
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmbeddingCache' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[EmbeddingCache] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [DocumentId] NVARCHAR(100) NOT NULL,
        [EmbeddingType] NVARCHAR(50) NOT NULL,
        [EmbeddingText] NVARCHAR(MAX) NOT NULL,
        [EmbeddingVector] VARBINARY(MAX) NULL,
        [QdrantPointId] NVARCHAR(100) NOT NULL,
        [ModelVersion] NVARCHAR(50) NOT NULL,
        [GeneratedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsStale] BIT NOT NULL DEFAULT 0,

        INDEX UX_EmbeddingCache_Document_Type UNIQUE (DocumentId, EmbeddingType),
        INDEX IX_EmbeddingCache_QdrantPointId (QdrantPointId),
        INDEX IX_EmbeddingCache_IsStale (IsStale)
    );
    PRINT '  Created [DaQa].[EmbeddingCache]';
END
ELSE PRINT '  [DaQa].[EmbeddingCache] already exists';
GO

-- TABLE 5: CategorySuggestions
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CategorySuggestions' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[CategorySuggestions] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SuggestionId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [DocumentId] NVARCHAR(100) NOT NULL,
        [CurrentCategory] NVARCHAR(100) NULL,
        [SuggestedCategory] NVARCHAR(100) NOT NULL,
        [ConfidenceScore] DECIMAL(5,4) NOT NULL,
        [Reasoning] NVARCHAR(MAX) NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [ReviewedBy] NVARCHAR(100) NULL,
        [ReviewedAt] DATETIME2 NULL,
        [ReviewNotes] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_CategorySuggestions_Status (Status),
        INDEX IX_CategorySuggestions_ConfidenceScore (ConfidenceScore DESC),
        INDEX IX_CategorySuggestions_DocumentId (DocumentId)
    );
    PRINT '  Created [DaQa].[CategorySuggestions]';
END
ELSE PRINT '  [DaQa].[CategorySuggestions] already exists';
GO

-- TABLE 6: GraphNodes
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GraphNodes' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[GraphNodes] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [NodeId] NVARCHAR(100) NOT NULL,
        [NodeType] NVARCHAR(50) NOT NULL,
        [NodeName] NVARCHAR(255) NOT NULL,
        [SchemaName] NVARCHAR(50) NULL,
        [DatabaseName] NVARCHAR(100) NULL,
        [Properties] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX UX_GraphNodes_NodeId UNIQUE (NodeId),
        INDEX IX_GraphNodes_NodeType (NodeType),
        INDEX IX_GraphNodes_NodeName (NodeName)
    );
    PRINT '  Created [DaQa].[GraphNodes]';
END
ELSE PRINT '  [DaQa].[GraphNodes] already exists';
GO

-- TABLE 7: GraphEdges
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GraphEdges' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[GraphEdges] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EdgeId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [SourceNodeId] NVARCHAR(100) NOT NULL,
        [TargetNodeId] NVARCHAR(100) NOT NULL,
        [EdgeType] NVARCHAR(50) NOT NULL,
        [EdgeWeight] DECIMAL(5,4) NULL,
        [Properties] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_GraphEdges_Source (SourceNodeId),
        INDEX IX_GraphEdges_Target (TargetNodeId),
        INDEX IX_GraphEdges_Type (EdgeType),
        INDEX IX_GraphEdges_PiiFlow (EdgeType) WHERE EdgeType = 'PII_FLOW'
    );
    PRINT '  Created [DaQa].[GraphEdges]';
END
ELSE PRINT '  [DaQa].[GraphEdges] already exists';
GO

-- Foreign Keys for GraphEdges
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_GraphEdges_SourceNode')
BEGIN
    ALTER TABLE [DaQa].[GraphEdges]
    ADD CONSTRAINT FK_GraphEdges_SourceNode
    FOREIGN KEY (SourceNodeId) REFERENCES [DaQa].[GraphNodes](NodeId);
    PRINT '  Added FK_GraphEdges_SourceNode';
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_GraphEdges_TargetNode')
BEGIN
    ALTER TABLE [DaQa].[GraphEdges]
    ADD CONSTRAINT FK_GraphEdges_TargetNode
    FOREIGN KEY (TargetNodeId) REFERENCES [DaQa].[GraphNodes](NodeId);
    PRINT '  Added FK_GraphEdges_TargetNode';
END
GO

PRINT '';
PRINT '  Section 1 Complete: 7 Search & Discovery tables';

-- ============================================================================
-- SECTION 2: DATA LINEAGE TRACER (Agent #3)
-- Migration 007: 8 tables + 3 stored procedures for column-level lineage
-- ============================================================================
PRINT '';
PRINT '=== SECTION 2: Data Lineage Tracer (Agent #3) ===';
PRINT '';

-- TABLE 1: ObjectDependencies
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ObjectDependencies' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[ObjectDependencies] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [ReferencingSchema] NVARCHAR(128) NOT NULL,
        [ReferencingObject] NVARCHAR(128) NOT NULL,
        [ReferencingType] NVARCHAR(50) NOT NULL,
        [ReferencedSchema] NVARCHAR(128) NOT NULL,
        [ReferencedObject] NVARCHAR(128) NOT NULL,
        [ReferencedType] NVARCHAR(50) NOT NULL,
        [DependencyType] NVARCHAR(50) NOT NULL,
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
    PRINT '  Created [DaQa].[ObjectDependencies]';
END
ELSE PRINT '  [DaQa].[ObjectDependencies] already exists';
GO

-- TABLE 2: ColumnLineage
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
        [OperationType] NVARCHAR(20) NOT NULL,
        [TransformationExpression] NVARCHAR(MAX) NULL,
        [StatementIndex] INT NOT NULL DEFAULT 0,
        [LineNumber] INT NULL,
        [IsPiiColumn] BIT NOT NULL DEFAULT 0,
        [PiiType] NVARCHAR(50) NULL,
        [RiskWeight] INT NOT NULL DEFAULT 1,
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
    PRINT '  Created [DaQa].[ColumnLineage]';
END
ELSE PRINT '  [DaQa].[ColumnLineage] already exists';
GO

-- TABLE 3: LineageScanHistory
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LineageScanHistory' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[LineageScanHistory] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [ScanId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [ScanType] NVARCHAR(50) NOT NULL,
        [ScanStatus] NVARCHAR(50) NOT NULL,
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
        [SagaState] NVARCHAR(MAX) NULL,
        [CorrelationId] UNIQUEIDENTIFIER NULL,
        [ParentScanId] UNIQUEIDENTIFIER NULL,

        INDEX IX_ScanHistory_ScanId UNIQUE (ScanId),
        INDEX IX_ScanHistory_Status (ScanStatus),
        INDEX IX_ScanHistory_StartedAt (StartedAt DESC),
        INDEX IX_ScanHistory_Correlation (CorrelationId)
    );
    PRINT '  Created [DaQa].[LineageScanHistory]';
END
ELSE PRINT '  [DaQa].[LineageScanHistory] already exists';
GO

-- TABLE 4: DynamicSqlProcedures
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DynamicSqlProcedures' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[DynamicSqlProcedures] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SchemaName] NVARCHAR(128) NOT NULL,
        [ProcedureName] NVARCHAR(128) NOT NULL,
        [DynamicSqlType] NVARCHAR(50) NOT NULL,
        [DetectedPattern] NVARCHAR(500) NULL,
        [LineNumber] INT NULL,
        [RiskLevel] NVARCHAR(20) NOT NULL,
        [ManuallyReviewed] BIT NOT NULL DEFAULT 0,
        [ReviewedBy] NVARCHAR(100) NULL,
        [ReviewedAt] DATETIME2 NULL,
        [ReviewNotes] NVARCHAR(MAX) NULL,
        [KnownTargets] NVARCHAR(MAX) NULL,
        [DetectedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [SourceScanId] UNIQUEIDENTIFIER NULL,

        INDEX IX_DynamicSql_Schema_Proc UNIQUE (SchemaName, ProcedureName, DynamicSqlType, LineNumber),
        INDEX IX_DynamicSql_Risk (RiskLevel),
        INDEX IX_DynamicSql_Unreviewed (ManuallyReviewed) WHERE ManuallyReviewed = 0
    );
    PRINT '  Created [DaQa].[DynamicSqlProcedures]';
END
ELSE PRINT '  [DaQa].[DynamicSqlProcedures] already exists';
GO

-- TABLE 5: LineageGraphNodes
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LineageGraphNodes' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[LineageGraphNodes] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [NodeId] NVARCHAR(500) NOT NULL,
        [NodeType] NVARCHAR(50) NOT NULL,
        [DatabaseName] NVARCHAR(128) NULL,
        [SchemaName] NVARCHAR(128) NOT NULL,
        [ObjectName] NVARCHAR(128) NOT NULL,
        [ColumnName] NVARCHAR(128) NULL,
        [DisplayName] NVARCHAR(256) NOT NULL,
        [IsPiiNode] BIT NOT NULL DEFAULT 0,
        [PiiType] NVARCHAR(50) NULL,
        [DataClassification] NVARCHAR(50) NULL,
        [Properties] NVARCHAR(MAX) NULL,
        [RiskScore] INT NOT NULL DEFAULT 0,
        [InDegree] INT NOT NULL DEFAULT 0,
        [OutDegree] INT NOT NULL DEFAULT 0,
        [ClusterGroup] NVARCHAR(100) NULL,
        [GraphNodeId] INT NULL,
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
    PRINT '  Created [DaQa].[LineageGraphNodes]';
END
ELSE PRINT '  [DaQa].[LineageGraphNodes] already exists';
GO

-- TABLE 6: LineageGraphEdges
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LineageGraphEdges' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[LineageGraphEdges] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EdgeId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [SourceNodeId] NVARCHAR(500) NOT NULL,
        [TargetNodeId] NVARCHAR(500) NOT NULL,
        [EdgeType] NVARCHAR(50) NOT NULL,
        [OperationType] NVARCHAR(20) NULL,
        [TransformationHint] NVARCHAR(500) NULL,
        [SourceProcedure] NVARCHAR(256) NULL,
        [Weight] DECIMAL(5,2) NOT NULL DEFAULT 1.0,
        [IsPiiFlow] BIT NOT NULL DEFAULT 0,
        [Properties] NVARCHAR(MAX) NULL,
        [GraphEdgeId] INT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_LineageEdge_EdgeId UNIQUE (EdgeId),
        INDEX IX_LineageEdge_Source (SourceNodeId),
        INDEX IX_LineageEdge_Target (TargetNodeId),
        INDEX IX_LineageEdge_Type (EdgeType),
        INDEX IX_LineageEdge_PiiFlow (IsPiiFlow) WHERE IsPiiFlow = 1,
        INDEX IX_LineageEdge_Proc (SourceProcedure)
    );
    PRINT '  Created [DaQa].[LineageGraphEdges]';
END
ELSE PRINT '  [DaQa].[LineageGraphEdges] already exists';
GO

-- TABLE 7: OpenLineageEvents
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OpenLineageEvents' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[OpenLineageEvents] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EventId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [EventType] NVARCHAR(50) NOT NULL,
        [EventTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Producer] NVARCHAR(256) NOT NULL DEFAULT 'enterprise-documentation-platform',
        [SchemaURL] NVARCHAR(500) NOT NULL DEFAULT 'https://openlineage.io/spec/1-0-5/OpenLineage.json',
        [JobNamespace] NVARCHAR(256) NOT NULL,
        [JobName] NVARCHAR(256) NOT NULL,
        [RunId] UNIQUEIDENTIFIER NOT NULL,
        [InputDatasets] NVARCHAR(MAX) NULL,
        [OutputDatasets] NVARCHAR(MAX) NULL,
        [ColumnLineage] NVARCHAR(MAX) NULL,
        [CustomFacets] NVARCHAR(MAX) NULL,
        [PublishedAt] DATETIME2 NULL,
        [PublishStatus] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [PublishError] NVARCHAR(MAX) NULL,

        INDEX IX_OpenLineage_EventId UNIQUE (EventId),
        INDEX IX_OpenLineage_RunId (RunId),
        INDEX IX_OpenLineage_Job (JobNamespace, JobName),
        INDEX IX_OpenLineage_EventTime (EventTime DESC),
        INDEX IX_OpenLineage_PublishStatus (PublishStatus) WHERE PublishStatus != 'Published'
    );
    PRINT '  Created [DaQa].[OpenLineageEvents]';
END
ELSE PRINT '  [DaQa].[OpenLineageEvents] already exists';
GO

-- TABLE 8: ColumnRiskScores
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
        [PiiExposureCount] INT NOT NULL DEFAULT 0,
        [RiskScore] INT NOT NULL DEFAULT 0,
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
    PRINT '  Created [DaQa].[ColumnRiskScores]';
END
ELSE PRINT '  [DaQa].[ColumnRiskScores] already exists';
GO

-- Lineage Stored Procedures
IF OBJECT_ID('DaQa.usp_GetColumnLineage', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_GetColumnLineage;
GO

CREATE PROCEDURE DaQa.usp_GetColumnLineage
    @SchemaName NVARCHAR(128),
    @TableName NVARCHAR(128),
    @ColumnName NVARCHAR(128) = NULL,
    @MaxDepth INT = 5,
    @Direction NVARCHAR(20) = 'Both'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StartNodeId NVARCHAR(500) =
        CASE
            WHEN @ColumnName IS NOT NULL THEN CONCAT(@SchemaName, '.', @TableName, '.', @ColumnName)
            ELSE CONCAT(@SchemaName, '.', @TableName)
        END;

    WITH LineageCTE AS (
        SELECT
            n.NodeId, n.NodeType, n.DisplayName, n.IsPiiNode, n.PiiType, n.RiskScore,
            CAST(n.NodeId AS NVARCHAR(MAX)) AS Path, 0 AS Depth, 'Start' AS Direction
        FROM DaQa.LineageGraphNodes n
        WHERE n.NodeId = @StartNodeId

        UNION ALL

        SELECT
            n.NodeId, n.NodeType, n.DisplayName, n.IsPiiNode, n.PiiType, n.RiskScore,
            cte.Path + ' <- ' + n.NodeId, cte.Depth + 1, 'Upstream'
        FROM LineageCTE cte
        JOIN DaQa.LineageGraphEdges e ON e.TargetNodeId = cte.NodeId
        JOIN DaQa.LineageGraphNodes n ON n.NodeId = e.SourceNodeId
        WHERE cte.Depth < @MaxDepth AND @Direction IN ('Both', 'Upstream')
          AND CHARINDEX(n.NodeId, cte.Path) = 0

        UNION ALL

        SELECT
            n.NodeId, n.NodeType, n.DisplayName, n.IsPiiNode, n.PiiType, n.RiskScore,
            cte.Path + ' -> ' + n.NodeId, cte.Depth + 1, 'Downstream'
        FROM LineageCTE cte
        JOIN DaQa.LineageGraphEdges e ON e.SourceNodeId = cte.NodeId
        JOIN DaQa.LineageGraphNodes n ON n.NodeId = e.TargetNodeId
        WHERE cte.Depth < @MaxDepth AND @Direction IN ('Both', 'Downstream')
          AND CHARINDEX(n.NodeId, cte.Path) = 0
    )
    SELECT DISTINCT NodeId, NodeType, DisplayName, IsPiiNode, PiiType, RiskScore, Path, Depth, Direction
    FROM LineageCTE ORDER BY Depth, Direction, NodeId;

    SELECT DISTINCT e.EdgeId, e.SourceNodeId, e.TargetNodeId, e.EdgeType, e.OperationType, e.IsPiiFlow, e.Weight
    FROM DaQa.LineageGraphEdges e
    WHERE EXISTS (SELECT 1 FROM LineageCTE cte WHERE cte.NodeId = e.SourceNodeId OR cte.NodeId = e.TargetNodeId);
END
GO
PRINT '  Created [DaQa].[usp_GetColumnLineage]';

IF OBJECT_ID('DaQa.usp_CalculateColumnRiskScores', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_CalculateColumnRiskScores;
GO

CREATE PROCEDURE DaQa.usp_CalculateColumnRiskScores
    @ScanId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @ScanId IS NOT NULL
        DELETE FROM DaQa.ColumnRiskScores WHERE SourceScanId = @ScanId;

    INSERT INTO DaQa.ColumnRiskScores (
        SchemaName, TableName, ColumnName, DirectDependentCount, TransitiveDependentCount,
        ReadOperations, WriteOperations, DeleteOperations, AffectedProcedures, AffectedViews,
        PiiExposureCount, RiskScore, SourceScanId
    )
    SELECT
        n.SchemaName, n.ObjectName, n.ColumnName, n.OutDegree,
        (SELECT COUNT(*) FROM DaQa.LineageGraphEdges e WHERE e.SourceNodeId = n.NodeId),
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.SourceColumn = n.ColumnName AND cl.OperationType = 'READ'),
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType IN ('INSERT', 'UPDATE', 'MERGE_UPDATE', 'MERGE_INSERT')),
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType = 'DELETE'),
        (SELECT COUNT(DISTINCT cl.ProcedureName) FROM DaQa.ColumnLineage cl WHERE cl.SourceColumn = n.ColumnName OR cl.TargetColumn = n.ColumnName),
        0,
        (SELECT COUNT(*) FROM DaQa.LineageGraphEdges e WHERE e.SourceNodeId = n.NodeId AND e.IsPiiFlow = 1),
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.SourceColumn = n.ColumnName AND cl.OperationType = 'READ') * 1 +
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType IN ('INSERT', 'MERGE_INSERT')) * 2 +
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType IN ('UPDATE', 'MERGE_UPDATE')) * 3 +
        (SELECT COUNT(*) FROM DaQa.ColumnLineage cl WHERE cl.TargetColumn = n.ColumnName AND cl.OperationType = 'DELETE') * 5 +
        CASE WHEN n.IsPiiNode = 1 THEN 10 ELSE 0 END,
        @ScanId
    FROM DaQa.LineageGraphNodes n
    WHERE n.NodeType = 'Column' AND n.ColumnName IS NOT NULL;

    UPDATE n SET n.RiskScore = rs.RiskScore, n.UpdatedAt = GETUTCDATE()
    FROM DaQa.LineageGraphNodes n
    JOIN DaQa.ColumnRiskScores rs ON rs.SchemaName = n.SchemaName AND rs.TableName = n.ObjectName AND rs.ColumnName = n.ColumnName;

    SELECT @@ROWCOUNT AS ColumnsScored;
END
GO
PRINT '  Created [DaQa].[usp_CalculateColumnRiskScores]';

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

    WITH PiiFlowCTE AS (
        SELECT e.SourceNodeId, e.TargetNodeId,
            CAST(e.SourceNodeId + ' -> ' + e.TargetNodeId AS NVARCHAR(MAX)) AS FlowPath, 1 AS PathLength
        FROM DaQa.LineageGraphEdges e
        WHERE e.SourceNodeId = @StartNodeId AND e.IsPiiFlow = 1

        UNION ALL

        SELECT cte.SourceNodeId, e.TargetNodeId, cte.FlowPath + ' -> ' + e.TargetNodeId, cte.PathLength + 1
        FROM PiiFlowCTE cte
        JOIN DaQa.LineageGraphEdges e ON e.SourceNodeId = cte.TargetNodeId
        WHERE e.IsPiiFlow = 1 AND cte.PathLength < 10 AND CHARINDEX(e.TargetNodeId, cte.FlowPath) = 0
    )
    SELECT FlowPath, PathLength, SourceNodeId AS StartColumn, TargetNodeId AS EndColumn
    FROM PiiFlowCTE ORDER BY PathLength;
END
GO
PRINT '  Created [DaQa].[usp_GetPiiFlowPaths]';

PRINT '';
PRINT '  Section 2 Complete: 8 Lineage tables + 3 stored procedures';

-- ============================================================================
-- SECTION 3: SCHEMA CHANGE DETECTION (Agent #4)
-- Migration 008: 6 tables + DDL trigger for real-time schema change detection
-- ============================================================================
PRINT '';
PRINT '=== SECTION 3: Schema Change Detection (Agent #4) ===';
PRINT '';

-- TABLE 1: SchemaChanges
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaChanges' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.SchemaChanges (
        ChangeId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        DatabaseName NVARCHAR(128) NOT NULL,
        SchemaName NVARCHAR(128) NOT NULL,
        ObjectName NVARCHAR(256) NOT NULL,
        ObjectType NVARCHAR(50) NOT NULL,
        ChangeType NVARCHAR(20) NOT NULL,
        ChangeDescription NVARCHAR(MAX),
        ChangedColumns NVARCHAR(MAX),
        OldDefinition NVARCHAR(MAX),
        NewDefinition NVARCHAR(MAX),
        DdlStatement NVARCHAR(MAX),
        DetectedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        DetectedBy NVARCHAR(128) NOT NULL,
        LoginName NVARCHAR(128),
        HostName NVARCHAR(128),
        ApplicationName NVARCHAR(256),
        ImpactScore INT DEFAULT 0,
        RiskLevel NVARCHAR(20) DEFAULT 'LOW',
        AffectedProcedures INT DEFAULT 0,
        AffectedViews INT DEFAULT 0,
        AffectedFunctions INT DEFAULT 0,
        HasPiiColumns BIT DEFAULT 0,
        HasLineageDownstream BIT DEFAULT 0,
        ProcessingStatus NVARCHAR(50) DEFAULT 'Pending',
        AcknowledgedBy NVARCHAR(128),
        AcknowledgedAt DATETIME2,
        AcknowledgementNotes NVARCHAR(MAX),
        ApprovalRequired BIT DEFAULT 0,
        ApprovalWorkflowId UNIQUEIDENTIFIER,
        DocumentationTriggered BIT DEFAULT 0,
        DocumentationTriggeredAt DATETIME2,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2,

        INDEX IX_SchemaChanges_DetectedAt (DetectedAt DESC),
        INDEX IX_SchemaChanges_ObjectName (SchemaName, ObjectName),
        INDEX IX_SchemaChanges_Status (ProcessingStatus),
        INDEX IX_SchemaChanges_RiskLevel (RiskLevel, DetectedAt DESC),
        INDEX IX_SchemaChanges_ChangeType (ChangeType, DetectedAt DESC)
    );
    PRINT '  Created [DaQa].[SchemaChanges]';
END
ELSE PRINT '  [DaQa].[SchemaChanges] already exists';
GO

-- TABLE 2: SchemaSnapshots
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaSnapshots' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.SchemaSnapshots (
        SnapshotId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        SnapshotName NVARCHAR(256) NOT NULL,
        SnapshotType NVARCHAR(50) NOT NULL,
        SchemaFilter NVARCHAR(128),
        SnapshotData VARBINARY(MAX) NOT NULL,
        ObjectCount INT NOT NULL DEFAULT 0,
        TableCount INT NOT NULL DEFAULT 0,
        ViewCount INT NOT NULL DEFAULT 0,
        ProcedureCount INT NOT NULL DEFAULT 0,
        FunctionCount INT NOT NULL DEFAULT 0,
        TakenAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        TakenBy NVARCHAR(128) NOT NULL,
        DatabaseVersion NVARCHAR(50),
        IsBaseline BIT DEFAULT 0,
        PreviousSnapshotId UNIQUEIDENTIFIER,
        DiffFromPrevious NVARCHAR(MAX),
        ExpiresAt DATETIME2,
        IsArchived BIT DEFAULT 0,

        INDEX IX_SchemaSnapshots_TakenAt (TakenAt DESC),
        INDEX IX_SchemaSnapshots_Baseline (IsBaseline) WHERE IsBaseline = 1
    );
    PRINT '  Created [DaQa].[SchemaSnapshots]';
END
ELSE PRINT '  [DaQa].[SchemaSnapshots] already exists';
GO

-- TABLE 3: SchemaDetectionRuns
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaDetectionRuns' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.SchemaDetectionRuns (
        RunId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        RunType NVARCHAR(50) NOT NULL,
        ScanScope NVARCHAR(50) NOT NULL,
        SchemaFilter NVARCHAR(128),
        ObjectFilter NVARCHAR(256),
        CurrentState NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        TotalObjects INT DEFAULT 0,
        ProcessedObjects INT DEFAULT 0,
        ChangesDetected INT DEFAULT 0,
        HighRiskChanges INT DEFAULT 0,
        StartedAt DATETIME2,
        SnapshotCompletedAt DATETIME2,
        ComparisonCompletedAt DATETIME2,
        AnalysisCompletedAt DATETIME2,
        CompletedAt DATETIME2,
        DurationMs BIGINT,
        ErrorMessage NVARCHAR(MAX),
        RetryCount INT DEFAULT 0,
        TriggeredBy NVARCHAR(128) NOT NULL,
        SnapshotId UNIQUEIDENTIFIER,
        ResultSummary NVARCHAR(MAX),

        INDEX IX_DetectionRuns_State (CurrentState, StartedAt DESC),
        INDEX IX_DetectionRuns_StartedAt (StartedAt DESC)
    );
    PRINT '  Created [DaQa].[SchemaDetectionRuns]';
END
ELSE PRINT '  [DaQa].[SchemaDetectionRuns] already exists';
GO

-- TABLE 4: ChangeImpactAnalysis
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChangeImpactAnalysis' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.ChangeImpactAnalysis (
        ImpactId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ChangeId UNIQUEIDENTIFIER NOT NULL,
        AffectedSchema NVARCHAR(128) NOT NULL,
        AffectedObject NVARCHAR(256) NOT NULL,
        AffectedObjectType NVARCHAR(50) NOT NULL,
        ImpactType NVARCHAR(50) NOT NULL,
        ImpactSeverity INT NOT NULL,
        ImpactDescription NVARCHAR(MAX),
        OperationType NVARCHAR(20),
        AffectedColumn NVARCHAR(256),
        LineNumber INT,
        SqlFragment NVARCHAR(MAX),
        SuggestedAction NVARCHAR(MAX),
        RequiresManualReview BIT DEFAULT 0,
        AnalyzedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_ChangeImpact_Change FOREIGN KEY (ChangeId)
            REFERENCES DaQa.SchemaChanges(ChangeId) ON DELETE CASCADE,

        INDEX IX_ChangeImpact_ChangeId (ChangeId),
        INDEX IX_ChangeImpact_Severity (ImpactSeverity DESC, ChangeId)
    );
    PRINT '  Created [DaQa].[ChangeImpactAnalysis]';
END
ELSE PRINT '  [DaQa].[ChangeImpactAnalysis] already exists';
GO

-- TABLE 5: ColumnChangeHistory
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ColumnChangeHistory' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.ColumnChangeHistory (
        ColumnChangeId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ChangeId UNIQUEIDENTIFIER NOT NULL,
        SchemaName NVARCHAR(128) NOT NULL,
        TableName NVARCHAR(256) NOT NULL,
        ColumnName NVARCHAR(256) NOT NULL,
        ColumnChangeType NVARCHAR(50) NOT NULL,
        OldDataType NVARCHAR(256),
        OldMaxLength INT,
        OldPrecision TINYINT,
        OldScale TINYINT,
        OldIsNullable BIT,
        OldDefaultValue NVARCHAR(MAX),
        OldIsIdentity BIT,
        OldIsPii BIT,
        OldPiiType NVARCHAR(50),
        NewDataType NVARCHAR(256),
        NewMaxLength INT,
        NewPrecision TINYINT,
        NewScale TINYINT,
        NewIsNullable BIT,
        NewDefaultValue NVARCHAR(MAX),
        NewIsIdentity BIT,
        NewIsPii BIT,
        NewPiiType NVARCHAR(50),
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
    PRINT '  Created [DaQa].[ColumnChangeHistory]';
END
ELSE PRINT '  [DaQa].[ColumnChangeHistory] already exists';
GO

-- TABLE 6: ChangeNotifications
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChangeNotifications' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE DaQa.ChangeNotifications (
        NotificationId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ChangeId UNIQUEIDENTIFIER NOT NULL,
        NotificationType NVARCHAR(50) NOT NULL,
        RecipientType NVARCHAR(50) NOT NULL,
        RecipientId NVARCHAR(256) NOT NULL,
        NotificationTitle NVARCHAR(500),
        NotificationBody NVARCHAR(MAX),
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
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
    PRINT '  Created [DaQa].[ChangeNotifications]';
END
ELSE PRINT '  [DaQa].[ChangeNotifications] already exists';
GO

-- DDL Trigger for real-time detection
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_CaptureSchemaChanges' AND parent_class = 0)
BEGIN
    DROP TRIGGER TR_CaptureSchemaChanges ON DATABASE;
    PRINT '  Dropped existing DDL trigger';
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
    DECLARE @SchemaName NVARCHAR(128), @ObjectName NVARCHAR(256), @ObjectType NVARCHAR(50);
    DECLARE @ChangeType NVARCHAR(20), @DdlStatement NVARCHAR(MAX);
    DECLARE @LoginName NVARCHAR(128), @HostName NVARCHAR(128), @AppName NVARCHAR(256);

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

    IF @SchemaName IN ('sys', 'INFORMATION_SCHEMA') OR (@SchemaName = 'DaQa' AND @ObjectName LIKE 'Schema%')
        RETURN;

    BEGIN TRY
        INSERT INTO DaQa.SchemaChanges (DatabaseName, SchemaName, ObjectName, ObjectType, ChangeType,
            DdlStatement, DetectedBy, LoginName, HostName, ApplicationName, ProcessingStatus)
        VALUES (DB_NAME(), @SchemaName, @ObjectName, @ObjectType, @ChangeType,
            @DdlStatement, 'DDL_TRIGGER', @LoginName, @HostName, @AppName, 'Pending');
    END TRY
    BEGIN CATCH
        DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR('Schema change capture warning: %s', 10, 1, @ErrorMsg) WITH LOG;
    END CATCH
END
GO
PRINT '  Created DDL trigger [TR_CaptureSchemaChanges]';

CREATE OR ALTER PROCEDURE DaQa.usp_GetPendingSchemaChanges
    @MaxCount INT = 100,
    @MinRiskLevel NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@MaxCount)
        ChangeId, DatabaseName, SchemaName, ObjectName, ObjectType, ChangeType,
        ChangeDescription, DdlStatement, DetectedAt, DetectedBy, LoginName,
        ImpactScore, RiskLevel, ProcessingStatus, AffectedProcedures, AffectedViews,
        HasPiiColumns, HasLineageDownstream
    FROM DaQa.SchemaChanges
    WHERE ProcessingStatus = 'Pending'
      AND (@MinRiskLevel IS NULL OR RiskLevel = @MinRiskLevel)
    ORDER BY
        CASE RiskLevel WHEN 'CRITICAL' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 WHEN 'LOW' THEN 4 END,
        DetectedAt ASC;
END
GO
PRINT '  Created [DaQa].[usp_GetPendingSchemaChanges]';

PRINT '';
PRINT '  Section 3 Complete: 6 Schema Change tables + DDL trigger';

-- ============================================================================
-- SECTION 4: DEPENDENCY TRACKING
-- ============================================================================
PRINT '';
PRINT '=== SECTION 4: Dependency Tracking ===';
PRINT '';

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'DaQa.StoredProcedureDependencies') AND type = 'U')
BEGIN
    CREATE TABLE DaQa.StoredProcedureDependencies (
        DependencyId INT IDENTITY(1,1) PRIMARY KEY,
        IndexID NVARCHAR(50) NOT NULL,
        DependencyType NVARCHAR(20) NOT NULL,
        DependencyName NVARCHAR(255) NOT NULL,
        DependencySchema NVARCHAR(128) NOT NULL DEFAULT 'dbo',
        IsCritical BIT NOT NULL DEFAULT 0,
        LastVerified DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_Dependencies_IndexID (IndexID),
        INDEX IX_Dependencies_Name (DependencyName, DependencySchema),
        UNIQUE INDEX UX_Dependencies (IndexID, DependencyType, DependencyName, DependencySchema)
    );
    PRINT '  Created [DaQa].[StoredProcedureDependencies]';
END
ELSE PRINT '  [DaQa].[StoredProcedureDependencies] already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'DaQa.vw_DependencyImpactAnalysis'))
BEGIN
    EXEC('
    CREATE VIEW DaQa.vw_DependencyImpactAnalysis
    AS
    SELECT
        dep.DependencyName + ''.'' + dep.DependencySchema as FullDependencyName,
        dep.DependencyType,
        COUNT(*) as UsageCount,
        STRING_AGG(CAST(mi.DocumentTitle AS NVARCHAR(MAX)), '', '') as AffectedProcedures,
        MAX(dep.LastVerified) as LastVerified,
        CASE WHEN SUM(CAST(dep.IsCritical as INT)) > 0 THEN 1 ELSE 0 END as HasCriticalUsage
    FROM DaQa.StoredProcedureDependencies dep
    INNER JOIN DaQa.MasterIndex mi ON dep.IndexID = CAST(mi.IndexID AS NVARCHAR(50))
    WHERE mi.Status = ''Active''
    GROUP BY dep.DependencyName, dep.DependencySchema, dep.DependencyType
    ');
    PRINT '  Created [DaQa].[vw_DependencyImpactAnalysis]';
END
ELSE PRINT '  [DaQa].[vw_DependencyImpactAnalysis] already exists';
GO

PRINT '';
PRINT '  Section 4 Complete: Dependency tracking table + view';

-- ============================================================================
-- SECTION 5: ALTERATIONS TO EXISTING TABLES
-- ============================================================================
PRINT '';
PRINT '=== SECTION 5: Alterations to Existing Tables ===';
PRINT '';

-- Add CodeQuality columns to DocumentChanges
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentChanges' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityScore')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges ADD CodeQualityScore INT NULL;
        PRINT '  Added CodeQualityScore to DocumentChanges';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityGrade')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges ADD CodeQualityGrade NVARCHAR(10) NULL;
        PRINT '  Added CodeQualityGrade to DocumentChanges';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'Documentation')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges ADD Documentation NVARCHAR(500) NULL;
        PRINT '  Added Documentation to DocumentChanges';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'DocumentationLink')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges ADD DocumentationLink NVARCHAR(500) NULL;
        PRINT '  Added DocumentationLink to DocumentChanges';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'DocId')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges ADD DocId NVARCHAR(50) NULL;
        PRINT '  Added DocId to DocumentChanges';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'ChangeApplied')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges ADD ChangeApplied NVARCHAR(255) NULL;
        PRINT '  Added ChangeApplied to DocumentChanges';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'LocationOfCodeChange')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges ADD LocationOfCodeChange NVARCHAR(500) NULL;
        PRINT '  Added LocationOfCodeChange to DocumentChanges';
    END

    -- Add constraints if columns exist
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityGrade')
       AND NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_DocumentChanges_CodeQualityGrade')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges
        ADD CONSTRAINT CK_DocumentChanges_CodeQualityGrade
        CHECK (CodeQualityGrade IN ('A+', 'A', 'B+', 'B', 'C+', 'C', 'D', 'F') OR CodeQualityGrade IS NULL);
        PRINT '  Added constraint CK_DocumentChanges_CodeQualityGrade';
    END

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityScore')
       AND NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_DocumentChanges_CodeQualityScore')
    BEGIN
        ALTER TABLE DaQa.DocumentChanges
        ADD CONSTRAINT CK_DocumentChanges_CodeQualityScore
        CHECK (CodeQualityScore BETWEEN 0 AND 100 OR CodeQualityScore IS NULL);
        PRINT '  Added constraint CK_DocumentChanges_CodeQualityScore';
    END

    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DaQa.DocumentChanges') AND name = 'CodeQualityScore')
       AND NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_DocumentChanges_CodeQuality' AND object_id = OBJECT_ID('DaQa.DocumentChanges'))
    BEGIN
        CREATE INDEX IX_DocumentChanges_CodeQuality
        ON DaQa.DocumentChanges(CodeQualityScore, CodeQualityGrade)
        WHERE CodeQualityScore IS NOT NULL;
        PRINT '  Created index IX_DocumentChanges_CodeQuality';
    END
END
ELSE
BEGIN
    PRINT '  [DaQa].[DocumentChanges] not found - skipping alterations';
END
GO

PRINT '';
PRINT '  Section 5 Complete: Table alterations applied';

-- ============================================================================
-- SUMMARY
-- ============================================================================
PRINT '';
PRINT '============================================================================';
PRINT 'MASTER MIGRATION COMPLETE';
PRINT 'Completed: ' + CONVERT(VARCHAR, GETUTCDATE(), 120);
PRINT '============================================================================';
PRINT '';
PRINT 'Tables Created/Verified:';
PRINT '  Search & Discovery (Agent #6):';
PRINT '    - SearchQueries, SearchResults, UserInteractions';
PRINT '    - EmbeddingCache, CategorySuggestions';
PRINT '    - GraphNodes, GraphEdges';
PRINT '';
PRINT '  Data Lineage (Agent #3):';
PRINT '    - ObjectDependencies, ColumnLineage, LineageScanHistory';
PRINT '    - DynamicSqlProcedures, LineageGraphNodes, LineageGraphEdges';
PRINT '    - OpenLineageEvents, ColumnRiskScores';
PRINT '';
PRINT '  Schema Change Detection (Agent #4):';
PRINT '    - SchemaChanges, SchemaSnapshots, SchemaDetectionRuns';
PRINT '    - ChangeImpactAnalysis, ColumnChangeHistory, ChangeNotifications';
PRINT '    - DDL Trigger: TR_CaptureSchemaChanges';
PRINT '';
PRINT '  Dependency Tracking:';
PRINT '    - StoredProcedureDependencies';
PRINT '    - vw_DependencyImpactAnalysis';
PRINT '';
PRINT 'Stored Procedures Created:';
PRINT '    - usp_GetColumnLineage';
PRINT '    - usp_CalculateColumnRiskScores';
PRINT '    - usp_GetPiiFlowPaths';
PRINT '    - usp_GetPendingSchemaChanges';
PRINT '============================================================================';
GO
