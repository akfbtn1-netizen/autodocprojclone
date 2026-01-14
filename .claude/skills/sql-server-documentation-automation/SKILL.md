---
name: sql-server-documentation-automation
description: >
  Enterprise SQL Server documentation automation using SQL Server 2025, AI/LLM integration,
  and MCP servers. Covers automated schema documentation, change detection with DDL triggers,
  data lineage tracking, stored procedure documentation, and CI/CD pipelines. Use when
  automating database documentation, implementing schema change detection, building MCP
  servers for database access, or creating documentation governance systems.
---

# SQL Server Documentation Automation

> **Version**: 2.0 (December 2025)  
> **Focus**: Enterprise-grade database documentation automation using SQL Server 2025, AI/LLM integration, MCP servers, and DevOps pipelines

## Overview

This skill enables automated generation, maintenance, and governance of SQL Server database documentation through AI-powered systems. It covers the complete lifecycle from metadata extraction through schema change detection to AI-generated documentation with full lineage tracking.

### When to Use This Skill

- **Database documentation projects** requiring automated schema documentation
- **SQL Server 2025 AI integration** using native Azure OpenAI features
- **MCP server implementations** for Claude/Copilot database connectivity
- **Schema change detection** with DDL triggers and Event Notifications
- **Data lineage automation** for compliance and governance
- **CI/CD documentation pipelines** using GitHub Actions or Azure DevOps
- **Stored procedure documentation** at scale (100+ procedures)
- **Enterprise metadata management** following ISO 11179 standards

### Key Technologies (2025)

| Category | Technologies |
|----------|--------------|
| Database | SQL Server 2025, Azure SQL, Extended Properties |
| AI/LLM | Azure OpenAI, Claude, GPT-4, Ollama, Custom Fine-tuned Models |
| Integration | MCP Servers (MSSQL, Doccler), REST APIs |
| DevOps | GitHub Actions, Azure DevOps, dbt, Flyway |
| Governance | Data Lineage Tools, ISO 11179, Unity Catalog |

---

## Part 1: SQL Server 2025 AI Integration

SQL Server 2025 introduces revolutionary native AI capabilities that eliminate the complexity of external API integration for documentation automation.

### 1.1 Native Azure OpenAI Integration

SQL Server 2025 allows direct integration with Azure OpenAI through T-SQL, enabling AI-powered documentation generation without leaving the database engine.

```sql
-- Create external model connection to Azure OpenAI
CREATE EXTERNAL MODEL AzureOpenAI_GPT4
WITH (
    LOCATION = 'https://your-resource.openai.azure.com/',
    API_KEY = 'your-api-key',
    MODEL_NAME = 'gpt-4',
    DEPLOYMENT_NAME = 'gpt-4-deployment'
);

-- Generate documentation for a stored procedure
DECLARE @procedure_text NVARCHAR(MAX);
DECLARE @documentation NVARCHAR(MAX);

SELECT @procedure_text = OBJECT_DEFINITION(OBJECT_ID('dbo.usp_ProcessOrders'));

-- Use native AI to generate documentation
SELECT @documentation = ai.generate_text(
    model => 'AzureOpenAI_GPT4',
    prompt => CONCAT(
        'Generate comprehensive technical documentation for this stored procedure. ',
        'Include: purpose, parameters, business logic, dependencies, and example usage.',
        CHAR(10), CHAR(10),
        @procedure_text
    ),
    max_tokens => 2000,
    temperature => 0.3
);

-- Store in extended properties
EXEC sp_addextendedproperty 
    @name = N'MS_Description',
    @value = @documentation,
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'PROCEDURE', @level1name = 'usp_ProcessOrders';
```

### 1.2 Vector Data Type for Semantic Search

SQL Server 2025 introduces native vector storage with DiskANN indexing for semantic search across documentation.

```sql
-- Create documentation table with vector embeddings
CREATE TABLE DocumentationIndex (
    DocumentId INT IDENTITY PRIMARY KEY,
    ObjectType NVARCHAR(50),
    SchemaName NVARCHAR(128),
    ObjectName NVARCHAR(128),
    Description NVARCHAR(MAX),
    Embedding VECTOR(1536),  -- Native vector type
    LastUpdated DATETIME2 DEFAULT GETDATE(),
    GeneratedBy NVARCHAR(50) DEFAULT 'AI_AutoDoc'
);

-- Create DiskANN index for similarity search
CREATE VECTOR INDEX IX_Documentation_Embedding
ON DocumentationIndex(Embedding)
WITH (METRIC = 'cosine', LISTS = 100);

-- Semantic search for related documentation
DECLARE @query_embedding VECTOR(1536);
SET @query_embedding = ai.generate_embedding(
    model => 'text-embedding-ada-002',
    text => 'customer order processing financial transactions'
);

SELECT TOP 10 
    ObjectType,
    SchemaName,
    ObjectName,
    Description,
    VECTOR_DISTANCE('cosine', Embedding, @query_embedding) AS similarity
FROM DocumentationIndex
ORDER BY VECTOR_DISTANCE('cosine', Embedding, @query_embedding);
```

### 1.3 REST Endpoint Integration

Use `sp_invoke_external_rest_endpoint` for calling external AI services.

```sql
-- Call external documentation service
DECLARE @response NVARCHAR(MAX);
DECLARE @request_body NVARCHAR(MAX) = (
    SELECT 
        'Generate documentation' AS task,
        OBJECT_DEFINITION(OBJECT_ID('dbo.usp_CalculateMetrics')) AS source_code
    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
);

EXEC sp_invoke_external_rest_endpoint
    @url = 'https://your-doc-service.azurewebsites.net/api/generate',
    @method = 'POST',
    @headers = '{"Content-Type": "application/json", "Authorization": "Bearer YOUR_TOKEN"}',
    @payload = @request_body,
    @response = @response OUTPUT;

SELECT @response AS DocumentationResult;
```

---

## Part 2: Metadata Extraction & Extended Properties

### 2.1 System Views Deep Dive

SQL Server stores comprehensive metadata in system views that form the foundation for automated documentation.

```sql
-- Comprehensive schema documentation extraction
WITH ObjectMetadata AS (
    SELECT 
        s.name AS SchemaName,
        o.name AS ObjectName,
        o.type_desc AS ObjectType,
        o.create_date AS CreatedDate,
        o.modify_date AS ModifiedDate,
        -- Get existing documentation
        CAST(ep.value AS NVARCHAR(MAX)) AS ExistingDescription,
        -- Calculate documentation completeness
        CASE WHEN ep.value IS NOT NULL THEN 1 ELSE 0 END AS HasDescription
    FROM sys.objects o
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = o.object_id 
        AND ep.minor_id = 0 
        AND ep.name = 'MS_Description'
    WHERE o.is_ms_shipped = 0
        AND o.type IN ('U', 'V', 'P', 'FN', 'TF', 'IF')  -- Tables, Views, Procedures, Functions
),
ColumnMetadata AS (
    SELECT 
        s.name AS SchemaName,
        t.name AS TableName,
        c.name AS ColumnName,
        ty.name AS DataType,
        c.max_length,
        c.precision,
        c.scale,
        c.is_nullable,
        c.is_identity,
        dc.definition AS DefaultValue,
        CAST(ep.value AS NVARCHAR(MAX)) AS ColumnDescription,
        CASE WHEN ep.value IS NOT NULL THEN 1 ELSE 0 END AS HasDescription
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = c.object_id 
        AND ep.minor_id = c.column_id 
        AND ep.name = 'MS_Description'
)
SELECT 
    'Objects' AS Category,
    COUNT(*) AS TotalCount,
    SUM(HasDescription) AS DocumentedCount,
    CAST(100.0 * SUM(HasDescription) / COUNT(*) AS DECIMAL(5,2)) AS DocumentationCoverage
FROM ObjectMetadata
UNION ALL
SELECT 
    'Columns' AS Category,
    COUNT(*) AS TotalCount,
    SUM(HasDescription) AS DocumentedCount,
    CAST(100.0 * SUM(HasDescription) / COUNT(*) AS DECIMAL(5,2)) AS DocumentationCoverage
FROM ColumnMetadata;
```

