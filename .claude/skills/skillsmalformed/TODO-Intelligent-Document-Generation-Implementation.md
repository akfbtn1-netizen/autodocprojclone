# TODO: Intelligent Document Generation Implementation

> **Project:** Enterprise Documentation Platform V2  
> **Skill:** intelligent-document-generation  
> **Created:** 2025-12-31  
> **Status:** Planning

---

## Overview

Integration roadmap for implementing the Intelligent Document Generation skill into our existing platform. This connects with our current 4 production agents (SchemaDetector, DocGenerator, ExcelChangeIntegrator, MetadataManager) and extends the documentation pipeline with Shadow Metadata tracking, multi-audience support, and approval workflows.

---

## Phase 1: Foundation Setup (Days 1-2)

### 1.1 Install Dependencies

- [ ] **Python Environment**
  ```bash
  pip install docxtpl python-docx lxml Pillow --break-system-packages
  ```

- [ ] **Mermaid CLI for Diagrams**
  ```bash
  npm install -g @mermaid-js/mermaid-cli
  ```

- [ ] **.NET Packages** (add to DocGenerator.csproj)
  ```xml
  <PackageReference Include="DocumentFormat.OpenXml" Version="3.3.0" />
  ```

- [ ] **Verify Node.js 22.21.1** is properly configured for npm workflows

### 1.2 Create Project Structure

- [ ] Create directories in solution:
  ```
  src/
  ├── DocGen.Application/
  │   └── DocumentGeneration/
  │       ├── Commands/
  │       ├── Queries/
  │       └── Services/
  ├── DocGen.Infrastructure/
  │   └── DocumentGeneration/
  │       ├── ShadowMetadataService.cs
  │       ├── TemplateEngine.cs
  │       └── DiagramGenerator.cs
  └── templates/
      ├── StoredProcedure_technical_dba.docx
      ├── StoredProcedure_developer.docx
      ├── StoredProcedure_business_analyst.docx
      └── Table_template.docx
  ```

- [ ] Add `ShadowMetadataService.cs` from skill references to Infrastructure layer

- [ ] Register services in DI container

---

## Phase 2: Shadow Metadata Integration (Days 3-4)

### 2.1 Database Schema Updates

- [ ] **Add Shadow Metadata tracking table**
  ```sql
  CREATE TABLE DocumentSync (
      DocumentSyncId INT IDENTITY(1,1) PRIMARY KEY,
      DocumentPath NVARCHAR(500) NOT NULL,
      DbObjectId NVARCHAR(255) NOT NULL,
      ContentHash NVARCHAR(100) NOT NULL,
      SyncStatus NVARCHAR(20) NOT NULL DEFAULT 'DRAFT',
      SchemaVersion NVARCHAR(20),
      LastSyncUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
      MasterIndexId INT FOREIGN KEY REFERENCES MasterIndex(Id),
      AudienceType NVARCHAR(50),
      CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
      ModifiedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  );
  
  CREATE INDEX IX_DocumentSync_DbObjectId ON DocumentSync(DbObjectId);
  CREATE INDEX IX_DocumentSync_SyncStatus ON DocumentSync(SyncStatus);
  ```

- [ ] **Add EF Core entity and configuration**

- [ ] **Create migration**: `Add-Migration AddDocumentSyncTable`

### 2.2 Implement IShadowMetadataService

- [ ] Create interface in Application layer:
  ```csharp
  public interface IShadowMetadataService
  {
      Task<ShadowMetadata> ReadMetadataAsync(string docxPath);
      Task WriteMetadataAsync(string docxPath, ShadowMetadata metadata);
      Task<SyncStatus> CheckSyncStatusAsync(string docxPath, string currentDbHash);
      Task UpdateSyncMetadataAsync(string docxPath, string dbObjectId, 
          string contentHash, int masterIndexId, AudienceType audience);
      string ComputeContentHash(string content);
  }
  ```

- [ ] Implement in Infrastructure layer using Open XML SDK

- [ ] Add unit tests for hash computation and property read/write

### 2.3 Integrate with Existing DocGeneratorService

- [ ] Modify `DocGeneratorService.cs` to:
  - [ ] Call `ComputeContentHash()` before generation
  - [ ] Check existing document sync status
  - [ ] Skip regeneration if `SyncStatus == CURRENT`
  - [ ] Update Shadow Metadata after successful generation

---

## Phase 3: Template Engine Implementation (Days 5-7)

### 3.1 Create Word Templates

- [ ] **StoredProcedure_technical_dba.docx** - Full technical detail
  - Parameters table with all columns
  - Full SQL code sample
  - Performance metrics
  - Execution plan notes
  - Change history

- [ ] **StoredProcedure_developer.docx** - Developer focus
  - Parameters table (simplified)
  - Usage examples
  - Error handling notes
  - Integration patterns

