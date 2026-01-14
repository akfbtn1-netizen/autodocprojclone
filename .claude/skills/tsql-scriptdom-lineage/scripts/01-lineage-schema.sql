-- =============================================================================
-- T-SQL ScriptDom Lineage Graph Database Schema
-- =============================================================================
-- Part of: tsql-scriptdom-lineage skill
-- Version: 1.0.0
-- Target: SQL Server 2019+ / Azure SQL Database
-- =============================================================================

USE [IRFS1];
GO

-- =============================================================================
-- SECTION 1: Core Lineage Tables
-- =============================================================================

-- Create schema if not exists
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'lineage')
BEGIN
    EXEC('CREATE SCHEMA lineage');
END
GO

-- -----------------------------------------------------------------------------
-- Node Types lookup table
-- -----------------------------------------------------------------------------
CREATE TABLE lineage.NodeType (
    NodeTypeId TINYINT PRIMARY KEY,
    TypeName VARCHAR(50) NOT NULL,
    TypeDescription VARCHAR(255)
);

INSERT INTO lineage.NodeType (NodeTypeId, TypeName, TypeDescription) VALUES
(1, 'Table', 'Physical database table'),
(2, 'View', 'Database view'),
(3, 'Column', 'Table or view column'),
(4, 'Procedure', 'Stored procedure'),
(5, 'Function', 'User-defined function'),
(6, 'Trigger', 'Database trigger'),
(7, 'CTE', 'Common Table Expression'),
(8, 'TempTable', 'Temporary table'),
(9, 'TableVariable', 'Table variable'),
(10, 'DerivedTable', 'Derived table / subquery');
GO

-- -----------------------------------------------------------------------------
-- Transformation Types lookup table
-- -----------------------------------------------------------------------------
CREATE TABLE lineage.TransformationType (
    TransformationTypeId TINYINT PRIMARY KEY,
    TypeName VARCHAR(50) NOT NULL,
    TypeDescription VARCHAR(255)
);

INSERT INTO lineage.TransformationType (TransformationTypeId, TypeName, TypeDescription) VALUES
(1, 'Direct', 'Direct column reference with no transformation'),
(2, 'Aggregation', 'Aggregate function (SUM, COUNT, AVG, etc.)'),
(3, 'CaseExpression', 'CASE WHEN expression'),
(4, 'Function', 'Scalar function transformation'),
(5, 'Arithmetic', 'Mathematical operation'),
(6, 'Concatenation', 'String concatenation'),
(7, 'Coalesce', 'COALESCE or ISNULL'),
(8, 'Join', 'Join condition dependency'),
(9, 'Filter', 'WHERE/HAVING clause filter'),
(10, 'Unknown', 'Transformation type could not be determined');
GO

-- -----------------------------------------------------------------------------
-- Lineage Nodes - Tables, Views, Columns
-- -----------------------------------------------------------------------------
CREATE TABLE lineage.Node (
    NodeId INT IDENTITY(1,1) PRIMARY KEY,
    NodeTypeId TINYINT NOT NULL REFERENCES lineage.NodeType(NodeTypeId),
    DatabaseName NVARCHAR(128) NOT NULL DEFAULT DB_NAME(),
    SchemaName NVARCHAR(128) NOT NULL DEFAULT 'dbo',
    ObjectName NVARCHAR(128) NOT NULL,
    ColumnName NVARCHAR(128) NULL,
    DataType NVARCHAR(128) NULL,
    IsNullable BIT NULL,
    
    -- Computed fully qualified name
    FullyQualifiedName AS (
        CONCAT(
            DatabaseName, '.', 
            SchemaName, '.', 
            ObjectName, 
            CASE WHEN ColumnName IS NOT NULL THEN '.' + ColumnName ELSE '' END
        )
    ) PERSISTED,
    
    -- Metadata
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    LastAnalyzedAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    
    -- Constraints
    CONSTRAINT UQ_Node_FQN UNIQUE (DatabaseName, SchemaName, ObjectName, ColumnName),
    
    -- Indexes
    INDEX IX_Node_Schema NONCLUSTERED (SchemaName, ObjectName),
    INDEX IX_Node_Type NONCLUSTERED (NodeTypeId) INCLUDE (SchemaName, ObjectName)
);
GO

