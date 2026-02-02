# Column-Level Lineage Reference

Track data dependencies at column granularity with operation type classification.

---

## Overview

Column-level lineage goes beyond table dependencies to track exactly which columns are read, updated, inserted, or deleted by each procedure. This enables precise impact analysis for schema changes.

### Operation Types

| Operation | Description | Risk Level |
|-----------|-------------|------------|
| READ | Column used in SELECT, WHERE, JOIN | Low |
| UPDATE | Column modified via UPDATE statement | Medium |
| INSERT | Column populated via INSERT | Medium |
| DELETE | Column's row deleted | High |
| MIXED | Multiple operations detected | High |
| MERGE_TARGET | Target column in MERGE | High |
| MERGE_SOURCE | Source column in MERGE | Low |
| TABLE_LEVEL_DELETE | DELETE without column granularity | High |

### Detection Methods

| Method | Coverage | Limitations |
|--------|----------|-------------|
| DMV | 80% | Misses dynamic SQL, temp tables |
| TEXT_PARSE | 95% | May have false positives from comments |
| MANUAL | 100% | Time-intensive |

**Recommendation**: Combine DMV + TEXT_PARSE for best coverage.

---

## Schema

```sql
-- Column lineage storage table
CREATE TABLE daqa.ColumnLineage (
    LineageId INT IDENTITY(1,1) PRIMARY KEY,
    procedure_schema NVARCHAR(128) NOT NULL,
    procedure_name NVARCHAR(128) NOT NULL,
    referenced_schema NVARCHAR(128) NOT NULL,
    referenced_table NVARCHAR(128) NOT NULL,
    referenced_column NVARCHAR(128) NOT NULL,
    operation_type NVARCHAR(20) NOT NULL,
    [read] BIT DEFAULT 0,
    [update] BIT DEFAULT 0,
    [insert] BIT DEFAULT 0,
    [delete] BIT DEFAULT 0,
    merge_target BIT DEFAULT 0,
    merge_source BIT DEFAULT 0,
    detection_method NVARCHAR(20) DEFAULT 'DMV',
    last_analyzed DATETIME2 DEFAULT GETUTCDATE(),
    
    INDEX IX_ColumnLineage_Proc (procedure_schema, procedure_name),
    INDEX IX_ColumnLineage_Table (referenced_schema, referenced_table),
    INDEX IX_ColumnLineage_Column (referenced_schema, referenced_table, referenced_column),
    INDEX IX_ColumnLineage_Operation (operation_type)
);

-- Dynamic SQL procedures tracking
CREATE TABLE daqa.DynamicSqlProcedures (
    ProcedureId INT IDENTITY(1,1) PRIMARY KEY,
    schema_name NVARCHAR(128) NOT NULL,
    procedure_name NVARCHAR(128) NOT NULL,
    dynamic_pattern NVARCHAR(50),
    requires_manual_analysis BIT DEFAULT 1,
    last_analyzed DATETIME2 DEFAULT GETUTCDATE(),
    notes NVARCHAR(MAX),
    
    UNIQUE (schema_name, procedure_name)
);
```

---

## Extraction Queries

### Extract Column Lineage from DMV

```sql
-- Column-level dependencies with operation flags
WITH ColumnDeps AS (
    SELECT 
        OBJECT_SCHEMA_NAME(sed.referencing_id) AS procedure_schema,
        OBJECT_NAME(sed.referencing_id) AS procedure_name,
        sed.referenced_schema_name AS referenced_schema,
        sed.referenced_entity_name AS referenced_table,
        sed.referenced_minor_name AS referenced_column,
        sed.is_updated,
        sed.is_select_all
    FROM sys.sql_expression_dependencies sed
    JOIN sys.objects o ON sed.referencing_id = o.object_id
    WHERE sed.referenced_minor_name IS NOT NULL
      AND o.type = 'P'
)
SELECT 
    procedure_schema,
    procedure_name,
    referenced_schema,
    referenced_table,
    referenced_column,
    CASE 
        WHEN is_updated = 1 THEN 'UPDATE'
        ELSE 'READ'
    END AS operation_type,
    CASE WHEN is_updated = 0 THEN 1 ELSE 0 END AS [read],
    CASE WHEN is_updated = 1 THEN 1 ELSE 0 END AS [update],
    0 AS [insert],
    0 AS [delete],
    'DMV' AS detection_method
FROM ColumnDeps
ORDER BY procedure_schema, procedure_name, referenced_table, referenced_column;
```

### Populate ColumnLineage Table

