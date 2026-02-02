# Working Memory - Enterprise Documentation Platform V2

## Last Updated: 2026-01-12

## Current Focus Areas
- End-to-end doc pipeline troubleshooting
- Frontend-API wiring verification
- Approval workflow implementation (Prompt 2A)

---

## Documentation Automation Pipeline

### End-to-End Flow
```
Excel File (BI Analytics Change Spreadsheet.xlsx)
    ↓ ExcelChangeIntegratorService (polls every 1 min)
    ↓ Reads rows with Status="Completed"
    ↓ Inserts into DaQa.DocumentChanges
    ↓
DaQa.DocumentChanges
    ↓ DocumentChangeWatcherService (polls every 1 min)
    ↓ Detects rows with DocId (not TBD, not NULL)
    ↓ Creates DaQa.WorkflowEvents entry
    ↓ Queues to DaQa.DocumentationQueue
    ↓
DaQa.DocumentationQueue
    ↓ DocGeneratorQueueProcessor (polls every 60 sec)
    ↓ Calls Node.js template engine
    ↓ Generates .docx document
    ↓
DaQa.MasterIndex
    ↓ Stores generated document metadata
    ↓ Triggers approval workflow
```

### Service Configuration (appsettings.Development.json)
```json
"ExcelChangeIntegrator": {
    "ExcelPath": "C:\\Users\\Alexander.Kirby\\Desktop\\Change Spreadsheet\\BI Analytics Change Spreadsheet.xlsx",
    "PollIntervalMinutes": 1
},
"DocumentChangeWatcher": {
    "PollIntervalMinutes": 1,
    "Enabled": true
},
"DocGeneratorQueueProcessor": {
    "PollIntervalSeconds": 60,
    "Enabled": true
}
```

### Key Tables (DaQa schema)
- `DocumentChanges` - Synced from Excel, source of truth for change requests
- `WorkflowEvents` - Event sourcing for audit trail
- `DocumentationQueue` - Pending document generation jobs
- `MasterIndex` - Generated document metadata catalog
- `DocumentApprovals` - Approval workflow state

### Service Classes
- `ExcelChangeIntegratorService.cs` - Polls Excel, syncs to DocumentChanges
- `DocumentChangeWatcherService.cs` - Detects new rows, triggers workflow
- `DocGeneratorQueueProcessor.cs` - Processes queue, generates documents

---

## Prompt 2A: Enterprise Approval Workflow (COMPLETED)

### Backend Endpoints Created
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/approvals` | GET | List approvals with filters |
| `/api/approvals/{id}` | GET | Get single approval detail |
| `/api/approvals/{id}/approve` | POST | Approve document |
| `/api/approvals/{id}/reject` | POST | Reject document |
| `/api/approvals/{id}/assign` | POST | Assign to user |
| `/api/approvals/{id}/reassign` | POST | Reassign to different user |
| `/api/approvals/{id}/escalate` | POST | Escalate approval |
| `/api/approvals/{id}/history` | GET | Get approval history |
| `/api/approvals/{id}/events` | GET | Get workflow events |
| `/api/approvals/{id}/edits` | GET/POST | Document edits |
| `/api/approvals/{id}/content` | GET | Get document content |
| `/api/approvals/{id}/regenerate` | POST | Request regeneration |
| `/api/approvals/{id}/feedback` | POST | Submit feedback |
| `/api/approvals/bulk/approve` | POST | Bulk approve |
| `/api/approvals/bulk/reject` | POST | Bulk reject |
| `/api/approvals/statistics` | GET | Dashboard stats |
| `/api/notifications` | GET | User notifications |
| `/api/notifications/unread-count` | GET | Unread count |

### SignalR Events (ApprovalHub)
- `DocumentGenerated`, `ApprovalRequested`, `ApprovalDecision`
- `ApprovalCompleted`, `ApprovalRejected`
- `MasterIndexUpdated`, `MasterIndexCreated`, `MasterIndexDeleted`
- `StatisticsChanged`, `AgentStatusChanged`, `AgentError`
- `DocumentUpdated`, `DocumentSyncStatusChanged`, `BulkOperationCompleted`

### Files Created/Modified
- `EnhancedApprovalDTOs.cs` - 17-table schema DTOs
- `ApprovalsController.cs` - 25+ endpoints
- `NotificationsController.cs` - Notification endpoints
- `ApprovalHub.cs` - Extended SignalR events

---

## Build Issues Resolved

### Namespace Ambiguity (CS0104)
Fixed by adding type aliases in ApprovalsController.cs:
```csharp
using ServiceApprovalDto = Enterprise.Documentation.Core.Application.Services.Approval.ApprovalDto;
using ServiceApprovalRequest = Enterprise.Documentation.Core.Application.Services.Approval.ApprovalRequest;
```

---

## Next Steps (Prompt 2B)
- Frontend: Wire React components to new API endpoints
- SignalR: Connect real-time updates
- Testing: E2E approval workflow tests
