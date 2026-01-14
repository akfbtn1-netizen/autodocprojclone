# Dynamic SQL Detection Reference

Identify and handle stored procedures with dynamic SQL that DMVs cannot fully analyze.

---

## The Problem

SQL Server's `sys.sql_expression_dependencies` DMV only tracks **static** SQL references. Dynamic SQL constructed at runtime is invisible to the DMV, creating gaps in lineage tracking.

### What DMVs Miss

| Pattern | Example | Coverage Gap |
|---------|---------|--------------|
| EXEC(@sql) | `EXEC(@dynamicQuery)` | 100% missed |
| sp_executesql | `EXEC sp_executesql @sql` | 100% missed |
| Dynamic table names | `SET @table = 'Orders_' + @year` | 100% missed |
| Temp table operations | `SELECT INTO #temp` | Partial |
| Cursor-based DML | `FETCH NEXT... UPDATE` | Often missed |

---

## Detection Queries

### Find All Dynamic SQL Procedures

```sql
-- Comprehensive dynamic SQL detection
SELECT 
    SCHEMA_NAME(o.schema_id) AS schema_name,
    o.name AS procedure_name,
    o.create_date,
    o.modify_date,
    CASE 
        WHEN m.definition LIKE '%sp_executesql%' THEN 'sp_executesql'
        WHEN m.definition LIKE '%EXEC%(@%' THEN 'EXEC variable'
        WHEN m.definition LIKE '%EXECUTE%(@%' THEN 'EXECUTE variable'
    END AS dynamic_pattern,
    LEN(m.definition) AS definition_length
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.type = 'P'
  AND (m.definition LIKE '%EXEC%(@%' 
       OR m.definition LIKE '%EXECUTE%(@%'
       OR m.definition LIKE '%sp_executesql%')
ORDER BY schema_name, procedure_name;
```

### Categorize by Risk Level

```sql
-- Risk-based categorization
WITH DynamicProcs AS (
    SELECT 
        SCHEMA_NAME(o.schema_id) AS schema_name,
        o.name AS procedure_name,
        m.definition,
        CASE WHEN m.definition LIKE '%sp_executesql%' THEN 1 ELSE 0 END AS has_sp_executesql,
        CASE WHEN m.definition LIKE '%EXEC%(@%' OR m.definition LIKE '%EXECUTE%(@%' THEN 1 ELSE 0 END AS has_exec_var,
        CASE WHEN m.definition LIKE '%DELETE%' THEN 1 ELSE 0 END AS has_delete,
        CASE WHEN m.definition LIKE '%TRUNCATE%' THEN 1 ELSE 0 END AS has_truncate,
        CASE WHEN m.definition LIKE '%DROP%TABLE%' THEN 1 ELSE 0 END AS has_drop
    FROM sys.sql_modules m
    JOIN sys.objects o ON m.object_id = o.object_id
    WHERE o.type = 'P'
      AND (m.definition LIKE '%EXEC%(@%' 
           OR m.definition LIKE '%EXECUTE%(@%'
           OR m.definition LIKE '%sp_executesql%')
)
SELECT 
    schema_name,
    procedure_name,
    CASE 
        WHEN has_drop = 1 THEN 'CRITICAL'
        WHEN has_truncate = 1 THEN 'CRITICAL'
        WHEN has_delete = 1 AND has_sp_executesql = 1 THEN 'HIGH'
        WHEN has_sp_executesql = 1 THEN 'MEDIUM'
        ELSE 'LOW'
    END AS risk_level,
    has_sp_executesql,
    has_exec_var,
    has_delete,
    has_truncate,
    has_drop
FROM DynamicProcs
ORDER BY 
    CASE 
        WHEN has_drop = 1 THEN 1
        WHEN has_truncate = 1 THEN 2
        WHEN has_delete = 1 THEN 3
        ELSE 4
    END,
    schema_name, procedure_name;
```

### Extract Dynamic SQL Context