```sql
-- Merge extracted data into lineage table
MERGE daqa.ColumnLineage AS target
USING (
    SELECT 
        OBJECT_SCHEMA_NAME(sed.referencing_id) AS procedure_schema,
        OBJECT_NAME(sed.referencing_id) AS procedure_name,
        sed.referenced_schema_name AS referenced_schema,
        sed.referenced_entity_name AS referenced_table,
        sed.referenced_minor_name AS referenced_column,
        CASE WHEN sed.is_updated = 1 THEN 'UPDATE' ELSE 'READ' END AS operation_type,
        CASE WHEN sed.is_updated = 0 THEN 1 ELSE 0 END AS [read],
        CASE WHEN sed.is_updated = 1 THEN 1 ELSE 0 END AS [update]
    FROM sys.sql_expression_dependencies sed
    JOIN sys.objects o ON sed.referencing_id = o.object_id
    WHERE sed.referenced_minor_name IS NOT NULL
      AND o.type = 'P'
) AS source
ON target.procedure_schema = source.procedure_schema
   AND target.procedure_name = source.procedure_name
   AND target.referenced_schema = source.referenced_schema
   AND target.referenced_table = source.referenced_table
   AND target.referenced_column = source.referenced_column
WHEN MATCHED THEN
    UPDATE SET 
        operation_type = source.operation_type,
        [read] = source.[read],
        [update] = source.[update],
        last_analyzed = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (procedure_schema, procedure_name, referenced_schema, 
            referenced_table, referenced_column, operation_type, [read], [update])
    VALUES (source.procedure_schema, source.procedure_name, source.referenced_schema,
            source.referenced_table, source.referenced_column, source.operation_type, 
            source.[read], source.[update]);
```

---

## Query Patterns

### By Operation Type

```sql
-- Find all columns UPDATED by a procedure
SELECT referenced_schema, referenced_table, referenced_column
FROM daqa.ColumnLineage
WHERE procedure_schema = @Schema 
  AND procedure_name = @ProcName
  AND [update] = 1;

-- Find all columns INSERTED by a procedure
SELECT referenced_schema, referenced_table, referenced_column
FROM daqa.ColumnLineage
WHERE procedure_schema = @Schema 
  AND procedure_name = @ProcName
  AND [insert] = 1;

-- Find columns with DELETE operations (high risk)
SELECT DISTINCT 
    procedure_schema, procedure_name,
    referenced_schema, referenced_table, referenced_column
FROM daqa.ColumnLineage
WHERE [delete] = 1
ORDER BY referenced_table, referenced_column;
```

### By Column (Impact Analysis)

```sql
-- Find all procedures that UPDATE a specific column
SELECT DISTINCT procedure_schema, procedure_name
FROM daqa.ColumnLineage
WHERE referenced_schema = @Schema
  AND referenced_table = @TableName
  AND referenced_column = @ColumnName
  AND [update] = 1;

-- Find all procedures that reference a column (any operation)
SELECT 
    procedure_schema, procedure_name, operation_type,
    [read], [update], [insert], [delete]
FROM daqa.ColumnLineage
WHERE referenced_schema = @Schema
  AND referenced_table = @TableName
  AND referenced_column = @ColumnName
ORDER BY operation_type DESC, procedure_name;

-- Find READ-ONLY references (safe for non-breaking changes)
SELECT procedure_schema, procedure_name
FROM daqa.ColumnLineage
WHERE referenced_schema = @Schema
  AND referenced_table = @TableName
  AND referenced_column = @ColumnName
  AND [read] = 1 
  AND [update] = 0 
  AND [insert] = 0 
  AND [delete] = 0;
```

### Mixed Operations Analysis

```sql
-- Find columns with MIXED operations (complex data flow)
SELECT 
    referenced_schema + '.' + referenced_table + '.' + referenced_column AS column_fqn,
    COUNT(DISTINCT procedure_name) AS proc_count,
    STRING_AGG(DISTINCT operation_type, ', ') AS operations
FROM daqa.ColumnLineage
GROUP BY referenced_schema, referenced_table, referenced_column
HAVING COUNT(DISTINCT operation_type) > 1
ORDER BY proc_count DESC;

-- Find procedures with both READ and WRITE to same table
SELECT 
    procedure_schema, procedure_name, referenced_table,
    SUM([read]) AS read_cols,
    SUM([update]) AS update_cols,
    SUM([insert]) AS insert_cols
FROM daqa.ColumnLineage
GROUP BY procedure_schema, procedure_name, referenced_table
HAVING SUM([read]) > 0 AND (SUM([update]) > 0 OR SUM([insert]) > 0)
ORDER BY update_cols + insert_cols DESC;
```

