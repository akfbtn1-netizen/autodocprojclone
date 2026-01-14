-- ============================================
-- MIGRATION 006: Smart Search & Discovery Agent
-- Created: 2026-01-12
-- Purpose: Support hybrid search, GraphRAG, continuous learning
-- Tables: 7 new tables for Agent #6
-- ============================================

USE [IRFS1];
GO

-- ===== TABLE 1: SearchQueries =====
-- Stores every search query for analytics and learning

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SearchQueries' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[SearchQueries] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [QueryId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [UserId] NVARCHAR(100) NOT NULL,
        [QueryText] NVARCHAR(2000) NOT NULL,
        [QueryType] NVARCHAR(50) NOT NULL, -- 'Simple', 'Semantic', 'Relationship', 'Metadata', 'Agentic'
        [RoutingPath] NVARCHAR(50) NOT NULL, -- 'Path1', 'Path2', 'Path3', 'Path4', 'Path5'
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
    PRINT '  Created table [DaQa].[SearchQueries]';
END
GO

-- ===== TABLE 2: SearchResults =====
-- Stores which results were returned for each query

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SearchResults' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[SearchResults] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [QueryId] UNIQUEIDENTIFIER NOT NULL,
        [DocumentId] NVARCHAR(100) NOT NULL,
        [ObjectType] NVARCHAR(50) NOT NULL, -- 'Table', 'Column', 'Procedure', 'View'
        [ObjectName] NVARCHAR(255) NOT NULL,
        [SchemaName] NVARCHAR(50) NULL,
        [Rank] INT NOT NULL, -- 1-based position in results
        [RelevanceScore] DECIMAL(5,4) NOT NULL, -- 0.0000 to 1.0000
        [WasClicked] BIT NOT NULL DEFAULT 0,
        [TimeSpentSeconds] INT NULL, -- How long user viewed this result
        [WasExported] BIT NOT NULL DEFAULT 0,
        [WasShared] BIT NOT NULL DEFAULT 0,

        INDEX IX_SearchResults_QueryId (QueryId),
        INDEX IX_SearchResults_WasClicked (WasClicked, Rank)
    );
    PRINT '  Created table [DaQa].[SearchResults]';
END
GO

-- ===== TABLE 3: UserInteractions =====
-- Detailed user interaction tracking for continuous learning

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserInteractions' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[UserInteractions] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [InteractionId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [QueryId] UNIQUEIDENTIFIER NOT NULL,
        [UserId] NVARCHAR(100) NOT NULL,
        [InteractionType] NVARCHAR(50) NOT NULL, -- 'Click', 'Export', 'Share', 'NotHelpful', 'FollowUp'
        [DocumentId] NVARCHAR(100) NULL,
        [InteractionData] NVARCHAR(MAX) NULL, -- JSON payload
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_UserInteractions_QueryId (QueryId),
        INDEX IX_UserInteractions_UserId (UserId),
        INDEX IX_UserInteractions_Timestamp (Timestamp DESC)
    );
    PRINT '  Created table [DaQa].[UserInteractions]';
END
GO

-- ===== TABLE 4: EmbeddingCache =====
-- Stores generated embeddings to avoid regeneration

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmbeddingCache' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[EmbeddingCache] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [DocumentId] NVARCHAR(100) NOT NULL,
        [EmbeddingType] NVARCHAR(50) NOT NULL, -- 'NaturalLanguage', 'Structured'
        [EmbeddingText] NVARCHAR(MAX) NOT NULL, -- The text that was embedded
        [EmbeddingVector] VARBINARY(MAX) NULL, -- Future: Store vector in SQL
        [QdrantPointId] NVARCHAR(100) NOT NULL, -- UUID in Qdrant
        [ModelVersion] NVARCHAR(50) NOT NULL, -- 'text-embedding-3-large-2024'
        [GeneratedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsStale] BIT NOT NULL DEFAULT 0, -- Mark for regeneration

        INDEX UX_EmbeddingCache_Document_Type UNIQUE (DocumentId, EmbeddingType),
        INDEX IX_EmbeddingCache_QdrantPointId (QdrantPointId),
        INDEX IX_EmbeddingCache_IsStale (IsStale)
    );
    PRINT '  Created table [DaQa].[EmbeddingCache]';