```sql
-- Find the dynamic SQL construction patterns
SELECT 
    SCHEMA_NAME(o.schema_id) AS schema_name,
    o.name AS procedure_name,
    -- Find variable declarations used in EXEC
    CASE 
        WHEN m.definition LIKE '%@sql%' THEN '@sql'
        WHEN m.definition LIKE '%@query%' THEN '@query'
        WHEN m.definition LIKE '%@cmd%' THEN '@cmd'
        WHEN m.definition LIKE '%@stmt%' THEN '@stmt'
        ELSE 'unknown'
    END AS likely_sql_variable,
    -- Check for parameter passing
    CASE 
        WHEN m.definition LIKE '%sp_executesql%@%,%@%' THEN 'Parameterized'
        WHEN m.definition LIKE '%sp_executesql%' THEN 'Non-parameterized'
        ELSE 'Direct EXEC'
    END AS execution_style
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.type = 'P'
  AND (m.definition LIKE '%EXEC%(@%' 
       OR m.definition LIKE '%sp_executesql%');
```

---

## Known Dynamic SQL Procedures

From your IRFS1 environment:

```sql
-- Seed known dynamic SQL procedures
INSERT INTO daqa.DynamicSqlProcedures 
    (schema_name, procedure_name, dynamic_pattern, requires_manual_analysis, notes)
VALUES 
    ('bal', 'uspDeletePeriodData', 'sp_executesql', 1, 
     'Period-based data cleanup - deletes based on accounting period parameter'),
    ('dbo', 'ArchiveTables', 'sp_executesql', 1, 
     'Table archiving utility - creates archive tables dynamically'),
    ('dbo', 'uspMonitorMissingCoverage', 'EXEC variable', 1, 
     'Coverage monitoring - queries built dynamically based on coverage types'),
    ('bal', 'uspDeletePeriodData_test_kz', 'sp_executesql', 1, 
     'Test version of period delete - same pattern as production'),
    ('dbo', 'usp_03541_Manual_Updates', 'sp_executesql', 1, 
     'Manual update utility - table/column names passed as parameters');
```

---

## Text Parsing Approach

When DMVs fail, parse procedure text directly:

### Extract Table References from Text

```sql
-- Find table references in procedure text (basic pattern matching)
WITH ProcText AS (
    SELECT 
        SCHEMA_NAME(o.schema_id) AS proc_schema,
        o.name AS proc_name,
        m.definition AS proc_text
    FROM sys.sql_modules m
    JOIN sys.objects o ON m.object_id = o.object_id
    WHERE o.type = 'P'
),
TableRefs AS (
    SELECT 
        proc_schema,
        proc_name,
        t.TABLE_SCHEMA AS ref_schema,
        t.TABLE_NAME AS ref_table
    FROM ProcText pt
    CROSS APPLY (
        SELECT TABLE_SCHEMA, TABLE_NAME 
        FROM INFORMATION_SCHEMA.TABLES
        WHERE pt.proc_text LIKE '%' + TABLE_NAME + '%'
    ) t
)
SELECT DISTINCT proc_schema, proc_name, ref_schema, ref_table
FROM TableRefs
ORDER BY proc_schema, proc_name, ref_table;
```

### Detect DML Operations in Text

```sql
-- Classify operations from procedure text
SELECT 
    SCHEMA_NAME(o.schema_id) AS schema_name,
    o.name AS procedure_name,
    CASE WHEN m.definition LIKE '%INSERT INTO%' OR m.definition LIKE '%INSERT%INTO%' THEN 1 ELSE 0 END AS has_insert,
    CASE WHEN m.definition LIKE '%UPDATE %SET%' THEN 1 ELSE 0 END AS has_update,
    CASE WHEN m.definition LIKE '%DELETE FROM%' OR m.definition LIKE '%DELETE %WHERE%' THEN 1 ELSE 0 END AS has_delete,
    CASE WHEN m.definition LIKE '%MERGE %INTO%' THEN 1 ELSE 0 END AS has_merge,
    CASE WHEN m.definition LIKE '%TRUNCATE TABLE%' THEN 1 ELSE 0 END AS has_truncate,
    CASE WHEN m.definition LIKE '%SELECT INTO%' THEN 1 ELSE 0 END AS has_select_into
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
WHERE o.type = 'P'
  AND SCHEMA_NAME(o.schema_id) IN ('bal', 'dbo', 'irfcycle', 'gwpc', 'gw', 'DaQa')
ORDER BY schema_name, procedure_name;
```

---

## C# Integration

### Model

```csharp
public class DynamicSqlProcedure
{
    public string SchemaName { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string DynamicPattern { get; set; } = string.Empty;
    public DynamicSqlRisk RiskLevel { get; set; }
    public bool RequiresManualAnalysis { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime LastAnalyzed { get; set; }
    
    public string Fqn => $"{SchemaName}.{ProcedureName}";
}

public enum DynamicSqlRisk
{
    Low,      // SELECT-only dynamic SQL
    Medium,   // INSERT/UPDATE dynamic SQL
    High,     // DELETE dynamic SQL
    Critical  // TRUNCATE/DROP dynamic SQL
}
```

