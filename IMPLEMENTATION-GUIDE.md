# Auto-Draft Generation Implementation Guide

This guide covers the complete implementation of the auto-draft generation system for the BI Analytics Change Spreadsheet.

## Overview

The system automatically creates draft documentation when Excel entries reach "Completed" status:

1. Excel sync detects completed entries without DocId
2. Generates unique DocId with sequential numbering
3. Enhances documentation using OpenAI
4. Generates Word document from template
5. Updates Excel and database with DocId
6. Notifies approvers (Teams integration pending)

## Components Implemented

### 1. SQL Database

**File**: `sql/CREATE_DocumentCounters_Table.sql`

Run this script to create the DocumentCounters table and stored procedures:

```sql
-- Creates DaQa.DocumentCounters table
-- Creates DaQa.usp_GetNextDocIdNumber stored procedure
-- Creates DaQa.usp_ResetDocIdCounter stored procedure
-- Creates DaQa.vw_DocumentCounterStatus view
```

**Run in SSMS**:
```sql
USE IRFS1;
GO
-- Execute the entire CREATE_DocumentCounters_Table.sql script
```

### 2. Services Created

- **DocIdGeneratorService**: Generates sequential DocIds
- **OpenAIEnhancementService**: Enhances documentation with AI
- **TemplateExecutorService**: Executes Node.js templates
- **AutoDraftService**: Orchestrates the entire workflow
- **ExcelToSqlSyncService**: Updated with auto-draft integration

### 3. Templates

- **TEMPLATE_BusinessRequest.js**: Business request documentation
- **TEMPLATE_Enhancement.js**: Enhancement documentation (NEW)
- **TEMPLATE_DefectFix.js**: Defect fix documentation

## Installation Steps

### Step 1: Update appsettings.json

Add to `src/Api/appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here",
    "Model": "gpt-4"
  },
  "DocumentGeneration": {
    "BaseOutputPath": "C:\\Temp\\Documentation-Catalog",
    "TemplatesPath": "C:\\Path\\To\\Templates",
    "NodeExecutable": "node"
  },
  "ExcelSync": {
    "LocalFilePath": "C:\\Users\\Alexander.Kirby\\Desktop\\Change Spreadsheet\\BI Analytics Change Spreadsheet.xlsx",
    "SyncIntervalSeconds": 60
  }
}
```

### Step 2: Register Services in DI Container

Add to `src/Api/Program.cs`:

```csharp
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

// ... existing code ...

// Register Document Generation Services
builder.Services.AddScoped<IDocIdGeneratorService, DocIdGeneratorService>();
builder.Services.AddScoped<IOpenAIEnhancementService, OpenAIEnhancementService>();
builder.Services.AddScoped<ITemplateExecutorService, TemplateExecutorService>();
builder.Services.AddScoped<IAutoDraftService, AutoDraftService>();

// Add HttpClient for OpenAI
builder.Services.AddHttpClient<IOpenAIEnhancementService, OpenAIEnhancementService>();

// Excel-to-SQL Sync Background Service (if configured)
if (!string.IsNullOrEmpty(builder.Configuration["ExcelSync:LocalFilePath"]))
{
    builder.Services.AddHostedService<ExcelToSqlSyncService>();
}
```

### Step 3: Install Node.js Dependencies

Navigate to the Templates directory and install dependencies:

```bash
cd Templates
npm install docx
```

### Step 4: Run SQL Script

Execute `CREATE_DocumentCounters_Table.sql` in SSMS against the IRFS1 database.

### Step 5: Update Excel Spreadsheet

Ensure the Excel file has the following columns (row 3 = headers):
- Date
- JIRA #
- CAB #
- Sprint #
- Status
- Priority
- Severity
- Table
- Column
- Change Type
- Description
- Reported By
- Assigned to
- Documentation
- Documentation Link
- DocId
- **ModifiedStoredProcedures** (NEW - add this column)

### Step 6: Configure OpenAI API Key