### 2.2 Extended Properties Management

Extended properties are the native SQL Server mechanism for storing metadata (7500 character limit per property).

```sql
-- Stored procedure for bulk documentation updates
CREATE OR ALTER PROCEDURE dbo.usp_UpdateDocumentation
    @SchemaName NVARCHAR(128),
    @ObjectName NVARCHAR(128),
    @ObjectType NVARCHAR(50),  -- 'TABLE', 'VIEW', 'PROCEDURE', 'COLUMN'
    @ColumnName NVARCHAR(128) = NULL,
    @Description NVARCHAR(MAX),
    @PropertyName NVARCHAR(128) = 'MS_Description'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @level0type NVARCHAR(128) = 'SCHEMA';
    DECLARE @level1type NVARCHAR(128);
    DECLARE @level2type NVARCHAR(128) = NULL;
    DECLARE @level2name NVARCHAR(128) = NULL;
    
    -- Map object types
    SET @level1type = CASE @ObjectType
        WHEN 'TABLE' THEN 'TABLE'
        WHEN 'VIEW' THEN 'VIEW'
        WHEN 'PROCEDURE' THEN 'PROCEDURE'
        WHEN 'FUNCTION' THEN 'FUNCTION'
        WHEN 'COLUMN' THEN 'TABLE'
        ELSE 'TABLE'
    END;
    
    IF @ObjectType = 'COLUMN'
    BEGIN
        SET @level2type = 'COLUMN';
        SET @level2name = @ColumnName;
    END
    
    -- Check if property exists
    IF EXISTS (
        SELECT 1 FROM sys.extended_properties ep
        INNER JOIN sys.objects o ON ep.major_id = o.object_id
        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE s.name = @SchemaName
            AND o.name = @ObjectName
            AND ep.name = @PropertyName
            AND ((@ColumnName IS NULL AND ep.minor_id = 0) 
                 OR (@ColumnName IS NOT NULL AND ep.minor_id = (
                     SELECT column_id FROM sys.columns 
                     WHERE object_id = o.object_id AND name = @ColumnName)))
    )
    BEGIN
        -- Update existing
        IF @level2type IS NULL
            EXEC sp_updateextendedproperty 
                @name = @PropertyName, @value = @Description,
                @level0type = @level0type, @level0name = @SchemaName,
                @level1type = @level1type, @level1name = @ObjectName;
        ELSE
            EXEC sp_updateextendedproperty 
                @name = @PropertyName, @value = @Description,
                @level0type = @level0type, @level0name = @SchemaName,
                @level1type = @level1type, @level1name = @ObjectName,
                @level2type = @level2type, @level2name = @level2name;
    END
    ELSE
    BEGIN
        -- Add new
        IF @level2type IS NULL
            EXEC sp_addextendedproperty 
                @name = @PropertyName, @value = @Description,
                @level0type = @level0type, @level0name = @SchemaName,
                @level1type = @level1type, @level1name = @ObjectName;
        ELSE
            EXEC sp_addextendedproperty 
                @name = @PropertyName, @value = @Description,
                @level0type = @level0type, @level0name = @SchemaName,
                @level1type = @level1type, @level1name = @ObjectName,
                @level2type = @level2type, @level2name = @level2name;
    END
    
    -- Log the update
    INSERT INTO dbo.DocumentationAuditLog (SchemaName, ObjectName, ObjectType, ColumnName, UpdatedAt)
    VALUES (@SchemaName, @ObjectName, @ObjectType, @ColumnName, GETDATE());
END;
GO
```

### 2.3 Bulk Column Documentation with LLM

Based on Databricks' approach (80%+ AI-assisted metadata updates), here's a pattern for bulk column documentation:

```sql
-- Bulk documentation generation procedure
CREATE OR ALTER PROCEDURE dbo.usp_BulkGenerateColumnDocumentation
    @SchemaName NVARCHAR(128) = NULL,
    @TableName NVARCHAR(128) = NULL,
    @OverwriteExisting BIT = 0,
    @ModelName NVARCHAR(100) = 'gpt-4',
    @BatchSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Create temp table for columns needing documentation
    CREATE TABLE #ColumnsToDocument (
        RowNum INT IDENTITY,
        SchemaName NVARCHAR(128),
        TableName NVARCHAR(128),
        ColumnName NVARCHAR(128),
        DataType NVARCHAR(50),
        MaxLength INT,
        IsNullable BIT,
        SampleValues NVARCHAR(MAX),
        TableContext NVARCHAR(MAX)
    );
    
    -- Identify columns needing documentation
    INSERT INTO #ColumnsToDocument (SchemaName, TableName, ColumnName, DataType, MaxLength, IsNullable)
    SELECT 
        s.name,
        t.name,
        c.name,
        ty.name,
        c.max_length,
        c.is_nullable
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = c.object_id 
        AND ep.minor_id = c.column_id 
        AND ep.name = 'MS_Description'
    WHERE (@SchemaName IS NULL OR s.name = @SchemaName)
        AND (@TableName IS NULL OR t.name = @TableName)
        AND (@OverwriteExisting = 1 OR ep.value IS NULL);
    
    DECLARE @TotalColumns INT = (SELECT COUNT(*) FROM #ColumnsToDocument);
    DECLARE @CurrentBatch INT = 1;
    DECLARE @MaxBatch INT = CEILING(@TotalColumns * 1.0 / @BatchSize);
    
    WHILE @CurrentBatch <= @MaxBatch
    BEGIN
        -- Build prompt for batch
        DECLARE @BatchPrompt NVARCHAR(MAX) = 
            'Generate concise column descriptions (max 200 chars each) for these database columns. ' +
            'Return JSON array with columnName and description fields. Consider the column name, ' +
            'data type, and table context to infer the business meaning.' + CHAR(10) + CHAR(10);
        
        SELECT @BatchPrompt = @BatchPrompt + 
            'Table: ' + SchemaName + '.' + TableName + 
            ', Column: ' + ColumnName + 
            ', Type: ' + DataType + 
            CASE WHEN MaxLength > 0 THEN '(' + CAST(MaxLength AS VARCHAR) + ')' ELSE '' END +
            CHAR(10)
        FROM #ColumnsToDocument
        WHERE RowNum BETWEEN ((@CurrentBatch - 1) * @BatchSize + 1) AND (@CurrentBatch * @BatchSize);
        
        -- Call AI model (SQL Server 2025 syntax)
        DECLARE @AIResponse NVARCHAR(MAX);
        SET @AIResponse = ai.generate_text(
            model => @ModelName,
            prompt => @BatchPrompt,
            max_tokens => 4000,
            temperature => 0.2
        );
        
        -- Parse JSON response and update extended properties
        -- (Implementation depends on AI response format)
        
        SET @CurrentBatch = @CurrentBatch + 1;
        
        -- Rate limiting delay
        WAITFOR DELAY '00:00:02';
    END
    
    DROP TABLE #ColumnsToDocument;
END;
GO
```