-- -----------------------------------------------------------------------------
-- Lineage Edges - Data flow relationships
-- -----------------------------------------------------------------------------
CREATE TABLE lineage.Edge (
    EdgeId INT IDENTITY(1,1) PRIMARY KEY,
    SourceNodeId INT NOT NULL REFERENCES lineage.Node(NodeId),
    TargetNodeId INT NOT NULL REFERENCES lineage.Node(NodeId),
    TransformationTypeId TINYINT NOT NULL REFERENCES lineage.TransformationType(TransformationTypeId),
    
    -- Context
    SourceObjectId INT NULL REFERENCES lineage.Node(NodeId), -- Procedure/View creating this edge
    TransformationExpression NVARCHAR(MAX) NULL,
    SourceLine INT NULL,
    
    -- Confidence and metadata
    Confidence DECIMAL(3,2) NOT NULL DEFAULT 1.0 CHECK (Confidence BETWEEN 0 AND 1),
    
    -- Metadata
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    
    -- Constraints
    CONSTRAINT UQ_Edge_Unique UNIQUE (SourceNodeId, TargetNodeId, SourceObjectId),
    CONSTRAINT CK_Edge_NotSelfRef CHECK (SourceNodeId <> TargetNodeId),
    
    -- Indexes
    INDEX IX_Edge_Source NONCLUSTERED (SourceNodeId) INCLUDE (TargetNodeId, TransformationTypeId),
    INDEX IX_Edge_Target NONCLUSTERED (TargetNodeId) INCLUDE (SourceNodeId, TransformationTypeId),
    INDEX IX_Edge_SourceObject NONCLUSTERED (SourceObjectId)
);
GO

-- -----------------------------------------------------------------------------
-- Extraction History - Track analysis runs
-- -----------------------------------------------------------------------------
CREATE TABLE lineage.ExtractionHistory (
    ExtractionId INT IDENTITY(1,1) PRIMARY KEY,
    ObjectNodeId INT NOT NULL REFERENCES lineage.Node(NodeId),
    ExtractedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    DurationMs INT NOT NULL,
    EdgesCreated INT NOT NULL DEFAULT 0,
    WarningsCount INT NOT NULL DEFAULT 0,
    DynamicSqlCount INT NOT NULL DEFAULT 0,
    OverallConfidence DECIMAL(3,2) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Success', -- Success, PartialSuccess, Failed
    ErrorMessage NVARCHAR(MAX) NULL,
    
    INDEX IX_ExtractionHistory_Object NONCLUSTERED (ObjectNodeId, ExtractedAt DESC)
);
GO

-- -----------------------------------------------------------------------------
-- Warnings - Issues found during extraction
-- -----------------------------------------------------------------------------
CREATE TABLE lineage.ExtractionWarning (
    WarningId INT IDENTITY(1,1) PRIMARY KEY,
    ExtractionId INT NOT NULL REFERENCES lineage.ExtractionHistory(ExtractionId),
    WarningType VARCHAR(50) NOT NULL,
    WarningMessage NVARCHAR(MAX) NOT NULL,
    SourceLine INT NULL,
    SourceColumn INT NULL,
    
    INDEX IX_Warning_Extraction NONCLUSTERED (ExtractionId)
);
GO

