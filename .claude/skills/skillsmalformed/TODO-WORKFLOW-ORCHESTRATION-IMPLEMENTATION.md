# TODO: Workflow Orchestration Implementation
## Documentation Automation Platform V2

**Created:** December 31, 2025  
**Based On:** workflow-orchestration-saga-patterns skill  
**Target:** MassTransit + Azure Service Bus + SQL Server + .NET 8

---

## Phase 1: Foundation (Week 1)
### Database Schema

- [ ] Create `DocumentEvents` table for event sourcing
  ```sql
  -- Location: database/migrations/003_DocumentEvents.sql
  CREATE TABLE DocumentEvents (
      EventId UNIQUEIDENTIFIER PRIMARY KEY,
      AggregateId UNIQUEIDENTIFIER NOT NULL,
      EventType NVARCHAR(100) NOT NULL,
      EventData NVARCHAR(MAX) NOT NULL,
      Version INT NOT NULL,
      OccurredAt DATETIME2 NOT NULL,
      CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
  );
  ```

- [ ] Create `DocumentApprovalState` table for saga persistence
  ```sql
  -- Location: database/migrations/004_DocumentApprovalState.sql
  CREATE TABLE DocumentApprovalState (
      CorrelationId UNIQUEIDENTIFIER PRIMARY KEY,
      CurrentState NVARCHAR(64) NOT NULL,
      DocumentType NVARCHAR(100),
      PhysicalName NVARCHAR(500),
      DatabaseName NVARCHAR(128),
      SubmittedAt DATETIME2,
      ApprovedAt DATETIME2,
      SubmittedBy NVARCHAR(100),
      ApprovedBy NVARCHAR(500),
      RequiredApprovers NVARCHAR(MAX),
      CompletedApprovers NVARCHAR(MAX),
      ApprovalTier INT,
      ExpirationTokenId UNIQUEIDENTIFIER,
      RowVersion ROWVERSION
  );
  ```

- [ ] Create `ApprovalTierConfiguration` table
- [ ] Add indexes for performance
- [ ] Run migrations on dev database

### NuGet Packages

- [ ] Add to `DocumentationAutomation.Core.csproj`:
  ```xml
  <PackageReference Include="MassTransit" Version="8.*" />
  <PackageReference Include="MassTransit.Azure.ServiceBus.Core" Version="8.*" />
  <PackageReference Include="MassTransit.EntityFrameworkCore" Version="8.*" />
  ```

- [ ] Add to API project:
  ```xml
  <PackageReference Include="MassTransit.AspNetCore" Version="8.*" />
  ```

---

## Phase 2: Domain Events (Week 1)
### Create Event Contracts

- [ ] Create `Source/DocumentationAutomation.Core/Events/` folder

- [ ] `IDocumentChangeDetected.cs`
  - DocumentId, DatabaseName, SchemaName, ObjectName, ChangeType, ChangeDetails

- [ ] `IDraftGenerated.cs`
  - DocumentId, DraftPath, GeneratedBy, ContentHash

- [ ] `IApprovalRequested.cs`
  - DocumentId, Approvers[], Tier, Deadline

- [ ] `IApprovalGranted.cs`
  - DocumentId, ApprovedBy, Comments, ApprovedAt

- [ ] `IApprovalRejected.cs`
  - DocumentId, RejectedBy, Reason, RejectedAt

- [ ] `IFinalDocumentGenerated.cs`
  - DocumentId, FinalPath, ContentHash

- [ ] `IDocumentFiled.cs`
  - DocumentId, SharePointUrl, FileId

- [ ] `IMasterIndexUpdated.cs`
  - DocumentId, MasterIndexId, PhysicalName

- [ ] `IApprovalTimeoutExpired.cs`
  - DocumentId

---

## Phase 3: State Machine (Week 1-2)
### MassTransit Saga Implementation

- [ ] Create `Source/WorkflowOrchestrator/` project (if not exists)

- [ ] Create `DocumentApprovalState.cs`
  - Implement `SagaStateMachineInstance`
  - Add all properties from schema

- [ ] Create `DocumentApprovalStateMachine.cs`
  - Define states: Initial, DraftPending, AwaitingApproval, Approved, Rejected, Escalated, GeneratingFinal, Filing, UpdatingMasterIndex, Final
  - Configure event correlations
  - Implement state transitions
  - Add timeout scheduling

- [ ] Create `DocumentApprovalStateMap.cs`
  - EF Core mapping for saga state

- [ ] Create `DocumentApprovalDbContext.cs`
  - Inherit from `SagaDbContext`

- [ ] Register in `Program.cs`:
  ```csharp
  services.AddMassTransit(x =>
  {
      x.AddSagaStateMachine<DocumentApprovalStateMachine, DocumentApprovalState>()
          .EntityFrameworkRepository(r =>
          {
              r.ConcurrencyMode = ConcurrencyMode.Optimistic;
              r.AddDbContext<DbContext, DocumentApprovalDbContext>();
          });
      
      x.UsingAzureServiceBus((context, cfg) =>
      {
          cfg.Host(connectionString);
          cfg.ConfigureEndpoints(context);
      });
  });
  ```