---

## Part 3: Schema Change Detection

### 3.1 DDL Triggers for Real-Time Detection

DDL triggers capture schema changes in real-time, enabling immediate documentation updates.

```sql
-- Create audit table for schema changes
CREATE TABLE dbo.SchemaChangeLog (
    ChangeId INT IDENTITY PRIMARY KEY,
    EventType NVARCHAR(100),
    ObjectName NVARCHAR(256),
    ObjectType NVARCHAR(50),
    SchemaName NVARCHAR(128),
    TSQLCommand NVARCHAR(MAX),
    LoginName NVARCHAR(128),
    HostName NVARCHAR(128),
    EventDate DATETIME2 DEFAULT GETDATE(),
    EventXML XML,
    DocumentationStatus NVARCHAR(50) DEFAULT 'Pending',
    ProcessedDate DATETIME2 NULL
);

-- Create DDL trigger for database-level events
CREATE OR ALTER TRIGGER TR_DDL_SchemaChangeCapture
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
    DECLARE @EventType NVARCHAR(100) = @EventData.value('(/EVENT_INSTANCE/EventType)[1]', 'NVARCHAR(100)');
    DECLARE @ObjectName NVARCHAR(256) = @EventData.value('(/EVENT_INSTANCE/ObjectName)[1]', 'NVARCHAR(256)');
    DECLARE @ObjectType NVARCHAR(50) = @EventData.value('(/EVENT_INSTANCE/ObjectType)[1]', 'NVARCHAR(50)');
    DECLARE @SchemaName NVARCHAR(128) = @EventData.value('(/EVENT_INSTANCE/SchemaName)[1]', 'NVARCHAR(128)');
    DECLARE @TSQLCommand NVARCHAR(MAX) = @EventData.value('(/EVENT_INSTANCE/TSQLCommand/CommandText)[1]', 'NVARCHAR(MAX)');
    DECLARE @LoginName NVARCHAR(128) = @EventData.value('(/EVENT_INSTANCE/LoginName)[1]', 'NVARCHAR(128)');
    DECLARE @HostName NVARCHAR(128) = HOST_NAME();
    
    -- Filter out routine maintenance (index rebuilds)
    IF @EventType NOT LIKE '%INDEX%' OR @TSQLCommand NOT LIKE '%REBUILD%'
    BEGIN
        INSERT INTO dbo.SchemaChangeLog (
            EventType, ObjectName, ObjectType, SchemaName, 
            TSQLCommand, LoginName, HostName, EventXML
        )
        VALUES (
            @EventType, @ObjectName, @ObjectType, @SchemaName,
            @TSQLCommand, @LoginName, @HostName, @EventData
        );
        
        -- Optional: Send notification for critical changes
        IF @EventType IN ('DROP_TABLE', 'DROP_PROCEDURE')
        BEGIN
            -- Trigger documentation review workflow
            EXEC dbo.usp_NotifyDocumentationTeam 
                @ChangeType = @EventType,
                @ObjectName = @SchemaName + '.' + @ObjectName;
        END
    END
END;
GO
```

### 3.2 Event Notifications (Production-Recommended)

For production systems, Event Notifications are preferred over DDL triggers as they're asynchronous and don't affect transaction performance.

```sql
-- Create Service Broker infrastructure
CREATE QUEUE SchemaChangeQueue;

CREATE SERVICE SchemaChangeService
ON QUEUE SchemaChangeQueue
([http://schemas.microsoft.com/SQL/Notifications/PostEventNotification]);

-- Create event notification
CREATE EVENT NOTIFICATION EN_SchemaChanges
ON DATABASE
FOR DDL_DATABASE_LEVEL_EVENTS
TO SERVICE 'SchemaChangeService', 'current database';

-- Create activation procedure to process changes
CREATE OR ALTER PROCEDURE dbo.usp_ProcessSchemaChangeNotification
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @message_body XML;
    DECLARE @message_type NVARCHAR(256);
    DECLARE @dialog UNIQUEIDENTIFIER;
    
    WHILE (1 = 1)
    BEGIN
        WAITFOR (
            RECEIVE TOP(1)
                @message_type = message_type_name,
                @message_body = CAST(message_body AS XML),
                @dialog = conversation_handle
            FROM SchemaChangeQueue
        ), TIMEOUT 5000;
        
        IF @@ROWCOUNT = 0 BREAK;
        
        IF @message_type = 'http://schemas.microsoft.com/SQL/Notifications/EventNotification'
        BEGIN
            -- Extract event data
            DECLARE @EventType NVARCHAR(100) = @message_body.value('(/EVENT_INSTANCE/EventType)[1]', 'NVARCHAR(100)');
            DECLARE @ObjectName NVARCHAR(256) = @message_body.value('(/EVENT_INSTANCE/ObjectName)[1]', 'NVARCHAR(256)');
            DECLARE @SchemaName NVARCHAR(128) = @message_body.value('(/EVENT_INSTANCE/SchemaName)[1]', 'NVARCHAR(128)');
            
            -- Log change
            INSERT INTO dbo.SchemaChangeLog (EventType, ObjectName, SchemaName, EventXML)
            VALUES (@EventType, @ObjectName, @SchemaName, @message_body);
            
            -- Queue for documentation update
            INSERT INTO dbo.DocumentationUpdateQueue (SchemaName, ObjectName, ChangeType, Priority)
            VALUES (@SchemaName, @ObjectName, @EventType, 
                CASE WHEN @EventType LIKE 'CREATE%' THEN 1 ELSE 2 END);
        END
        
        END CONVERSATION @dialog;
    END
END;
GO

-- Enable activation
ALTER QUEUE SchemaChangeQueue
WITH ACTIVATION (
    STATUS = ON,
    PROCEDURE_NAME = dbo.usp_ProcessSchemaChangeNotification,
    MAX_QUEUE_READERS = 2,
    EXECUTE AS OWNER
);
```

### 3.3 Change Event Streaming (SQL Server 2025)

SQL Server 2025 introduces Change Event Streaming (CES) to Azure Event Hubs for real-time change capture.