### Service

```csharp
public interface IDynamicSqlService
{
    Task<IEnumerable<DynamicSqlProcedure>> GetAllDynamicProceduresAsync();
    Task<IEnumerable<DynamicSqlProcedure>> GetByRiskLevelAsync(DynamicSqlRisk minRisk);
    Task<bool> IsDynamicSqlProcedureAsync(string schema, string procName);
    Task RefreshDetectionAsync();
    Task<string> GetProcedureTextAsync(string schema, string procName);
}

public class DynamicSqlService : IDynamicSqlService
{
    public async Task<IEnumerable<DynamicSqlProcedure>> GetAllDynamicProceduresAsync()
    {
        const string sql = @"
            SELECT 
                SCHEMA_NAME(o.schema_id) AS SchemaName,
                o.name AS ProcedureName,
                CASE 
                    WHEN m.definition LIKE '%sp_executesql%' THEN 'sp_executesql'
                    WHEN m.definition LIKE '%EXEC%(@%' THEN 'EXEC variable'
                    ELSE 'Unknown'
                END AS DynamicPattern,
                CASE 
                    WHEN m.definition LIKE '%DROP%TABLE%' OR m.definition LIKE '%TRUNCATE%' THEN 3
                    WHEN m.definition LIKE '%DELETE%' THEN 2
                    WHEN m.definition LIKE '%UPDATE%' OR m.definition LIKE '%INSERT%' THEN 1
                    ELSE 0
                END AS RiskLevel
            FROM sys.sql_modules m
            JOIN sys.objects o ON m.object_id = o.object_id
            WHERE o.type = 'P'
              AND (m.definition LIKE '%EXEC%(@%' 
                   OR m.definition LIKE '%sp_executesql%')";
        
        return await _connection.QueryAsync<DynamicSqlProcedure>(sql);
    }
}
```

---

## Handling Strategies

### 1. Manual Documentation

For critical dynamic SQL procedures, document manually:

```sql
-- Add manual lineage entry
INSERT INTO daqa.ColumnLineage 
    (procedure_schema, procedure_name, referenced_schema, referenced_table, 
     referenced_column, operation_type, detection_method, [delete])
VALUES 
    ('bal', 'uspDeletePeriodData', 'bal', 'bal_loss_tran', '*', 
     'TABLE_LEVEL_DELETE', 'MANUAL', 1);
```

### 2. Runtime Tracking

For production, consider:

```sql
-- Audit trigger on target tables
CREATE TRIGGER tr_AuditDynamicChanges ON bal.bal_loss_tran
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    INSERT INTO daqa.RuntimeLineage (table_name, operation, row_count, executed_at)
    SELECT 
        'bal.bal_loss_tran',
        CASE 
            WHEN EXISTS(SELECT 1 FROM inserted) AND EXISTS(SELECT 1 FROM deleted) THEN 'UPDATE'
            WHEN EXISTS(SELECT 1 FROM inserted) THEN 'INSERT'
            ELSE 'DELETE'
        END,
        COALESCE((SELECT COUNT(*) FROM inserted), 0) + COALESCE((SELECT COUNT(*) FROM deleted), 0),
        GETUTCDATE();
END;
```

### 3. Extended Events

Capture actual execution:

```sql
-- Extended events for dynamic SQL tracking
CREATE EVENT SESSION DynamicSqlTracking ON SERVER
ADD EVENT sqlserver.sp_statement_completed (
    WHERE sqlserver.like_i_sql_unicode_string(statement, N'%EXEC%@%')
       OR sqlserver.like_i_sql_unicode_string(statement, N'%sp_executesql%')
)
ADD TARGET package0.event_file (SET filename = N'DynamicSqlTracking.xel');
```

---

## Best Practices

1. **Flag at deployment**: Require review for procedures with dynamic SQL
2. **Document manually**: Add to DynamicSqlProcedures table with notes
3. **Audit periodically**: Re-scan for new dynamic SQL patterns
4. **Test coverage**: Ensure integration tests cover dynamic SQL paths
5. **Runtime monitoring**: Consider Extended Events for production tracking
