---
name: database-intelligence-engine
description: |
  SQL Server database intelligence for lineage tracking, dependency analysis, impact assessment,
  and schema documentation. Use when analyzing database dependencies, tracking data flow,
  performing impact analysis before changes, auto-classifying data, or generating documentation.
  Keywords: data lineage, dependency graph, impact analysis, SQL Server DMVs, schema analysis,
  sys.sql_expression_dependencies, foreign keys, stored procedure analysis, change detection.
version: 2025.12
license: MIT
---

# Database Intelligence Engine

Cognitive database understanding for SQL Server: lineage tracking, dependency analysis, impact assessment, and intelligent documentation generation.

## When to Use This Skill

- Analyzing object dependencies (what uses what)
- Tracking data lineage (source → transformation → destination)
- Impact analysis before schema changes
- **Column-level impact analysis** (what procedures UPDATE column X?)
- Auto-classifying columns (PII, financial, audit)
- Detecting orphaned/unused objects
- Generating dependency documentation
- Understanding stored procedure data flow
- **Identifying dynamic SQL gaps** in dependency tracking

---

## Column-Level Lineage (Key Enhancement)

Track operations at column granularity—critical for precise impact analysis.

### Quick Reference

```sql
-- Find all procedures that UPDATE a specific column
SELECT DISTINCT procedure_schema, procedure_name
FROM daqa.ColumnLineage
WHERE referenced_schema = 'irfcycle'
  AND referenced_table = 'irf_policy'
  AND referenced_column = 'POL_NO'
  AND [update] = 1;

-- Column risk score (weighted by operation severity)
SELECT 
    referenced_column,
    COUNT(DISTINCT procedure_name) AS total_refs,
    SUM(CASE WHEN [update] = 1 THEN 3 ELSE 0 END) +
    SUM(CASE WHEN [delete] = 1 THEN 5 ELSE 0 END) AS risk_score
FROM daqa.ColumnLineage
WHERE referenced_table = 'irf_policy'
GROUP BY referenced_column
ORDER BY risk_score DESC;
```

### Operation Types

| Type | Weight | Meaning |
|------|--------|---------|
| READ | 1 | Column used in SELECT/WHERE/JOIN |
| UPDATE | 3 | Column modified |
| INSERT | 2 | Column populated |
| DELETE | 5 | Row deleted |
| MIXED | varies | Multiple operations |

**See:** `references/column-lineage.md` for full patterns.

## Reference Files

| Topic | File | Use When |
|-------|------|----------|
| SQL Queries | `references/sql-queries.md` | Extracting metadata from SQL Server |
| Lineage Patterns | `references/lineage-patterns.md` | Building lineage graphs |
| Impact Analysis | `references/impact-analysis.md` | Assessing change risk |
| Column Lineage | `references/column-lineage.md` | Column-level operations (READ/UPDATE/INSERT/DELETE) |
| Dynamic SQL | `references/dynamic-sql.md` | Detecting and handling dynamic SQL gaps |

---

## Core Concepts

### Lineage Terminology

```
UPSTREAM (Sources)          DOWNSTREAM (Consumers)
      ↓                            ↓
┌─────────┐    ┌─────────┐    ┌─────────┐
│ Source  │───→│ Process │───→│ Target  │
│ Tables  │    │ (Proc)  │    │ Table   │
└─────────┘    └─────────┘    └─────────┘
      ↑                            ↑
  "What feeds X?"           "What does X feed?"
```

- **Upstream**: Objects that feed INTO the current object
- **Downstream**: Objects that CONSUME from the current object
- **Node**: Any database object (table, view, proc, function)
- **Edge**: A dependency relationship between nodes

### SQL Server System Views (The Foundation)

| View | Purpose | Key Columns |
|------|---------|-------------|
| `sys.sql_expression_dependencies` | All object dependencies | referencing_id, referenced_entity_name |
| `sys.foreign_keys` | FK relationships | parent_object_id, referenced_object_id |
| `sys.objects` | Object metadata | object_id, name, type, schema_id |
| `sys.columns` | Column definitions | object_id, name, column_id |
| `sys.indexes` | Index information | object_id, name, type |
| `sys.schemas` | Schema names | schema_id, name |

