# Complete MassTransit State Machine Implementation

## Full State Machine

```csharp
public class DocumentApprovalStateMachine : MassTransitStateMachine<DocumentApprovalState>
{
    public DocumentApprovalStateMachine()
    {
        InstanceState(x => x.CurrentState);
        
        // Event correlations
        Event(() => DocumentChangeDetected, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => DraftGenerated, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => ApprovalRequested, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => ApprovalGranted, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => ApprovalRejected, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => FinalDocumentGenerated, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => DocumentFiled, x => x.CorrelateById(m => m.Message.DocumentId));
        Event(() => MasterIndexUpdated, x => x.CorrelateById(m => m.Message.DocumentId));
        
        // Timeout schedule
        Schedule(() => ApprovalTimeout, x => x.ExpirationTokenId, x =>
        {
            x.Delay = TimeSpan.FromHours(48);
            x.Received = e => e.CorrelateById(m => m.Message.DocumentId);
        });
        
        // INITIAL STATE
        Initially(
            When(DocumentChangeDetected)
                .Then(ctx =>
                {
                    ctx.Saga.SubmittedAt = DateTime.UtcNow;
                    ctx.Saga.DocumentType = ctx.Message.DocumentType;
                    ctx.Saga.PhysicalName = ctx.Message.PhysicalName;
                    ctx.Saga.DatabaseName = ctx.Message.DatabaseName;
                })
                .PublishAsync(ctx => ctx.Init<IGenerateDraftCommand>(new
                {
                    DocumentId = ctx.Saga.CorrelationId,
                    ctx.Message.PhysicalName,
                    ctx.Message.DatabaseName
                }))
                .TransitionTo(DraftPending)
        );
        
        // DRAFT PENDING STATE
        During(DraftPending,
            When(DraftGenerated)
                .Then(ctx => 
                {
                    ctx.Saga.ApprovalTier = DetermineApprovalTier(ctx.Message);
                    ctx.Saga.RequiredApprovers = GetApproversForTier(ctx.Saga.ApprovalTier);
                    ctx.Saga.CompletedApprovers = new List<string>();
                })
                .Schedule(ApprovalTimeout, ctx => ctx.Init<IApprovalTimeoutExpired>(new
                {
                    DocumentId = ctx.Saga.CorrelationId
                }))
                .PublishAsync(ctx => ctx.Init<IRequestApprovalCommand>(new
                {
                    DocumentId = ctx.Saga.CorrelationId,
                    Approvers = ctx.Saga.RequiredApprovers,
                    Tier = ctx.Saga.ApprovalTier,
                    Deadline = DateTime.UtcNow.AddHours(GetSlaHours(ctx.Saga.ApprovalTier))
                }))
                .TransitionTo(AwaitingApproval)
        );
        
        // AWAITING APPROVAL STATE
        During(AwaitingApproval,
            When(ApprovalGranted)
                .Then(ctx =>
                {
                    ctx.Saga.CompletedApprovers.Add(ctx.Message.ApprovedBy);
                })
                .IfElse(
                    ctx => ctx.Saga.CompletedApprovers.Count >= ctx.Saga.RequiredApprovers.Count,
                    approved => approved
                        .Unschedule(ApprovalTimeout)
                        .Then(ctx =>
                        {
                            ctx.Saga.ApprovedAt = DateTime.UtcNow;
                            ctx.Saga.ApprovedBy = string.Join(", ", ctx.Saga.CompletedApprovers);
                        })
                        .PublishAsync(ctx => ctx.Init<IGenerateFinalDocumentCommand>(new
                        {
                            DocumentId = ctx.Saga.CorrelationId,
                            ApprovedBy = ctx.Saga.ApprovedBy,
                            ApprovedAt = ctx.Saga.ApprovedAt
                        }))
                        .TransitionTo(GeneratingFinal),
                    pending => pending
                ),
            
            When(ApprovalRejected)
                .Unschedule(ApprovalTimeout)
                .PublishAsync(ctx => ctx.Init<INotifyRejection>(new
                {
                    DocumentId = ctx.Saga.CorrelationId,
                    RejectedBy = ctx.Message.RejectedBy,
                    Reason = ctx.Message.Reason
                }))
                .TransitionTo(Rejected),
            
            When(ApprovalTimeout.Received)
                .PublishAsync(ctx => ctx.Init<IEscalateApproval>(new
                {
                    DocumentId = ctx.Saga.CorrelationId,
                    PendingApprovers = ctx.Saga.RequiredApprovers
                        .Except(ctx.Saga.CompletedApprovers).ToList()
                }))
                .TransitionTo(Escalated)
        );
        
        // GENERATING FINAL STATE
        During(GeneratingFinal,
            When(FinalDocumentGenerated)
                .PublishAsync(ctx => ctx.Init<IFileToSharePointCommand>(new
                {
                    DocumentId = ctx.Saga.CorrelationId,
                    FinalPath = ctx.Message.FinalPath
                }))
                .TransitionTo(Filing)
        );
        
        // FILING STATE
        During(Filing,
            When(DocumentFiled)
                .PublishAsync(ctx => ctx.Init<IUpdateMasterIndexCommand>(new
                {
                    DocumentId = ctx.Saga.CorrelationId,
                    SharePointUrl = ctx.Message.SharePointUrl
                }))
                .TransitionTo(UpdatingMasterIndex)
        );
        
        // UPDATING MASTER INDEX STATE
        During(UpdatingMasterIndex,
            When(MasterIndexUpdated)
                .Finalize()
        );
        
        SetCompletedWhenFinalized();
    }
    
    // States
    public State DraftPending { get; private set; }
    public State AwaitingApproval { get; private set; }
    public State Escalated { get; private set; }
    public State Rejected { get; private set; }
    public State GeneratingFinal { get; private set; }
    public State Filing { get; private set; }
    public State UpdatingMasterIndex { get; private set; }
    
    // Events
    public Event<IDocumentChangeDetected> DocumentChangeDetected { get; private set; }
    public Event<IDraftGenerated> DraftGenerated { get; private set; }
    public Event<IApprovalRequested> ApprovalRequested { get; private set; }
    public Event<IApprovalGranted> ApprovalGranted { get; private set; }
    public Event<IApprovalRejected> ApprovalRejected { get; private set; }
    public Event<IFinalDocumentGenerated> FinalDocumentGenerated { get; private set; }
    public Event<IDocumentFiled> DocumentFiled { get; private set; }
    public Event<IMasterIndexUpdated> MasterIndexUpdated { get; private set; }
    
    // Schedule
    public Schedule<DocumentApprovalState, IApprovalTimeoutExpired> ApprovalTimeout { get; private set; }
    
    // Helper methods
    private static int DetermineApprovalTier(IDraftGenerated message)
    {
        // Tier determination logic based on document characteristics
        if (message.ContainsPII || message.DataClassification == "Restricted")
            return 3;
        if (message.DatabaseCriticality == "Critical")
            return 4;
        if (message.ObjectType == "Table" || message.ObjectType == "View")
            return 2;
        return 1;
    }
    
    private static List<string> GetApproversForTier(int tier) => tier switch
    {
        1 => new List<string> { "data-steward" },
        2 => new List<string> { "data-steward", "dba" },
        3 => new List<string> { "data-steward", "dba", "compliance-officer" },
        4 => new List<string> { "data-steward", "dba", "compliance-officer", "data-governance-lead" },
        _ => new List<string> { "data-steward" }
    };
    
    private static int GetSlaHours(int tier) => tier switch
    {
        1 => 24,
        2 => 48,
        3 => 72,
        4 => 120,
        _ => 48
    };
}
```