```sql
-- Configure Change Event Streaming to Event Hubs
ALTER DATABASE CurrentDb
SET CHANGE_EVENT_STREAMING = ON
WITH (
    EVENT_HUB_ENDPOINT = 'your-namespace.servicebus.windows.net',
    EVENT_HUB_NAME = 'schema-changes',
    AUTHENTICATION = MANAGED_IDENTITY
);

-- Filter to capture only schema changes
ALTER DATABASE CurrentDb
SET CHANGE_EVENT_STREAMING_FILTER = 'DDL_DATABASE_LEVEL_EVENTS';
```

---

## Part 4: MCP Server Integration

The Model Context Protocol (MCP) has become the universal standard for connecting AI models to databases, with 97M+ monthly SDK downloads and adoption by Claude, ChatGPT, Copilot, Gemini, and VS Code.

### 4.1 MSSQL MCP Server Setup

```json
// claude_desktop_config.json
{
  "mcpServers": {
    "mssql": {
      "command": "npx",
      "args": ["-y", "@executeautomation/database-server"],
      "env": {
        "MSSQL_CONNECTION_STRING": "Server=localhost;Database=YourDB;User Id=sa;Password=YourPassword;Encrypt=true;TrustServerCertificate=true"
      }
    },
    "mssql-mcp-python": {
      "command": "uvx",
      "args": ["mssql-mcp-server"],
      "env": {
        "MSSQL_HOST": "localhost",
        "MSSQL_DATABASE": "YourDB",
        "MSSQL_USER": "readonly_user",
        "MSSQL_PASSWORD": "secure_password"
      }
    }
  }
}
```

### 4.2 Custom Documentation MCP Server

Build a specialized MCP server for documentation automation:

```python
# documentation_mcp_server.py
from mcp.server import Server
from mcp.types import Tool, TextContent
import pyodbc
import json

server = Server("sql-documentation-server")

@server.tool()
async def get_undocumented_objects(schema: str = None) -> str:
    """Get list of database objects missing documentation"""
    conn = get_connection()
    cursor = conn.cursor()
    
    query = """
    SELECT 
        s.name AS schema_name,
        o.name AS object_name,
        o.type_desc AS object_type,
        o.create_date,
        o.modify_date
    FROM sys.objects o
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = o.object_id 
        AND ep.minor_id = 0 
        AND ep.name = 'MS_Description'
    WHERE o.is_ms_shipped = 0
        AND ep.value IS NULL
        AND (@schema IS NULL OR s.name = @schema)
    ORDER BY s.name, o.name
    """
    
    cursor.execute(query, (schema,))
    results = cursor.fetchall()
    
    return json.dumps([{
        "schema": row.schema_name,
        "object": row.object_name,
        "type": row.object_type,
        "created": str(row.create_date),
        "modified": str(row.modify_date)
    } for row in results], indent=2)

@server.tool()
async def generate_documentation(
    schema: str, 
    object_name: str,
    include_dependencies: bool = True
) -> str:
    """Generate AI documentation for a database object"""
    conn = get_connection()
    
    # Get object definition
    definition = get_object_definition(conn, schema, object_name)
    
    # Get dependencies if requested
    dependencies = []
    if include_dependencies:
        dependencies = get_dependencies(conn, schema, object_name)
    
    # Build context for AI
    context = {
        "object_name": f"{schema}.{object_name}",
        "definition": definition,
        "dependencies": dependencies,
        "existing_docs": get_existing_docs(conn, schema, object_name)
    }
    
    return json.dumps(context, indent=2)

@server.tool()
async def save_documentation(
    schema: str,
    object_name: str,
    description: str,
    column_docs: dict = None
) -> str:
    """Save generated documentation to extended properties"""
    conn = get_connection()
    cursor = conn.cursor()
    
    try:
        # Update object description
        cursor.execute("""
            EXEC dbo.usp_UpdateDocumentation 
                @SchemaName = ?,
                @ObjectName = ?,
                @ObjectType = 'TABLE',
                @Description = ?
        """, (schema, object_name, description))
        
        # Update column descriptions if provided
        if column_docs:
            for col_name, col_desc in column_docs.items():
                cursor.execute("""
                    EXEC dbo.usp_UpdateDocumentation 
                        @SchemaName = ?,
                        @ObjectName = ?,
                        @ObjectType = 'COLUMN',
                        @ColumnName = ?,
                        @Description = ?
                """, (schema, object_name, col_name, col_desc))
        
        conn.commit()
        return json.dumps({"status": "success", "message": "Documentation saved"})
        
    except Exception as e:
        conn.rollback()
        return json.dumps({"status": "error", "message": str(e)})

@server.tool()
async def get_schema_lineage(object_name: str) -> str:
    """Get data lineage for an object"""
    conn = get_connection()
    cursor = conn.cursor()
    
    # Query dependency graph
    query = """
    WITH DependencyTree AS (
        SELECT 
            OBJECT_NAME(referencing_id) AS referencing_object,
            OBJECT_NAME(referenced_id) AS referenced_object,
            1 AS level
        FROM sys.sql_expression_dependencies
        WHERE referenced_id = OBJECT_ID(@object_name)
        
        UNION ALL
        
        SELECT 
            OBJECT_NAME(d.referencing_id),
            OBJECT_NAME(d.referenced_id),
            dt.level + 1
        FROM sys.sql_expression_dependencies d
        INNER JOIN DependencyTree dt 
            ON OBJECT_NAME(d.referenced_id) = dt.referencing_object
        WHERE dt.level < 10
    )
    SELECT DISTINCT * FROM DependencyTree
    ORDER BY level, referencing_object
    """
    
    cursor.execute(query, (object_name,))
    results = cursor.fetchall()
    
    return json.dumps([{
        "referencing": row.referencing_object,
        "referenced": row.referenced_object,
        "level": row.level
    } for row in results], indent=2)

if __name__ == "__main__":
    import asyncio
    asyncio.run(server.run())
```

### 4.3 Microsoft Learn MCP Server

Use the official Microsoft Learn MCP server for documentation reference:

```json
// Add to claude_desktop_config.json
{
  "mcpServers": {
    "microsoft-learn": {
      "type": "remote",
      "url": "https://learn.microsoft.com/api/mcp"
    }
  }
}
```

System prompt for effective usage:
```
You have access to MCP tools called `microsoft_docs_search`, `microsoft_docs_fetch`, 
and `microsoft_code_sample_search` - these tools allow you to search through and 
fetch Microsoft's latest official documentation and code samples. When handling 
questions about SQL Server, T-SQL, or database development, use these tools for 
research purposes to ensure accuracy.
```

---

## Part 5: Data Lineage Automation

### 5.1 Dependency Analysis

