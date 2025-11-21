# Complete Auto-Draft System Implementation Guide

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Database Setup](#database-setup)
4. [Service Registration](#service-registration)
5. [Configuration](#configuration)
6. [End-to-End Workflow](#end-to-end-workflow)
7. [Testing](#testing)
8. [Monitoring & Troubleshooting](#monitoring--troubleshooting)

---

## Overview

The Auto-Draft Generation System automatically creates documentation drafts when Excel entries reach "Completed" status, enhances them with AI, manages approval workflows, and publishes to SharePoint with full metadata indexing.

### Complete Workflow

```
Excel Entry Status → "Completed"
    ↓
Excel Sync Service detects completion
    ↓
Auto-Draft Service orchestrates:
  1. Generate DocId (EN-0001-irf_policy-PolicyNumber-BAS-123)
  2. Enhance with OpenAI
  3. Generate Word document from template
  4. Save to local path (future: SharePoint)
  5. Update Excel with DocId
    ↓
Teams Notification (batched 24-hour)
    ↓
Approver reviews draft
    ↓
Approval Action (Approved/Edited/Rejected/Rerequested)
    ↓
Approval Tracking (for AI training)
    ↓
If Approved:
  - Populate MasterIndex with metadata
  - Upload to SharePoint (future)
  - Update Excel with SharePoint link
```

---

## Architecture

### Services Overview

| Service | Purpose | Key Features |
|---------|---------|--------------|
| **ExcelToSqlSyncService** | Monitors Excel for changes | File watcher, deduplication, auto-draft trigger |
| **AutoDraftService** | Orchestrates draft creation | Calls DocId, OpenAI, Template services |
| **DocIdGeneratorService** | Generates unique DocIds | Sequential counters, atomic increment |
| **OpenAIEnhancementService** | AI-enhances documentation | GPT-4, strict interpretation |
| **TemplateExecutorService** | Generates Word documents | Node.js template execution |
| **TeamsNotificationService** | Sends Teams notifications | 24-hour batching, Adaptive Cards |
| **NotificationBatchingService** | Background notification sender | Hourly checks for pending notifications |
| **ApprovalTrackingService** | Tracks approval actions | AI training data, diff tracking |
| **MasterIndexService** | Populates metadata index | 115+ columns, quality scores |
| **ExcelUpdateService** | Updates Excel with results | DocId, SharePoint link write-back |

---

## Database Setup

### Step 1: Create DocumentCounters Table

**File**: `sql/CREATE_DocumentCounters_Table.sql`

```sql
-- Run in SSMS against IRFS1 database
USE IRFS1;
GO

-- Execute the entire CREATE_DocumentCounters_Table.sql script
-- This creates:
--   - DaQa.DocumentCounters table
--   - DaQa.usp_GetNextDocIdNumber stored procedure
--   - DaQa.usp_ResetDocIdCounter stored procedure
--   - DaQa.vw_DocumentCounterStatus view
```

**Verify**:
```sql
SELECT * FROM DaQa.DocumentCounters;
SELECT * FROM DaQa.vw_DocumentCounterStatus;
```

### Step 2: Create MasterIndex Table

**File**: `sql/CREATE_MasterIndex_Table.sql`

```sql
-- Run in SSMS against IRFS1 database
USE IRFS1;
GO

-- Execute the entire CREATE_MasterIndex_Table.sql script
-- This creates:
--   - DaQa.MasterIndex table (115+ columns)
--   - 10 indexes for efficient querying
--   - Full-text search catalog
--   - 4 views for common queries
```

**Verify**:
```sql
SELECT * FROM DaQa.MasterIndex;
SELECT * FROM DaQa.vw_DocumentStatistics;
```

### Step 3: Create ApprovalTracking Table

**File**: `sql/CREATE_ApprovalTracking_Table.sql`

```sql
-- Run in SSMS against IRFS1 database
USE IRFS1;
GO

-- Execute the entire CREATE_ApprovalTracking_Table.sql script
-- This creates:
--   - DaQa.ApprovalTracking table
--   - Indexes for querying
--   - DaQa.vw_ApprovalInsights view
--   - DaQa.vw_CommonEdits view
```

**Verify**:
```sql
SELECT * FROM DaQa.vw_ApprovalInsights;
```

---

## Service Registration

Add to `src/Api/Program.cs`:

```csharp
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Services.ExcelSync;
using Enterprise.Documentation.Core.Application.Services.Notifications;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;
using Enterprise.Documentation.Core.Application.Services.ApprovalTracking;

// ... existing code ...

var builder = WebApplication.CreateBuilder(args);

// =============================================
// DOCUMENT GENERATION SERVICES
// =============================================

// Core generation services
builder.Services.AddScoped<IDocIdGeneratorService, DocIdGeneratorService>();
builder.Services.AddScoped<ITemplateExecutorService, TemplateExecutorService>();
builder.Services.AddScoped<IAutoDraftService, AutoDraftService>();

// OpenAI enhancement (requires HttpClient)
builder.Services.AddHttpClient<IOpenAIEnhancementService, OpenAIEnhancementService>();

// =============================================
// EXCEL SYNC SERVICES
// =============================================

// Excel update service
builder.Services.AddScoped<IExcelUpdateService, ExcelUpdateService>();

// Excel-to-SQL Sync Background Service (if configured)
if (!string.IsNullOrEmpty(builder.Configuration["ExcelSync:LocalFilePath"]))
{
    builder.Services.AddHostedService<ExcelToSqlSyncService>();
}

// =============================================
// NOTIFICATION SERVICES
// =============================================

// Teams notifications (requires HttpClient)
builder.Services.AddHttpClient<ITeamsNotificationService, TeamsNotificationService>();
builder.Services.AddSingleton<ITeamsNotificationService, TeamsNotificationService>();

// Notification batching background service
if (!string.IsNullOrEmpty(builder.Configuration["Teams:DraftsWebhookUrl"]))
{
    builder.Services.AddHostedService<NotificationBatchingService>();
}

// =============================================
// METADATA AND TRACKING SERVICES
// =============================================

// MasterIndex population
builder.Services.AddScoped<IMasterIndexService, MasterIndexService>();

// Approval tracking for AI training
builder.Services.AddScoped<IApprovalTrackingService, ApprovalTrackingService>();

// ... rest of existing code ...
```

---

## Configuration

### Complete appsettings.json

Add to `src/Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=IRFS1;Trusted_Connection=true;MultipleActiveResultSets=true"
  },

  "OpenAI": {
    "ApiKey": "sk-your-api-key-here",
    "Model": "gpt-4",
    "Temperature": 0.3,
    "MaxTokens": 1500
  },

  "DocumentGeneration": {
    "BaseOutputPath": "C:\\Temp\\Documentation-Catalog",
    "TemplatesPath": "C:\\Projects\\EnterpriseDocumentationPlatform.V2\\Templates",
    "NodeExecutable": "node"
  },

  "ExcelSync": {
    "LocalFilePath": "C:\\Users\\Alexander.Kirby\\Desktop\\Change Spreadsheet\\BI Analytics Change Spreadsheet.xlsx",
    "SyncIntervalSeconds": 60
  },

  "Teams": {
    "DraftsWebhookUrl": "https://outlook.office.com/webhook/your-webhook-url-for-drafts",
    "DefectsWebhookUrl": "https://outlook.office.com/webhook/your-webhook-url-for-defects",
    "ApprovalBaseUrl": "http://localhost:5195/approvals",
    "BatchCheckIntervalMinutes": 60
  },

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Enterprise.Documentation.Core.Application.Services": "Information"
    }
  }
}
```

### Environment-Specific Configuration

**Development** (`appsettings.Development.json`):
```json
{
  "OpenAI": {
    "ApiKey": "sk-dev-key-here"
  },
  "Teams": {
    "DraftsWebhookUrl": "https://your-dev-webhook-url",
    "ApprovalBaseUrl": "http://localhost:5195/approvals"
  }
}
```

**Production** (`appsettings.Production.json`):
```json
{
  "OpenAI": {
    "ApiKey": "sk-prod-key-here"
  },
  "Teams": {
    "DraftsWebhookUrl": "https://your-prod-webhook-url",
    "ApprovalBaseUrl": "https://production-domain.com/approvals"
  },
  "DocumentGeneration": {
    "BaseOutputPath": "\\\\SharePointPath\\Documentation-Catalog"
  }
}
```

---

## End-to-End Workflow

### 1. Excel Entry Completion

**User Action**:
1. User updates Excel entry Status to "Completed"
2. Excel file saved (triggers file watcher)

**System Response**:
```
ExcelToSqlSyncService:
  - File watcher detects change
  - Reads Excel from row 3 (headers), row 4+ (data)
  - Finds entry with Status="Completed" AND DocId IS NULL
  - Triggers AutoDraftService
```

### 2. Auto-Draft Generation

```
AutoDraftService.CreateDraftForCompletedEntryAsync():

  Step 1: Generate DocId
    DocIdGeneratorService.GenerateDocIdAsync()
      - Maps ChangeType → DocumentType (BR/EN/DF)
      - Parses Table → Schema + ObjectName
      - Calls DaQa.usp_GetNextDocIdNumber
      - Formats: EN-0001-irf_policy-PolicyNumber-BAS-123

  Step 2: Enhance Documentation
    OpenAIEnhancementService.EnhanceDocumentationAsync()
      - Sends Description + Documentation to GPT-4
      - Returns enhanced text with key points

  Step 3: Execute Template
    TemplateExecutorService.GenerateDocumentAsync()
      - Creates JSON data file
      - Executes: node TEMPLATE_Enhancement.js data.json output.docx
      - Returns path to generated .docx

  Step 4: Save Document
    - Determines path: {BaseOutputPath}\IRFS1\{Schema}\{ObjectType}\{ObjectName}\Change Documentation\{DocId}.docx
    - Copies generated document to path

  Step 5: Update Excel
    ExcelUpdateService.UpdateDocIdAsync()
      - Updates Excel row with DocId
      - Updates DaQa.DocumentChanges table
```

### 3. Teams Notification

```
TeamsNotificationService.SendDraftReadyNotificationAsync():
  - First notification: Immediate
  - Subsequent notifications: Batched for 24 hours
  - Sends Adaptive Card with document details
```

**Notification Format**:
```
📋 New Document Ready for Approval

EN-0001-irf_policy-PolicyNumber-BAS-123
Type: Enhancement | Object: gwpc.irf_policy - PolicyNumber
Jira: BAS-123
Description: Enhanced PolicyNumber validation

[Review EN-0001-irf_policy-PolicyNumber-BAS-123]
```

### 4. Approval Workflow

**Approver Actions**:
1. Reviews draft document
2. Takes action: Approve | Edit | Reject | Rerequest

**System Response**:
```
ApprovalTrackingService.TrackApprovalAsync():
  - Records action in DaQa.ApprovalTracking
  - Stores feedback for AI training
  - Captures diffs if edited

If Approved:
  MasterIndexService.PopulateIndexAsync():
    - Extracts all metadata
    - Calculates quality scores
    - Populates DaQa.MasterIndex (115+ columns)
```

### 5. SharePoint Upload (Future)

```
SharePointUploadService (to be implemented):
  - Upload document to SharePoint
  - Preserve folder structure
  - Get SharePoint URL

MasterIndexService.UpdateDocumentationLinkAsync():
  - Update MasterIndex with SharePoint URL

ExcelUpdateService.UpdateDocumentationLinkAsync():
  - Update Excel with hyperlink to SharePoint
```

---

## Testing

### Test 1: DocId Generation

```powershell
# Insert test entry
Invoke-Sqlcmd -Query @"
INSERT INTO DaQa.DocumentChanges (CABNumber, JiraNumber, Status, [Table], [Column], ChangeType, Description, Documentation, DateEntered)
VALUES ('CAB-TEST-001', 'BAS-999', 'Completed', 'gwpc.irf_policy', 'PolicyNumber', 'Enhancement', 'Test enhancement', 'Test documentation', GETDATE())
"@

# Check logs
# Expected: "Auto-draft created successfully for CAB: CAB-TEST-001, DocId: EN-0001-irf_policy-PolicyNumber-BAS-999"
```

### Test 2: OpenAI Enhancement

Check logs for:
```
Enhanced description: [AI-enhanced text]
Key points: [bullet points]
Technical details: [details]
```

### Test 3: Document Generation

```powershell
# Check file exists
Test-Path "C:\Temp\Documentation-Catalog\IRFS1\gwpc\Tables\irf_policy\Change Documentation\EN-0001-irf_policy-PolicyNumber-BAS-999.docx"

# Expected: True
```

### Test 4: Excel Update

```powershell
# Check Excel
# Expected: DocId column populated with "EN-0001-irf_policy-PolicyNumber-BAS-999"
```

### Test 5: Teams Notification

Check Teams channel for notification card with document details.

---

## Monitoring & Troubleshooting

### Key Metrics

```sql
-- Document counter status
SELECT * FROM DaQa.vw_DocumentCounterStatus;

-- Recent auto-generated documents
SELECT TOP 10 *
FROM DaQa.MasterIndex
WHERE CreatedBy = 'AutoDraftService'
ORDER BY CreatedDate DESC;

-- Approval tracking insights
SELECT * FROM DaQa.vw_ApprovalInsights;

-- Common edits (for AI improvement)
SELECT * FROM DaQa.vw_CommonEdits;
```

### Logs to Monitor

```csharp
// Excel sync
"Excel-to-SQL Sync Service starting"
"Reading worksheet: {WorksheetName}, Dimensions: {Dimensions}"
"Processing {TotalRows} data rows"

// Auto-draft
"Creating auto-draft for CAB: {CABNumber}"
"Generated DocId: {DocId}"
"AI enhancement completed"
"Template executed successfully"
"Auto-draft created successfully"

// Notifications
"Teams notification sent for {Count} draft(s)"
"Batch notification scheduled"

// Approval
"Approval action tracked: {Action} for DocId: {DocId}"
"MasterIndex populated with IndexID: {IndexId}"
```

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| DocId not generating | DocumentCounters table missing | Run CREATE_DocumentCounters_Table.sql |
| OpenAI enhancement fails | Invalid API key | Check appsettings.json OpenAI:ApiKey |
| Template fails | Node.js not found | Verify node is in PATH, check DocumentGeneration:NodeExecutable |
| Excel not updating | File locked | Close Excel, wait 30 seconds, retry |
| Teams notification fails | Invalid webhook URL | Check Teams:DraftsWebhookUrl |
| MasterIndex not populating | Table doesn't exist | Run CREATE_MasterIndex_Table.sql |

### Debugging Queries

```sql
-- Find entries that should have triggered auto-draft
SELECT *
FROM DaQa.DocumentChanges
WHERE Status = 'Completed'
  AND DocId IS NULL;

-- Check if DocId was generated but not updated
SELECT *
FROM DaQa.MasterIndex
WHERE SourceDocumentID LIKE '%TEST%';

-- Review approval actions
SELECT *
FROM DaQa.ApprovalTracking
WHERE ActionDate > DATEADD(day, -7, GETUTCDATE())
ORDER BY ActionDate DESC;
```

---

## Next Steps

1. ✅ Database setup (DocumentCounters, MasterIndex, ApprovalTracking)
2. ✅ Service registration in Program.cs
3. ✅ Configuration in appsettings.json
4. ⏳ Test auto-draft workflow
5. ⏳ Set up Teams webhooks
6. ⏳ Integrate with approval UI
7. ⏳ Implement SharePoint upload
8. ⏳ Train AI with approval feedback

---

## Support

- **Documentation**: See `docs/NAMING-CONVENTIONS-v2.1.html`
- **Logs**: Check `logs/` directory
- **Database**: Query `DaQa.vw_*` views for insights
- **Issues**: Create issue in project repository

---

**Version**: 1.0
**Last Updated**: November 2025
**Owner**: Enterprise Documentation Platform Team