---

## Quick Start Queries

### 1. Find All Dependencies for an Object

```sql
-- What does [ObjectName] reference? (UPSTREAM)
SELECT 
    OBJECT_SCHEMA_NAME(d.referencing_id) AS referencing_schema,
    OBJECT_NAME(d.referencing_id) AS referencing_object,
    d.referenced_schema_name,
    d.referenced_entity_name,
    o.type_desc AS referencing_type
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE OBJECT_NAME(d.referencing_id) = 'YourObjectName'
ORDER BY d.referenced_schema_name, d.referenced_entity_name;
```

### 2. Find What References an Object (Impact Analysis)

```sql
-- What references [TableName]? (DOWNSTREAM - for impact analysis)
SELECT 
    OBJECT_SCHEMA_NAME(d.referencing_id) AS dependent_schema,
    OBJECT_NAME(d.referencing_id) AS dependent_object,
    o.type_desc AS dependent_type
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE d.referenced_entity_name = 'YourTableName'
ORDER BY o.type_desc, dependent_object;
```

### 3. Complete Dependency Graph

```sql
-- Full dependency graph for documentation
SELECT 
    s1.name AS source_schema,
    OBJECT_NAME(d.referencing_id) AS source_object,
    o1.type_desc AS source_type,
    '→' AS direction,
    COALESCE(d.referenced_schema_name, 'dbo') AS target_schema,
    d.referenced_entity_name AS target_object
FROM sys.sql_expression_dependencies d
JOIN sys.objects o1 ON d.referencing_id = o1.object_id
JOIN sys.schemas s1 ON o1.schema_id = s1.schema_id
WHERE d.referenced_entity_name IS NOT NULL
  AND d.referenced_schema_name NOT IN ('sys')
ORDER BY source_schema, source_object;
```

---

## Lineage Analysis Patterns

### Pattern 1: Trace Data Flow (Source to Target)

```sql
-- Recursive CTE to trace full lineage path
WITH LineageTree AS (
    -- Anchor: Start from target object
    SELECT 
        d.referencing_id AS object_id,
        OBJECT_NAME(d.referencing_id) AS object_name,
        d.referenced_entity_name AS depends_on,
        0 AS depth,
        CAST(OBJECT_NAME(d.referencing_id) AS VARCHAR(MAX)) AS path
    FROM sys.sql_expression_dependencies d
    WHERE OBJECT_NAME(d.referencing_id) = @TargetObject
    
    UNION ALL
    
    -- Recursive: Walk upstream
    SELECT 
        d.referencing_id,
        OBJECT_NAME(d.referencing_id),
        d.referenced_entity_name,
        lt.depth + 1,
        lt.path + ' → ' + OBJECT_NAME(d.referencing_id)
    FROM sys.sql_expression_dependencies d
    JOIN LineageTree lt ON OBJECT_NAME(d.referencing_id) = lt.depends_on
    WHERE lt.depth < 10  -- Prevent infinite loops
)
SELECT DISTINCT object_name, depends_on, depth, path
FROM LineageTree
ORDER BY depth, object_name;
```

### Pattern 2: Find Root Sources (Tables with No Dependencies)

```sql
-- Tables that are pure sources (don't depend on anything)
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    (SELECT COUNT(*) FROM sys.sql_expression_dependencies 
     WHERE referenced_entity_name = t.name) AS downstream_count
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE NOT EXISTS (
    SELECT 1 FROM sys.sql_expression_dependencies d
    WHERE d.referencing_id = t.object_id
)
ORDER BY downstream_count DESC;
```

### Pattern 3: Find Orphaned Objects (No References)

```sql
-- Objects that nothing references (potential cleanup candidates)
SELECT 
    s.name AS schema_name,
    o.name AS object_name,
    o.type_desc,
    o.create_date
FROM sys.objects o
JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type IN ('P', 'V', 'FN', 'IF', 'TF')  -- Procs, Views, Functions
  AND NOT EXISTS (
    SELECT 1 FROM sys.sql_expression_dependencies d
    WHERE d.referenced_entity_name = o.name
  )
ORDER BY o.create_date;
```

---

## Impact Analysis Framework