```sql
-- Comprehensive dependency analysis stored procedure
CREATE OR ALTER PROCEDURE dbo.usp_GetObjectLineage
    @SchemaName NVARCHAR(128),
    @ObjectName NVARCHAR(128),
    @Direction NVARCHAR(10) = 'BOTH'  -- 'UPSTREAM', 'DOWNSTREAM', 'BOTH'
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Upstream dependencies (what this object depends on)
    IF @Direction IN ('UPSTREAM', 'BOTH')
    BEGIN
        SELECT 
            'UPSTREAM' AS Direction,
            OBJECT_SCHEMA_NAME(d.referenced_id) AS DependencySchema,
            OBJECT_NAME(d.referenced_id) AS DependencyObject,
            o.type_desc AS DependencyType,
            d.referenced_minor_id,
            COL_NAME(d.referenced_id, d.referenced_minor_id) AS ReferencedColumn,
            d.is_caller_dependent,
            d.is_ambiguous
        FROM sys.sql_expression_dependencies d
        INNER JOIN sys.objects o ON d.referenced_id = o.object_id
        WHERE d.referencing_id = OBJECT_ID(@SchemaName + '.' + @ObjectName)
        ORDER BY DependencySchema, DependencyObject;
    END
    
    -- Downstream dependencies (what depends on this object)
    IF @Direction IN ('DOWNSTREAM', 'BOTH')
    BEGIN
        SELECT 
            'DOWNSTREAM' AS Direction,
            OBJECT_SCHEMA_NAME(d.referencing_id) AS DependentSchema,
            OBJECT_NAME(d.referencing_id) AS DependentObject,
            o.type_desc AS DependentType,
            d.referencing_minor_id
        FROM sys.sql_expression_dependencies d
        INNER JOIN sys.objects o ON d.referencing_id = o.object_id
        WHERE d.referenced_id = OBJECT_ID(@SchemaName + '.' + @ObjectName)
        ORDER BY DependentSchema, DependentObject;
    END
    
    -- Cross-database dependencies
    SELECT 
        d.referenced_server_name AS ServerName,
        d.referenced_database_name AS DatabaseName,
        d.referenced_schema_name AS SchemaName,
        d.referenced_entity_name AS ObjectName,
        'CROSS_DATABASE' AS DependencyType
    FROM sys.sql_expression_dependencies d
    WHERE d.referencing_id = OBJECT_ID(@SchemaName + '.' + @ObjectName)
        AND d.referenced_database_name IS NOT NULL;
END;
GO
```

### 5.2 Lineage Visualization Data Export

```sql
-- Export lineage for visualization tools (Mermaid, D3.js)
CREATE OR ALTER PROCEDURE dbo.usp_ExportLineageForVisualization
    @Format NVARCHAR(20) = 'MERMAID'  -- 'MERMAID', 'D3', 'JSON'
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @Format = 'MERMAID'
    BEGIN
        -- Generate Mermaid flowchart syntax
        SELECT 'flowchart LR' AS MermaidCode
        UNION ALL
        SELECT 
            '    ' + 
            REPLACE(OBJECT_NAME(d.referencing_id), ' ', '_') + 
            ' --> ' + 
            REPLACE(OBJECT_NAME(d.referenced_id), ' ', '_')
        FROM sys.sql_expression_dependencies d
        INNER JOIN sys.objects o ON d.referenced_id = o.object_id
        WHERE o.is_ms_shipped = 0
        ORDER BY MermaidCode;
    END
    
    IF @Format = 'JSON'
    BEGIN
        SELECT 
            (
                SELECT 
                    OBJECT_NAME(d.referencing_id) AS source,
                    OBJECT_NAME(d.referenced_id) AS target,
                    o.type_desc AS targetType
                FROM sys.sql_expression_dependencies d
                INNER JOIN sys.objects o ON d.referenced_id = o.object_id
                WHERE o.is_ms_shipped = 0
                FOR JSON PATH
            ) AS LineageJSON;
    END
END;
GO
```

---

## Part 6: CI/CD Integration

### 6.1 GitHub Actions Workflow

```yaml
# .github/workflows/documentation-automation.yml
name: Database Documentation Automation

on:
  push:
    paths:
      - 'database/**/*.sql'
  schedule:
    - cron: '0 6 * * 1'  # Weekly on Monday 6 AM
  workflow_dispatch:
    inputs:
      full_refresh:
        description: 'Regenerate all documentation'
        required: false
        default: 'false'

env:
  AZURE_SQL_CONNECTION: ${{ secrets.AZURE_SQL_CONNECTION }}
  AZURE_OPENAI_KEY: ${{ secrets.AZURE_OPENAI_KEY }}
  AZURE_OPENAI_ENDPOINT: ${{ secrets.AZURE_OPENAI_ENDPOINT }}

jobs:
  detect-changes:
    runs-on: ubuntu-latest
    outputs:
      changed_objects: ${{ steps.detect.outputs.objects }}
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 2
      
      - name: Detect Changed SQL Objects
        id: detect
        run: |
          CHANGED=$(git diff --name-only HEAD~1 HEAD -- 'database/**/*.sql' | jq -R -s -c 'split("\n")[:-1]')
          echo "objects=$CHANGED" >> $GITHUB_OUTPUT

  generate-documentation:
    needs: detect-changes
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Python
        uses: actions/setup-python@v4
        with:
          python-version: '3.11'
      
      - name: Install Dependencies
        run: |
          pip install pyodbc openai python-docx markdown
      
      - name: Generate Documentation
        run: |
          python scripts/generate_documentation.py \
            --connection "${{ env.AZURE_SQL_CONNECTION }}" \
            --openai-key "${{ env.AZURE_OPENAI_KEY }}" \
            --openai-endpoint "${{ env.AZURE_OPENAI_ENDPOINT }}" \
            --changed-objects '${{ needs.detect-changes.outputs.changed_objects }}' \
            --output-dir ./docs/generated
      
      - name: Commit Documentation
        run: |
          git config --local user.email "action@github.com"
          git config --local user.name "Documentation Bot"
          git add docs/generated/
          git diff --staged --quiet || git commit -m "docs: auto-generated documentation update"
          git push

  validate-documentation:
    needs: generate-documentation
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Validate Documentation Coverage
        run: |
          python scripts/validate_coverage.py \
            --connection "${{ env.AZURE_SQL_CONNECTION }}" \
            --min-coverage 80 \
            --fail-on-missing-critical
      
      - name: Generate Coverage Report
        run: |
          python scripts/coverage_report.py --format markdown > coverage.md
      
      - name: Post Coverage to PR
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v6
        with:
          script: |
            const fs = require('fs');
            const coverage = fs.readFileSync('coverage.md', 'utf8');
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: coverage
            });
```

### 6.2 Azure DevOps Pipeline

```yaml
# azure-pipelines.yml
trigger:
  paths:
    include:
      - database/*

pool:
  vmImage: 'ubuntu-latest'

variables:
  - group: documentation-secrets

stages:
  - stage: Documentation
    jobs:
      - job: GenerateDocs
        steps:
          - task: UsePythonVersion@0
            inputs:
              versionSpec: '3.11'
          
          - script: |
              pip install pyodbc openai sqlalchemy
            displayName: 'Install dependencies'
          
          - task: AzureCLI@2
            displayName: 'Generate AI Documentation'
            inputs:
              azureSubscription: $(AZURE_SUBSCRIPTION)
              scriptType: 'bash'
              scriptLocation: 'inlineScript'
              inlineScript: |
                python scripts/generate_docs.py \
                  --server $(SQL_SERVER) \
                  --database $(SQL_DATABASE) \
                  --output $(Build.ArtifactStagingDirectory)/docs
          
          - task: PublishBuildArtifacts@1
            inputs:
              pathToPublish: $(Build.ArtifactStagingDirectory)/docs
              artifactName: documentation

  - stage: Deploy
    dependsOn: Documentation
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    jobs:
      - job: DeployDocs
        steps:
          - task: DownloadBuildArtifacts@0
            inputs:
              artifactName: documentation
          
          - task: AzureFileCopy@4
            inputs:
              sourcePath: $(System.ArtifactsDirectory)/documentation/*
              azureSubscription: $(AZURE_SUBSCRIPTION)
              destination: AzureBlob
              storage: $(STORAGE_ACCOUNT)
              containerName: documentation
```

