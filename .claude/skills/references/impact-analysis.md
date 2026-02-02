# Impact Analysis Reference

Framework for assessing the risk and scope of database changes before implementation.

---

## Impact Analysis Framework

### The 5 Questions Before Any Change

1. **What directly depends on this object?** (Direct Impact)
2. **What depends on THOSE objects?** (Cascade Impact)
3. **Which business processes use this data?** (Business Impact)
4. **Which reports/dashboards consume this?** (Reporting Impact)
5. **What's the rollback strategy?** (Recovery Plan)

### Impact Severity Matrix

| Level | Direct Deps | Cascade Deps | Criteria | Action Required |
|-------|-------------|--------------|----------|-----------------|
| CRITICAL | >20 | >50 | Core tables, >100 refs | Executive approval, full regression |
| HIGH | 10-20 | 20-50 | Shared dimensions, KPIs | Team lead approval, targeted testing |
| MEDIUM | 5-10 | 10-20 | Domain-specific tables | Peer review, unit tests |
| LOW | <5 | <10 | Isolated tables | Standard review |

---

## SQL Queries for Impact Assessment

### Complete Impact Report

```sql
-- Generate full impact assessment for an object
DECLARE @ObjectName NVARCHAR(128) = 'YourTableName';
DECLARE @SchemaName NVARCHAR(128) = 'dbo';

-- 1. Direct Dependencies
SELECT 'DIRECT DEPENDENTS' AS section;

SELECT 
    OBJECT_SCHEMA_NAME(d.referencing_id) AS schema_name,
    OBJECT_NAME(d.referencing_id) AS object_name,
    o.type_desc AS object_type,
    o.modify_date AS last_modified
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE d.referenced_entity_name = @ObjectName
  AND (d.referenced_schema_name = @SchemaName OR d.referenced_schema_name IS NULL)
ORDER BY o.type_desc, object_name;

-- 2. Cascade Dependencies (2 levels)
SELECT 'CASCADE DEPENDENTS (Level 2)' AS section;

WITH Level1 AS (
    SELECT OBJECT_NAME(d.referencing_id) AS object_name
    FROM sys.sql_expression_dependencies d
    WHERE d.referenced_entity_name = @ObjectName
)
SELECT DISTINCT
    OBJECT_SCHEMA_NAME(d.referencing_id) AS schema_name,
    OBJECT_NAME(d.referencing_id) AS object_name,
    o.type_desc AS object_type
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
JOIN Level1 l ON d.referenced_entity_name = l.object_name
WHERE OBJECT_NAME(d.referencing_id) NOT IN (SELECT object_name FROM Level1)
ORDER BY o.type_desc, object_name;

-- 3. Summary Metrics
SELECT 'IMPACT SUMMARY' AS section;

SELECT 
    (SELECT COUNT(DISTINCT referencing_id) 
     FROM sys.sql_expression_dependencies 
     WHERE referenced_entity_name = @ObjectName) AS direct_dependents,
    
    (SELECT COUNT(DISTINCT d2.referencing_id)
     FROM sys.sql_expression_dependencies d1
     JOIN sys.sql_expression_dependencies d2 
         ON d2.referenced_entity_name = OBJECT_NAME(d1.referencing_id)
     WHERE d1.referenced_entity_name = @ObjectName) AS cascade_dependents,
    
    CASE 
        WHEN (SELECT COUNT(*) FROM sys.sql_expression_dependencies 
              WHERE referenced_entity_name = @ObjectName) > 20 THEN 'CRITICAL'
        WHEN (SELECT COUNT(*) FROM sys.sql_expression_dependencies 
              WHERE referenced_entity_name = @ObjectName) > 10 THEN 'HIGH'
        WHEN (SELECT COUNT(*) FROM sys.sql_expression_dependencies 
              WHERE referenced_entity_name = @ObjectName) > 5 THEN 'MEDIUM'
        ELSE 'LOW'
    END AS impact_severity;
```

### Impact by Object Type

```sql
-- Group dependents by type for targeted testing
SELECT 
    o.type_desc AS object_type,
    COUNT(*) AS count,
    STRING_AGG(OBJECT_NAME(d.referencing_id), ', ') 
        WITHIN GROUP (ORDER BY OBJECT_NAME(d.referencing_id)) AS objects
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE d.referenced_entity_name = @TableName
GROUP BY o.type_desc
ORDER BY count DESC;
```

### Foreign Key Impact

```sql
-- Check FK constraints that would be affected
SELECT 
    fk.name AS fk_name,
    ps.name + '.' + pt.name AS parent_table,
    pc.name AS parent_column,
    'Would fail if row deleted' AS impact
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id 
    AND fkc.parent_column_id = pc.column_id
JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
WHERE rt.name = @TableName;
```

