---
name: tsql-scriptdom-lineage
description: |
  T-SQL static analysis and column-level lineage extraction using Microsoft ScriptDom. 
  Covers AST parsing, visitor patterns, schema-aware column resolution, and graph-based 
  lineage storage. Use when extracting column lineage from stored procedures, implementing 
  impact analysis for schema changes, building automated SQL documentation, or performing 
  data governance analysis.
---

# T-SQL ScriptDom Static Analysis & Column-Level Lineage Extraction

## Skill Overview

This skill provides comprehensive guidance for implementing T-SQL static analysis using Microsoft's ScriptDom library, with a focus on extracting column-level data lineage from stored procedures, views, and functions. The skill covers AST parsing, visitor patterns, schema-aware column resolution, and graph-based lineage storage.

**Primary Use Cases:**
- Extract column-level lineage from T-SQL stored procedures
- Build automated documentation for database objects
- Implement impact analysis for schema changes
- Create data governance and compliance reporting
- Detect code quality issues through static analysis

**Trigger Keywords:** ScriptDom, T-SQL parsing, column lineage, AST analysis, SQL static analysis, data flow analysis, stored procedure parsing, SQL dependency analysis, impact analysis

---

## Core Technology: Microsoft ScriptDom

### What is ScriptDom?

ScriptDom is Microsoft's official T-SQL parser library that provides full-fidelity parsing of T-SQL syntax. It parses SQL text into an Abstract Syntax Tree (AST) that can be traversed and analyzed programmatically.

**Key Characteristics:**
- **Full-fidelity parsing**: Uses the same grammar as SQL Server's query engine
- **MIT Licensed**: Open-sourced in April 2023 on GitHub
- **Version-specific parsers**: TSql80Parser through TSql160Parser for different SQL Server versions
- **No database connection required**: Pure client-side parsing
- **Cross-platform**: .NET Framework and .NET Core assemblies

### Installation

```xml
<!-- NuGet Package -->
<PackageReference Include="Microsoft.SqlServer.TransactSql.ScriptDom" Version="161.8901.0" />

<!-- Or via DacFx -->
<PackageReference Include="Microsoft.SqlServer.DacFx" Version="162.2.111" />
```

### Parser Selection by SQL Server Version

| SQL Server Version | Parser Class |
|-------------------|--------------|
| SQL Server 2000 | TSql80Parser |
| SQL Server 2005 | TSql90Parser |
| SQL Server 2008 | TSql100Parser |
| SQL Server 2012 | TSql110Parser |
| SQL Server 2014 | TSql120Parser |
| SQL Server 2016 | TSql130Parser |
| SQL Server 2017 | TSql140Parser |
| SQL Server 2019 | TSql150Parser |
| SQL Server 2022 / Azure SQL | TSql160Parser |

---

## Part 1: AST Fundamentals

### Basic Parsing Example

```csharp
using Microsoft.SqlServer.TransactSql.ScriptDom;

public class TsqlParser
{
    public TSqlFragment ParseSql(string sql)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        IList<ParseError> errors;
        
        using var reader = new StringReader(sql);
        var fragment = parser.Parse(reader, out errors);
        
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.WriteLine($"Line {error.Line}, Col {error.Column}: {error.Message}");
            }
            throw new InvalidOperationException("SQL parsing failed");
        }
        
        return fragment;
    }
}
```

### Understanding TSqlFragment Hierarchy

The AST consists of strongly-typed nodes representing SQL constructs:

```
TSqlFragment (base class)
├── TSqlScript (root - contains batches)
│   └── TSqlBatch (contains statements)
│       └── TSqlStatement (abstract)
│           ├── SelectStatement
│           ├── InsertStatement
│           ├── UpdateStatement
│           ├── DeleteStatement
│           ├── MergeStatement
│           ├── CreateProcedureStatement
│           ├── CreateViewStatement
│           └── ... (850+ concrete types)
├── QueryExpression (abstract)
│   ├── QuerySpecification (actual SELECT)
│   ├── BinaryQueryExpression (UNION, EXCEPT, INTERSECT)
│   └── QueryParenthesisExpression
├── TableReference (abstract)
│   ├── NamedTableReference
│   ├── JoinTableReference
│   ├── QueryDerivedTable (subquery)
│   └── ...
└── ScalarExpression (abstract)
    ├── ColumnReferenceExpression
    ├── FunctionCall
    ├── CastCall
    ├── CaseExpression
    └── ...
```

---

## Part 2: Visitor Pattern Implementation

### TSqlConcreteFragmentVisitor vs TSqlFragmentVisitor

**TSqlConcreteFragmentVisitor (Recommended)**
- Has Visit methods only for concrete types (856 types)
- Each fragment visited once
- Simpler, more efficient

**TSqlFragmentVisitor**
- Has Visit methods for all types including abstract (984 types)
- Same fragment may be visited multiple times (as TSqlFragment, base type, and concrete type)
- Use only when you need to catch abstract type patterns

### Basic Visitor Implementation