### 6.3 dbt Integration for Documentation

```yaml
# dbt_project.yml
name: 'enterprise_data_platform'
version: '1.0.0'

models:
  enterprise_data_platform:
    +persist_docs:
      relation: true
      columns: true

# Generate documentation on build
on-run-end:
  - "{{ dbt_utils.generate_model_yaml(model_names=dbt_run.results|selectattr('status', 'equalto', 'success')|map(attribute='node')|map(attribute='name')|list) }}"
```

```sql
-- models/staging/stg_orders.sql
{{
  config(
    materialized='view',
    description='Staged orders data from source system with initial transformations'
  )
}}

/*
  Model: stg_orders
  Description: Staging layer for orders, applying initial data quality rules
  Source: raw.orders
  Refresh: Daily at 6 AM UTC
  Owner: data-engineering@company.com
*/

SELECT
    order_id,
    customer_id,
    order_date,
    {{ dbt_utils.generate_surrogate_key(['order_id', 'customer_id']) }} as order_key
FROM {{ source('raw', 'orders') }}
WHERE order_date >= '2020-01-01'
```

---

## Part 7: Documentation Tool Comparison

### 7.1 Enterprise Tools Matrix

| Tool | Price | Databases | Output Formats | AI Features | Best For |
|------|-------|-----------|----------------|-------------|----------|
| **ApexSQL Doc** | $699/user | SQL/SSIS/SSAS/SSRS/Tableau | CHM/HTML/PDF/DOC/DOCX/Markdown | None | High customization |
| **Redgate SQL Doc** | $291-385/user | SQL Server only | HTML/PDF/Word/Markdown | None | SSMS integration, speed |
| **Dataedo** | Quote | Oracle/MySQL/PostgreSQL/SQL Server | HTML/PDF/Excel | AI descriptions | Multi-DB, governance |
| **DBInsights.ai** | SaaS | SQL Server/MySQL/Access | HTML/PDF | AI-powered | Full automation |
| **SchemaSpy** | Free (OSS) | Any JDBC | HTML | None | Quick visualization |

### 7.2 Selection Criteria

**Choose ApexSQL Doc when:**
- Need maximum customization of output
- Document SSIS/SSAS/SSRS packages
- Require CHM help file format

**Choose Redgate SQL Doc when:**
- Want fastest generation times
- Need tight SSMS integration
- Prefer simple setup

**Choose Dataedo when:**
- Multi-database environment
- Need data catalog features
- Require change tracking

**Choose DBInsights.ai when:**
- Want full automation
- AI-powered documentation
- Limited DBA resources

---

## Part 8: ISO 11179 Metadata Standards

### 8.1 Naming Convention Validator

```sql
-- ISO 11179 compliant naming convention validator
CREATE OR ALTER FUNCTION dbo.fn_ValidateISO11179Name
(
    @ObjectName NVARCHAR(128),
    @ObjectType NVARCHAR(50)
)
RETURNS TABLE
AS
RETURN
(
    WITH NameParts AS (
        SELECT 
            @ObjectName AS FullName,
            -- Parse components: ObjectClass_Property_Qualifier_RepresentationTerm
            PARSENAME(REPLACE(@ObjectName, '_', '.'), 4) AS ObjectClass,
            PARSENAME(REPLACE(@ObjectName, '_', '.'), 3) AS Property,
            PARSENAME(REPLACE(@ObjectName, '_', '.'), 2) AS Qualifier,
            PARSENAME(REPLACE(@ObjectName, '_', '.'), 1) AS RepresentationTerm
    ),
    ValidationRules AS (
        SELECT
            FullName,
            ObjectClass,
            Property,
            Qualifier,
            RepresentationTerm,
            -- Rule 1: Must have at least ObjectClass and Property
            CASE WHEN ObjectClass IS NOT NULL AND Property IS NOT NULL 
                 THEN 1 ELSE 0 END AS HasRequiredParts,
            -- Rule 2: Valid representation terms
            CASE WHEN RepresentationTerm IN (
                'Amount', 'Code', 'Date', 'DateTime', 'Description', 
                'Identifier', 'Indicator', 'Name', 'Number', 'Percent',
                'Quantity', 'Rate', 'Text', 'Time', 'Value'
            ) THEN 1 ELSE 0 END AS HasValidRepTerm,
            -- Rule 3: No abbreviations in ObjectClass
            CASE WHEN LEN(ObjectClass) >= 3 THEN 1 ELSE 0 END AS NoAbbreviations,
            -- Rule 4: PascalCase check
            CASE WHEN @ObjectName COLLATE Latin1_General_BIN 
                 LIKE '[A-Z]%' THEN 1 ELSE 0 END AS IsPascalCase
        FROM NameParts
    )
    SELECT 
        FullName,
        ObjectClass,
        Property,
        Qualifier,
        RepresentationTerm,
        HasRequiredParts,
        HasValidRepTerm,
        NoAbbreviations,
        IsPascalCase,
        CASE 
            WHEN HasRequiredParts = 1 AND HasValidRepTerm = 1 
                 AND NoAbbreviations = 1 AND IsPascalCase = 1
            THEN 'COMPLIANT'
            ELSE 'NON-COMPLIANT'
        END AS ComplianceStatus,
        CASE 
            WHEN HasRequiredParts = 0 THEN 'Missing ObjectClass or Property; '
            ELSE ''
        END +
        CASE 
            WHEN HasValidRepTerm = 0 THEN 'Invalid RepresentationTerm; '
            ELSE ''
        END +
        CASE 
            WHEN NoAbbreviations = 0 THEN 'ObjectClass too short (abbreviation?); '
            ELSE ''
        END +
        CASE 
            WHEN IsPascalCase = 0 THEN 'Not PascalCase; '
            ELSE ''
        END AS ValidationMessages
    FROM ValidationRules
);
GO

-- Example usage
SELECT * FROM dbo.fn_ValidateISO11179Name('Customer_Mailing_Address_Text', 'COLUMN');
SELECT * FROM dbo.fn_ValidateISO11179Name('cust_addr', 'COLUMN');  -- Non-compliant
```

### 8.2 Metadata Registry Structure

