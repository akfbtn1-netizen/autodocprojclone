# SQL Server Metadata Queries Reference

Comprehensive query library for extracting database intelligence from SQL Server system views.

---

## Core System Views

### sys.sql_expression_dependencies

The primary source for object dependencies. Captures references from:
- Stored procedures
- Views
- Functions
- Triggers
- Computed columns
- Check constraints

```sql
-- Structure
SELECT 
    referencing_id,           -- Object ID of the referencing object
    referencing_minor_id,     -- Column ID (0 for object-level)
    referencing_class,        -- 1 = Object, 6 = Type, 10 = XML Schema
    referencing_class_desc,   -- OBJECT_OR_COLUMN, TYPE, XML_SCHEMA_COLLECTION
    is_schema_bound_reference,-- 1 if SCHEMABINDING
    referenced_class,         -- Same as referencing_class
    referenced_class_desc,
    referenced_server_name,   -- For cross-server refs
    referenced_database_name, -- For cross-database refs
    referenced_schema_name,   -- Schema of referenced object
    referenced_entity_name,   -- Name of referenced object
    referenced_id,            -- Object ID (NULL if unresolved)
    referenced_minor_id,      -- Column ID
    is_caller_dependent,      -- 1 if resolution depends on caller
    is_ambiguous              -- 1 if reference is ambiguous
FROM sys.sql_expression_dependencies;
```

### Key Limitations

1. **Dynamic SQL** - Not captured (SQL built as strings)
2. **EXEC()** statements - Not captured
3. **Synonyms** - May show synonym, not underlying object
4. **Cross-database** - Captured but referenced_id is NULL

---

## Dependency Analysis Queries

### Complete Object Dependencies

```sql
-- All dependencies for an object with full metadata
SELECT 
    -- Referencing object (the one doing the referencing)
    s_ref.name AS referencing_schema,
    o_ref.name AS referencing_object,
    o_ref.type_desc AS referencing_type,
    
    -- Referenced object (the one being referenced)
    COALESCE(d.referenced_schema_name, 'dbo') AS referenced_schema,
    d.referenced_entity_name AS referenced_object,
    COALESCE(o_tgt.type_desc, 'UNRESOLVED') AS referenced_type,
    
    -- Additional context
    d.is_schema_bound_reference,
    d.is_caller_dependent,
    d.referenced_database_name  -- NULL if same database
FROM sys.sql_expression_dependencies d
JOIN sys.objects o_ref ON d.referencing_id = o_ref.object_id
JOIN sys.schemas s_ref ON o_ref.schema_id = s_ref.schema_id
LEFT JOIN sys.objects o_tgt ON d.referenced_id = o_tgt.object_id
WHERE o_ref.name = @ObjectName
ORDER BY referenced_schema, referenced_object;
```

### Reverse Dependencies (What Uses This?)

```sql
-- Find everything that references a specific table/view
-- Critical for impact analysis before changes
SELECT 
    s.name AS dependent_schema,
    o.name AS dependent_object,
    o.type_desc AS dependent_type,
    o.create_date,
    o.modify_date
FROM sys.sql_expression_dependencies d
JOIN sys.objects o ON d.referencing_id = o.object_id
JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE d.referenced_entity_name = @TableName
  AND (d.referenced_schema_name = @SchemaName OR d.referenced_schema_name IS NULL)
ORDER BY o.type_desc, s.name, o.name;
```

### Multi-Level Cascade Dependencies

```sql
-- Find all objects affected by a change (recursive)
WITH CascadeDependencies AS (
    -- Level 0: Direct dependents
    SELECT 
        d.referencing_id AS object_id,
        OBJECT_SCHEMA_NAME(d.referencing_id) AS schema_name,
        OBJECT_NAME(d.referencing_id) AS object_name,
        o.type_desc,
        0 AS level,
        CAST(OBJECT_NAME(d.referencing_id) AS NVARCHAR(MAX)) AS dependency_path
    FROM sys.sql_expression_dependencies d
    JOIN sys.objects o ON d.referencing_id = o.object_id
    WHERE d.referenced_entity_name = @RootObject
    
    UNION ALL
    
    -- Level N: Indirect dependents
    SELECT 
        d.referencing_id,
        OBJECT_SCHEMA_NAME(d.referencing_id),
        OBJECT_NAME(d.referencing_id),
        o.type_desc,
        cd.level + 1,
        cd.dependency_path + ' → ' + OBJECT_NAME(d.referencing_id)
    FROM sys.sql_expression_dependencies d
    JOIN sys.objects o ON d.referencing_id = o.object_id
    JOIN CascadeDependencies cd ON d.referenced_entity_name = cd.object_name
    WHERE cd.level < 5  -- Limit depth
      AND CHARINDEX(OBJECT_NAME(d.referencing_id), cd.dependency_path) = 0  -- Prevent cycles
)
SELECT DISTINCT 
    level,
    schema_name,
    object_name,
    type_desc,
    dependency_path
FROM CascadeDependencies
ORDER BY level, schema_name, object_name;
```