-- =============================================================================
-- SECTION 2: Node Management Stored Procedures
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Upsert a node (table, view, or column)
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE lineage.UpsertNode
    @DatabaseName NVARCHAR(128),
    @SchemaName NVARCHAR(128),
    @ObjectName NVARCHAR(128),
    @ColumnName NVARCHAR(128) = NULL,
    @NodeType VARCHAR(50),
    @DataType NVARCHAR(128) = NULL,
    @IsNullable BIT = NULL,
    @NodeId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @NodeTypeId TINYINT;
    SELECT @NodeTypeId = NodeTypeId FROM lineage.NodeType WHERE TypeName = @NodeType;
    
    IF @NodeTypeId IS NULL
    BEGIN
        RAISERROR('Invalid node type: %s', 16, 1, @NodeType);
        RETURN;
    END
    
    -- Try to find existing node
    SELECT @NodeId = NodeId 
    FROM lineage.Node 
    WHERE DatabaseName = @DatabaseName 
      AND SchemaName = @SchemaName 
      AND ObjectName = @ObjectName 
      AND ((@ColumnName IS NULL AND ColumnName IS NULL) OR ColumnName = @ColumnName);
    
    IF @NodeId IS NULL
    BEGIN
        -- Insert new node
        INSERT INTO lineage.Node (NodeTypeId, DatabaseName, SchemaName, ObjectName, ColumnName, DataType, IsNullable)
        VALUES (@NodeTypeId, @DatabaseName, @SchemaName, @ObjectName, @ColumnName, @DataType, @IsNullable);
        
        SET @NodeId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        -- Update existing node
        UPDATE lineage.Node
        SET DataType = COALESCE(@DataType, DataType),
            IsNullable = COALESCE(@IsNullable, IsNullable),
            UpdatedAt = GETDATE()
        WHERE NodeId = @NodeId;
    END
END
GO

-- -----------------------------------------------------------------------------
-- Upsert a lineage edge
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE lineage.UpsertEdge
    @SourceNodeId INT,
    @TargetNodeId INT,
    @TransformationType VARCHAR(50),
    @SourceObjectId INT = NULL,
    @TransformationExpression NVARCHAR(MAX) = NULL,
    @SourceLine INT = NULL,
    @Confidence DECIMAL(3,2) = 1.0,
    @EdgeId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @TransformationTypeId TINYINT;
    SELECT @TransformationTypeId = TransformationTypeId 
    FROM lineage.TransformationType 
    WHERE TypeName = @TransformationType;
    
    IF @TransformationTypeId IS NULL
        SET @TransformationTypeId = 10; -- Unknown
    
    -- Try to find existing edge
    SELECT @EdgeId = EdgeId 
    FROM lineage.Edge 
    WHERE SourceNodeId = @SourceNodeId 
      AND TargetNodeId = @TargetNodeId 
      AND ((@SourceObjectId IS NULL AND SourceObjectId IS NULL) OR SourceObjectId = @SourceObjectId);
    
    IF @EdgeId IS NULL
    BEGIN
        INSERT INTO lineage.Edge (
            SourceNodeId, TargetNodeId, TransformationTypeId, 
            SourceObjectId, TransformationExpression, SourceLine, Confidence
        )
        VALUES (
            @SourceNodeId, @TargetNodeId, @TransformationTypeId,
            @SourceObjectId, @TransformationExpression, @SourceLine, @Confidence
        );
        
        SET @EdgeId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE lineage.Edge
        SET TransformationTypeId = @TransformationTypeId,
            TransformationExpression = COALESCE(@TransformationExpression, TransformationExpression),
            SourceLine = COALESCE(@SourceLine, SourceLine),
            Confidence = @Confidence,
            UpdatedAt = GETDATE()
        WHERE EdgeId = @EdgeId;
    END
END
GO