- [ ] **StoredProcedure_business_analyst.docx** - Business focus
  - Plain English description
  - Business rules
  - Data flow overview (no SQL)

- [ ] **Table_template.docx** - Table documentation
- [ ] **View_template.docx** - View documentation
- [ ] **Function_template.docx** - Function documentation

### 3.2 Python Template Service

- [ ] Create `template_service.py` with:
  ```python
  class TemplateService:
      def generate_document(self, template_name, context, output_path)
      def get_template_for_audience(self, object_type, audience)
      def validate_context(self, template_name, context)
  ```

- [ ] Create FastAPI endpoint for template generation (or integrate with existing .NET API)

- [ ] Add template caching for performance

### 3.3 .NET Template Integration

- [ ] **Option A:** Call Python service via HTTP
- [ ] **Option B:** Use DocX library for .NET (limited Jinja2 support)
- [ ] **Option C:** Hybrid - Python generates, .NET manages workflow

- [ ] Decide approach: **[_____________]**

---

## Phase 4: Diagram Generation (Days 8-9)

### 4.1 Mermaid Integration

- [ ] Create `DiagramGeneratorService`:
  ```csharp
  public interface IDiagramGeneratorService
  {
      Task<string> GenerateErdAsync(IEnumerable<TableMetadata> tables);
      Task<string> GenerateFlowchartAsync(ProcedureFlow flow);
      Task<string> GenerateDataLineageAsync(LineageData lineage);
      Task<string> RenderToImageAsync(string mermaidCode, string outputPath);
  }
  ```

- [ ] Implement ERD generation from schema metadata

- [ ] Implement stored procedure flowchart from parsed SQL

- [ ] Integrate with existing column-level lineage from `database-intelligence-skill-v2`

### 4.2 Diagram Caching

- [ ] Cache generated diagrams by content hash
- [ ] Invalidate cache when source objects change
- [ ] Store in `/diagrams/` folder structure

---

## Phase 5: Multi-Audience Generation (Days 10-11)

### 5.1 Audience Profile Configuration

- [ ] Add `AudienceProfiles` table or config:
  ```json
  {
    "TECHNICAL_DBA": { "depth": 10, "includeCode": true, "includeMetrics": true },
    "DEVELOPER": { "depth": 7, "includeCode": true, "includeMetrics": false },
    "BUSINESS_ANALYST": { "depth": 4, "includeCode": false, "includeMetrics": true },
    "EXECUTIVE": { "depth": 1, "includeCode": false, "includeMetrics": true }
  }
  ```

- [ ] Create audience selection UI in Next.js frontend

### 5.2 Content Adaptation Service

- [ ] **Without Azure OpenAI** (current state):
  - Template-based content filtering
  - Remove code sections for non-technical audiences
  - Use predefined simplification rules

- [ ] **With Azure OpenAI** (when keys available):
  - Implement `ContentAdapterService` using GPT-4o
  - Generate executive summaries
  - Adapt technical descriptions per audience

### 5.3 Batch Multi-Audience Generation

- [ ] Generate all audience variants in single operation
- [ ] Track variants in DocumentSync table
- [ ] Link variants via MasterIndexId

---

## Phase 6: SharePoint Integration (Days 12-14)

### 6.1 SharePoint Library Setup

- [ ] Run `Deploy-DocumentApprovalWorkflow.ps1`:
  ```powershell
  .\Deploy-DocumentApprovalWorkflow.ps1 `
      -SiteUrl "https://[tenant].sharepoint.com/sites/Documentation" `
      -LibraryName "Generated Documentation" `
      -ApproverEmails "approver1@company.com,approver2@company.com" `
      -CreateLibrary
  ```

- [ ] Verify custom columns created
- [ ] Verify views created (Pending, Stale, By Audience)
- [ ] Test folder structure (Pending, Approved, Rejected)

### 6.2 Document Upload Service

- [ ] Create `SharePointUploadService`:
  ```csharp
  public interface ISharePointUploadService
  {
      Task<string> UploadDocumentAsync(string localPath, string libraryFolder);
      Task UpdateMetadataAsync(string fileUrl, Dictionary<string, object> properties);
      Task<DocumentApprovalStatus> GetApprovalStatusAsync(string fileUrl);
  }
  ```

- [ ] Use Microsoft Graph API or PnP.Core SDK

- [ ] Map Shadow Metadata to SharePoint columns on upload

### 6.3 Power Automate Flow

- [ ] Import flow definition from `PowerAutomate_FlowDefinition.json`
- [ ] Configure connections (SharePoint, Approvals, Outlook)
- [ ] Test approval workflow end-to-end
- [ ] Configure Teams notifications

---

## Phase 7: Quality Assurance Pipeline (Days 15-16)

### 7.1 Implement Validators

- [ ] **SchemaValidator** - Compare document against current DB schema
- [ ] **ContentValidator** - Check for empty sections, minimum content
- [ ] **AccessibilityValidator** - WCAG 2.0 Level AA checks