---

## Change Type Analysis

### Column Rename Impact

```sql
-- Find all references to a specific column
SELECT 
    OBJECT_SCHEMA_NAME(d.referencing_id) AS schema_name,
    OBJECT_NAME(d.referencing_id) AS object_name,
    o.type_desc,
    m.definition  -- Full object definition to find column usage
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
JOIN sys.sql_modules m ON o.object_id = m.object_id
WHERE d.referenced_entity_name = @TableName
  AND m.definition LIKE '%' + @ColumnName + '%'
ORDER BY o.type_desc, object_name;
```

### Data Type Change Impact

```sql
-- Columns that reference this column (for type changes)
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    c.name AS column_name,
    ty.name AS current_type,
    c.max_length,
    'Check compatibility with new type' AS action
FROM sys.foreign_key_columns fkc
JOIN sys.columns c ON fkc.parent_object_id = c.object_id 
    AND fkc.parent_column_id = c.column_id
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE fkc.referenced_object_id = OBJECT_ID(@TableName)
  AND fkc.referenced_column_id = (
      SELECT column_id FROM sys.columns 
      WHERE object_id = OBJECT_ID(@TableName) AND name = @ColumnName
  );
```

### Table Drop Impact

```sql
-- Full impact report for dropping a table
DECLARE @TableName NVARCHAR(128) = 'YourTable';

SELECT 'BLOCKING CONSTRAINTS' AS category, 
    fk.name AS item,
    'FK on ' + OBJECT_NAME(fk.parent_object_id) AS detail
FROM sys.foreign_keys fk
WHERE fk.referenced_object_id = OBJECT_ID(@TableName)

UNION ALL

SELECT 'DEPENDENT PROCEDURES',
    OBJECT_NAME(d.referencing_id),
    'Will fail after drop'
FROM sys.sql_expression_dependencies d
JOIN sys.procedures p ON d.referencing_id = p.object_id
WHERE d.referenced_entity_name = @TableName

UNION ALL

SELECT 'DEPENDENT VIEWS',
    OBJECT_NAME(d.referencing_id),
    'Will become invalid'
FROM sys.sql_expression_dependencies d
JOIN sys.views v ON d.referencing_id = v.object_id
WHERE d.referenced_entity_name = @TableName

UNION ALL

SELECT 'DEPENDENT FUNCTIONS',
    OBJECT_NAME(d.referencing_id),
    'Will fail after drop'
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE d.referenced_entity_name = @TableName
  AND o.type IN ('FN', 'IF', 'TF');
```

---

## Business Impact Patterns

### Identify Business-Critical Objects

```sql
-- Objects with high dependency counts are likely business-critical
SELECT TOP 20
    d.referenced_entity_name AS object_name,
    COALESCE(d.referenced_schema_name, 'dbo') AS schema_name,
    COUNT(DISTINCT d.referencing_id) AS dependent_count,
    COUNT(DISTINCT CASE WHEN o.type = 'P' THEN d.referencing_id END) AS proc_refs,
    COUNT(DISTINCT CASE WHEN o.type = 'V' THEN d.referencing_id END) AS view_refs,
    CASE 
        WHEN COUNT(*) > 50 THEN '🔴 CRITICAL'
        WHEN COUNT(*) > 20 THEN '🟠 HIGH'
        WHEN COUNT(*) > 10 THEN '🟡 MEDIUM'
        ELSE '🟢 LOW'
    END AS criticality
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE d.referenced_entity_name IS NOT NULL
GROUP BY d.referenced_entity_name, d.referenced_schema_name
ORDER BY dependent_count DESC;
```

### ETL Process Impact

```sql
-- Find ETL-related objects that would be affected
SELECT 
    OBJECT_NAME(d.referencing_id) AS etl_object,
    o.type_desc,
    o.modify_date AS last_run_proxy
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
WHERE d.referenced_entity_name = @TableName
  AND (
    OBJECT_NAME(d.referencing_id) LIKE '%load%'
    OR OBJECT_NAME(d.referencing_id) LIKE '%etl%'
    OR OBJECT_NAME(d.referencing_id) LIKE '%sync%'
    OR OBJECT_NAME(d.referencing_id) LIKE '%import%'
    OR OBJECT_NAME(d.referencing_id) LIKE '%refresh%'
  );
```

---

## .NET Impact Assessment Service