```sql
-- ISO 11179 Metadata Registry tables
CREATE TABLE MetadataRegistry.DataElementConcept (
    ConceptId INT IDENTITY PRIMARY KEY,
    ObjectClassName NVARCHAR(128) NOT NULL,
    PropertyName NVARCHAR(128) NOT NULL,
    Definition NVARCHAR(MAX),
    Context NVARCHAR(256),
    Status NVARCHAR(50) DEFAULT 'Draft',
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    ModifiedDate DATETIME2,
    CONSTRAINT UQ_DataElementConcept UNIQUE (ObjectClassName, PropertyName)
);

CREATE TABLE MetadataRegistry.ValueDomain (
    DomainId INT IDENTITY PRIMARY KEY,
    DomainName NVARCHAR(128) NOT NULL,
    DataType NVARCHAR(50),
    Format NVARCHAR(256),
    MaxLength INT,
    MinValue NVARCHAR(50),
    MaxValue NVARCHAR(50),
    PermissibleValues NVARCHAR(MAX),  -- JSON array
    Definition NVARCHAR(MAX),
    CONSTRAINT UQ_ValueDomain UNIQUE (DomainName)
);

CREATE TABLE MetadataRegistry.DataElement (
    ElementId INT IDENTITY PRIMARY KEY,
    ConceptId INT REFERENCES MetadataRegistry.DataElementConcept(ConceptId),
    DomainId INT REFERENCES MetadataRegistry.ValueDomain(DomainId),
    ElementName NVARCHAR(128) NOT NULL,  -- Full ISO 11179 name
    RepresentationTerm NVARCHAR(50),
    Definition NVARCHAR(MAX),
    UsageNotes NVARCHAR(MAX),
    Status NVARCHAR(50) DEFAULT 'Draft',
    CONSTRAINT UQ_DataElement UNIQUE (ElementName)
);
```

---

## Part 9: AI Documentation Generation Patterns

### 9.1 Tiered Documentation Strategy

Based on research showing **70% time savings** through right-sizing documentation effort:

| Tier | Criteria | Documentation Level | Token Budget |
|------|----------|---------------------|--------------|
| **Critical** | Core business processes, >100 references | Full technical spec + business context | 2000 |
| **Standard** | Regular usage, 10-100 references | Parameter docs + usage examples | 1000 |
| **Basic** | Low usage, <10 references | Auto-generated description only | 200 |

```python
# tiered_documentation.py
def classify_object_tier(conn, schema, object_name):
    """Classify database object into documentation tier"""
    
    # Get reference count
    cursor = conn.cursor()
    cursor.execute("""
        SELECT COUNT(*) as ref_count
        FROM sys.sql_expression_dependencies
        WHERE referenced_id = OBJECT_ID(?)
    """, f"{schema}.{object_name}")
    ref_count = cursor.fetchone().ref_count
    
    # Get object type and complexity
    cursor.execute("""
        SELECT 
            o.type_desc,
            LEN(OBJECT_DEFINITION(o.object_id)) as code_length
        FROM sys.objects o
        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
        WHERE s.name = ? AND o.name = ?
    """, schema, object_name)
    
    result = cursor.fetchone()
    code_length = result.code_length or 0
    
    # Classification logic
    if ref_count > 100 or code_length > 10000:
        return 'CRITICAL', 2000
    elif ref_count > 10 or code_length > 2000:
        return 'STANDARD', 1000
    else:
        return 'BASIC', 200
```

### 9.2 Bespoke LLM Fine-Tuning Pattern

Following Databricks' approach (2 engineers, 1 month, <$1000):

```python
# fine_tune_documentation_model.py
from transformers import AutoModelForCausalLM, AutoTokenizer, TrainingArguments
from datasets import Dataset
import json

def prepare_training_data():
    """Generate synthetic training data for documentation model"""
    
    # Use NAICS codes and internal taxonomy for diversity
    training_examples = []
    
    # Template for training examples
    template = {
        "input": "Generate documentation for this SQL object:\n{create_statement}",
        "output": "## {object_name}\n\n**Purpose**: {purpose}\n\n**Parameters**:\n{parameters}\n\n**Returns**: {returns}\n\n**Example**:\n```sql\n{example}\n```"
    }
    
    # Generate ~3600 examples (Databricks found this sufficient)
    # ... generation logic ...
    
    return Dataset.from_list(training_examples)

def fine_tune_model(base_model="mistralai/Mistral-7B-v0.1"):
    """Fine-tune model for SQL documentation generation"""
    
    model = AutoModelForCausalLM.from_pretrained(base_model)
    tokenizer = AutoTokenizer.from_pretrained(base_model)
    
    training_args = TrainingArguments(
        output_dir="./sql-doc-model",
        num_train_epochs=3,
        per_device_train_batch_size=4,
        gradient_accumulation_steps=4,
        learning_rate=2e-5,
        warmup_steps=100,
        logging_steps=10,
        save_steps=500,
        fp16=True
    )
    
    # Training takes ~15 minutes on A10 GPU
    # Total cost: ~$5-10 per training run
    
    return model, tokenizer
```

### 9.3 Dual-Process Description Generation

Based on 2025 research on automatic database description generation:

```python
# dual_process_description.py
def generate_column_descriptions(conn, table_name, model):
    """
    Dual-process approach for column descriptions:
    1. Coarse-to-fine: Database -> Table -> Column context
    2. Fine-to-coarse: Column details -> Table understanding
    """
    
    # Phase 1: Coarse-to-fine (get table context first)
    table_context = get_table_context(conn, table_name)
    
    # Phase 2: Get column relationships
    column_relations = get_column_relations(conn, table_name)
    
    # Phase 3: Generate descriptions with full context
    prompt = f"""
    Given this database table context:
    {table_context}
    
    And these column relationships:
    {column_relations}
    
    Generate concise descriptions (max 20 words each) for each column.
    Consider:
    - The column name and data type
    - Relationships with other columns in the same table
    - The overall table purpose
    
    Return JSON: {{"column_name": "description", ...}}
    """
    
    response = model.generate(prompt, max_tokens=2000, temperature=0.2)
    
    return json.loads(response)
```

---

## Part 10: Security Considerations

### 10.1 MCP Security Best Practices

Following Thoughtworks Technology Radar Vol.33 guidance on MCP security:

```python
# Toxic flow analysis for MCP documentation servers
def analyze_mcp_data_flows(mcp_config):
    """
    Map data flows through MCP servers to identify vulnerabilities.
    Based on 'toxic flow analysis for AI' technique.
    """
    
    security_checks = {
        "tool_poisoning": check_tool_descriptions(mcp_config),
        "credential_exposure": check_credential_handling(mcp_config),
        "sql_injection": check_query_sanitization(mcp_config),
        "data_exfiltration": check_output_filtering(mcp_config),
        "cross_server_shadowing": check_tool_namespaces(mcp_config)
    }
    
    return security_checks

def secure_mcp_configuration():
    """Generate secure MCP server configuration"""
    
    return {
        "security": {
            # Principle of least privilege
            "database_permissions": "db_datareader",  # Read-only
            "allowed_schemas": ["dbo", "staging"],
            "blocked_tables": ["Users", "Credentials", "AuditLog"],
            
            # Query restrictions
            "max_rows_returned": 1000,
            "query_timeout_seconds": 30,
            "blocked_commands": ["DROP", "DELETE", "TRUNCATE", "UPDATE", "INSERT"],
            
            # Output sanitization
            "mask_pii_columns": True,
            "pii_patterns": ["SSN", "CreditCard", "Password", "Email"]
        }
    }
```

### 10.2 Credential Management

