# SQL Script Fixes - Testing Guide

## ✅ Issues Fixed

### Problem 1: SQL Syntax Errors
**Original errors**:
```
Msg 229: REFERENCES permission denied
Msg 1088: Cannot find object
Msg 102: Incorrect syntax near '('
```

**Root causes**:
1. Computed column syntax not compatible with your SQL Server version
2. Tables created in wrong order (trying to reference BatchJobs before it was created)
3. Views trying to query tables that don't exist yet

**Fixes applied**:
- Removed `ALTER TABLE ADD` computed column syntax
- Changed `ProgressPercentage` to regular `FLOAT` column (calculated in application code)
- Reordered DROP statements: views → procedures → tables (correct dependency order)
- Moved all constraints to end of `CREATE TABLE` statements
- Added proper `PRINT` statements for progress tracking

### Problem 2: Hangfire Schema Restriction
**Issue**: You can only write to `DaQa` schema, but Hangfire was configured for `Hangfire` schema

**Fix**: Updated `HangfireConfiguration.cs` line 46:
```csharp
// OLD:
SchemaName = "Hangfire",

// NEW:
SchemaName = "DaQa",  // Only schema with write permissions
```

---

## 🧪 Testing the Fixed Script

### Step 1: Pull Latest Changes

```powershell
cd C:\Git\autodocprojclone
git pull origin claude/add-sync-service-logging-01P3rHxqMWFpWvVAk7WXSUUw
```

### Step 2: Run PowerShell Copy Script

```powershell
.\Copy-BatchProcessingFiles-Windows.ps1
```

This will copy the **fixed** SQL script to your V2 project.

### Step 3: Run SQL Script in SSMS

1. Open **SQL Server Management Studio (SSMS)**
2. Connect to: `(localdb)\mssqllocaldb`
3. Open file: `C:\Projects\EnterpriseDocumentationPlatform.V2\sql\CREATE_BatchProcessing_Tables.sql`
4. Select database: **IRFS1**
5. Click **Execute** (F5)

### Expected Output:

```
Starting Batch Processing Tables creation...
Existing objects dropped successfully
BatchJobs table created successfully
BatchJobItems table created successfully
Indexes created successfully
vw_BatchJobSummary created successfully
vw_ItemsRequiringReview created successfully
vw_BatchProcessingMetrics created successfully
vw_ConfidenceDistribution created successfully
vw_VectorIndexingStatus created successfully
usp_GetBatchStatus created successfully
usp_GetItemsRequiringReview created successfully
usp_ApproveItems created successfully
usp_RejectItems created successfully
usp_CancelBatch created successfully

============================================================================
Batch processing tables created successfully
============================================================================

Tables:
  - DaQa.BatchJobs
  - DaQa.BatchJobItems

Views:
  - DaQa.vw_BatchJobSummary
  - DaQa.vw_ItemsRequiringReview
  - DaQa.vw_BatchProcessingMetrics
  - DaQa.vw_ConfidenceDistribution
  - DaQa.vw_VectorIndexingStatus

Stored Procedures:
  - DaQa.usp_GetBatchStatus
  - DaQa.usp_GetItemsRequiringReview
  - DaQa.usp_ApproveItems
  - DaQa.usp_RejectItems
  - DaQa.usp_CancelBatch

Note: Hangfire will also create its tables in the DaQa schema
      Configure in HangfireConfiguration.cs with SchemaName = "DaQa"

============================================================================
```

### Step 4: Verify Tables Created

Run this query in SSMS:

```sql
-- Check tables exist
SELECT
    name AS TableName,
    create_date AS CreatedDate
FROM sys.tables
WHERE schema_id = SCHEMA_ID('DaQa')
  AND name LIKE 'Batch%'
ORDER BY name;

-- Check views exist
SELECT
    name AS ViewName,
    create_date AS CreatedDate
FROM sys.views
WHERE schema_id = SCHEMA_ID('DaQa')
  AND name LIKE 'vw_Batch%'
ORDER BY name;

-- Check stored procedures exist
SELECT
    name AS ProcedureName,
    create_date AS CreatedDate
FROM sys.procedures
WHERE schema_id = SCHEMA_ID('DaQa')
  AND name LIKE 'usp_%'
ORDER BY name;
```

**Expected results**:
- **2 tables**: BatchJobs, BatchJobItems
- **5 views**: vw_BatchJobSummary, vw_BatchProcessingMetrics, vw_ConfidenceDistribution, vw_ItemsRequiringReview, vw_VectorIndexingStatus
- **5 procedures**: usp_ApproveItems, usp_CancelBatch, usp_GetBatchStatus, usp_GetItemsRequiringReview, usp_RejectItems

---

## 🎯 What Changed

### SQL Script Changes (600 lines):

**Line 16**: Added progress message
```sql
PRINT 'Starting Batch Processing Tables creation...'
```

**Lines 23-72**: Proper DROP order (views → procedures → tables)
```sql
-- Drop views first
IF OBJECT_ID('DaQa.vw_VectorIndexingStatus', 'V') IS NOT NULL
    DROP VIEW DaQa.vw_VectorIndexingStatus
-- ... more views

-- Drop stored procedures
IF OBJECT_ID('DaQa.usp_CancelBatch', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_CancelBatch
-- ... more procedures

-- Drop tables (child first, then parent)
IF OBJECT_ID('DaQa.BatchJobItems', 'U') IS NOT NULL
    DROP TABLE DaQa.BatchJobItems

IF OBJECT_ID('DaQa.BatchJobs', 'U') IS NOT NULL
    DROP TABLE DaQa.BatchJobs
```