END
GO

-- ===== TABLE 5: CategorySuggestions =====
-- AI-generated category suggestions awaiting human approval

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CategorySuggestions' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[CategorySuggestions] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SuggestionId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [DocumentId] NVARCHAR(100) NOT NULL,
        [CurrentCategory] NVARCHAR(100) NULL,
        [SuggestedCategory] NVARCHAR(100) NOT NULL,
        [ConfidenceScore] DECIMAL(5,4) NOT NULL, -- 0.0000 to 1.0000
        [Reasoning] NVARCHAR(MAX) NULL, -- AI explanation
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected'
        [ReviewedBy] NVARCHAR(100) NULL,
        [ReviewedAt] DATETIME2 NULL,
        [ReviewNotes] NVARCHAR(1000) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_CategorySuggestions_Status (Status),
        INDEX IX_CategorySuggestions_ConfidenceScore (ConfidenceScore DESC),
        INDEX IX_CategorySuggestions_DocumentId (DocumentId)
    );
    PRINT '  Created table [DaQa].[CategorySuggestions]';
END
GO

-- ===== TABLE 6: GraphNodes =====
-- In-memory graph cache (supports future Neo4j migration)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GraphNodes' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[GraphNodes] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [NodeId] NVARCHAR(100) NOT NULL,
        [NodeType] NVARCHAR(50) NOT NULL, -- 'Table', 'Column', 'Procedure', 'View', 'Function'
        [NodeName] NVARCHAR(255) NOT NULL,
        [SchemaName] NVARCHAR(50) NULL,
        [DatabaseName] NVARCHAR(100) NULL,
        [Properties] NVARCHAR(MAX) NULL, -- JSON: {classification, pii_type, owner, etc}
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX UX_GraphNodes_NodeId UNIQUE (NodeId),
        INDEX IX_GraphNodes_NodeType (NodeType),
        INDEX IX_GraphNodes_NodeName (NodeName)
    );
    PRINT '  Created table [DaQa].[GraphNodes]';
END
GO

-- ===== TABLE 7: GraphEdges =====
-- Relationship edges between nodes (PII_FLOW tracking for compliance)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GraphEdges' AND schema_id = SCHEMA_ID('DaQa'))
BEGIN
    CREATE TABLE [DaQa].[GraphEdges] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EdgeId] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        [SourceNodeId] NVARCHAR(100) NOT NULL,
        [TargetNodeId] NVARCHAR(100) NOT NULL,
        [EdgeType] NVARCHAR(50) NOT NULL, -- 'DEPENDS_ON', 'CONTAINS', 'READS_FROM', 'WRITES_TO', 'PII_FLOW'
        [EdgeWeight] DECIMAL(5,4) NULL, -- Strength of relationship (0.0000 to 1.0000)
        [Properties] NVARCHAR(MAX) NULL, -- JSON metadata
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_GraphEdges_Source (SourceNodeId),
        INDEX IX_GraphEdges_Target (TargetNodeId),
        INDEX IX_GraphEdges_Type (EdgeType),
        INDEX IX_GraphEdges_PiiFlow (EdgeType) WHERE EdgeType = 'PII_FLOW'
    );
    PRINT '  Created table [DaQa].[GraphEdges]';
END
GO

-- ===== Add Foreign Key after both tables exist =====
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
PRINT '============================================';
PRINT 'Migration 006 Complete: Smart Search Tables';
PRINT '============================================';
PRINT 'Tables created:';
PRINT '  1. SearchQueries - Query analytics';
PRINT '  2. SearchResults - Result tracking';
PRINT '  3. UserInteractions - Learning feedback';
PRINT '  4. EmbeddingCache - Vector cache';
PRINT '  5. CategorySuggestions - AI suggestions';
PRINT '  6. GraphNodes - GraphRAG nodes';
PRINT '  7. GraphEdges - GraphRAG edges (PII_FLOW)';
PRINT '============================================';
GO
