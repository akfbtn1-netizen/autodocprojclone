# Service Implementations Reference

## Background Services (Poll-Based)

### ExcelChangeIntegratorService
**File:** `src/Core/Application/Services/ExcelSync/ExcelChangeIntegratorService.cs`
**Type:** `BackgroundService` + `IExcelChangeIntegratorService`
**Poll:** 1 minute (configurable)

**Function:** Syncs Excel spreadsheet to database
- Reads from `ExcelChangeIntegrator:ExcelPath` config
- Headers in row 3, data starts row 4
- Only processes rows where `Status="Completed"`
- Generates UniqueKey = SHA256(JIRA+Table+Column)
- INSERT/UPDATE to `DaQa.DocumentChanges`

**Excel Columns Expected:**
DocID, Date, JIRA #, CAB#, Sprint#, Status, Priority, Severity, Table, Column, 
Change Type, Description, Reported By, Assigned To, Change Applied, Location of Changed Code

**Note:** Excel writeback is DISABLED (lines 383-384) - was causing file corruption

---

### DocumentChangeWatcherService  
**File:** `src/Core/Application/Services/Watcher/DocumentChangeWatcherService.cs`
**Type:** `BackgroundService`
**Poll:** 1 minute

**Function:** Monitors DocumentChanges for new entries needing DocId
- Queries for rows where DocId IS NULL and Status = 'Completed'
- Calls `DocIdGeneratorService.GenerateDocIdAsync()`
- INSERT to `DaQa.DocumentationQueue` with Status='Pending'

---

### DocGeneratorQueueProcessor
**File:** `src/Core/Application/Services/QueueProcessor/DocGeneratorQueueProcessor.cs`
**Type:** `BackgroundService`
**Poll:** 1 minute (configurable via `DocGeneratorQueueProcessor:PollIntervalSeconds`)

**Function:** Processes queue items, generates drafts
- Polls `DaQa.DocumentationQueue` for Status='Pending'
- Orchestrates: CodeExtraction → QualityAudit → DraftGeneration → TemplateExecution
- Creates `DaQa.ApprovalWorkflow` entry
- Updates queue status to 'Completed' or 'Failed'

**Key Flow:**
```
GetNextPendingItemAsync()
  → GetDocumentChangeAsync()
  → CodeExtractionService.ExtractMarkedCodeAsync()
  → EnterpriseCodeQualityAuditService
  → DraftGenerationService.GenerateDraftAsync()
  → TemplateExecutorService.GenerateDocumentAsync()
  → CreateApprovalWorkflowEntryAsync()
```

---

## Document Generation Services

### DraftGenerationService
**File:** `src/Core/Application/Services/DraftGeneration/DraftGenerationService.cs`
**Interface:** `IDraftGenerationService`

**THE MAIN ORCHESTRATOR** - Contains AI prompt logic (lines 907-992)

**Flow:**
1. `MapToChangeData()` - Convert DocumentChangeEntry to internal DTO
2. `DetermineDocumentType()` - BR/EN/DF/SP based on ChangeType
3. `SelectTemplateAsync()` - Get appropriate template
4. `PrepareTemplateData()` - Build base template data
5. `SqlAnalysisService.AnalyzeSql()` - Parse SQL structure
6. `EnrichWithAIAsync()` - **AI content enhancement**
7. `MergeTemplateData()` - Combine all data sources
8. `MetadataExtractionService.ExtractMetadataAsync()` - Generate metadata + embeddings
9. `MasterIndexPersistenceService.SaveMetadataAsync()` - Persist to database
10. `DocumentGenerationService.GenerateDocumentAsync()` - Create Word doc

**AI Prompt Location:** Lines 907-992 (BuildAIPrompt method)

---

### DocumentGenerationService
**File:** `src/Core/Application/Services/DocumentGeneration/DocumentGenerationService.cs`
**Interface:** `IDocumentGenerationService`

**Function:** Executes Python templates via subprocess
- Writes JSON data file for template input
- Calls `python.exe TEMPLATE_*.py data.json output.docx`
- Embeds CustomProperties in generated document

---

### TemplateExecutorService
**File:** `src/Core/Application/Services/DocumentGeneration/TemplateExecutorService.cs`
**Interface:** `ITemplateExecutorService`

**Function:** Dual template system
- Path A: OpenXML C# templates (BusinessRequestTemplate, etc.)
- Path B: Python templates via subprocess

**Config:** `UseOpenXmlTemplates: false` → Python templates active

---

### DocIdGeneratorService
**File:** `src/Core/Application/Services/DocumentGeneration/DocIdGeneratorService.cs`
**Interface:** `IDocIdGeneratorService`

**Format:** `{Type}-{Sequence}-{ObjectName}-{Column?}-{JIRA}`
**Example:** `EN-0015-irf_policy-PolicyStatus-BAS-9818`

**Sequence Source:** `DaQa.usp_GetNextDocIdNumber` stored procedure
**Fallback:** Query MAX existing + 1

---

## Approval Services

### ApprovalTrackingService
**File:** `src/Core/Application/Services/Approval/ApprovalTrackingService.cs`
**Interface:** `IApprovalTrackingService`

**Function:** Processes approvals, triggers post-approval workflows