-- =============================================================================
-- SECTION 3: Lineage Traversal Functions
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Get upstream lineage (data sources) for a column
-- -----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION lineage.GetUpstreamLineage(
    @NodeId INT,
    @MaxDepth INT = 10
)
RETURNS TABLE
AS
RETURN
WITH UpstreamCTE AS (
    -- Anchor: direct sources
    SELECT 
        e.SourceNodeId,
        e.TargetNodeId,
        e.TransformationTypeId,
        e.SourceObjectId,
        e.Confidence,
        1 AS Depth,
        CAST(sn.FullyQualifiedName AS NVARCHAR(MAX)) AS LineagePath
    FROM lineage.Edge e
    INNER JOIN lineage.Node sn ON e.SourceNodeId = sn.NodeId
    WHERE e.TargetNodeId = @NodeId
      AND e.IsActive = 1
    
    UNION ALL
    
    -- Recursive: continue upstream
    SELECT 
        e.SourceNodeId,
        e.TargetNodeId,
        e.TransformationTypeId,
        e.SourceObjectId,
        e.Confidence * cte.Confidence AS Confidence,
        cte.Depth + 1,
        sn.FullyQualifiedName + N' → ' + cte.LineagePath
    FROM lineage.Edge e
    INNER JOIN lineage.Node sn ON e.SourceNodeId = sn.NodeId
    INNER JOIN UpstreamCTE cte ON e.TargetNodeId = cte.SourceNodeId
    WHERE cte.Depth < @MaxDepth
      AND e.IsActive = 1
)
SELECT 
    cte.SourceNodeId,
    sn.FullyQualifiedName AS SourceFQN,
    sn.SchemaName AS SourceSchema,
    sn.ObjectName AS SourceTable,
    sn.ColumnName AS SourceColumn,
    cte.TargetNodeId,
    tn.FullyQualifiedName AS TargetFQN,
    tt.TypeName AS TransformationType,
    so.FullyQualifiedName AS SourceObject,
    cte.Confidence,
    cte.Depth,
    cte.LineagePath
FROM UpstreamCTE cte
INNER JOIN lineage.Node sn ON cte.SourceNodeId = sn.NodeId
INNER JOIN lineage.Node tn ON cte.TargetNodeId = tn.NodeId
INNER JOIN lineage.TransformationType tt ON cte.TransformationTypeId = tt.TransformationTypeId
LEFT JOIN lineage.Node so ON cte.SourceObjectId = so.NodeId;
GO

-- -----------------------------------------------------------------------------
-- Get downstream impact (affected targets) for a column
-- -----------------------------------------------------------------------------
CREATE OR ALTER FUNCTION lineage.GetDownstreamImpact(
    @NodeId INT,
    @MaxDepth INT = 10
)
RETURNS TABLE
AS
RETURN
WITH DownstreamCTE AS (
    -- Anchor: direct targets
    SELECT 
        e.SourceNodeId,
        e.TargetNodeId,
        e.TransformationTypeId,
        e.SourceObjectId,
        e.Confidence,
        1 AS Depth,
        CAST(tn.FullyQualifiedName AS NVARCHAR(MAX)) AS ImpactPath
    FROM lineage.Edge e
    INNER JOIN lineage.Node tn ON e.TargetNodeId = tn.NodeId
    WHERE e.SourceNodeId = @NodeId
      AND e.IsActive = 1
    
    UNION ALL
    
    -- Recursive: continue downstream
    SELECT 
        e.SourceNodeId,
        e.TargetNodeId,
        e.TransformationTypeId,
        e.SourceObjectId,
        e.Confidence * cte.Confidence AS Confidence,
        cte.Depth + 1,
        cte.ImpactPath + N' → ' + tn.FullyQualifiedName
    FROM lineage.Edge e
    INNER JOIN lineage.Node tn ON e.TargetNodeId = tn.NodeId
    INNER JOIN DownstreamCTE cte ON e.SourceNodeId = cte.TargetNodeId
    WHERE cte.Depth < @MaxDepth
      AND e.IsActive = 1
)
SELECT 
    cte.SourceNodeId,
    sn.FullyQualifiedName AS SourceFQN,
    cte.TargetNodeId,
    tn.FullyQualifiedName AS TargetFQN,
    tn.SchemaName AS TargetSchema,
    tn.ObjectName AS TargetTable,
    tn.ColumnName AS TargetColumn,
    tt.TypeName AS TransformationType,
    so.FullyQualifiedName AS TransformingObject,
    cte.Confidence,
    cte.Depth,
    cte.ImpactPath