---

## Risk Scoring

```sql
-- Column risk score calculation
SELECT 
    referenced_schema + '.' + referenced_table + '.' + referenced_column AS column_fqn,
    COUNT(DISTINCT procedure_name) AS total_references,
    SUM(CASE WHEN [read] = 1 THEN 1 ELSE 0 END) AS read_count,
    SUM(CASE WHEN [update] = 1 THEN 1 ELSE 0 END) AS update_count,
    SUM(CASE WHEN [insert] = 1 THEN 1 ELSE 0 END) AS insert_count,
    SUM(CASE WHEN [delete] = 1 THEN 1 ELSE 0 END) AS delete_count,
    -- Weighted risk score
    (COUNT(DISTINCT procedure_name) * 1) +        -- Base: 1 per reference
    (SUM(CASE WHEN [update] = 1 THEN 3 ELSE 0 END)) +  -- UPDATE: 3 points
    (SUM(CASE WHEN [insert] = 1 THEN 2 ELSE 0 END)) +  -- INSERT: 2 points
    (SUM(CASE WHEN [delete] = 1 THEN 5 ELSE 0 END))    -- DELETE: 5 points
    AS risk_score
FROM daqa.ColumnLineage
WHERE referenced_schema = @Schema AND referenced_table = @TableName
GROUP BY referenced_schema, referenced_table, referenced_column
ORDER BY risk_score DESC;

-- Risk classification
SELECT 
    column_fqn,
    risk_score,
    CASE 
        WHEN risk_score >= 50 THEN 'CRITICAL'
        WHEN risk_score >= 25 THEN 'HIGH'
        WHEN risk_score >= 10 THEN 'MEDIUM'
        ELSE 'LOW'
    END AS risk_level
FROM (
    SELECT 
        referenced_schema + '.' + referenced_table + '.' + referenced_column AS column_fqn,
        (COUNT(DISTINCT procedure_name) * 1) +
        (SUM(CASE WHEN [update] = 1 THEN 3 ELSE 0 END)) +
        (SUM(CASE WHEN [delete] = 1 THEN 5 ELSE 0 END)) AS risk_score
    FROM daqa.ColumnLineage
    WHERE referenced_table = @TableName
    GROUP BY referenced_schema, referenced_table, referenced_column
) scored
ORDER BY risk_score DESC;
```

---

## C# Integration

### Models

```csharp
public class ColumnLineage
{
    public string ProcedureSchema { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string ReferencedSchema { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public string ReferencedColumn { get; set; } = string.Empty;
    public OperationType Operation { get; set; }
    public bool IsRead { get; set; }
    public bool IsUpdate { get; set; }
    public bool IsInsert { get; set; }
    public bool IsDelete { get; set; }
    public string DetectionMethod { get; set; } = "DMV";
    
    public string ColumnFqn => $"{ReferencedSchema}.{ReferencedTable}.{ReferencedColumn}";
    public string ProcedureFqn => $"{ProcedureSchema}.{ProcedureName}";
}

public enum OperationType
{
    Read,
    Update,
    Insert,
    Delete,
    Mixed,
    MergeTarget,
    MergeSource,
    TableLevelDelete
}

public class ColumnImpactAnalysis
{
    public string ColumnFqn { get; set; } = string.Empty;
    public int TotalReferences { get; set; }
    public int ReadCount { get; set; }
    public int UpdateCount { get; set; }
    public int InsertCount { get; set; }
    public int DeleteCount { get; set; }
    public int RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public List<string> AffectedProcedures { get; set; } = [];
    public List<string> DynamicSqlWarnings { get; set; } = [];
}

public enum RiskLevel { Low, Medium, High, Critical }
```

### Service Interface

```csharp
public interface IColumnLineageService
{
    Task<IEnumerable<ColumnLineage>> GetByProcedureAsync(string schema, string procName);
    Task<IEnumerable<ColumnLineage>> GetByColumnAsync(string schema, string table, string column);
    Task<IEnumerable<ColumnLineage>> GetByOperationAsync(OperationType operation);
    Task<ColumnImpactAnalysis> AnalyzeImpactAsync(string schema, string table, string column);
    Task RefreshLineageAsync();
}
```

### Implementation Example