### 7.2 Validation Pipeline

- [ ] Create `ValidationPipeline` orchestrator
- [ ] Run validation before SharePoint upload
- [ ] Block upload if critical errors
- [ ] Log warnings but allow upload

### 7.3 Validation Dashboard

- [ ] Add validation results to API response
- [ ] Display validation status in Next.js UI
- [ ] Allow override for warnings (with audit trail)

---

## Phase 8: API Endpoints (Days 17-18)

### 8.1 New Endpoints

- [ ] `POST /api/documents/generate`
  ```json
  {
    "dbObjectId": "SP_GetCustomerOrders",
    "objectType": "STORED_PROCEDURE",
    "audiences": ["TECHNICAL_DBA", "DEVELOPER"],
    "uploadToSharePoint": true
  }
  ```

- [ ] `GET /api/documents/{id}/sync-status`

- [ ] `POST /api/documents/batch`

- [ ] `GET /api/documents/{id}/validation`

- [ ] `POST /api/documents/{id}/regenerate`

### 8.2 Update Existing Endpoints

- [ ] Modify `DocumentsController` to include Shadow Metadata
- [ ] Add sync status to document list queries
- [ ] Include validation results in responses

---

## Phase 9: Frontend Integration (Days 19-21)

### 9.1 Document Generation UI

- [ ] Add "Generate Documentation" button to object detail pages
- [ ] Audience selection checkboxes
- [ ] Generation progress indicator
- [ ] Download generated documents

### 9.2 Sync Status Dashboard

- [ ] Create `/documents/sync` page showing:
  - Documents by sync status (pie chart)
  - Stale documents list with regenerate action
  - Orphaned documents requiring attention

### 9.3 Approval Workflow UI

- [ ] Pending approvals list
- [ ] Approval status badges on documents
- [ ] Link to SharePoint for detailed review

---

## Phase 10: Testing & Deployment (Days 22-25)

### 10.1 Unit Tests

- [ ] ShadowMetadataService tests
- [ ] TemplateEngine tests
- [ ] DiagramGenerator tests
- [ ] Validator tests

### 10.2 Integration Tests

- [ ] End-to-end generation pipeline
- [ ] SharePoint upload and metadata sync
- [ ] Approval workflow trigger

### 10.3 Performance Testing

- [ ] Batch generation of 100+ documents
- [ ] Measure generation time per document type
- [ ] Optimize template caching

### 10.4 Deployment

- [ ] Update deployment scripts
- [ ] Add new environment variables
- [ ] Document configuration requirements
- [ ] Create runbook for operations

---

## Configuration Required

### Environment Variables

```env
# SharePoint
SHAREPOINT_SITE_URL=https://[tenant].sharepoint.com/sites/Documentation
SHAREPOINT_CLIENT_ID=
SHAREPOINT_CLIENT_SECRET=

# Templates
TEMPLATE_PATH=/app/templates
DIAGRAM_OUTPUT_PATH=/app/diagrams

# Mermaid
MERMAID_CLI_PATH=mmdc

# Azure OpenAI (when available)
AZURE_OPENAI_ENDPOINT=
AZURE_OPENAI_KEY=
AZURE_OPENAI_DEPLOYMENT=gpt-4o
```

### appsettings.json Additions

```json
{
  "DocumentGeneration": {
    "DefaultAudience": "TECHNICAL_DBA",
    "EnableMultiAudience": true,
    "EnableDiagrams": true,
    "EnableSharePointUpload": false,
    "ValidationMode": "Strict"
  }
}
```

---

## Dependencies on Other Work

| Dependency | Status | Blocker? |
|------------|--------|----------|
| Azure OpenAI API keys | Pending | No (graceful fallback) |
| SharePoint site provisioned | Pending | Yes for Phase 6 |
| Master Index populated | Complete | No |
| Schema metadata extraction | Complete | No |
| Column lineage tracking | Complete | No |

---

## Success Criteria

- [ ] Documents generate with correct Shadow Metadata
- [ ] Sync status accurately reflects database changes
- [ ] Multi-audience variants generate from single source
- [ ] Diagrams auto-generate and embed correctly
- [ ] SharePoint approval workflow triggers on upload
- [ ] Validation catches schema drift before publishing
- [ ] Batch generation handles 100+ documents without timeout

---

## Notes

- Start with Python template engine (docxtpl) for rapid iteration
- Consider .NET-native solution later if performance critical
- Shadow Metadata is the foundation - get this right first
- Multi-audience can be simplified initially (template switching only)
- Azure OpenAI content adaptation is a nice-to-have enhancement

---

## Related Files

- `/mnt/skills/user/intelligent-document-generation/SKILL.md`
- `/mnt/project/DocGeneratorService.cs`
- `/mnt/project/DocGeneratorService_Enterprise.cs`
- `/mnt/project/ApprovalTrackingService_Complete.cs`
