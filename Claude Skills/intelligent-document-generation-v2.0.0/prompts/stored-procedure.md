# Stored Procedure Documentation Prompt

## System Prompt

```
You are an expert SQL Server database documentation specialist.
Your task is to generate comprehensive, accurate documentation for stored procedures.

## Output Format
Always respond with valid JSON matching the requested schema.
Never include markdown formatting, code blocks, or explanatory text outside the JSON.

## Documentation Standards
1. ACCURACY: Every statement must be verifiable from the provided metadata
2. COMPLETENESS: Cover purpose, parameters, tables, columns, business logic
3. CLARITY: Use precise technical language appropriate for DBAs and developers
4. CONSISTENCY: Follow the exact JSON schema provided

## Business Logic Analysis
When analyzing stored procedures:
- Identify the primary business operation (CRUD, ETL, reporting, etc.)
- Document conditional logic paths (IF/ELSE, CASE statements)
- Note error handling patterns (TRY/CATCH, RAISERROR)
- Identify transaction boundaries and isolation levels
- Document dynamic SQL usage and security implications

## Parameter Documentation
For each parameter, determine:
- Business purpose (not just technical type)
- Valid value ranges or constraints
- Default behavior when NULL
- Impact on query execution

## Table Access Analysis
For each table accessed:
- Operation type (SELECT, INSERT, UPDATE, DELETE, MERGE)
- Columns used and their purpose
- Join conditions and their business meaning
- Filter conditions (WHERE clauses)
```

## User Prompt Template

```
Generate documentation for the following stored procedure.

## Procedure Metadata
- **Name:** {schema_name}.{procedure_name}
- **Created:** {created_date}
- **Last Modified:** {modified_date}
- **Master Index ID:** {master_index_id}

## Parameters
{parameters_list}

## Tables Accessed
{tables_accessed_list}

## Dependencies
{dependencies_list}

## SQL Definition
```sql
{sql_definition}
```

## Required Output Schema
{
  "summary": "2-3 sentence executive summary of what this procedure does",
  "purpose": "Detailed explanation of the business purpose",
  "businessContext": "How this fits into larger business processes",
  "parameters": [
    {
      "name": "@ParameterName",
      "type": "INT",
      "direction": "INPUT|OUTPUT|INOUT",
      "description": "Business purpose of this parameter",
      "validValues": "Constraints or valid ranges",
      "defaultBehavior": "What happens when NULL or default"
    }
  ],
  "tablesAccessed": [
    {
      "table": "schema.TableName",
      "operation": "SELECT|INSERT|UPDATE|DELETE|MERGE",
      "purpose": "Why this table is accessed",
      "columnsUsed": ["col1", "col2"],
      "joinConditions": "Description of join logic if applicable",
      "filterConditions": "Description of WHERE conditions"
    }
  ],
  "businessLogic": {
    "mainFlow": "Step-by-step description of main execution path",
    "conditionalPaths": ["Description of IF/ELSE branches"],
    "errorHandling": "How errors are handled",
    "transactions": "Transaction scope and isolation level"
  },
  "securityConsiderations": ["Any security notes, dynamic SQL warnings, etc."],
  "performanceNotes": ["Index usage, potential bottlenecks, optimization opportunities"],
  "relatedObjects": ["Other procedures, views, or functions this relates to"],
  "exampleUsage": "EXEC schema.ProcedureName @Param1 = value1, @Param2 = value2;",
  "changeHistory": "Notable changes based on modification patterns",
  "tier": 1
}

Respond ONLY with the JSON object. No markdown, no explanation.
```

## Variable Substitution

| Variable | Source | Example |
|----------|--------|---------|
| `{schema_name}` | sys.schemas | `dbo` |
| `{procedure_name}` | sys.procedures | `SP_GetCustomerOrders` |
| `{created_date}` | sys.procedures.create_date | `2024-01-15` |
| `{modified_date}` | sys.procedures.modify_date | `2025-12-01` |
| `{master_index_id}` | daqa.MasterIndex | `12847` |
| `{parameters_list}` | sys.parameters | See below |
| `{tables_accessed_list}` | sys.sql_expression_dependencies | See below |
| `{dependencies_list}` | sys.sql_expression_dependencies | See below |
| `{sql_definition}` | OBJECT_DEFINITION() | Full SQL |

## Parameters List Format

```
- @CustomerID (INT, INPUT)
- @StartDate (DATE, INPUT) DEFAULT GETDATE()
- @OrderCount (INT, OUTPUT)
```

## Tables Accessed List Format

```
- dbo.Customers: SELECT [CustomerID, CustomerName, Email]
- dbo.Orders: SELECT [OrderID, OrderDate, TotalAmount]
- dbo.OrderItems: SELECT [ItemID, ProductID, Quantity]
```

## Tier-Specific Variations

### Tier 1 (Complex)
- Full schema with all 15+ fields
- Max 8,000 prompt tokens
- Max 4,000 completion tokens
- Model: GPT-4o

### Tier 2 (Standard)
- Reduced schema (8-10 fields)
- Max 4,000 prompt tokens
- Max 2,000 completion tokens
- Model: GPT-4o-mini

### Tier 3 (Simple)
- Minimal schema (4-5 fields)
- Max 2,000 prompt tokens
- Max 1,000 completion tokens
- Model: GPT-4o-mini

## Token Optimization Tips

1. **Remove SQL comments** before including in prompt
2. **Compress whitespace** to reduce character count
3. **Truncate long procedures** while preserving key sections
4. **Use abbreviations** in prompt (not output): `PROC` vs `PROCEDURE`
5. **Skip redundant metadata** if derivable from SQL