---

## Phase 4: Event Store (Week 2)
### Event Sourcing Implementation

- [ ] Create `Source/DocumentationAutomation.Core/EventSourcing/` folder

- [ ] Create `IDomainEvent.cs` interface

- [ ] Create `DomainEvent.cs` base class

- [ ] Create concrete event classes:
  - [ ] `DocumentChangeDetectedEvent.cs`
  - [ ] `DraftGeneratedEvent.cs`
  - [ ] `ApprovalRequestedEvent.cs`
  - [ ] `ApprovalGrantedEvent.cs`
  - [ ] `ApprovalRejectedEvent.cs`
  - [ ] `FinalDocumentGeneratedEvent.cs`
  - [ ] `DocumentFiledEvent.cs`
  - [ ] `MasterIndexUpdatedEvent.cs`

- [ ] Create `IEventStore.cs` interface

- [ ] Create `SqlEventStore.cs` implementation
  - AppendEventsAsync with optimistic concurrency
  - GetEventsAsync for replay
  - Event deserialization

- [ ] Register in DI container

---

## Phase 5: Approval Routing (Week 2)
### Tier-Based Approval Configuration

- [ ] Create `ApprovalTierConfiguration.cs`
  ```csharp
  public class ApprovalTierConfiguration
  {
      public int Tier { get; set; }
      public string Name { get; set; }
      public List<ApprovalCriteria> Criteria { get; set; }
      public List<ApproverRole> Approvers { get; set; }
      public string RoutingType { get; set; } // parallel, sequential, staged
      public TimeSpan TotalSLA { get; set; }
  }
  ```

- [ ] Create `IApprovalRouter.cs` interface

- [ ] Create `ApprovalRouter.cs` implementation
  - DetermineApprovalTier() based on document type, PII, criticality
  - GetApproversForTier()
  - CalculateDeadline()

- [ ] Create `appraisal-tiers.json` configuration file
  - Tier 1: Standard (24h, Data Steward)
  - Tier 2: Tables/Views (48h, + DBA)
  - Tier 3: Sensitive (72h, + Compliance)
  - Tier 4: Critical (120h, + Governance Lead)

- [ ] Load configuration in startup

---

## Phase 6: Compensating Transactions (Week 2)
### Rollback Logic

- [ ] Create `ICompensationService.cs` interface

- [ ] Create `CompensationService.cs`
  - [ ] `CompensateDraftCreation()` - Delete draft document
  - [ ] `CompensateApproverReservation()` - Release approver
  - [ ] `CompensateSharePointFiling()` - Delete from SharePoint
  - [ ] `CompensateMasterIndexUpdate()` - Restore previous version

- [ ] Add compensation events to event store

- [ ] Wire into state machine failure handlers

---

## Phase 7: API Endpoints (Week 3)
### Approvals Controller

- [ ] Create `Source/DocumentationAutomation.Api/Controllers/ApprovalsController.cs`

- [ ] Endpoints:
  - [ ] `GET /api/approvals/pending` - List pending approvals
  - [ ] `GET /api/approvals/{id}` - Get approval details
  - [ ] `POST /api/approvals/{id}/approve` - Approve document
  - [ ] `POST /api/approvals/{id}/reject` - Reject with reason
  - [ ] `POST /api/approvals/{id}/escalate` - Manual escalation
  - [ ] `GET /api/approvals/{id}/history` - Event history (audit trail)
  - [ ] `GET /api/approvals/metrics` - Approval metrics

- [ ] Create DTOs:
  - [ ] `ApprovalListItemDto`
  - [ ] `ApprovalDetailDto`
  - [ ] `ApproveRequestDto`
  - [ ] `RejectRequestDto`
  - [ ] `ApprovalHistoryDto`

---

## Phase 8: Agent Integration (Week 3)
### Connect Existing Agents

- [ ] **SchemaDetector** modifications:
  - [ ] Publish `IDocumentChangeDetected` to start saga
  - [ ] Include all metadata for tier determination

- [ ] **DocGenerator** modifications:
  - [ ] Subscribe to saga events (not direct topic)
  - [ ] Publish `IDraftGenerated` when complete

- [ ] **MetadataManager** modifications:
  - [ ] Subscribe to `IMasterIndexUpdated`
  - [ ] Update version tracking

- [ ] **ExcelChangeIntegrator** modifications:
  - [ ] Trigger saga for bulk imports
  - [ ] Handle batch approval routing

- [ ] Create **SharePointFilingAgent** (new):
  - [ ] Subscribe to `IFinalDocumentGenerated`
  - [ ] File to SharePoint
  - [ ] Publish `IDocumentFiled`

---

## Phase 9: Frontend (Week 3-4)
### Approval UI Components

- [ ] Create `frontend/src/app/approvals/page.tsx`
  - Pending approvals list
  - Filter by tier, status, deadline

- [ ] Create `frontend/src/app/approvals/[id]/page.tsx`
  - Document preview
  - Approval history timeline
  - Approve/Reject buttons
  - Comments input

