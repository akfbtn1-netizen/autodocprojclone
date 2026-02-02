---
name: workflow-orchestration-saga-patterns
description: |
  Enterprise workflow orchestration using saga patterns, state machines, event sourcing,
  and human-in-the-loop approvals. Use for document approval workflows, distributed
  transactions, compensating transactions, multi-agent coordination, and audit trails.
  Triggers: saga pattern, workflow orchestration, state machine, document approval,
  compensating transaction, event sourcing, distributed transaction, approval routing,
  human-in-the-loop, durable functions, MassTransit.
---

# Workflow Orchestration & Saga Patterns

Enterprise-grade workflow orchestration for document approval systems using MassTransit state machines, event sourcing, and human-in-the-loop patterns.

## When to Use

- Document approval workflows (Draft → Review → Approval → Final → Filing)
- Distributed transactions across microservices
- Human-in-the-loop approval checkpoints
- Audit trails with event sourcing
- Multi-agent AI system coordination
- Long-running business processes with compensation

## Quick Reference

### Technology Stack
- **Primary**: MassTransit State Machines + Azure Service Bus
- **Persistence**: Entity Framework Core + SQL Server
- **Audit Trail**: Event Sourcing pattern
- **Alternative**: Azure Durable Functions (serverless)

### Document Workflow States
```
Initial → DraftPending → AwaitingApproval → (Approved | Rejected | Escalated) 
→ GeneratingFinal → Filing → UpdatingMasterIndex → Completed
```

### Approval Tiers
| Tier | SLA | Approvers | Criteria |
|------|-----|-----------|----------|
| 1 | 24h | Data Steward | Standard schema changes |
| 2 | 48h | + DBA | Tables, Views, Procs |
| 3 | 72h | + Compliance | PII/Financial data |
| 4 | 120h | + Governance Lead | Critical systems |

## Implementation Pattern

### 1. Define State Machine (MassTransit)

```csharp
public class DocumentApprovalState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public string DocumentType { get; set; }
    public string PhysicalName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<string> RequiredApprovers { get; set; }
    public List<string> CompletedApprovers { get; set; }
    public int ApprovalTier { get; set; }
    public Guid? ExpirationTokenId { get; set; }
    public byte[] RowVersion { get; set; }
}

public class DocumentApprovalStateMachine : MassTransitStateMachine<DocumentApprovalState>
{
    public DocumentApprovalStateMachine()
    {
        InstanceState(x => x.CurrentState);
        
        // Configure events with correlation
        Event(() => DocumentChangeDetected, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => ApprovalGranted, x => x.CorrelateById(m => m.Message.DocumentId));
        
        // Configure timeout
        Schedule(() => ApprovalTimeout, x => x.ExpirationTokenId, x =>
        {
            x.Delay = TimeSpan.FromHours(48);
            x.Received = e => e.CorrelateById(m => m.Message.DocumentId);
        });
        
        // State transitions - see references/state-machine-full.md for complete implementation
        Initially(
            When(DocumentChangeDetected)
                .Then(ctx => ctx.Saga.SubmittedAt = DateTime.UtcNow)
                .PublishAsync(ctx => ctx.Init<IGenerateDraftCommand>(new { DocumentId = ctx.Saga.CorrelationId }))
                .TransitionTo(DraftPending)
        );
        
        During(AwaitingApproval,
            When(ApprovalGranted)
                .IfElse(ctx => AllApproversComplete(ctx),
                    approved => approved.Unschedule(ApprovalTimeout).TransitionTo(GeneratingFinal),
                    pending => pending),
            When(ApprovalTimeout.Received)
                .PublishAsync(ctx => ctx.Init<IEscalateApproval>(new { DocumentId = ctx.Saga.CorrelationId }))
                .TransitionTo(Escalated)
        );
    }
    
    public State DraftPending { get; private set; }
    public State AwaitingApproval { get; private set; }
    public State Escalated { get; private set; }
    public State GeneratingFinal { get; private set; }
    // ... additional states
}
```

### 2. Configure Persistence (EF Core)

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

### 3. Event Sourcing for Audit

```csharp
public interface IEventStore
{
    Task AppendEventsAsync(Guid aggregateId, IEnumerable<IDomainEvent> events, int expectedVersion);
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId);
}

// All state changes recorded as immutable events
await _eventStore.AppendEventsAsync(documentId, new[]
{
    new ApprovalGrantedEvent(documentId, approvedBy, comments, DateTime.UtcNow)
}, expectedVersion: currentVersion);
```

### 4. Compensating Transactions

Each saga step has a rollback action:

| Step | Action | Compensation |
|------|--------|--------------|
| 1 | Generate Draft | Delete Draft |
| 2 | Reserve Approver | Release Approver |
| 3 | File to SharePoint | Delete from SharePoint |
| 4 | Update MasterIndex | Restore Previous Version |

```csharp
public async Task CompensateMasterIndexUpdate(Guid masterIndexId)
{
    var previousVersion = await _versionService.GetPreviousVersion(masterIndexId);
    await _masterIndexService.RestoreVersion(masterIndexId, previousVersion);
    await _eventStore.AppendEvent(new MasterIndexRestored(masterIndexId));
}
```

### 5. Human-in-the-Loop (Confidence Routing)

```csharp
public async Task<RoutingDecision> DetermineRouting(Guid documentId, string content)
{
    var confidence = await _aiService.EvaluateDocumentQuality(content);
    
    if (confidence.Score >= 0.95 && !confidence.HasSensitiveContent)
        return new RoutingDecision { Route = ApprovalRoute.AutoApprove };
    
    if (confidence.Score >= 0.80)
        return new RoutingDecision { Route = ApprovalRoute.SingleApprover };
    
    return new RoutingDecision { Route = ApprovalRoute.FullChain };
}
```

## Required NuGet Packages

```xml
<PackageReference Include="MassTransit" Version="8.*" />
<PackageReference Include="MassTransit.Azure.ServiceBus.Core" Version="8.*" />
<PackageReference Include="MassTransit.EntityFrameworkCore" Version="8.*" />
```

## Database Tables

See `references/sql-schemas.md` for complete schemas:
- `DocumentEvents` - Event sourcing store
- `DocumentApprovalState` - Saga persistence
- `ApprovalTierConfiguration` - Tier rules

## References

- `references/state-machine-full.md` - Complete MassTransit implementation
- `references/event-sourcing.md` - Event store patterns
- `references/sql-schemas.md` - Database schemas
- `references/durable-functions.md` - Azure serverless alternative

## Key Success Factors

1. **Idempotency** - All operations safely retriable
2. **Explicit States** - Clear visibility for debugging
3. **Timeout Handling** - Auto-escalation at deadline
4. **Event Logging** - Complete audit trail
5. **Compensation** - Rollback for every step