```csharp
public class ImpactAssessmentService
{
    private readonly IDbConnection _db;
    
    public async Task<ImpactReport> AssessImpactAsync(string objectName, string schemaName = "dbo")
    {
        var report = new ImpactReport { ObjectName = objectName, Schema = schemaName };
        
        // Get direct dependents
        report.DirectDependents = await GetDirectDependentsAsync(objectName, schemaName);
        
        // Get cascade dependents
        report.CascadeDependents = await GetCascadeDependentsAsync(
            report.DirectDependents.Select(d => d.ObjectName).ToList());
        
        // Calculate severity
        report.Severity = CalculateSeverity(
            report.DirectDependents.Count, 
            report.CascadeDependents.Count);
        
        // Get affected by type
        report.AffectedProcedures = report.DirectDependents
            .Where(d => d.ObjectType == "SQL_STORED_PROCEDURE")
            .Select(d => d.ObjectName).ToList();
            
        report.AffectedViews = report.DirectDependents
            .Where(d => d.ObjectType == "VIEW")
            .Select(d => d.ObjectName).ToList();
        
        return report;
    }
    
    private ImpactSeverity CalculateSeverity(int direct, int cascade)
    {
        var total = direct + cascade;
        return total switch
        {
            > 50 => ImpactSeverity.Critical,
            > 20 => ImpactSeverity.High,
            > 10 => ImpactSeverity.Medium,
            _ => ImpactSeverity.Low
        };
    }
}

public class ImpactReport
{
    public string ObjectName { get; set; }
    public string Schema { get; set; }
    public ImpactSeverity Severity { get; set; }
    public List<DependentObject> DirectDependents { get; set; } = [];
    public List<DependentObject> CascadeDependents { get; set; } = [];
    public List<string> AffectedProcedures { get; set; } = [];
    public List<string> AffectedViews { get; set; } = [];
    public List<string> AffectedForeignKeys { get; set; } = [];
    
    public string GenerateMarkdownReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Impact Assessment: {Schema}.{ObjectName}");
        sb.AppendLine();
        sb.AppendLine($"**Severity:** {Severity}");
        sb.AppendLine($"**Direct Dependents:** {DirectDependents.Count}");
        sb.AppendLine($"**Cascade Dependents:** {CascadeDependents.Count}");
        sb.AppendLine();
        
        if (AffectedProcedures.Any())
        {
            sb.AppendLine("## Affected Procedures");
            foreach (var proc in AffectedProcedures)
                sb.AppendLine($"- {proc}");
        }
        
        if (AffectedViews.Any())
        {
            sb.AppendLine("## Affected Views");
            foreach (var view in AffectedViews)
                sb.AppendLine($"- {view}");
        }
        
        return sb.ToString();
    }
}

public enum ImpactSeverity { Low, Medium, High, Critical }

public record DependentObject(
    string Schema,
    string ObjectName,
    string ObjectType,
    DateTime? LastModified
);
```

---

## Pre-Change Checklist

### Before Schema Changes

- [ ] Run impact assessment query
- [ ] Identify all dependent stored procedures
- [ ] Identify all dependent views
- [ ] Check foreign key relationships
- [ ] Review ETL/load processes that use this object
- [ ] Document rollback procedure
- [ ] Schedule change during low-activity window
- [ ] Notify dependent teams

### Before Dropping Objects

- [ ] Verify object is truly unused (check execution stats)
- [ ] Remove all foreign key references first
- [ ] Update or remove dependent views
- [ ] Update or remove dependent procedures
- [ ] Archive object definition for potential recovery
- [ ] Document in change log

### After Changes

- [ ] Verify all dependent objects still function
- [ ] Run regression tests on affected procedures
- [ ] Validate views return expected results
- [ ] Monitor error logs for failures
- [ ] Update documentation

---

## Change Impact Documentation Template

```markdown
# Change Impact Assessment

## Change Details
- **Object:** [schema].[object_name]
- **Change Type:** [ADD/MODIFY/DROP] [COLUMN/TABLE/PROCEDURE]
- **Requested By:** [Name]
- **Date:** [YYYY-MM-DD]

## Impact Summary
- **Severity:** [CRITICAL/HIGH/MEDIUM/LOW]
- **Direct Dependents:** [count]
- **Cascade Dependents:** [count]

## Affected Objects

### Stored Procedures
| Name | Last Modified | Owner | Status |
|------|--------------|-------|--------|
| | | | |

### Views
| Name | Last Modified | Owner | Status |
|------|--------------|-------|--------|
| | | | |

### Foreign Keys
| FK Name | Parent Table | Status |
|---------|--------------|--------|
| | | |

## Testing Plan
1. [ ] Unit tests for modified procedures
2. [ ] View validation queries
3. [ ] ETL process dry run
4. [ ] User acceptance testing

## Rollback Plan
1. [Step-by-step rollback instructions]

## Approvals
- [ ] Technical Lead
- [ ] Data Steward
- [ ] Release Manager (if CRITICAL)
```