---

## Schema Analysis Queries

### Complete Schema Inventory

```sql
-- Full inventory of a schema
SELECT 
    s.name AS schema_name,
    o.name AS object_name,
    o.type_desc AS object_type,
    o.create_date,
    o.modify_date,
    
    -- Dependency counts
    (SELECT COUNT(*) FROM sys.sql_expression_dependencies d 
     WHERE d.referencing_id = o.object_id) AS depends_on_count,
    (SELECT COUNT(*) FROM sys.sql_expression_dependencies d 
     WHERE d.referenced_entity_name = o.name 
       AND d.referenced_schema_name = s.name) AS depended_by_count
       
FROM sys.objects o
JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE s.name = @SchemaName
  AND o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF', 'TR')
ORDER BY o.type_desc, o.name;
```

### Schema Dependency Matrix

```sql
-- Cross-schema dependencies (which schemas depend on which)
SELECT 
    OBJECT_SCHEMA_NAME(d.referencing_id) AS source_schema,
    COALESCE(d.referenced_schema_name, 'dbo') AS target_schema,
    COUNT(*) AS dependency_count,
    COUNT(DISTINCT d.referencing_id) AS distinct_objects
FROM sys.sql_expression_dependencies d
WHERE d.referenced_entity_name IS NOT NULL
GROUP BY OBJECT_SCHEMA_NAME(d.referencing_id), d.referenced_schema_name
ORDER BY dependency_count DESC;
```

---

## Foreign Key Relationships

### Complete FK Map

```sql
-- All foreign key relationships with column details
SELECT 
    -- Parent (referencing) table
    ps.name AS parent_schema,
    pt.name AS parent_table,
    pc.name AS parent_column,
    
    -- Referenced table
    rs.name AS referenced_schema,
    rt.name AS referenced_table,
    rc.name AS referenced_column,
    
    -- FK metadata
    fk.name AS fk_name,
    fk.is_disabled,
    fk.delete_referential_action_desc AS on_delete,
    fk.update_referential_action_desc AS on_update
    
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id 
    AND fkc.parent_column_id = pc.column_id
JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id 
    AND fkc.referenced_column_id = rc.column_id
ORDER BY parent_schema, parent_table, fk.name;
```

### Table Relationship Graph

```sql
-- Generate relationship graph (tables connected by FKs)
SELECT DISTINCT
    ps.name + '.' + pt.name AS from_table,
    rs.name + '.' + rt.name AS to_table,
    fk.name AS relationship_name
FROM sys.foreign_keys fk
JOIN sys.tables pt ON fk.parent_object_id = pt.object_id
JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
WHERE ps.name = @SchemaName OR rs.name = @SchemaName
ORDER BY from_table, to_table;
```

---

## Index Analysis

### Index Coverage Report

```sql
-- Index analysis for a table
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    i.name AS index_name,
    i.type_desc AS index_type,
    i.is_unique,
    i.is_primary_key,
    
    -- Columns in the index
    STUFF((
        SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
        FROM sys.index_columns ic
        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
          AND ic.is_included_column = 0
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS key_columns,
    
    -- Included columns
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
          AND ic.is_included_column = 1
        ORDER BY ic.index_column_id
        FOR XML PATH('')
    ), 1, 2, '') AS included_columns

FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE t.name = @TableName
  AND i.type > 0  -- Exclude heaps
ORDER BY i.index_id;
```

---

## Table Statistics & Size

### Table Size and Row Counts

```sql
-- Table sizes with row counts
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    p.rows AS row_count,
    SUM(a.total_pages) * 8 / 1024.0 AS total_mb,
    SUM(a.used_pages) * 8 / 1024.0 AS used_mb,
    (SUM(a.total_pages) - SUM(a.used_pages)) * 8 / 1024.0 AS unused_mb
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.indexes i ON t.object_id = i.object_id
JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
JOIN sys.allocation_units a ON p.partition_id = a.container_id
WHERE s.name = @SchemaName
GROUP BY s.name, t.name, p.rows
ORDER BY total_mb DESC;
```

---

## Stored Procedure Analysis

### Procedure Dependencies Summary

```sql
-- What tables/views does each proc use?
SELECT 
    OBJECT_SCHEMA_NAME(p.object_id) AS proc_schema,
    p.name AS proc_name,
    COUNT(DISTINCT d.referenced_entity_name) AS table_count,
    STRING_AGG(DISTINCT d.referenced_entity_name, ', ') 
        WITHIN GROUP (ORDER BY d.referenced_entity_name) AS tables_used
FROM sys.procedures p
LEFT JOIN sys.sql_expression_dependencies d ON p.object_id = d.referencing_id
WHERE d.referenced_class_desc = 'OBJECT_OR_COLUMN'
GROUP BY p.object_id, p.name
ORDER BY table_count DESC;
```

### Find Procedures That Modify a Table

