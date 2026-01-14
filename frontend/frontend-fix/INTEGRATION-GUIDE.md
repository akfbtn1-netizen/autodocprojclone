# Frontend V2 Fix Package - Integration Guide

## Overview

This package fixes and enhances the existing frontend to properly integrate with the V2 API and multi-agent system.

**Key Changes:**
1. ✅ Fixed API port (5195 instead of 7001)
2. ✅ Added comprehensive types matching MasterIndex (119 columns)
3. ✅ Created service layer for all V2 API endpoints
4. ✅ Added React Query hooks for data fetching
5. ✅ Enhanced SignalR hooks for real-time updates
6. ✅ Created Agent Monitoring panel
7. ✅ Created Metadata Display component
8. ✅ Created Lineage Visualization component
9. ✅ Created Faceted Search component
10. ✅ Updated Dashboard with real data
11. ✅ Updated Zustand stores

---

## Installation Steps

### Step 1: Backup Existing Files

```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2\frontend

# Create backup directory
mkdir backup-$(Get-Date -Format 'yyyy-MM-dd')

# Backup existing files
Copy-Item -Path src -Destination backup-$(Get-Date -Format 'yyyy-MM-dd')/src -Recurse
Copy-Item -Path vite.config.ts -Destination backup-$(Get-Date -Format 'yyyy-MM-dd')/
```

### Step 2: Apply Config Fixes

**Replace `vite.config.ts`:**
```powershell
Copy-Item -Path fix-package/config/vite.config.ts -Destination vite.config.ts -Force
```

### Step 3: Replace Type Definitions

```powershell
# Replace types
Copy-Item -Path fix-package/src/types/index.ts -Destination src/types/index.ts -Force
```

### Step 4: Add New Services

```powershell
# Copy all service files
Copy-Item -Path fix-package/src/services/* -Destination src/services/ -Force
```

### Step 5: Add New Hooks

```powershell
# Copy all hook files
Copy-Item -Path fix-package/src/hooks/* -Destination src/hooks/ -Force
```

### Step 6: Add New Components

```powershell
# Create new component directories
mkdir -p src/components/agents
mkdir -p src/components/metadata
mkdir -p src/components/lineage
mkdir -p src/components/search

# Copy components
Copy-Item -Path fix-package/src/components/agents/* -Destination src/components/agents/ -Force
Copy-Item -Path fix-package/src/components/metadata/* -Destination src/components/metadata/ -Force
Copy-Item -Path fix-package/src/components/lineage/* -Destination src/components/lineage/ -Force
Copy-Item -Path fix-package/src/components/search/* -Destination src/components/search/ -Force
```

### Step 7: Update Pages

```powershell
# Update Dashboard
Copy-Item -Path fix-package/src/pages/Dashboard.tsx -Destination src/pages/Dashboard.tsx -Force
```

### Step 8: Update Stores

```powershell
Copy-Item -Path fix-package/src/stores/index.ts -Destination src/stores/index.ts -Force
```

### Step 9: Verify Installation

```powershell
# Install any missing dependencies
npm install

# Type check
npm run typecheck

# Run dev server
npm run dev
```

---

## API Endpoint Requirements

The frontend expects these API endpoints to be available:

### Dashboard Endpoints
```
GET  /api/dashboard/overview     → DashboardOverview
GET  /api/dashboard/kpis         → DashboardKpis
GET  /api/dashboard/trends       → DashboardTrends
GET  /api/dashboard/activity     → ActivityItem[]
```

### Document Endpoints
```
GET  /api/documents              → DocumentListResponse
GET  /api/documents/:id          → Document
GET  /api/documents/by-docid/:docId → Document
GET  /api/documents/:id/metadata → MasterIndexMetadata
POST /api/documents/search       → SearchResult
GET  /api/documents/facets       → SearchFacets
GET  /api/documents/recent       → Document[]
POST /api/documents/:id/enrich   → MasterIndexMetadata
```

### Approval Endpoints
```
GET  /api/approvals/pending      → ApprovalListResponse
GET  /api/approvals/:id          → ApprovalRequest
PUT  /api/approvals/:id/approve  → ApprovalRequest
PUT  /api/approvals/:id/reject   → ApprovalRequest
GET  /api/approvals/stats        → ApprovalStats
```

### Agent Endpoints
```
GET  /api/agents                 → Agent[]
GET  /api/agents/health          → AgentHealthCheck[]
GET  /api/agents/activity        → AgentActivity[]
GET  /api/agents/stats           → AgentStats
POST /api/agents/:type/command   → CommandResult
```

### Lineage Endpoints
```
GET  /api/lineage/table/:schema/:table        → LineageGraph
GET  /api/lineage/column/:schema/:table/:col  → LineageGraph
GET  /api/lineage/impact/table/:schema/:table → ImpactAnalysis
GET  /api/lineage/search                      → LineageSearchResult
```

### SignalR Hubs
```
/hubs/approval  - Document and approval real-time updates
/hubs/agents    - Agent status real-time updates
```

---

## Backend API Stubs

If endpoints don't exist yet, add these stubs to your API:

### DashboardController.cs

```csharp
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    [HttpGet("kpis")]
    public async Task<ActionResult<DashboardKpis>> GetKpis()
    {
        // TODO: Implement with real data
        return Ok(new DashboardKpis
        {
            TotalDocuments = await _context.MasterIndex.CountAsync(),
            PendingApprovals = await _context.ApprovalRequests.CountAsync(a => a.Status == "pending"),
            ApprovedToday = await _context.ApprovalRequests.CountAsync(a => a.ApprovedDate.Date == DateTime.Today),
            AvgProcessingTimeHours = 2.4,
            CompletionRate = 94.2,
            AiEnhancedPercentage = 71.5,
        });
    }
}
```