**Line 99**: Changed computed column to regular column
```sql
-- OLD (problematic):
-- Computed column added later with ALTER TABLE

-- NEW (fixed):
ProgressPercentage FLOAT NULL, -- Calculated: ProcessedCount / TotalItems * 100
```

**Lines 131, 192**: Moved constraints to end of table definition
```sql
-- Primary Key Constraint
CONSTRAINT PK_BatchJobs PRIMARY KEY CLUSTERED (BatchId)
```

**Lines 195-196**: Proper foreign key syntax
```sql
CONSTRAINT FK_BatchJobItems_BatchJobs FOREIGN KEY (BatchId)
    REFERENCES DaQa.BatchJobs(BatchId) ON DELETE CASCADE
```

**Lines 573-599**: Added summary output
```sql
PRINT '============================================================================'
PRINT 'Batch processing tables created successfully'
-- ... lists all objects created
```

### Hangfire Configuration Change:

**File**: `src/Api/Configuration/HangfireConfiguration.cs`
**Line 46**:
```csharp
// OLD:
SchemaName = "Hangfire",

// NEW:
SchemaName = "DaQa",  // MUST use DaQa schema (only schema with write permissions)
```

---

## 🔍 Verification Queries

After running the script successfully, test with these queries:

### 1. Check Schema Objects
```sql
SELECT
    OBJECT_SCHEMA_NAME(object_id) AS SchemaName,
    name AS ObjectName,
    type_desc AS ObjectType
FROM sys.objects
WHERE OBJECT_SCHEMA_NAME(object_id) = 'DaQa'
  AND name LIKE '%Batch%'
ORDER BY type_desc, name;
```

### 2. Test a View
```sql
-- Should return empty result set (no batches yet)
SELECT * FROM DaQa.vw_BatchJobSummary;
```

### 3. Test a Stored Procedure
```sql
-- Should return empty result set (no items requiring review yet)
EXEC DaQa.usp_GetItemsRequiringReview @TopN = 10;
```

### 4. Insert Test Data
```sql
-- Create a test batch
DECLARE @BatchId UNIQUEIDENTIFIER = NEWID();

INSERT INTO DaQa.BatchJobs (
    BatchId, SourceType, DatabaseName, SchemaName,
    Status, TotalItems, CreatedBy
)
VALUES (
    @BatchId, 'DatabaseSchema', 'IRFS1', 'gwpc',
    'Pending', 0, 'TestUser'
);

-- Verify insert
SELECT * FROM DaQa.BatchJobs WHERE BatchId = @BatchId;

-- Clean up test data
DELETE FROM DaQa.BatchJobs WHERE BatchId = @BatchId;
```

---

## 🚨 Troubleshooting

### Error: "STRING_SPLIT not found"
**SQL Server Version**: You may be on SQL Server 2014 or earlier

**Fix**: Update stored procedures to use XML parsing instead:
```sql
-- Replace STRING_SPLIT(@ItemIds, ',') with:
SELECT CAST(Item.value('.', 'UNIQUEIDENTIFIER') AS UNIQUEIDENTIFIER)
FROM (SELECT CAST('<i>' + REPLACE(@ItemIds, ',', '</i><i>') + '</i>' AS XML) AS Items) AS A
CROSS APPLY Items.nodes('i') AS Item
```

### Error: "Cannot create index with WHERE clause"
**SQL Server Version**: You may be on SQL Server 2005 or 2008

**Fix**: Remove `WHERE` clauses from filtered indexes (lines 214, 222, 230, 236)

### Tables Already Exist
**Solution**: The script already handles this with `DROP IF EXISTS`

If you still get errors, manually drop in SSMS:
```sql
DROP VIEW DaQa.vw_VectorIndexingStatus;
DROP VIEW DaQa.vw_ConfidenceDistribution;
DROP VIEW DaQa.vw_BatchProcessingMetrics;
DROP VIEW DaQa.vw_ItemsRequiringReview;
DROP VIEW DaQa.vw_BatchJobSummary;
DROP PROCEDURE DaQa.usp_CancelBatch;
DROP PROCEDURE DaQa.usp_RejectItems;
DROP PROCEDURE DaQa.usp_ApproveItems;
DROP PROCEDURE DaQa.usp_GetItemsRequiringReview;
DROP PROCEDURE DaQa.usp_GetBatchStatus;
DROP TABLE DaQa.BatchJobItems;
DROP TABLE DaQa.BatchJobs;
```

Then re-run the script.

---

## ✅ Success Checklist

- [ ] Pulled latest changes from git
- [ ] Ran PowerShell copy script
- [ ] Executed SQL script in SSMS without errors
- [ ] Verified 2 tables created
- [ ] Verified 5 views created
- [ ] Verified 5 stored procedures created
- [ ] Tested a view (returns empty result)
- [ ] Tested a stored procedure (returns empty result)
- [ ] Ready to proceed with Program.cs configuration

---

## 📚 Next Steps

Once SQL script runs successfully:

1. **Update Program.cs** - Follow `docs\BATCH-PROCESSING-SETUP.md`
2. **Install NuGet packages** - Hangfire.AspNetCore, Hangfire.SqlServer, etc.
3. **Configure appsettings.json** - OpenAI, Pinecone, Hangfire settings
4. **Build and run** - Test API endpoints

---

**Git Commit**: `34ee383`
**Files Changed**:
- `sql/CREATE_BatchProcessing_Tables.sql` (complete rewrite)
- `src/Api/Configuration/HangfireConfiguration.cs` (schema change)

**Status**: ✅ Ready to test