**Post-Approval Actions:**
1. Update `DaQa.ApprovalWorkflow` status
2. Move file to final location
3. `CustomPropertiesHelper.AddCustomProperties()`
4. `ComprehensiveMasterIndexService.PopulateMasterIndexFromApprovedDocumentAsync()`
5. `StoredProcedureDocumentationService.CreateOrUpdateSPDocumentationAsync()`
6. `TeamsNotificationService.SendDraftApprovalNotificationAsync()` ⚠️ Method name mismatch!

---

### ApprovalService
**File:** `src/Core/Application/Services/Approval/ApprovalService.cs`

**Function:** Facade/proxy that delegates to ApprovalTrackingService

---

## Metadata Services

### MetadataExtractionService
**File:** `src/Core/Application/Services/Metadata/MetadataExtractionService.cs`
**Interface:** `IMetadataExtractionService`

**Function:** Extracts metadata from template data, generates embeddings

**AI Integration:**
- Uses Azure OpenAI for classification enrichment
- Generates semantic embeddings via `text-embedding-ada-002`
- Domain tags from predefined list (Policy Management, Financial Reporting, etc.)

---

### MasterIndexPersistenceService
**File:** `src/Core/Application/Services/Metadata/MasterIndexPersistenceService.cs`
**Interface:** `IMasterIndexPersistenceService`

**Function:** Saves metadata to `DaQa.MasterIndex` table
- INSERT new records
- UPDATE existing records (by DocId)
- Updates document path after generation

---

### ComprehensiveMasterIndexService
**File:** `src/Core/Application/Services/MasterIndex/ComprehensiveMasterIndexService.cs`
**Interface:** `IComprehensiveMasterIndexService`

**Function:** 14-phase metadata population + AI Phase 15

**Phases:**
1. Source System Data
2. Document Analysis (Word doc parsing)
3. Database Metadata
4. Business Context
5. Technical Details
6. Ownership Data
7. Classification
8. Relationships
9. Quality Metrics
10. Usage Tracking
11. Lifecycle Data
12. Compliance
13. Performance Stats
14. Audit Trail
15. AI Inference (OpenAI-powered)

---

## Analysis Services

### SqlAnalysisService
**File:** `src/Core/Application/Services/SqlAnalysis/SqlAnalysisService.cs`
**Interface:** `ISqlAnalysisService`

**Function:** Parses SQL stored procedures
- BAS marker detection: `-- Begin BAS-####` / `-- End BAS-####`
- Extracts: Schema, ProcedureName, Parameters, Dependencies
- Calculates complexity (lines, joins, CTEs, temp tables)

---

### CodeExtractionService
**File:** `src/Core/Application/Services/CodeExtraction/CodeExtractionService.cs`
**Interface:** `ICodeExtractionService`

**Function:** Extracts marked code sections from stored procedures
- Queries OBJECT_DEFINITION from sys.sql_modules
- Finds code between BAS markers

---

### EnterpriseCodeQualityAuditService
**File:** `src/Core/Application/Services/Quality/EnterpriseCodeQualityAuditService.cs`
**Interface:** `IEnterpriseCodeQualityAuditService`

**Function:** Analyzes code quality, produces grade (A-F)

---

## Notification Services

### TeamsNotificationService
**File:** `src/Core/Application/Services/Notifications/TeamsNotificationService.cs`
**Interface:** `ITeamsNotificationService`

**Method:** `SendDraftApprovalNotificationAsync(string docId, string jiraNumber, string assignedTo)`

**Note:** ApprovalTrackingService calls `SendDraftReadyNotificationAsync(notification)` - METHOD MISMATCH!

**Config:**
- `Teams:Enabled` - Enable/disable notifications
- `Teams:WebhookUrl` - Power Automate webhook URL

---

## Stored Procedure Services

### StoredProcedureDocumentationService
**File:** `src/Core/Application/Services/StoredProcedure/StoredProcedureDocumentationService.cs`
**Interface:** `IStoredProcedureDocumentationService`

**Function:** Creates/updates SP documentation
- Queries sys.objects and sys.sql_modules for SP metadata
- Creates Word document with SP definition
- Updates MasterIndex

**Output Path:** `C:\Temp\Documentation-Catalog\Database\IRFS1\{Schema}\StoredProcedures\{ProcName}_v{Version}.docx`

---

### StoredProcedureChangeDetectionService (DISABLED)
**File:** `src/Core/Application/Services/Documentation/StoredProcedureChangeDetectionService.cs`
**Status:** Wrapped in `#if false` - temporarily disabled

**Would do:** Auto-detect SP changes via hash comparison, trigger re-documentation

---

## Event System

### WorkflowEventService
**File:** `src/Core/Application/Services/Workflow/WorkflowEventService.cs`
**Interface:** `IWorkflowEventService`

**Function:** Publishes workflow events to `DaQa.WorkflowEvents` table

**Event Types:** DocumentApproved, DocumentRejected, FinalDocumentGenerationStarted/Completed, 
MasterIndexPopulationStarted/Completed, FileSavedToSharePoint, WorkflowCompleted

---

## OpenAI Services

### OpenAIEnhancementService
**File:** `src/Core/Application/Services/DocumentGeneration/OpenAIEnhancementService.cs`
**Interface:** `IOpenAIEnhancementService`

**Function:** Enhances document content via Azure OpenAI
- Uses HttpClient for API calls
- Returns EnhancedDocumentation object

**Config Issues:**
- Uses old API version: `2023-12-01-preview` (should be `2024-08-01-preview`)
- MaxTokens: 1500 (may truncate complex content)