```sql
-- Procedures that INSERT/UPDATE/DELETE a specific table
-- Note: This checks dependencies, not actual DML operations
SELECT 
    OBJECT_SCHEMA_NAME(d.referencing_id) AS proc_schema,
    OBJECT_NAME(d.referencing_id) AS proc_name,
    d.referenced_schema_name,
    d.referenced_entity_name
FROM sys.sql_expression_dependencies d
JOIN sys.procedures p ON d.referencing_id = p.object_id
WHERE d.referenced_entity_name = @TableName
ORDER BY proc_name;
```

---

## Orphan Detection

### Unused Stored Procedures

```sql
-- Procedures that nothing references
-- Candidates for cleanup (verify with execution stats)
SELECT 
    s.name AS schema_name,
    p.name AS proc_name,
    p.create_date,
    p.modify_date,
    -- Check if ever executed (requires query store or execution stats)
    COALESCE(qs.execution_count, 0) AS execution_count
FROM sys.procedures p
JOIN sys.schemas s ON p.schema_id = s.schema_id
LEFT JOIN sys.dm_exec_procedure_stats qs ON p.object_id = qs.object_id
WHERE NOT EXISTS (
    SELECT 1 FROM sys.sql_expression_dependencies d
    WHERE d.referenced_entity_name = p.name
)
ORDER BY p.create_date;
```

### Orphaned Tables (No Dependencies Either Way)

```sql
-- Tables with no FKs and not referenced by any object
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    t.create_date,
    p.rows AS row_count
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
WHERE NOT EXISTS (
    SELECT 1 FROM sys.sql_expression_dependencies d
    WHERE d.referenced_entity_name = t.name
)
AND NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    WHERE fk.parent_object_id = t.object_id 
       OR fk.referenced_object_id = t.object_id
)
ORDER BY t.create_date;
```

---

## Data Classification Queries

### PII Column Detection

```sql
-- Identify potential PII columns by name pattern
SELECT 
    s.name AS schema_name,
    t.name AS table_name,
    c.name AS column_name,
    ty.name AS data_type,
    c.max_length,
    CASE 
        WHEN c.name LIKE '%SSN%' OR c.name LIKE '%Social%' THEN 'SSN'
        WHEN c.name LIKE '%TaxID%' OR c.name LIKE '%EIN%' THEN 'Tax ID'
        WHEN c.name LIKE '%email%' THEN 'Email'
        WHEN c.name LIKE '%phone%' OR c.name LIKE '%mobile%' OR c.name LIKE '%fax%' THEN 'Phone'
        WHEN c.name LIKE '%addr%' OR c.name LIKE '%street%' OR c.name LIKE '%city%' THEN 'Address'
        WHEN c.name LIKE '%zip%' OR c.name LIKE '%postal%' THEN 'Postal Code'
        WHEN c.name LIKE '%DOB%' OR c.name LIKE '%birth%' THEN 'Date of Birth'
        WHEN c.name LIKE '%salary%' OR c.name LIKE '%wage%' THEN 'Compensation'
        WHEN c.name LIKE '%password%' OR c.name LIKE '%pwd%' THEN 'Credential'
        WHEN c.name LIKE '%account%num%' OR c.name LIKE '%acct%' THEN 'Account Number'
        WHEN c.name LIKE '%credit%card%' OR c.name LIKE '%cc_%' THEN 'Credit Card'
    END AS pii_classification
FROM sys.columns c
JOIN sys.tables t ON c.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.name LIKE '%SSN%' 
   OR c.name LIKE '%email%'
   OR c.name LIKE '%phone%'
   OR c.name LIKE '%addr%'
   OR c.name LIKE '%DOB%'
   OR c.name LIKE '%birth%'
   OR c.name LIKE '%salary%'
   OR c.name LIKE '%password%'
   OR c.name LIKE '%account%num%'
   OR c.name LIKE '%credit%card%'
   OR c.name LIKE '%Social%'
   OR c.name LIKE '%TaxID%'
ORDER BY pii_classification, schema_name, table_name;
```

---

## Output Formatting

### Generate HTML Documentation

```sql
-- Table documentation in HTML format
SELECT 
    '<h2>' + s.name + '.' + t.name + '</h2>' +
    '<p>Created: ' + CONVERT(VARCHAR, t.create_date, 120) + '</p>' +
    '<table border="1"><tr><th>Column</th><th>Type</th><th>Nullable</th></tr>' +
    STUFF((
        SELECT '<tr><td>' + c.name + '</td><td>' + ty.name + 
               CASE WHEN ty.name IN ('varchar', 'nvarchar', 'char') 
                    THEN '(' + CAST(c.max_length AS VARCHAR) + ')' ELSE '' END +
               '</td><td>' + CASE WHEN c.is_nullable = 1 THEN 'Yes' ELSE 'No' END + '</td></tr>'
        FROM sys.columns c
        JOIN sys.types ty ON c.user_type_id = ty.user_type_id
        WHERE c.object_id = t.object_id
        ORDER BY c.column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 0, '') +
    '</table>' AS html_doc
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = @SchemaName;
```