### AgentsController.cs

```csharp
[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly IAgentHealthService _healthService;

    [HttpGet]
    public async Task<ActionResult<List<AgentDto>>> GetAllAgents()
    {
        var agents = new List<AgentDto>
        {
            new() { Id = "1", Name = "Schema Detector", Type = "SchemaDetector", Status = "idle" },
            new() { Id = "2", Name = "Document Generator", Type = "DocGenerator", Status = "processing" },
            new() { Id = "3", Name = "Excel Change Integrator", Type = "ExcelChangeIntegrator", Status = "idle" },
            new() { Id = "4", Name = "Metadata Manager", Type = "MetadataManager", Status = "idle" },
        };
        return Ok(agents);
    }
}
```

---

## File Structure After Fix

```
frontend/
├── vite.config.ts              # UPDATED - port 5195
├── src/
│   ├── types/
│   │   └── index.ts            # REPLACED - 119 column types
│   ├── services/
│   │   ├── api.ts              # UPDATED
│   │   ├── documents.ts        # NEW
│   │   ├── agents.ts           # NEW
│   │   ├── approvals.ts        # NEW
│   │   ├── dashboard.ts        # NEW
│   │   ├── lineage.ts          # NEW
│   │   └── index.ts            # NEW
│   ├── hooks/
│   │   ├── useQueries.ts       # NEW - React Query hooks
│   │   ├── useSignalR.ts       # UPDATED - Enhanced
│   │   └── index.ts            # NEW
│   ├── stores/
│   │   └── index.ts            # UPDATED - Agent + Search stores
│   ├── components/
│   │   ├── agents/
│   │   │   └── AgentPanel.tsx  # NEW
│   │   ├── metadata/
│   │   │   └── MetadataDisplay.tsx # NEW
│   │   ├── lineage/
│   │   │   └── LineageViewer.tsx   # NEW
│   │   └── search/
│   │       └── SearchFilters.tsx   # NEW
│   └── pages/
│       └── Dashboard.tsx       # UPDATED - Real data
```

---

## Type Mapping Reference

### MasterIndex → Frontend Types

| MasterIndex Column | Frontend Type Property |
|--------------------|------------------------|
| IndexID | indexId |
| DocId | docId |
| DocumentTitle | documentTitle |
| DocumentType | documentType |
| SchemaName | schemaName |
| TableName | tableName |
| ColumnName | columnName |
| BusinessDomain | businessDomain |
| SemanticCategory | semanticCategory |
| PIIIndicator | piiIndicator |
| PIITypes | piiTypes |
| ComplianceTags | complianceTags |
| DataClassification | dataClassification |
| AIGeneratedTags | aiGeneratedTags |
| Keywords | keywords |
| CompletenessScore | completenessScore |
| QualityScore | qualityScore |
| RelatedTables | relatedTables |
| StoredProcedures | storedProcedures |
| ApprovalStatus | approvalStatus |

### Agent Types

| Agent Type | Description |
|------------|-------------|
| SchemaDetector | Monitors database schema changes |
| DocGenerator | Generates Word documents using AI |
| ExcelChangeIntegrator | Watches Excel for change requests |
| MetadataManager | Populates MasterIndex metadata |

---

## Testing the Integration

### 1. Start Backend API

```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2
dotnet run --project src/Api/Api.csproj
```

### 2. Start Frontend

```powershell
cd frontend
npm run dev
```

### 3. Verify Connections

Open browser to `http://localhost:5173`

**Check:**
- [ ] KPI cards load with data
- [ ] Agent panel shows 4 agents
- [ ] Workflow canvas displays
- [ ] Pending approvals list populates
- [ ] Recent documents load
- [ ] SignalR shows "Live" status

### 4. Test Real-Time Updates

1. Open two browser tabs
2. Approve a document in tab 1
3. Tab 2 should auto-update

---

## Troubleshooting

### API Connection Failed

```
Error: Network Error
```

**Fix:** Ensure API is running on port 5195:
```powershell
dotnet run --project src/Api/Api.csproj --urls "http://localhost:5195"
```

### SignalR Not Connecting

```
SignalR connection error: ...
```

**Fix:** Add hub endpoints in API:
```csharp
app.MapHub<ApprovalHub>("/hubs/approval");
app.MapHub<AgentHub>("/hubs/agents");
```

### Types Not Matching

```
Type 'X' is not assignable to type 'Y'
```

**Fix:** Ensure backend DTOs match frontend types. Check `src/types/index.ts`.

### Agent Panel Empty

**Fix:** Add AgentsController to API (see stubs above).

---

## Next Steps After Integration

1. **Add remaining pages:**
   - Documents page with search filters
   - Approvals page with bulk actions
   - Settings page

2. **Add document preview:**
   - Integrate with SharePoint preview API
   - Or use PDF.js for local preview

3. **Add lineage page:**
   - Full lineage visualization
   - Impact analysis before changes

4. **Add quality dashboard:**
   - Metadata quality trends
   - Completeness heatmap
   - PII distribution

---

## Support

If you encounter issues:
1. Check browser console for errors
2. Check API logs for backend errors
3. Verify network tab shows correct URLs
4. Ensure CORS is configured in API

Created by: Claude AI
For: Enterprise Documentation Platform V2
Date: 2025-01-05
