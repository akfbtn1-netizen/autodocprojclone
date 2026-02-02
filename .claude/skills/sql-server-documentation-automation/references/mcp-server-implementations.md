# MCP Server Implementations for SQL Server

> **Last Updated**: December 2025  
> **Protocol Version**: MCP 1.0 (Agentic AI Foundation)

## Overview

The Model Context Protocol (MCP) has become the universal standard for connecting AI models to databases, with 97M+ monthly SDK downloads and adoption by Claude, ChatGPT, Copilot, Gemini, and VS Code. This document covers SQL Server-specific MCP implementations.

## Protocol Background

### What is MCP?

MCP is an open standard introduced by Anthropic in November 2024 to standardize how AI systems integrate with external tools and data sources. Think of it as "USB-C for AI applications."

**Key Milestones:**
- November 2024: Anthropic launches MCP
- March 2025: OpenAI officially adopts MCP
- December 2025: MCP donated to Agentic AI Foundation (Linux Foundation)
- 10,000+ published MCP servers
- Supported by: Claude, ChatGPT, Cursor, VS Code, Gemini, Microsoft Copilot

### Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   AI Model  │────▶│  MCP Client │────▶│  MCP Server │
│  (Claude)   │     │ (Desktop/VS)│     │  (MSSQL)    │
└─────────────┘     └─────────────┘     └─────────────┘
                          │                    │
                          │    JSON-RPC 2.0    │
                          │    over stdio/SSE  │
                          ▼                    ▼
                    ┌─────────────┐     ┌─────────────┐
                    │   Tools     │     │  SQL Server │
                    │  Resources  │     │  Database   │
                    │   Prompts   │     └─────────────┘
                    └─────────────┘
```

## Available MSSQL MCP Servers

### 1. @executeautomation/database-server (Node.js)

**Repository**: npm package  
**Databases**: SQL Server, PostgreSQL, MySQL, SQLite

**Features:**
- Multi-database support
- Schema introspection
- Query execution
- Table management

**Configuration:**
```json
{
  "mcpServers": {
    "database": {
      "command": "npx",
      "args": ["-y", "@executeautomation/database-server"],
      "env": {
        "MSSQL_CONNECTION_STRING": "Server=localhost;Database=MyDB;User Id=user;Password=pass;Encrypt=true;TrustServerCertificate=true"
      }
    }
  }
}
```

**Tools Exposed:**
- `list_tables` - List all tables in database
- `describe_table` - Get table schema
- `execute_query` - Run SELECT queries
- `get_table_data` - Retrieve table contents

### 2. mssql-mcp-server (Python)

**Repository**: PyPI package  
**Author**: Community maintained

**Features:**
- Python-based for easy customization
- Windows and SQL authentication
- Read-only by default (security)
- Pagination support

**Configuration:**
```json
{
  "mcpServers": {
    "mssql": {
      "command": "uvx",
      "args": ["mssql-mcp-server"],
      "env": {
        "MSSQL_HOST": "localhost",
        "MSSQL_DATABASE": "MyDB",
        "MSSQL_USER": "readonly_user",
        "MSSQL_PASSWORD": "secure_password",
        "MSSQL_PORT": "1433"
      }
    }
  }
}
```

**Tools Exposed:**
- `discover_tables` - Pattern-based table discovery
- `table_details` - Detailed schema information
- `execute_query` - Parameterized queries
- `get_stored_procedures` - List procedures

### 3. dperussina/mssql-mcp-server

**Repository**: GitHub  
**Features**: Pattern-based discovery, natural language prompts

**Unique Features:**
- Natural language query prompts
- Smart schema caching
- Connection pooling
- Query result formatting

### 4. Azure MCP Server (Official Microsoft)

**Repository**: github.com/MicrosoftDocs/mcp  
**Purpose**: Azure SQL Database management

**Configuration:**
```json
{
  "mcpServers": {
    "azure-sql": {
      "type": "remote",
      "url": "https://management.azure.com/mcp"
    }
  }
}
```

**Tools:**
- `azure_sql_query` - Execute queries on Azure SQL
- `azure_sql_databases` - List databases
- `azure_sql_metrics` - Performance metrics

### 5. Microsoft Learn MCP Server

**Repository**: github.com/MicrosoftDocs/mcp  
**Purpose**: Documentation reference

**Configuration:**
```json
{
  "mcpServers": {
    "microsoft-learn": {
      "type": "remote",
      "url": "https://learn.microsoft.com/api/mcp"
    }
  }
}
```

**Tools:**
- `microsoft_docs_search` - Search documentation
- `microsoft_docs_fetch` - Get full doc content
- `microsoft_code_sample_search` - Find code samples

## Building Custom MCP Servers

### Python Template (FastMCP)

```python
from fastmcp import FastMCP, Tool
import pyodbc