1. Get an OpenAI API key from https://platform.openai.com/api-keys
2. Add to appsettings.json or user secrets:
   ```bash
   dotnet user-secrets set "OpenAI:ApiKey" "your-key-here"
   ```

### Step 7: Test the Workflow

1. Start the API:
   ```bash
   cd src/Api
   dotnet run
   ```

2. Modify an Excel entry:
   - Set Status = "Completed"
   - Ensure JIRA #, CAB #, Table, Change Type are filled
   - Save the Excel file

3. Wait for sync (60 seconds or trigger file save)

4. Check logs for:
   ```
   [INF] Found 1 completed entries without DocId, creating drafts
   [INF] Generated DocId: EN-0001-irf_policy-PolicyNumber-BAS-123
   [INF] Enhanced documentation with OpenAI
   [INF] Document generated successfully at: C:\Temp\...
   [INF] Updated Excel row with DocId: EN-0001-irf_policy-PolicyNumber-BAS-123
   ```

5. Verify:
   - Word document created in `C:\Temp\Documentation-Catalog\IRFS1\...`
   - Excel updated with DocId
   - Database updated (check `daqa.DocumentChanges`)

## DocId Naming Convention

**Format**: `{Type}-{Seq#}-{ObjectName}[-{Column}]-{Jira}`

**Examples**:
- `EN-0001-irf_policy-PolicyNumber-BAS-123` (Enhancement to column)
- `BR-0002-usp_LoadPolicy-BAS-456` (Business request for SP)
- `DF-0003-irf_claim-BAS-789` (Defect fix for table)

**Change Type Mapping**:
- Business Request → BR
- Enhancement → EN
- Defect Fix → DF

## File Path Structure

```
C:\Temp\Documentation-Catalog\
└── IRFS1\
    └── {Schema}\
        └── {ObjectType}\
            └── {ObjectName}\
                └── Change Documentation\
                    └── {DocId}.docx
```

**Example**:
```
C:\Temp\Documentation-Catalog\IRFS1\gwpc\Tables\irf_policy\Change Documentation\EN-0001-irf_policy-PolicyNumber-BAS-123.docx
```

## Troubleshooting

### "Node.js template execution failed"

**Solution**: Ensure Node.js is installed and `docx` package is available:
```bash
node --version  # Should show v14+
cd Templates
npm install docx
```

### "OpenAI API error"

**Solution**: Check API key is valid and you have credits:
```bash
dotnet user-secrets list  # Verify key is set
```

### "DocId not updating in Excel"

**Solution**: Ensure Excel file is not open in Excel (file lock). The sync service needs exclusive access to write.

### "Template not found"

**Solution**: Update `DocumentGeneration:TemplatesPath` in appsettings.json to point to the Templates directory.

## Next Steps (Pending Implementation)

1. **Teams Notification Service** - Notify approvers when drafts are ready
2. **Approval Workflow Integration** - Link to existing approval UI
3. **MasterIndex Population** - Populate metadata after approval
4. **SharePoint Upload** - Replace local path with SharePoint after approval

## Monitoring

### Counter Status Dashboard

```sql
SELECT * FROM DaQa.vw_DocumentCounterStatus;
```

Shows:
- Current counter for each document type
- Remaining capacity (9999 max)
- Percent used
- Last update info

### Recent Drafts Created

```sql
SELECT TOP 10 *
FROM daqa.DocumentChanges
WHERE DocId IS NOT NULL
  AND Status = 'Completed'
ORDER BY LastSyncedFromExcel DESC;
```

## Security Notes

1. **OpenAI API Key**: Store in user secrets or Azure Key Vault, not in source control
2. **File Paths**: Validate paths to prevent directory traversal
3. **SQL Injection**: All queries use parameterized Dapper/EF Core
4. **File Access**: Excel sync has exclusive write access when updating

## Support

For issues or questions:
1. Check logs in the API console output
2. Review this implementation guide
3. Check the counter status dashboard
4. Verify configuration in appsettings.json