## Event Contracts

```csharp
// Events that drive state transitions
public interface IDocumentChangeDetected
{
    Guid DocumentId { get; }
    string DocumentType { get; }
    string PhysicalName { get; }
    string DatabaseName { get; }
    string ChangeType { get; }
}

public interface IDraftGenerated
{
    Guid DocumentId { get; }
    string DraftPath { get; }
    string ObjectType { get; }
    bool ContainsPII { get; }
    string DataClassification { get; }
    string DatabaseCriticality { get; }
}

public interface IApprovalRequested
{
    Guid DocumentId { get; }
    List<string> Approvers { get; }
    int Tier { get; }
    DateTime Deadline { get; }
}

public interface IApprovalGranted
{
    Guid DocumentId { get; }
    string ApprovedBy { get; }
    string Comments { get; }
    DateTime ApprovedAt { get; }
}

public interface IApprovalRejected
{
    Guid DocumentId { get; }
    string RejectedBy { get; }
    string Reason { get; }
}

public interface IFinalDocumentGenerated
{
    Guid DocumentId { get; }
    string FinalPath { get; }
}

public interface IDocumentFiled
{
    Guid DocumentId { get; }
    string SharePointUrl { get; }
}

public interface IMasterIndexUpdated
{
    Guid DocumentId { get; }
    Guid MasterIndexId { get; }
}

public interface IApprovalTimeoutExpired
{
    Guid DocumentId { get; }
}
```

## Commands

```csharp
public interface IGenerateDraftCommand
{
    Guid DocumentId { get; }
    string PhysicalName { get; }
    string DatabaseName { get; }
}

public interface IRequestApprovalCommand
{
    Guid DocumentId { get; }
    List<string> Approvers { get; }
    int Tier { get; }
    DateTime Deadline { get; }
}

public interface IGenerateFinalDocumentCommand
{
    Guid DocumentId { get; }
    string ApprovedBy { get; }
    DateTime? ApprovedAt { get; }
}

public interface IFileToSharePointCommand
{
    Guid DocumentId { get; }
    string FinalPath { get; }
}

public interface IUpdateMasterIndexCommand
{
    Guid DocumentId { get; }
    string SharePointUrl { get; }
}

public interface IEscalateApproval
{
    Guid DocumentId { get; }
    List<string> PendingApprovers { get; }
}

public interface INotifyRejection
{
    Guid DocumentId { get; }
    string RejectedBy { get; }
    string Reason { get; }
}
```

## Entity Framework Configuration

```csharp
public class DocumentApprovalStateMap : SagaClassMap<DocumentApprovalState>
{
    protected override void Configure(EntityTypeBuilder<DocumentApprovalState> entity, ModelBuilder model)
    {
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.DocumentType).HasMaxLength(100);
        entity.Property(x => x.PhysicalName).HasMaxLength(500);
        entity.Property(x => x.DatabaseName).HasMaxLength(128);
        entity.Property(x => x.SubmittedBy).HasMaxLength(100);
        entity.Property(x => x.ApprovedBy).HasMaxLength(500);
        
        entity.Property(x => x.RequiredApprovers).HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null));
        
        entity.Property(x => x.CompletedApprovers).HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null));
        
        entity.Property(x => x.RowVersion).IsRowVersion();
    }
}

public class DocumentApprovalDbContext : SagaDbContext
{
    public DocumentApprovalDbContext(DbContextOptions options) : base(options) { }
    
    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new DocumentApprovalStateMap(); }
    }
}
```

## Registration

```csharp
// Program.cs or Startup.cs
services.AddDbContext<DocumentApprovalDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<DocumentApprovalStateMachine, DocumentApprovalState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<DocumentApprovalDbContext>();
            r.UseSqlServer();
        });
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(Configuration["ServiceBus:ConnectionString"]);
        cfg.ConfigureEndpoints(context);
    });
});
```