mcp = FastMCP("sql-documentation-server")

@mcp.tool()
def get_undocumented_tables(schema: str = "dbo") -> str:
    """Get list of tables without documentation"""
    conn = pyodbc.connect(CONNECTION_STRING)
    cursor = conn.cursor()
    
    query = """
    SELECT s.name + '.' + t.name AS table_name
    FROM sys.tables t
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = t.object_id 
        AND ep.minor_id = 0 
        AND ep.name = 'MS_Description'
    WHERE s.name = ? AND ep.value IS NULL
    """
    
    cursor.execute(query, (schema,))
    tables = [row.table_name for row in cursor.fetchall()]
    
    return json.dumps(tables)

@mcp.tool()
def save_documentation(
    schema: str,
    table: str,
    description: str
) -> str:
    """Save generated documentation to database"""
    conn = pyodbc.connect(CONNECTION_STRING)
    cursor = conn.cursor()
    
    cursor.execute("""
        IF EXISTS (SELECT 1 FROM sys.extended_properties 
                   WHERE major_id = OBJECT_ID(?) AND minor_id = 0)
            EXEC sp_updateextendedproperty 'MS_Description', ?, 
                'SCHEMA', ?, 'TABLE', ?
        ELSE
            EXEC sp_addextendedproperty 'MS_Description', ?,
                'SCHEMA', ?, 'TABLE', ?
    """, (f"{schema}.{table}", description, schema, table, 
          description, schema, table))
    
    conn.commit()
    return json.dumps({"status": "saved"})

if __name__ == "__main__":
    mcp.run()
```

### TypeScript Template (MCP SDK)

```typescript
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import sql from "mssql";