```sql
-- Use SQL Server credential management for API keys
CREATE CREDENTIAL AzureOpenAI_Credential
WITH IDENTITY = 'AZURE_OPENAI',
SECRET = 'your-api-key-here';

-- Create database scoped credential for external services
CREATE DATABASE SCOPED CREDENTIAL DocGenService_Credential
WITH IDENTITY = 'DOCUMENTATION_SERVICE',
SECRET = 'service-api-key';

-- Never store credentials in extended properties or comments!
```

---

## Part 11: Monitoring & Observability

### 11.1 Documentation Health Dashboard

```sql
-- Create documentation health monitoring view
CREATE OR ALTER VIEW dbo.vw_DocumentationHealth AS
WITH ObjectCoverage AS (
    SELECT 
        s.name AS SchemaName,
        o.type_desc AS ObjectType,
        COUNT(*) AS TotalObjects,
        SUM(CASE WHEN ep.value IS NOT NULL THEN 1 ELSE 0 END) AS DocumentedObjects
    FROM sys.objects o
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = o.object_id 
        AND ep.minor_id = 0 
        AND ep.name = 'MS_Description'
    WHERE o.is_ms_shipped = 0
        AND o.type IN ('U', 'V', 'P', 'FN', 'TF', 'IF')
    GROUP BY s.name, o.type_desc
),
ColumnCoverage AS (
    SELECT 
        s.name AS SchemaName,
        COUNT(*) AS TotalColumns,
        SUM(CASE WHEN ep.value IS NOT NULL THEN 1 ELSE 0 END) AS DocumentedColumns
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = c.object_id 
        AND ep.minor_id = c.column_id 
        AND ep.name = 'MS_Description'
    GROUP BY s.name
),
Freshness AS (
    SELECT 
        SchemaName,
        ObjectName,
        DATEDIFF(DAY, ModifiedDate, GETDATE()) AS DaysSinceModified,
        DATEDIFF(DAY, LastDocUpdate, GETDATE()) AS DaysSinceDocUpdate
    FROM dbo.DocumentationAuditLog
)
SELECT 
    oc.SchemaName,
    oc.ObjectType,
    oc.TotalObjects,
    oc.DocumentedObjects,
    CAST(100.0 * oc.DocumentedObjects / NULLIF(oc.TotalObjects, 0) AS DECIMAL(5,2)) AS ObjectCoverage,
    cc.TotalColumns,
    cc.DocumentedColumns,
    CAST(100.0 * cc.DocumentedColumns / NULLIF(cc.TotalColumns, 0) AS DECIMAL(5,2)) AS ColumnCoverage,
    AVG(f.DaysSinceDocUpdate) AS AvgDocAge
FROM ObjectCoverage oc
LEFT JOIN ColumnCoverage cc ON oc.SchemaName = cc.SchemaName
LEFT JOIN Freshness f ON oc.SchemaName = f.SchemaName
GROUP BY oc.SchemaName, oc.ObjectType, oc.TotalObjects, oc.DocumentedObjects,
         cc.TotalColumns, cc.DocumentedColumns;
GO
```

### 11.2 Alerting on Documentation Gaps

```sql
-- Alert when new objects lack documentation after 7 days
CREATE OR ALTER PROCEDURE dbo.usp_AlertDocumentationGaps
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @GapReport NVARCHAR(MAX);
    
    SELECT @GapReport = (
        SELECT 
            s.name AS schema_name,
            o.name AS object_name,
            o.type_desc AS object_type,
            o.create_date,
            DATEDIFF(DAY, o.create_date, GETDATE()) AS days_without_docs
        FROM sys.objects o
        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
        LEFT JOIN sys.extended_properties ep 
            ON ep.major_id = o.object_id 
            AND ep.minor_id = 0 
            AND ep.name = 'MS_Description'
        WHERE o.is_ms_shipped = 0
            AND ep.value IS NULL
            AND DATEDIFF(DAY, o.create_date, GETDATE()) > 7
        FOR JSON PATH
    );
    
    IF @GapReport IS NOT NULL
    BEGIN
        -- Send alert (Teams, email, etc.)
        EXEC msdb.dbo.sp_send_dbmail
            @profile_name = 'Documentation Alerts',
            @recipients = 'data-team@company.com',
            @subject = 'Documentation Gap Alert',
            @body = @GapReport,
            @body_format = 'TEXT';
    END
END;
GO
```

---

## Quick Reference

### Essential Queries

```sql
-- Get documentation coverage summary
SELECT 
    'Tables' AS Category, 
    COUNT(*) AS Total,
    SUM(CASE WHEN ep.value IS NOT NULL THEN 1 ELSE 0 END) AS Documented
FROM sys.tables t
LEFT JOIN sys.extended_properties ep ON ep.major_id = t.object_id AND ep.minor_id = 0;

-- Find undocumented critical objects (>50 dependencies)
SELECT o.name, COUNT(d.referencing_id) AS DependencyCount
FROM sys.objects o
LEFT JOIN sys.sql_expression_dependencies d ON d.referenced_id = o.object_id
LEFT JOIN sys.extended_properties ep ON ep.major_id = o.object_id AND ep.minor_id = 0
WHERE ep.value IS NULL AND o.is_ms_shipped = 0
GROUP BY o.name HAVING COUNT(d.referencing_id) > 50;

-- Export all documentation to JSON
SELECT 
    s.name AS schema_name,
    o.name AS object_name,
    CAST(ep.value AS NVARCHAR(MAX)) AS description
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
INNER JOIN sys.extended_properties ep ON ep.major_id = o.object_id AND ep.minor_id = 0
FOR JSON PATH;
```

### MCP Configuration Templates

```json
// Minimal secure MSSQL MCP setup
{
  "mcpServers": {
    "sql-docs": {
      "command": "npx",
      "args": ["-y", "@executeautomation/database-server"],
      "env": {
        "MSSQL_CONNECTION_STRING": "Server=localhost;Database=Docs;User=readonly;Password=***;Encrypt=true"
      }
    }
  }
}
```

---

## References

### Official Documentation
- Microsoft SQL Server 2025 Documentation
- Model Context Protocol Specification (modelcontextprotocol.io)
- ISO/IEC 11179 Metadata Registries Standard

### Research Papers
- "Synthetic SQL Column Descriptions and Their Impact on Text-to-SQL Performance" (arXiv, 2024)
- "Automatic Database Description Generation for Text-to-SQL" (arXiv, 2025)
- Stanford HAI 2025 AI Index Report

### Industry Analysis
- Thoughtworks Technology Radar Vol.33 (November 2025)
- Gartner Hype Cycle for Data & Analytics Governance 2025
- Databricks: Creating a Bespoke LLM for AI-Generated Documentation

### Tools & Platforms
- DBInsights.ai - AI-powered database documentation
- Dataedo - Multi-database documentation platform
- ApexSQL Doc / Redgate SQL Doc - Enterprise documentation tools
- dbt - Data transformation and documentation

---

**Skill Version**: 2.0  
**Last Updated**: December 2025  
**Sources Synthesized**: 100+  
**Maintainer**: Enterprise Documentation Automation Team