FROM DownstreamCTE cte
INNER JOIN lineage.Node sn ON cte.SourceNodeId = sn.NodeId
INNER JOIN lineage.Node tn ON cte.TargetNodeId = tn.NodeId
INNER JOIN lineage.TransformationType tt ON cte.TransformationTypeId = tt.TransformationTypeId
LEFT JOIN lineage.Node so ON cte.SourceObjectId = so.NodeId;
GO

-- =============================================================================
-- SECTION 4: Impact Analysis Procedures
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Analyze impact of dropping/modifying a column
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE lineage.AnalyzeColumnImpact
    @SchemaName NVARCHAR(128),
    @TableName NVARCHAR(128),
    @ColumnName NVARCHAR(128),
    @MaxDepth INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Find the column node
    DECLARE @NodeId INT;
    SELECT @NodeId = NodeId 
    FROM lineage.Node 
    WHERE SchemaName = @SchemaName 
      AND ObjectName = @TableName 
      AND ColumnName = @ColumnName
      AND IsActive = 1;
    
    IF @NodeId IS NULL
    BEGIN
        PRINT 'Column not found in lineage graph. Please run lineage extraction first.';
        RETURN;
    END
    
    -- Get impact summary
    SELECT 
        'Downstream Impact Analysis' AS AnalysisType,
        @SchemaName + '.' + @TableName + '.' + @ColumnName AS SourceColumn;
    
    -- Impacted columns by depth
    SELECT 
        Depth,
        COUNT(*) AS ImpactedColumns,
        STRING_AGG(TargetFQN, ', ') WITHIN GROUP (ORDER BY TargetFQN) AS Columns
    FROM lineage.GetDownstreamImpact(@NodeId, @MaxDepth)
    GROUP BY Depth
    ORDER BY Depth;
    
    -- Impacted objects (procedures, views)
    SELECT DISTINCT
        TransformingObject,
        COUNT(*) AS AffectedEdges
    FROM lineage.GetDownstreamImpact(@NodeId, @MaxDepth)
    WHERE TransformingObject IS NOT NULL
    GROUP BY TransformingObject
    ORDER BY AffectedEdges DESC;
    
    -- Full impact detail
    SELECT 
        TargetSchema,
        TargetTable,
        TargetColumn,
        TransformationType,
        TransformingObject,
        Depth,
        Confidence,
        ImpactPath
    FROM lineage.GetDownstreamImpact(@NodeId, @MaxDepth)
    ORDER BY Depth, TargetSchema, TargetTable, TargetColumn;
END
GO

-- -----------------------------------------------------------------------------
-- Find all sources for a reporting column
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE lineage.TraceColumnOrigin
    @SchemaName NVARCHAR(128),
    @TableName NVARCHAR(128),
    @ColumnName NVARCHAR(128),
    @MaxDepth INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @NodeId INT;
    SELECT @NodeId = NodeId 
    FROM lineage.Node 
    WHERE SchemaName = @SchemaName 
      AND ObjectName = @TableName 
      AND ColumnName = @ColumnName
      AND IsActive = 1;
    
    IF @NodeId IS NULL
    BEGIN
        PRINT 'Column not found in lineage graph.';
        RETURN;
    END
    
    -- Origin summary
    SELECT 
        'Origin Trace Analysis' AS AnalysisType,
        @SchemaName + '.' + @TableName + '.' + @ColumnName AS TargetColumn;
    
    -- Source tables (ultimate origins)
    SELECT DISTINCT
        SourceSchema,
        SourceTable,
        SourceColumn,
        MIN(Depth) AS ShortestPath,
        MAX(Confidence) AS MaxConfidence
    FROM lineage.GetUpstreamLineage(@NodeId, @MaxDepth)
    WHERE NOT EXISTS (
        -- Only show nodes that are true sources (have no upstream)
        SELECT 1 FROM lineage.Edge e2 WHERE e2.TargetNodeId = SourceNodeId
    )
    GROUP BY SourceSchema, SourceTable, SourceColumn
    ORDER BY ShortestPath, SourceSchema, SourceTable;
    
    -- Transformation chain
    SELECT 
        SourceSchema,
        SourceTable,
        SourceColumn,
        TransformationType,
        SourceObject,
        Depth,
        Confidence,
        LineagePath
    FROM lineage.GetUpstreamLineage(@NodeId, @MaxDepth)
    ORDER BY Depth, SourceSchema, SourceTable, SourceColumn;