```csharp
public class ColumnLineageService : IColumnLineageService
{
    private readonly IDbConnection _connection;
    private readonly ILogger<ColumnLineageService> _logger;

    public async Task<ColumnImpactAnalysis> AnalyzeImpactAsync(
        string schema, string table, string column)
    {
        var lineage = await GetByColumnAsync(schema, table, column);
        var items = lineage.ToList();
        
        var analysis = new ColumnImpactAnalysis
        {
            ColumnFqn = $"{schema}.{table}.{column}",
            TotalReferences = items.Select(l => l.ProcedureFqn).Distinct().Count(),
            ReadCount = items.Count(l => l.IsRead),
            UpdateCount = items.Count(l => l.IsUpdate),
            InsertCount = items.Count(l => l.IsInsert),
            DeleteCount = items.Count(l => l.IsDelete),
            AffectedProcedures = items.Select(l => l.ProcedureFqn).Distinct().ToList()
        };
        
        // Calculate risk score
        analysis.RiskScore = analysis.TotalReferences +
                            (analysis.UpdateCount * 3) +
                            (analysis.InsertCount * 2) +
                            (analysis.DeleteCount * 5);
        
        analysis.RiskLevel = analysis.RiskScore switch
        {
            >= 50 => RiskLevel.Critical,
            >= 25 => RiskLevel.High,
            >= 10 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };
        
        // Check for dynamic SQL procedures
        var dynamicProcs = await GetDynamicSqlProceduresAsync();
        analysis.DynamicSqlWarnings = items
            .Where(l => dynamicProcs.Contains(l.ProcedureFqn))
            .Select(l => $"{l.ProcedureFqn} uses dynamic SQL - manual review required")
            .Distinct()
            .ToList();
        
        return analysis;
    }
}
```

---

## Data Flow Queries

### Source to Target Mapping

```sql
-- Find data flow: source tables → procedure → target tables
WITH DataFlow AS (
    SELECT 
        s.procedure_schema + '.' + s.procedure_name AS procedure_fqn,
        s.referenced_schema + '.' + s.referenced_table AS source_table,
        t.referenced_schema + '.' + t.referenced_table AS target_table
    FROM daqa.ColumnLineage s
    JOIN daqa.ColumnLineage t 
        ON s.procedure_schema = t.procedure_schema 
        AND s.procedure_name = t.procedure_name
    WHERE s.[read] = 1 
      AND (t.[insert] = 1 OR t.[update] = 1)
      AND s.referenced_table <> t.referenced_table
)
SELECT DISTINCT source_table, procedure_fqn, target_table
FROM DataFlow
ORDER BY source_table, target_table;
```

### Column Transformation Tracking

```sql
-- Track column-to-column data flow within a procedure
SELECT 
    s.referenced_column AS source_column,
    s.referenced_table AS source_table,
    t.referenced_column AS target_column,
    t.referenced_table AS target_table,
    s.procedure_name
FROM daqa.ColumnLineage s
JOIN daqa.ColumnLineage t 
    ON s.procedure_schema = t.procedure_schema 
    AND s.procedure_name = t.procedure_name
WHERE s.[read] = 1 
  AND t.[insert] = 1
  AND s.referenced_table <> t.referenced_table
  AND s.procedure_name = @ProcName
ORDER BY s.referenced_column;
```

---

## Environment Data Patterns

From your IRFS1 database analysis:

### High-Usage Columns

| Schema | Table | Column | References | Primary Operation |
|--------|-------|--------|------------|-------------------|
| bal | bal_loss_tran | CLAIM_KEY | 50+ | READ |
| bal | bal_loss_tran | TRANS_AMT | 40+ | READ/UPDATE |
| irfcycle | irf_policy | POL_KEY | 80+ | READ |
| bal | balance_financials | * | 40+ | DELETE/INSERT (ETL reload) |

### Common Patterns Detected

1. **ETL Reload Pattern**: DELETE all + INSERT new
   - `bal.balance_financials` - TABLE_LEVEL_DELETE then INSERT columns
   
2. **Accumulator Pattern**: READ + UPDATE same column
   - `bal.balance_financials_lob` - UPDATE on LAE_PAID_CURR_PER, LOSS_PAID_CURR_PER

3. **Lookup Pattern**: READ-only on key columns
   - `bal.bal_loss_tran.CLAIM_KEY` - READ in joins

### Dynamic SQL Procedures

These require TEXT_PARSE or manual analysis:

| Schema | Procedure | Pattern | Notes |
|--------|-----------|---------|-------|
| bal | uspDeletePeriodData | sp_executesql | Period-based cleanup |
| dbo | ArchiveTables | sp_executesql | Table archiving |
| dbo | uspMonitorMissingCoverage | EXEC variable | Coverage monitoring |
| bal | uspDeletePeriodData_test_kz | sp_executesql | Test version |
| dbo | usp_03541_Manual_Updates | sp_executesql | Manual updates |