### Before Making Changes, Ask:

1. **Direct Impact**: What immediately references this object?
2. **Cascade Impact**: What references THOSE objects?
3. **Report Impact**: Which BI reports/dashboards use this data?
4. **Process Impact**: Which ETL jobs touch this object?

### Impact Severity Scoring

```sql
-- Calculate impact score for a table
WITH ImpactMetrics AS (
    SELECT 
        d.referenced_entity_name AS table_name,
        COUNT(DISTINCT d.referencing_id) AS direct_dependents,
        COUNT(DISTINCT CASE WHEN o.type = 'P' THEN d.referencing_id END) AS proc_count,
        COUNT(DISTINCT CASE WHEN o.type = 'V' THEN d.referencing_id END) AS view_count
    FROM sys.sql_expression_dependencies d
    JOIN sys.objects o ON d.referencing_id = o.object_id
    WHERE d.referenced_entity_name = @TableName
    GROUP BY d.referenced_entity_name
)
SELECT 
    table_name,
    direct_dependents,
    proc_count,
    view_count,
    CASE 
        WHEN direct_dependents > 20 THEN 'CRITICAL'
        WHEN direct_dependents > 10 THEN 'HIGH'
        WHEN direct_dependents > 5 THEN 'MEDIUM'
        ELSE 'LOW'
    END AS impact_severity
FROM ImpactMetrics;
```

---

## Auto-Classification Patterns

### Column Classification by Naming Convention

```sql
-- Auto-classify columns based on naming patterns
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    c.name AS column_name,
    ty.name AS data_type,
    CASE 
        -- PII Detection
        WHEN c.name LIKE '%SSN%' OR c.name LIKE '%social%security%' THEN 'PII-SSN'
        WHEN c.name LIKE '%email%' THEN 'PII-Email'
        WHEN c.name LIKE '%phone%' OR c.name LIKE '%mobile%' THEN 'PII-Phone'
        WHEN c.name LIKE '%address%' OR c.name LIKE '%street%' THEN 'PII-Address'
        WHEN c.name LIKE '%DOB%' OR c.name LIKE '%birth%date%' THEN 'PII-DOB'
        
        -- Financial
        WHEN c.name LIKE '%amount%' OR c.name LIKE '%price%' THEN 'Financial'
        WHEN c.name LIKE '%premium%' OR c.name LIKE '%revenue%' THEN 'Financial'
        WHEN c.name LIKE '%cost%' OR c.name LIKE '%balance%' THEN 'Financial'
        
        -- Audit
        WHEN c.name LIKE '%created%' OR c.name LIKE '%modified%' THEN 'Audit'
        WHEN c.name LIKE '%updated%' OR c.name LIKE '%_by' THEN 'Audit'
        
        -- Keys
        WHEN c.name LIKE '%_id' OR c.name LIKE '%_key' THEN 'Key'
        WHEN c.name LIKE '%_fk' OR c.name LIKE '%_pk' THEN 'Key'
        
        ELSE 'General'
    END AS classification
FROM sys.columns c
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
ORDER BY classification, schema_name, table_name;
```

---

## Visualization Output Formats

### Mermaid Diagram Generation

```sql
-- Generate Mermaid flowchart from dependencies
SELECT 'graph LR' AS mermaid_line
UNION ALL
SELECT DISTINCT
    '    ' + REPLACE(OBJECT_NAME(d.referencing_id), ' ', '_') + 
    ' --> ' + REPLACE(d.referenced_entity_name, ' ', '_')
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE d.referenced_entity_name IS NOT NULL
  AND d.referenced_schema_name = @SchemaName;
```

### JSON Graph Format (for D3.js/React Flow)

```sql
-- Generate nodes and edges for visualization
SELECT 
    (SELECT DISTINCT
        OBJECT_NAME(referencing_id) AS id,
        OBJECT_NAME(referencing_id) AS label,
        o.type_desc AS type
     FROM sys.sql_expression_dependencies d
     JOIN sys.objects o ON d.referencing_id = o.object_id
     FOR JSON PATH) AS nodes,
    (SELECT 
        OBJECT_NAME(referencing_id) AS source,
        referenced_entity_name AS target
     FROM sys.sql_expression_dependencies
     WHERE referenced_entity_name IS NOT NULL
     FOR JSON PATH) AS edges
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
```