END
GO

-- =============================================================================
-- SECTION 5: Lineage Visualization Queries
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Get lineage graph for D3.js / visualization
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE lineage.GetLineageGraphForVisualization
    @SchemaName NVARCHAR(128) = NULL,
    @ObjectName NVARCHAR(128) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Nodes
    SELECT 
        n.NodeId AS id,
        n.FullyQualifiedName AS label,
        nt.TypeName AS nodeType,
        n.SchemaName,
        n.ObjectName,
        n.ColumnName,
        n.DataType,
        CASE nt.TypeName
            WHEN 'Table' THEN '#4a90d9'
            WHEN 'View' THEN '#67b7dc'
            WHEN 'Column' THEN '#6794dc'
            WHEN 'Procedure' THEN '#d9534f'
            ELSE '#999999'
        END AS color
    FROM lineage.Node n
    INNER JOIN lineage.NodeType nt ON n.NodeTypeId = nt.NodeTypeId
    WHERE n.IsActive = 1
      AND (@SchemaName IS NULL OR n.SchemaName = @SchemaName)
      AND (@ObjectName IS NULL OR n.ObjectName = @ObjectName OR n.ObjectName LIKE @ObjectName + '%');
    
    -- Edges
    SELECT 
        e.EdgeId AS id,
        e.SourceNodeId AS source,
        e.TargetNodeId AS target,
        tt.TypeName AS transformationType,
        e.Confidence,
        CASE tt.TypeName
            WHEN 'Direct' THEN 2
            WHEN 'Aggregation' THEN 3
            ELSE 1
        END AS weight
    FROM lineage.Edge e
    INNER JOIN lineage.TransformationType tt ON e.TransformationTypeId = tt.TransformationTypeId
    INNER JOIN lineage.Node sn ON e.SourceNodeId = sn.NodeId
    INNER JOIN lineage.Node tn ON e.TargetNodeId = tn.NodeId
    WHERE e.IsActive = 1
      AND (@SchemaName IS NULL OR sn.SchemaName = @SchemaName OR tn.SchemaName = @SchemaName)
      AND (@ObjectName IS NULL OR sn.ObjectName = @ObjectName OR tn.ObjectName = @ObjectName);
END
GO