const server = new Server(
  { name: "mssql-documentation", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

server.setRequestHandler("tools/list", async () => ({
  tools: [
    {
      name: "get_schema_documentation",
      description: "Get documentation for database schema",
      inputSchema: {
        type: "object",
        properties: {
          schema: { type: "string", description: "Schema name" }
        }
      }
    }
  ]
}));

server.setRequestHandler("tools/call", async (request) => {
  const { name, arguments: args } = request.params;
  
  if (name === "get_schema_documentation") {
    const pool = await sql.connect(config);
    const result = await pool.request()
      .input("schema", sql.NVarChar, args.schema)
      .query(`
        SELECT o.name, CAST(ep.value AS NVARCHAR(MAX)) AS description
        FROM sys.objects o
        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
        LEFT JOIN sys.extended_properties ep ON ep.major_id = o.object_id
        WHERE s.name = @schema
      `);
    
    return { content: [{ type: "text", text: JSON.stringify(result.recordset) }] };
  }
});

const transport = new StdioServerTransport();
await server.connect(transport);
```

## Security Best Practices

### 1. Principle of Least Privilege

```sql
-- Create dedicated read-only user for MCP
CREATE LOGIN mcp_reader WITH PASSWORD = 'SecurePassword123!';
CREATE USER mcp_reader FOR LOGIN mcp_reader;

-- Grant minimal permissions
GRANT SELECT ON SCHEMA::dbo TO mcp_reader;
GRANT VIEW DEFINITION ON SCHEMA::dbo TO mcp_reader;

-- Deny dangerous operations
DENY INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO mcp_reader;
```

### 2. Query Restrictions

```python
# Implement query validation
BLOCKED_KEYWORDS = ['DROP', 'DELETE', 'TRUNCATE', 'UPDATE', 'INSERT', 'EXEC', 'EXECUTE']

def validate_query(query: str) -> bool:
    query_upper = query.upper()
    return not any(keyword in query_upper for keyword in BLOCKED_KEYWORDS)
```

### 3. Connection Security

```json
{
  "env": {
    "MSSQL_CONNECTION_STRING": "Server=localhost;Database=MyDB;User Id=mcp_reader;Password=${MSSQL_PASSWORD};Encrypt=true;TrustServerCertificate=false;Connection Timeout=30"
  }
}
```

### 4. Output Sanitization

```python
# Mask sensitive columns
SENSITIVE_PATTERNS = ['ssn', 'password', 'creditcard', 'secret']

def sanitize_output(data: dict) -> dict:
    for key in data:
        if any(pattern in key.lower() for pattern in SENSITIVE_PATTERNS):
            data[key] = "***REDACTED***"
    return data
```

## MCP Security Risks (Thoughtworks Technology Radar)

Based on Thoughtworks Technology Radar Vol.33 analysis:

### Known Attack Vectors

1. **Tool Poisoning**: Malicious descriptions in tool definitions
2. **Silent/Mutated Definitions**: Tools that change behavior
3. **Cross-Server Shadowing**: Malicious tools intercepting trusted calls
4. **Prompt Injection via Tools**: Embedding prompts in tool responses

### Mitigations

1. **Use MCP-scan** for vulnerability detection
2. **Implement toxic flow analysis** for data path mapping
3. **Avoid naive API-to-MCP conversion**
4. **Validate all tool definitions**

## Client Configuration Examples

### Claude Desktop

```json
// ~/Library/Application Support/Claude/claude_desktop_config.json (macOS)
// %APPDATA%\Claude\claude_desktop_config.json (Windows)
{
  "mcpServers": {
    "mssql": {
      "command": "npx",
      "args": ["-y", "@executeautomation/database-server"],
      "env": {
        "MSSQL_CONNECTION_STRING": "Server=localhost;Database=Docs;..."
      }
    }
  }
}
```

### VS Code (Copilot)

```json
// .vscode/mcp.json
{
  "servers": {
    "mssql": {
      "type": "stdio",
      "command": "python",
      "args": ["-m", "mssql_mcp_server"],
      "env": {
        "MSSQL_HOST": "localhost"
      }
    }
  }
}
```

### Cursor

```json
// ~/.cursor/mcp.json
{
  "mcpServers": {
    "mssql": {
      "command": "uvx",
      "args": ["mssql-mcp-server"]
    }
  }
}
```

## Troubleshooting

### Common Issues

1. **Connection Refused**
   - Check SQL Server is running
   - Verify firewall rules
   - Confirm TCP/IP is enabled

2. **Authentication Failed**
   - Verify credentials
   - Check SQL authentication mode
   - Confirm user permissions

3. **Tools Not Appearing**
   - Restart AI client after config change
   - Check MCP server logs
   - Verify JSON syntax

4. **Slow Queries**
   - Add query timeout
   - Implement connection pooling
   - Add pagination for large results

## References

- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [MCP TypeScript SDK](https://github.com/modelcontextprotocol/sdk)
- [FastMCP Python Framework](https://github.com/jlowin/fastmcp)
- [Thoughtworks Technology Radar - MCP](https://www.thoughtworks.com/radar/platforms/model-context-protocol-mcp)
- [Agentic AI Foundation](https://www.linuxfoundation.org/projects/agentic-ai)