---

## .NET Integration Pattern

### LineageService Interface

```csharp
public interface ILineageService
{
    Task<List<DependencyNode>> GetUpstreamDependencies(string objectName);
    Task<List<DependencyNode>> GetDownstreamDependencies(string objectName);
    Task<ImpactAssessment> AssessChangeImpact(string objectName);
    Task<LineageGraph> BuildFullLineageGraph(string schemaName);
}

public record DependencyNode(
    string Schema,
    string ObjectName,
    string ObjectType,
    int Depth,
    List<DependencyNode> Children
);

public record ImpactAssessment(
    string ObjectName,
    int DirectDependents,
    int CascadeDependents,
    string Severity,
    List<string> AffectedProcs,
    List<string> AffectedViews
);
```

---

## Dynamic SQL Detection

DMVs miss dynamic SQL dependencies. Flag and track these procedures separately.

### Quick Detection

```sql
-- Find all dynamic SQL procedures
SELECT 
    SCHEMA_NAME(o.schema_id) AS schema_name,
    o.name AS procedure_name,
    CASE 
        WHEN m.definition LIKE '%sp_executesql%' THEN 'sp_executesql'
        WHEN m.definition LIKE '%EXEC%(@%' THEN 'EXEC variable'
        ELSE 'Unknown'
    END AS pattern
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.type = 'P'
  AND (m.definition LIKE '%EXEC%(@%' 
       OR m.definition LIKE '%sp_executesql%');
```

### Known Dynamic SQL in Your Environment

| Schema | Procedure | Pattern | Risk |
|--------|-----------|---------|------|
| bal | uspDeletePeriodData | sp_executesql | HIGH |
| dbo | ArchiveTables | sp_executesql | CRITICAL |
| dbo | uspMonitorMissingCoverage | EXEC variable | MEDIUM |
| bal | uspDeletePeriodData_test_kz | sp_executesql | HIGH |
| dbo | usp_03541_Manual_Updates | sp_executesql | HIGH |

**See:** `references/dynamic-sql.md` for handling strategies.

---

## Constraints

### MUST DO
- Always check `sys.sql_expression_dependencies` first
- Include schema names in all queries
- Limit recursive CTEs with depth checks
- Cache dependency graphs (they're expensive to compute)
- Validate object existence before analysis

### MUST NOT DO
- Don't assume all dependencies are captured (dynamic SQL is invisible)
- Don't ignore cross-database dependencies
- Don't run heavy lineage queries during peak hours
- Don't trust `referenced_id` when NULL (unresolved references)

---

## Common Schema Patterns (From Your Data)

Based on analysis of your `expression_dependencies`:

| Schema | Purpose | Key Objects |
|--------|---------|-------------|
| `irfcycle` | Insurance cycle processing | sumprmtransaction, dimmonthlytime |
| `gw` | Guidewire integration | dim* tables, irf_claim, irf_policy |
| `gwpcDaily` | Daily PC sync | irf_policy, irf_filter_policy |
| `dbo` | Core utilities | IRFLoadData, uspLog, uspDailyLog |
| `DaQa` | Data quality/documentation | Your schema |

### Most Referenced Objects (High Impact)

1. `IRFLoadData` (168 refs) - CRITICAL
2. `uspDailyLog` (127 refs) - CRITICAL  
3. `irf_policy` (81 refs) - HIGH
4. `dimmonthlytime` (70 refs) - HIGH
5. `irf_policy_risk` (41 refs) - MEDIUM

---

## Quick Reference: Key DMVs

```sql
-- Object info
SELECT * FROM sys.objects WHERE name = 'X'

-- Column details  
SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('X')

-- Dependencies
SELECT * FROM sys.sql_expression_dependencies WHERE referencing_id = OBJECT_ID('X')

-- Foreign keys
SELECT * FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('X')

-- Indexes
SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('X')
```

## Related Skills

- **enterprise-clean-architecture** - Application patterns
- **azure-openai-integration** - AI-powered documentation
- **generating-stored-procedures** - SP analysis