-- =============================================================================
-- SECTION 6: Maintenance Procedures
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Sync lineage nodes with actual database objects
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE lineage.SyncNodesFromDatabase
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DatabaseName NVARCHAR(128) = DB_NAME();
    DECLARE @InsertedCount INT = 0;
    DECLARE @DeactivatedCount INT = 0;
    
    -- Insert new tables
    INSERT INTO lineage.Node (NodeTypeId, DatabaseName, SchemaName, ObjectName)
    SELECT 1, @DatabaseName, s.name, t.name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    WHERE NOT EXISTS (
        SELECT 1 FROM lineage.Node n 
        WHERE n.SchemaName = s.name 
          AND n.ObjectName = t.name 
          AND n.ColumnName IS NULL
          AND n.NodeTypeId = 1
    );
    SET @InsertedCount = @InsertedCount + @@ROWCOUNT;
    
    -- Insert new views
    INSERT INTO lineage.Node (NodeTypeId, DatabaseName, SchemaName, ObjectName)
    SELECT 2, @DatabaseName, s.name, v.name
    FROM sys.views v
    INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
    WHERE NOT EXISTS (
        SELECT 1 FROM lineage.Node n 
        WHERE n.SchemaName = s.name 
          AND n.ObjectName = v.name 
          AND n.ColumnName IS NULL
          AND n.NodeTypeId = 2
    );
    SET @InsertedCount = @InsertedCount + @@ROWCOUNT;
    
    -- Insert new columns
    INSERT INTO lineage.Node (NodeTypeId, DatabaseName, SchemaName, ObjectName, ColumnName, DataType, IsNullable)
    SELECT 
        3, 
        @DatabaseName, 
        s.name, 
        o.name, 
        c.name,
        t.name,
        c.is_nullable
    FROM sys.columns c
    INNER JOIN sys.objects o ON c.object_id = o.object_id
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE o.type IN ('U', 'V') -- Tables and Views
    AND NOT EXISTS (
        SELECT 1 FROM lineage.Node n 
        WHERE n.SchemaName = s.name 
          AND n.ObjectName = o.name 
          AND n.ColumnName = c.name
    );
    SET @InsertedCount = @InsertedCount + @@ROWCOUNT;
    
    -- Insert procedures
    INSERT INTO lineage.Node (NodeTypeId, DatabaseName, SchemaName, ObjectName)
    SELECT 4, @DatabaseName, s.name, p.name
    FROM sys.procedures p
    INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
    WHERE NOT EXISTS (
        SELECT 1 FROM lineage.Node n 
        WHERE n.SchemaName = s.name 
          AND n.ObjectName = p.name 
          AND n.ColumnName IS NULL
          AND n.NodeTypeId = 4
    );
    SET @InsertedCount = @InsertedCount + @@ROWCOUNT;
    
    -- Deactivate nodes for dropped objects
    UPDATE lineage.Node
    SET IsActive = 0, UpdatedAt = GETDATE()
    WHERE IsActive = 1
      AND NodeTypeId IN (1, 2, 3) -- Tables, Views, Columns
      AND NOT EXISTS (
          SELECT 1 
          FROM sys.objects o
          INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
          WHERE s.name = lineage.Node.SchemaName 
            AND o.name = lineage.Node.ObjectName
      );
    SET @DeactivatedCount = @@ROWCOUNT;
    
    SELECT 
        @InsertedCount AS NodesInserted,
        @DeactivatedCount AS NodesDeactivated,
        GETDATE() AS SyncCompletedAt;
END
GO

-- -----------------------------------------------------------------------------
-- Clear all lineage data (use with caution)
-- -----------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE lineage.ClearAllLineageData
    @Confirm BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @Confirm <> 1
    BEGIN
        PRINT 'To clear all lineage data, call this procedure with @Confirm = 1';
        RETURN;
    END
    
    DELETE FROM lineage.ExtractionWarning;
    DELETE FROM lineage.ExtractionHistory;
    DELETE FROM lineage.Edge;
    DELETE FROM lineage.Node;
    
    PRINT 'All lineage data has been cleared.';
END
GO

-- =============================================================================
-- SECTION 7: Usage Examples
-- =============================================================================

/*
-- Example 1: Find what feeds into a reporting column
EXEC lineage.TraceColumnOrigin 
    @SchemaName = 'rpt', 
    @TableName = 'SalesReport', 
    @ColumnName = 'TotalRevenue';

-- Example 2: Analyze impact before dropping a column
EXEC lineage.AnalyzeColumnImpact 
    @SchemaName = 'dbo', 
    @TableName = 'Customers', 
    @ColumnName = 'CustomerCode';

-- Example 3: Get lineage graph for visualization
EXEC lineage.GetLineageGraphForVisualization 
    @SchemaName = 'dbo';

-- Example 4: Sync nodes from database
EXEC lineage.SyncNodesFromDatabase;

-- Example 5: Query upstream lineage directly
SELECT * FROM lineage.GetUpstreamLineage(
    (SELECT NodeId FROM lineage.Node WHERE SchemaName = 'dbo' AND ObjectName = 'Orders' AND ColumnName = 'TotalAmount'),
    5
);

-- Example 6: Query downstream impact directly
SELECT * FROM lineage.GetDownstreamImpact(
    (SELECT NodeId FROM lineage.Node WHERE SchemaName = 'dbo' AND ObjectName = 'Products' AND ColumnName = 'Price'),
    10
);
*/

PRINT 'Lineage schema and objects created successfully.';
GO