- [ ] Create components:
  - [ ] `ApprovalCard.tsx`
  - [ ] `ApprovalTimeline.tsx`
  - [ ] `ApprovalActions.tsx`
  - [ ] `ApprovalMetrics.tsx`

- [ ] Add API hooks:
  - [ ] `useApprovals()`
  - [ ] `useApprovalDetail(id)`
  - [ ] `useApprove()`
  - [ ] `useReject()`

---

## Phase 10: Monitoring (Week 4)
### Observability

- [ ] Add OpenTelemetry metrics:
  - [ ] `documents_submitted_total`
  - [ ] `documents_approved_total`
  - [ ] `documents_rejected_total`
  - [ ] `approval_duration_hours`
  - [ ] `pending_approvals_gauge`
  - [ ] `overdue_approvals_gauge`

- [ ] Create Grafana dashboard JSON

- [ ] Add Application Insights tracking

- [ ] Create alerts:
  - [ ] Approval overdue > 4 hours
  - [ ] Saga stuck in state > 1 hour
  - [ ] High rejection rate > 30%

---

## Phase 11: Testing (Week 4)
### Test Coverage

- [ ] Unit tests:
  - [ ] `DocumentApprovalStateMachineTests.cs`
  - [ ] `ApprovalRouterTests.cs`
  - [ ] `SqlEventStoreTests.cs`
  - [ ] `CompensationServiceTests.cs`

- [ ] Integration tests:
  - [ ] Full saga flow (happy path)
  - [ ] Rejection flow
  - [ ] Timeout/escalation flow
  - [ ] Compensation flow

- [ ] Load tests:
  - [ ] 100 concurrent approvals
  - [ ] Event store performance

---

## Phase 12: Documentation (Week 4)
### Update Project Docs

- [ ] Update `ARCHITECTURE.md` with workflow orchestration section
- [ ] Create `docs/APPROVAL-WORKFLOW.md` user guide
- [ ] Add sequence diagrams for saga flows
- [ ] Document approval tier configuration
- [ ] Update API documentation (Swagger)

---

## Quick Reference: File Locations

```
Source/
├── DocumentationAutomation.Core/
│   ├── Events/
│   │   ├── IDocumentChangeDetected.cs
│   │   ├── IApprovalGranted.cs
│   │   └── ...
│   ├── EventSourcing/
│   │   ├── IDomainEvent.cs
│   │   ├── DomainEvent.cs
│   │   ├── IEventStore.cs
│   │   └── SqlEventStore.cs
│   └── Approval/
│       ├── ApprovalTierConfiguration.cs
│       ├── IApprovalRouter.cs
│       └── ApprovalRouter.cs
├── WorkflowOrchestrator/
│   ├── StateMachine/
│   │   ├── DocumentApprovalState.cs
│   │   ├── DocumentApprovalStateMachine.cs
│   │   └── DocumentApprovalStateMap.cs
│   ├── Compensation/
│   │   ├── ICompensationService.cs
│   │   └── CompensationService.cs
│   └── DbContext/
│       └── DocumentApprovalDbContext.cs
├── DocumentationAutomation.Api/
│   └── Controllers/
│       └── ApprovalsController.cs
└── SharePointFilingAgent/
    └── SharePointFilingService.cs

database/
├── migrations/
│   ├── 003_DocumentEvents.sql
│   ├── 004_DocumentApprovalState.sql
│   └── 005_ApprovalTierConfiguration.sql

frontend/
└── src/app/
    └── approvals/
        ├── page.tsx
        └── [id]/
            └── page.tsx

config/
└── approval-tiers.json
```

---

## Dependencies Between Tasks

```
Phase 1 (DB) ─────┬─────> Phase 3 (State Machine)
                  │
Phase 2 (Events) ─┴─────> Phase 4 (Event Store)
                                    │
Phase 5 (Routing) ──────────────────┤
                                    │
Phase 6 (Compensation) ─────────────┤
                                    ▼
                          Phase 7 (API) ────> Phase 9 (Frontend)
                                    │
                          Phase 8 (Agents)
                                    │
                                    ▼
                          Phase 10 (Monitoring)
                                    │
                          Phase 11 (Testing)
                                    │
                          Phase 12 (Docs)
```

---

## Estimated Hours

| Phase | Task | Hours |
|-------|------|-------|
| 1 | Database Schema | 2 |
| 2 | Domain Events | 2 |
| 3 | State Machine | 8 |
| 4 | Event Store | 4 |
| 5 | Approval Routing | 4 |
| 6 | Compensations | 3 |
| 7 | API Endpoints | 4 |
| 8 | Agent Integration | 6 |
| 9 | Frontend | 8 |
| 10 | Monitoring | 3 |
| 11 | Testing | 6 |
| 12 | Documentation | 2 |
| **Total** | | **52 hours** |

---

## Success Criteria

- [ ] Document submitted → automatically routed to correct approvers
- [ ] Approval/rejection → triggers next workflow step
- [ ] Timeout → automatic escalation
- [ ] Complete audit trail in event store
- [ ] Dashboard shows pending approvals with SLA status
- [ ] Rejected documents → feedback captured for learning
- [ ] SharePoint filing → automatic on final approval
- [ ] MasterIndex updated → saga completes