```csharp
public class ColumnReferenceVisitor : TSqlConcreteFragmentVisitor
{
    public List<ColumnReference> Columns { get; } = new();
    
    public override void Visit(ColumnReferenceExpression node)
    {
        var column = new ColumnReference
        {
            ColumnName = node.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value,
            TableAlias = GetTableAlias(node.MultiPartIdentifier),
            Line = node.StartLine,
            Column = node.StartColumn
        };
        
        Columns.Add(column);
        base.Visit(node);
    }
    
    private string GetTableAlias(MultiPartIdentifier identifier)
    {
        if (identifier?.Identifiers.Count >= 2)
        {
            return identifier.Identifiers[identifier.Identifiers.Count - 2].Value;
        }
        return null;
    }
}

public record ColumnReference
{
    public string ColumnName { get; init; }
    public string TableAlias { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
}
```

### Traversal Execution

```csharp
public List<ColumnReference> ExtractColumns(string sql)
{
    var fragment = ParseSql(sql);
    var visitor = new ColumnReferenceVisitor();
    fragment.Accept(visitor);
    return visitor.Columns;
}
```

### Handling Large/Deeply Nested SQL (Avoiding Stack Overflow)

**Known Issue:** Recursive visitor pattern can cause stack overflow with thousands of UNIONs or deeply nested queries (GitHub Issue #49).

**Solution: Iterative Processing**

```csharp
public class IterativeVisitor
{
    public void ProcessIteratively(TSqlFragment root)
    {
        var stack = new Stack<TSqlFragment>();
        stack.Push(root);
        
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            ProcessNode(current);
            
            // Add children to stack (in reverse order for correct processing order)
            foreach (var child in GetChildren(current).Reverse())
            {
                stack.Push(child);
            }
        }
    }
    
    private IEnumerable<TSqlFragment> GetChildren(TSqlFragment node)
    {
        // Use reflection or type-specific logic to enumerate children
        // Example for BinaryQueryExpression:
        if (node is BinaryQueryExpression bqe)
        {
            if (bqe.FirstQueryExpression != null) yield return bqe.FirstQueryExpression;
            if (bqe.SecondQueryExpression != null) yield return bqe.SecondQueryExpression;
        }
        // Add other types as needed
    }
}
```

---

## Part 3: Column-Level Lineage Extraction

### The Challenge: Schema-Aware Parsing

**Critical Insight:** Pure SQL parsing without schema metadata cannot accurately resolve column lineage for queries with unqualified column names.

```sql
-- Which table does 'name' come from?
SELECT name, email 
FROM customers c
JOIN orders o ON c.id = o.customer_id
```

Without knowing the schemas of `customers` and `orders`, the parser cannot determine which table each unqualified column belongs to.

### Lineage Extraction Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Lineage Extraction Pipeline                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐    ┌─────────┐    ┌──────────────┐    ┌────────┐ │
│  │ T-SQL    │───▶│ Script  │───▶│ Schema-Aware │───▶│ Lineage│ │
│  │ Source   │    │ DOM AST │    │ Column       │    │ Graph  │ │
│  │          │    │         │    │ Resolver     │    │        │ │
│  └──────────┘    └─────────┘    └──────────────┘    └────────┘ │
│                        ▲               ▲                        │
│                        │               │                        │
│                  ┌─────┴─────┐   ┌─────┴─────┐                  │
│                  │ SQL Server│   │ Metadata  │                  │
│                  │ Metadata  │   │ Catalog   │                  │
│                  │ Views     │   │ Cache     │                  │
│                  └───────────┘   └───────────┘                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Comprehensive Lineage Visitor

```csharp
public class LineageExtractorVisitor : TSqlConcreteFragmentVisitor
{
    private readonly ISchemaMetadataProvider _schemaProvider;
    private readonly Stack<QueryContext> _contextStack = new();
    private readonly List<LineageEdge> _lineageEdges = new();
    
    public LineageExtractorVisitor(ISchemaMetadataProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }
    
    public IReadOnlyList<LineageEdge> LineageEdges => _lineageEdges;
    
    // Track CTEs
    private readonly Dictionary<string, QueryExpression> _cteDefinitions = new();
    
    public override void Visit(CommonTableExpression node)
    {
        var cteName = node.ExpressionName.Value;
        _cteDefinitions[cteName] = node.QueryExpression;
        base.Visit(node);
    }
    
    public override void Visit(InsertStatement node)
    {
        var targetTable = ExtractTableName(node.InsertSpecification.Target);
        var targetColumns = ExtractInsertColumns(node.InsertSpecification);
        
        // Push context for processing source
        _contextStack.Push(new QueryContext
        {
            TargetTable = targetTable,
            TargetColumns = targetColumns,
            StatementType = StatementType.Insert
        });
        
        base.Visit(node);
        _contextStack.Pop();
    }
    
    public override void Visit(UpdateStatement node)
    {
        var targetTable = ExtractUpdateTarget(node);
        
        _contextStack.Push(new QueryContext
        {
            TargetTable = targetTable,
            StatementType = StatementType.Update
        });
        
        // Process SET clauses
        foreach (var setClause in node.UpdateSpecification.SetClauses)
        {
            if (setClause is AssignmentSetClause assignment)
            {
                var targetColumn = ExtractColumnName(assignment.Column);
                var sourceExpression = assignment.NewValue;
                
                ProcessAssignment(targetTable, targetColumn, sourceExpression);
            }
        }
        
        base.Visit(node);
        _contextStack.Pop();
    }
    
    public override void Visit(SelectStatement node)
    {
        var context = _contextStack.Count > 0 ? _contextStack.Peek() : null;
        
        if (node.QueryExpression is QuerySpecification spec)
        {
            ProcessQuerySpecification(spec, context);
        }
        
        base.Visit(node);
    }
    
    private void ProcessQuerySpecification(QuerySpecification spec, QueryContext context)
    {
        // Build table alias map from FROM clause
        var tableAliasMap = BuildTableAliasMap(spec.FromClause);
        
        // Process SELECT list
        int columnIndex = 0;
        foreach (var selectElement in spec.SelectElements)
        {
            if (selectElement is SelectStarExpression star)
            {
                // Expand SELECT * using schema metadata
                ExpandSelectStar(star, tableAliasMap, context, ref columnIndex);
            }
            else if (selectElement is SelectScalarExpression scalar)
            {
                var outputAlias = scalar.ColumnName?.Value;
                ProcessScalarExpression(scalar.Expression, tableAliasMap, context, columnIndex, outputAlias);
                columnIndex++;
            }
        }
    }
    
    private void ProcessScalarExpression(
        ScalarExpression expr, 
        Dictionary<string, TableInfo> aliasMap,
        QueryContext context,
        int outputIndex,
        string outputAlias)
    {
        // Handle different expression types
        switch (expr)
        {
            case ColumnReferenceExpression colRef:
                var resolved = ResolveColumn(colRef, aliasMap);
                if (context?.TargetTable != null)
                {
                    var targetColumn = context.TargetColumns?.ElementAtOrDefault(outputIndex) 
                                       ?? outputAlias;
                    AddLineageEdge(resolved, context.TargetTable, targetColumn);
                }
                break;
                
            case FunctionCall func:
                // Process function arguments as source columns
                foreach (var param in func.Parameters)
                {
                    ProcessScalarExpression(param, aliasMap, context, outputIndex, outputAlias);
                }
                break;
                
            case CaseExpression caseExpr:
                // CASE WHEN creates lineage from all branches
                ProcessCaseExpression(caseExpr, aliasMap, context, outputIndex, outputAlias);
                break;
                
            case CastCall cast:
                ProcessScalarExpression(cast.Parameter, aliasMap, context, outputIndex, outputAlias);
                break;
                
            case CoalesceExpression coalesce:
                foreach (var subExpr in coalesce.Expressions)
                {
                    ProcessScalarExpression(subExpr, aliasMap, context, outputIndex, outputAlias);
                }
                break;
        }
    }
    
    private ColumnInfo ResolveColumn(ColumnReferenceExpression colRef, Dictionary<string, TableInfo> aliasMap)
    {
        var identifiers = colRef.MultiPartIdentifier.Identifiers;
        var columnName = identifiers.Last().Value;
        string tableAlias = null;
        
        if (identifiers.Count >= 2)
        {
            tableAlias = identifiers[identifiers.Count - 2].Value;
        }
        
        // If alias provided, look up in map
        if (tableAlias != null && aliasMap.TryGetValue(tableAlias, out var tableInfo))
        {
            return new ColumnInfo
            {
                Schema = tableInfo.Schema,
                Table = tableInfo.Table,
                Column = columnName
            };
        }
        
        // No alias - need to search all tables in scope using schema metadata
        return ResolveUnqualifiedColumn(columnName, aliasMap);
    }
    
    private ColumnInfo ResolveUnqualifiedColumn(string columnName, Dictionary<string, TableInfo> aliasMap)
    {
        var matches = new List<ColumnInfo>();
        
        foreach (var (alias, tableInfo) in aliasMap)
        {
            var columns = _schemaProvider.GetTableColumns(tableInfo.Schema, tableInfo.Table);
            if (columns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            {
                matches.Add(new ColumnInfo
                {
                    Schema = tableInfo.Schema,
                    Table = tableInfo.Table,
                    Column = columnName
                });
            }
        }
        
        // If ambiguous (multiple matches), record all edges
        // Real implementation should log warning
        return matches.FirstOrDefault() ?? new ColumnInfo { Column = columnName };
    }
    
    private void AddLineageEdge(ColumnInfo source, string targetTable, string targetColumn)
    {
        _lineageEdges.Add(new LineageEdge
        {
            SourceSchema = source.Schema,
            SourceTable = source.Table,
            SourceColumn = source.Column,
            TargetTable = targetTable,
            TargetColumn = targetColumn,
            TransformationType = DetermineTransformation(source)
        });
    }
}

// Supporting types
public record LineageEdge
{
    public string SourceSchema { get; init; }
    public string SourceTable { get; init; }
    public string SourceColumn { get; init; }
    public string TargetTable { get; init; }
    public string TargetColumn { get; init; }
    public string TransformationType { get; init; } // "direct", "aggregation", "case", "function"
}

public record ColumnInfo
{
    public string Schema { get; init; }
    public string Table { get; init; }
    public string Column { get; init; }
}

public record TableInfo
{
    public string Schema { get; init; }
    public string Table { get; init; }
}

public class QueryContext
{
    public string TargetTable { get; init; }
    public List<string> TargetColumns { get; init; }
    public StatementType StatementType { get; init; }
}

public enum StatementType { Select, Insert, Update, Delete, Merge }
```

### Schema Metadata Provider

```csharp
public interface ISchemaMetadataProvider
{
    IReadOnlyList<string> GetTableColumns(string schema, string table);
    bool ColumnExists(string schema, string table, string column);
    string GetColumnDataType(string schema, string table, string column);
}

public class SqlServerMetadataProvider : ISchemaMetadataProvider
{
    private readonly string _connectionString;
    private readonly Dictionary<(string, string), List<string>> _columnCache = new();
    
    public SqlServerMetadataProvider(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public IReadOnlyList<string> GetTableColumns(string schema, string table)
    {
        var key = (schema?.ToLowerInvariant() ?? "dbo", table.ToLowerInvariant());
        
        if (_columnCache.TryGetValue(key, out var cached))
            return cached;
        
        var columns = LoadColumnsFromDatabase(schema ?? "dbo", table);
        _columnCache[key] = columns;
        return columns;
    }
    
    private List<string> LoadColumnsFromDatabase(string schema, string table)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        
        var sql = @"
            SELECT c.name
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @Schema AND t.name = @Table
            ORDER BY c.column_id";
        
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@Table", table);
        
        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        
        return columns;
    }
}
```

---

## Part 4: Handling Special SQL Constructs

### CTEs (Common Table Expressions)

```csharp
public class CteAwareLineageVisitor : TSqlConcreteFragmentVisitor
{
    private readonly Dictionary<string, CteDefinition> _ctes = new();
    
    public override void Visit(WithCtesAndXmlNamespaces node)
    {
        // First pass: collect all CTE definitions
        foreach (var cte in node.CommonTableExpressions)
        {
            var cteName = cte.ExpressionName.Value;
            var columns = ExtractCteColumns(cte);
            
            _ctes[cteName] = new CteDefinition
            {
                Name = cteName,
                Columns = columns,
                QueryExpression = cte.QueryExpression
            };
        }
        
        base.Visit(node);
    }
    
    private List<string> ExtractCteColumns(CommonTableExpression cte)
    {
        // If CTE explicitly declares columns
        if (cte.Columns.Count > 0)
        {
            return cte.Columns.Select(c => c.Value).ToList();
        }
        
        // Otherwise, derive from the SELECT list
        return ExtractColumnsFromQuery(cte.QueryExpression);
    }
    
    protected override void ProcessTableReference(NamedTableReference table)
    {
        var tableName = table.SchemaObject.BaseIdentifier.Value;
        
        // Check if this is a CTE reference
        if (_ctes.TryGetValue(tableName, out var cteDefinition))
        {
            // Recursively process the CTE definition
            ProcessCteReference(cteDefinition);
        }
        else
        {
            // Regular table reference
            base.ProcessTableReference(table);
        }
    }
}

public record CteDefinition
{
    public string Name { get; init; }
    public List<string> Columns { get; init; }
    public QueryExpression QueryExpression { get; init; }
}
```

### Dynamic SQL Detection

Dynamic SQL in EXEC or sp_executesql cannot be statically analyzed without runtime execution.

```csharp
public class DynamicSqlDetector : TSqlConcreteFragmentVisitor
{
    public List<DynamicSqlUsage> DynamicSqlUsages { get; } = new();
    
    public override void Visit(ExecuteStatement node)
    {
        if (node.ExecuteSpecification.ExecutableEntity is ExecutableProcedureReference procRef)
        {
            var procName = procRef.ProcedureReference?.ProcedureReference?.Name?.BaseIdentifier?.Value;
            
            if (procName?.Equals("sp_executesql", StringComparison.OrdinalIgnoreCase) == true)
            {
                DynamicSqlUsages.Add(new DynamicSqlUsage
                {
                    Type = DynamicSqlType.SpExecuteSql,
                    Line = node.StartLine,
                    Warning = "Dynamic SQL detected - lineage cannot be statically determined"
                });
            }
        }
        else if (node.ExecuteSpecification.ExecutableEntity is ExecutableStringList)
        {
            DynamicSqlUsages.Add(new DynamicSqlUsage
            {
                Type = DynamicSqlType.ExecString,
                Line = node.StartLine,
                Warning = "EXEC with string - potential SQL injection and lineage gap"
            });
        }
        
        base.Visit(node);
    }
}

public record DynamicSqlUsage
{
    public DynamicSqlType Type { get; init; }
    public int Line { get; init; }
    public string Warning { get; init; }
}

public enum DynamicSqlType { ExecString, SpExecuteSql, OpenRowset }
```

### Temp Tables and Table Variables

```csharp
public class TempTableTracker : TSqlConcreteFragmentVisitor
{
    public Dictionary<string, TempTableInfo> TempTables { get; } = new();
    
    public override void Visit(CreateTableStatement node)
    {
        var tableName = node.SchemaObjectName.BaseIdentifier.Value;
        
        if (tableName.StartsWith("#"))
        {
            var columns = node.Definition.ColumnDefinitions
                .Select(c => new TempColumnInfo
                {
                    Name = c.ColumnIdentifier.Value,
                    DataType = c.DataType?.ToString()
                })
                .ToList();
            
            TempTables[tableName] = new TempTableInfo
            {
                Name = tableName,
                Columns = columns,
                IsGlobal = tableName.StartsWith("##")
            };
        }
        
        base.Visit(node);
    }
    
    public override void Visit(DeclareTableVariableStatement node)
    {
        var varName = node.Body.VariableName.Value;
        
        var columns = node.Body.Definition.ColumnDefinitions
            .Select(c => new TempColumnInfo
            {
                Name = c.ColumnIdentifier.Value,
                DataType = c.DataType?.ToString()
            })
            .ToList();
        
        TempTables[varName] = new TempTableInfo
        {
            Name = varName,
            Columns = columns,
            IsTableVariable = true
        };
        
        base.Visit(node);
    }
}

public record TempTableInfo
{
    public string Name { get; init; }
    public List<TempColumnInfo> Columns { get; init; }
    public bool IsGlobal { get; init; }
    public bool IsTableVariable { get; init; }
}

public record TempColumnInfo
{
    public string Name { get; init; }
    public string DataType { get; init; }
}
```

### MERGE Statement Handling

MERGE combines INSERT, UPDATE, and DELETE - each branch creates different lineage:

```csharp
public override void Visit(MergeStatement node)
{
    var targetTable = ExtractTableName(node.MergeSpecification.Target);
    var sourceTable = ExtractMergeSource(node.MergeSpecification.TableReference);
    
    foreach (var clause in node.MergeSpecification.ActionClauses)
    {
        switch (clause)
        {
            case MergeActionClause mergeClause:
                switch (mergeClause.Action)
                {
                    case InsertMergeAction insert:
                        ProcessMergeInsert(targetTable, sourceTable, insert);
                        break;
                    case UpdateMergeAction update:
                        ProcessMergeUpdate(targetTable, sourceTable, update);
                        break;
                    case DeleteMergeAction delete:
                        // DELETE doesn't create column lineage, but note dependency
                        break;
                }
                break;
        }
    }
    
    base.Visit(node);
}
```

---

## Part 5: Leveraging SQL Server Metadata Views

### sys.sql_expression_dependencies

```sql
-- Find all objects that reference a specific table
SELECT 
    OBJECT_NAME(sed.referencing_id) AS ReferencingObject,
    o.type_desc AS ObjectType,
    sed.referenced_schema_name,
    sed.referenced_entity_name,
    COALESCE(COL_NAME(sed.referenced_id, sed.referenced_minor_id), '(n/a)') AS ReferencedColumn
FROM sys.sql_expression_dependencies sed
INNER JOIN sys.objects o ON sed.referencing_id = o.object_id
WHERE sed.referenced_id = OBJECT_ID('dbo.MyTable');

-- Find cross-database dependencies
SELECT 
    OBJECT_NAME(referencing_id) AS ReferencingObject,
    referenced_server_name,
    referenced_database_name,
    referenced_schema_name,
    referenced_entity_name
FROM sys.sql_expression_dependencies
WHERE referenced_database_name IS NOT NULL
  AND is_ambiguous = 0;
```

### sys.dm_sql_referenced_entities

```sql
-- Get detailed dependencies for a stored procedure
SELECT 
    referenced_schema_name,
    referenced_entity_name,
    referenced_minor_name AS referenced_column,
    is_updated,
    is_selected
FROM sys.dm_sql_referenced_entities('dbo.MyProcedure', 'OBJECT')
WHERE referenced_minor_name IS NOT NULL;
```

### Combining with ScriptDom

```csharp
public class HybridLineageExtractor
{
    private readonly ISchemaMetadataProvider _schemaProvider;
    private readonly SqlConnection _connection;
    
    public async Task<LineageResult> ExtractLineageAsync(string objectName)
    {
        // 1. Get SQL definition
        var definition = await GetObjectDefinitionAsync(objectName);
        
        // 2. Parse with ScriptDom for detailed column mapping
        var fragment = ParseSql(definition);
        var visitor = new LineageExtractorVisitor(_schemaProvider);
        fragment.Accept(visitor);
        
        // 3. Supplement with sys.sql_expression_dependencies for object-level
        var objectDeps = await GetObjectDependenciesAsync(objectName);
        
        // 4. Cross-reference and validate
        return new LineageResult
        {
            ColumnLineage = visitor.LineageEdges,
            ObjectDependencies = objectDeps,
            DynamicSqlWarnings = visitor.DynamicSqlUsages,
            Confidence = CalculateConfidence(visitor)
        };
    }
}
```

---

## Part 6: Graph Storage for Lineage

### Schema Design

```sql
-- Nodes table (tables and columns)
CREATE TABLE daqa.LineageNode (
    NodeId INT IDENTITY(1,1) PRIMARY KEY,
    NodeType VARCHAR(20) NOT NULL, -- 'Table', 'Column', 'Procedure', 'View'
    SchemaName NVARCHAR(128),
    ObjectName NVARCHAR(128) NOT NULL,
    ColumnName NVARCHAR(128),
    FullyQualifiedName AS (
        ISNULL(SchemaName + '.', '') + ObjectName + 
        ISNULL('.' + ColumnName, '')
    ) PERSISTED,
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    UpdatedAt DATETIME2 DEFAULT GETDATE(),
    
    CONSTRAINT UQ_LineageNode_FQN UNIQUE (SchemaName, ObjectName, ColumnName)
);

-- Edges table (lineage relationships)
CREATE TABLE daqa.LineageEdge (
    EdgeId INT IDENTITY(1,1) PRIMARY KEY,
    SourceNodeId INT NOT NULL REFERENCES daqa.LineageNode(NodeId),
    TargetNodeId INT NOT NULL REFERENCES daqa.LineageNode(NodeId),
    TransformationType VARCHAR(50), -- 'direct', 'aggregation', 'case', 'function', 'join'
    TransformationExpression NVARCHAR(MAX), -- SQL expression if applicable
    SourceObjectId INT, -- The procedure/view that creates this lineage
    Confidence DECIMAL(3,2) DEFAULT 1.0, -- 0.0 to 1.0
    CreatedAt DATETIME2 DEFAULT GETDATE(),
    
    INDEX IX_LineageEdge_Source (SourceNodeId),
    INDEX IX_LineageEdge_Target (TargetNodeId)
);

-- Recursive CTE for upstream lineage
CREATE OR ALTER FUNCTION daqa.GetUpstreamLineage(@ColumnNodeId INT, @MaxDepth INT = 10)
RETURNS TABLE
AS
RETURN
WITH LineageCTE AS (
    -- Anchor: direct sources
    SELECT 
        e.SourceNodeId,
        e.TargetNodeId,
        e.TransformationType,
        1 AS Depth,
        CAST(n.FullyQualifiedName AS NVARCHAR(MAX)) AS Path
    FROM daqa.LineageEdge e
    INNER JOIN daqa.LineageNode n ON e.SourceNodeId = n.NodeId
    WHERE e.TargetNodeId = @ColumnNodeId
    
    UNION ALL
    
    -- Recursive: upstream sources
    SELECT 
        e.SourceNodeId,
        e.TargetNodeId,
        e.TransformationType,
        cte.Depth + 1,
        n.FullyQualifiedName + ' -> ' + cte.Path
    FROM daqa.LineageEdge e
    INNER JOIN daqa.LineageNode n ON e.SourceNodeId = n.NodeId
    INNER JOIN LineageCTE cte ON e.TargetNodeId = cte.SourceNodeId
    WHERE cte.Depth < @MaxDepth
)
SELECT DISTINCT
    SourceNodeId,
    TargetNodeId,
    TransformationType,
    Depth,
    Path
FROM LineageCTE;
```

### C# Graph Service

```csharp
public class LineageGraphService
{
    private readonly IDbConnection _db;
    
    public async Task<int> EnsureNodeExistsAsync(ColumnInfo column)
    {
        var sql = @"
            MERGE daqa.LineageNode AS target
            USING (SELECT @Schema, @Table, @Column) AS source (SchemaName, ObjectName, ColumnName)
            ON target.SchemaName = source.SchemaName 
               AND target.ObjectName = source.ObjectName 
               AND target.ColumnName = source.ColumnName
            WHEN NOT MATCHED THEN
                INSERT (NodeType, SchemaName, ObjectName, ColumnName)
                VALUES ('Column', @Schema, @Table, @Column)
            OUTPUT inserted.NodeId;";
        
        return await _db.ExecuteScalarAsync<int>(sql, new { column.Schema, column.Table, column.Column });
    }
    
    public async Task AddLineageEdgeAsync(LineageEdge edge)
    {
        var sourceId = await EnsureNodeExistsAsync(new ColumnInfo 
        { 
            Schema = edge.SourceSchema, 
            Table = edge.SourceTable, 
            Column = edge.SourceColumn 
        });
        
        var targetId = await EnsureNodeExistsAsync(new ColumnInfo 
        { 
            Schema = "dbo", // or extract from edge
            Table = edge.TargetTable, 
            Column = edge.TargetColumn 
        });
        
        var sql = @"
            INSERT INTO daqa.LineageEdge (SourceNodeId, TargetNodeId, TransformationType)
            VALUES (@SourceId, @TargetId, @TransformationType)";
        
        await _db.ExecuteAsync(sql, new { sourceId, targetId, edge.TransformationType });
    }
    
    public async Task<IEnumerable<LineagePath>> GetUpstreamLineageAsync(int columnNodeId, int maxDepth = 10)
    {
        return await _db.QueryAsync<LineagePath>(
            "SELECT * FROM daqa.GetUpstreamLineage(@NodeId, @MaxDepth)",
            new { NodeId = columnNodeId, MaxDepth = maxDepth });
    }
    
    public async Task<IEnumerable<LineagePath>> GetDownstreamImpactAsync(int columnNodeId, int maxDepth = 10)
    {
        // Similar CTE but traversing forward from source to targets
        var sql = @"
            WITH ImpactCTE AS (
                SELECT TargetNodeId, 1 AS Depth
                FROM daqa.LineageEdge
                WHERE SourceNodeId = @NodeId
                
                UNION ALL
                
                SELECT e.TargetNodeId, cte.Depth + 1
                FROM daqa.LineageEdge e
                INNER JOIN ImpactCTE cte ON e.SourceNodeId = cte.TargetNodeId
                WHERE cte.Depth < @MaxDepth
            )
            SELECT DISTINCT n.*, cte.Depth
            FROM ImpactCTE cte
            INNER JOIN daqa.LineageNode n ON cte.TargetNodeId = n.NodeId";
        
        return await _db.QueryAsync<LineagePath>(sql, new { NodeId = columnNodeId, MaxDepth = maxDepth });
    }
}
```

---

## Part 7: Production Considerations

### Error Handling and Resilience

```csharp
public class ResilientLineageExtractor
{
    private readonly ILogger<ResilientLineageExtractor> _logger;
    
    public async Task<LineageResult> ExtractWithFallbackAsync(string sql, string objectName)
    {
        try
        {
            // Primary: Full ScriptDom analysis
            return await ExtractFullLineageAsync(sql);
        }
        catch (ParseException ex) when (ex.Message.Contains("stack overflow"))
        {
            _logger.LogWarning("Stack overflow in ScriptDom, falling back to iterative parsing");
            return await ExtractIterativeLineageAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScriptDom parsing failed for {ObjectName}", objectName);
            
            // Fallback: Use SQL Server metadata only
            return await ExtractMetadataOnlyLineageAsync(objectName);
        }
    }
}
```

### Performance Optimization

```csharp
public class CachedMetadataProvider : ISchemaMetadataProvider
{
    private readonly IMemoryCache _cache;
    private readonly ISchemaMetadataProvider _inner;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);
    
    public IReadOnlyList<string> GetTableColumns(string schema, string table)
    {
        var key = $"columns:{schema}:{table}";
        
        return _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            return _inner.GetTableColumns(schema, table);
        });
    }
}
```

### Batch Processing

```csharp
public class BatchLineageProcessor
{
    public async Task ProcessAllProceduresAsync(string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        
        var procedures = await conn.QueryAsync<ProcedureInfo>(@"
            SELECT 
                s.name AS SchemaName,
                p.name AS ProcedureName,
                m.definition AS Definition
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            INNER JOIN sys.sql_modules m ON p.object_id = m.object_id
            WHERE m.definition IS NOT NULL");
        
        var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
        
        await Parallel.ForEachAsync(procedures, options, async (proc, ct) =>
        {
            try
            {
                var lineage = await ExtractLineageAsync(proc.Definition);
                await SaveLineageAsync(proc.SchemaName, proc.ProcedureName, lineage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract lineage for {Schema}.{Proc}", 
                    proc.SchemaName, proc.ProcedureName);
            }
        });
    }
}
```

---

## Part 8: Alternative Tools Comparison

| Tool | Type | T-SQL Support | Column Lineage | Schema-Aware | Open Source |
|------|------|---------------|----------------|--------------|-------------|
| **ScriptDom** | .NET Library | Full | Manual impl | Via integration | ✅ MIT |
| **SQLGlot** | Python | Partial | Built-in | ✅ | ✅ MIT |
| **sqllineage** | Python | Via sqlfluff | ✅ | Optional | ✅ Apache 2.0 |
| **General SQL Parser** | Commercial | Full | ✅ | ✅ | ❌ |
| **SQLFlow** | SaaS | Full | ✅ | ✅ | ❌ |
| **DataHub** | Platform | Via sqlglot | ✅ | ✅ | ✅ Apache 2.0 |

### When to Use ScriptDom

**Best for:**
- T-SQL-specific syntax (MERGE, OUTPUT, etc.)
- .NET ecosystem integration
- Custom rule development
- High-fidelity parsing requirements
- Enterprise SQL Server environments

**Consider alternatives when:**
- Multi-database dialect support needed
- Python ecosystem preferred
- Quick prototyping required
- Column lineage is primary goal (SQLGlot may be easier)

---

## References

### Microsoft Official Documentation
1. Azure SQL DevBlog - Programmatically Parsing T-SQL (March 2024) - https://devblogs.microsoft.com/azure-sql/programmatically-parsing-transact-sql-t-sql-with-the-scriptdom-parser/
2. GitHub - microsoft/SqlScriptDOM - https://github.com/microsoft/SqlScriptDOM
3. Microsoft Learn - TSqlParser Class - https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.transactsql.scriptdom.tsqlparser
4. Microsoft Learn - sys.sql_expression_dependencies - https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-sql-expression-dependencies-transact-sql
5. Microsoft Learn - sys.columns - https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-columns-transact-sql
6. Microsoft Learn - MERGE Statement - https://learn.microsoft.com/en-us/sql/t-sql/statements/merge-transact-sql
7. Microsoft Learn - sp_executesql - https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-executesql-transact-sql
8. Microsoft Learn - Common Table Expressions - https://learn.microsoft.com/en-us/sql/t-sql/queries/with-common-table-expression-transact-sql
9. Microsoft Learn - Code Analysis Rules Extensibility - https://learn.microsoft.com/en-us/sql/ssdt/overview-of-extensibility-for-database-code-analysis-rules

### Community Tutorials & Implementation Guides
10. Dan Guzman's Blog - Microsoft SQL Server Script DOM - https://www.dbdelta.com/microsoft-sql-server-script-dom/
11. The Agile SQL Club - How to Get Started with ScriptDom - https://the.agilesql.club/2015/11/how-to-get-started-with-the-scriptdom/
12. SQLServerCentral - Stairway to ScriptDOM Series - https://www.sqlservercentral.com/steps/stairway-to-scriptdom-level-1-an-introduction-to-scriptdom
13. DSkrzypiec Blog - Parsing T-SQL (ScriptDom vs ANTLR4) - https://dskrzypiec.dev/parsing-tsql/
14. GitHub Gist - TransactSqlScriptDomTest.cs - https://gist.github.com/philippwiddra/2ee47ac4f8a0248c3a0e

### Column-Level Lineage Research
15. DataHub Blog - Extracting Column-Level Lineage from SQL (August 2025) - https://datahub.com/blog/extracting-column-level-lineage-from-sql/
16. arXiv - LineageX: Column Lineage Extraction System (May 2025) - https://arxiv.org/html/2505.23133v1
17. Metaplane Blog - Column-Level Lineage: SQL Parsing Adventure - https://www.metaplane.dev/blog/column-level-lineage-an-adventure-in-sql-parsing
18. SQLLineage Documentation - Column-Level Lineage Design - https://sqllineage.readthedocs.io/en/latest/behind_the_scene/column-level_lineage_design.html
19. DataHub Docs - SQL Parsing - https://docs.datahub.com/docs/lineage/sql_parsing

### Open Source Lineage Tools
20. GitHub - reata/sqllineage - https://github.com/reata/sqllineage
21. GitHub - tobymao/sqlglot - https://github.com/tobymao/sqlglot
22. GitHub - OpenLineage/OpenLineage - https://github.com/OpenLineage/OpenLineage
23. General SQL Parser - Data Lineage Demo - http://support.sqlparser.com/tutorials/gsp-demo-data-lineage/

### Graph & Lineage Architecture
24. Neo4j Blog - What is Data Lineage - https://neo4j.com/blog/graph-database/what-is-data-lineage/
25. Memgraph Blog - Data Lineage Graph Analysis - https://memgraph.com/blog/better-data-management-get-solutions-by-analyzing-the-data-lineage-graph
26. Memgraph Blog - Data Lineage is a Graph Problem - https://memgraph.com/blog/join-the-dots-data-lineage-is-a-graph-problem-heres-why
27. Rittman Mead - Data Lineage Analysis with Python and NetworkX - https://www.rittmanmead.com/blog/2024/08/data-lineage-analysis-with-python-and-networkx/

### SQL Server Dependencies & Metadata
28. MSSQLTips - Finding SQL Server Object Dependencies with DMVs - https://www.mssqltips.com/sqlservertip/4868/finding-sql-server-object-dependencies-with-dmvs/
29. MSSQLTips - Different Ways to Find SQL Server Object Dependencies - https://www.mssqltips.com/sqlservertip/2999/different-ways-to-find-sql-server-object-dependencies/
30. Red-Gate Simple Talk - Dependencies and References in SQL Server - https://www.red-gate.com/simple-talk/databases/sql-server/t-sql-programming-sql-server/dependencies-and-references-in-sql-server/
31. Sommarskog - Where Is that Table Used? - https://www.sommarskog.se/sqlutil/SearchCode.html

### Dynamic SQL & Security
32. Sommarskog - The Curse and Blessings of Dynamic SQL - https://www.sommarskog.se/dynamic_sql.html
33. Microsoft TechCommunity - Dynamic SQL & SQL Injection - https://techcommunity.microsoft.com/t5/sql-server-blog/dynamic-sql-amp-sql-injection/ba-p/383196
34. Redgate - The Risks of Using EXECUTE - https://www.red-gate.com/hub/product-learning/sql-prompt/the-risks-of-using-execute-sql-script

### Static Code Analysis & Linting
35. Red-Gate Simple Talk - Database Code Analysis - https://www.red-gate.com/simple-talk/devops/database-devops/database-code-analysis/
36. GitHub - tcartwright/SqlServer.Rules - https://github.com/tcartwright/sqlserver.rules
37. The Agile SQL Club - SSDT Analysis Extensions - https://the.agilesql.club/2014/12/enforcing-t-sql-quality-with-ssdt-analysis-extensions/

### AST & Compiler Theory
38. Wikipedia - Abstract Syntax Tree - https://en.wikipedia.org/wiki/Abstract_syntax_tree
39. Pat Shaughnessy - Visiting an Abstract Syntax Tree - https://patshaughnessy.net/2022/1/22/visiting-an-abstract-syntax-tree
40. CS453 - AST and Visitor Patterns (Colorado State) - https://www.cs.colostate.edu/~cs453/yr2014/Slides/10-AST-visitor.ppt.pdf

### OpenLineage Standard
41. OpenLineage Official Site - https://openlineage.io/
42. OpenLineage Specification - https://github.com/OpenLineage/OpenLineage/blob/main/spec/OpenLineage.md
43. Kai Waehner - Open Standards for Data Lineage - https://www.kai-waehner.de/blog/2024/05/13/open-standards-for-data-lineage-openlineage-for-batch-and-streaming/

### Performance & Best Practices
44. GitHub Issue #49 - Stack Overflow with Large UNIONs - https://github.com/microsoft/SqlScriptDOM/issues/49
45. Brent Ozar - CTEs vs Temp Tables - https://www.brentozar.com/archive/2019/06/whats-better-ctes-or-temp-tables/
46. MSSQLTips - SQL MERGE Performance - https://www.mssqltips.com/sqlservertip/7590/sql-merge-performance-vs-insert-update-delete/

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-03 | Initial release with 110+ source research |

---

## Skill Metadata

```yaml
skill_id: tsql-scriptdom-lineage
version: 1.0.0
category: database-intelligence
tags:
  - t-sql
  - scriptdom
  - column-lineage
  - static-analysis
  - ast
  - sql-server
  - data-governance
prerequisites:
  - csharp-developer
  - sql-optimization-patterns
  - database-implementation
related_skills:
  - generating-stored-procedures
  - detecting-sql-injection-vulnerabilities
  - schema-optimization-orchestrator
triggers:
  - scriptdom
  - t-sql parsing
  - column lineage
  - sql static analysis
  - stored procedure analysis
  - impact analysis
  - data flow analysis
```
