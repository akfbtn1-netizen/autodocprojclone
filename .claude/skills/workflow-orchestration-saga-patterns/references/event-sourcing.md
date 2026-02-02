# Event Sourcing Implementation

## Core Interfaces

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    Guid AggregateId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
    int Version { get; }
}

public interface IEventStore
{
    Task AppendEventsAsync(Guid aggregateId, IEnumerable<IDomainEvent> events, int expectedVersion);
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId);
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion);
    Task<int> GetCurrentVersionAsync(Guid aggregateId);
}
```

## Base Event Class

```csharp
public abstract class DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
    public int Version { get; init; }
}
```

## Document Workflow Events

```csharp
public record DocumentChangeDetectedEvent(
    Guid AggregateId,
    string DatabaseName,
    string SchemaName,
    string ObjectName,
    string ChangeType,
    string ChangeDetails) : DomainEvent
{
    public override string EventType => "DocumentChangeDetected";
}

public record DraftGeneratedEvent(
    Guid AggregateId,
    string DraftPath,
    string GeneratedBy,
    string ContentHash) : DomainEvent
{
    public override string EventType => "DraftGenerated";
}

public record ApprovalRequestedEvent(
    Guid AggregateId,
    List<string> Approvers,
    int Tier,
    DateTime Deadline) : DomainEvent
{
    public override string EventType => "ApprovalRequested";
}

public record ApprovalGrantedEvent(
    Guid AggregateId,
    string ApprovedBy,
    string Comments,
    DateTime ApprovedAt) : DomainEvent
{
    public override string EventType => "ApprovalGranted";
}

public record ApprovalRejectedEvent(
    Guid AggregateId,
    string RejectedBy,
    string Reason,
    DateTime RejectedAt) : DomainEvent
{
    public override string EventType => "ApprovalRejected";
}

public record FinalDocumentGeneratedEvent(
    Guid AggregateId,
    string FinalPath,
    string ContentHash,
    DateTime GeneratedAt) : DomainEvent
{
    public override string EventType => "FinalDocumentGenerated";
}

public record DocumentFiledEvent(
    Guid AggregateId,
    string SharePointUrl,
    string FileId,
    DateTime FiledAt) : DomainEvent
{
    public override string EventType => "DocumentFiled";
}

public record MasterIndexUpdatedEvent(
    Guid AggregateId,
    Guid MasterIndexId,
    string PhysicalName,
    DateTime UpdatedAt) : DomainEvent
{
    public override string EventType => "MasterIndexUpdated";
}

// Compensation events
public record DraftDeletionCompensatedEvent(Guid AggregateId) : DomainEvent
{
    public override string EventType => "DraftDeletionCompensated";
}

public record MasterIndexRestoredEvent(
    Guid AggregateId,
    Guid MasterIndexId,
    int RestoredVersion) : DomainEvent
{
    public override string EventType => "MasterIndexRestored";
}
```

## SQL Server Event Store Implementation

```csharp
public class SqlEventStore : IEventStore
{
    private readonly string _connectionString;
    private readonly ILogger<SqlEventStore> _logger;
    
    private static readonly Dictionary<string, Type> EventTypeMap = new()
    {
        ["DocumentChangeDetected"] = typeof(DocumentChangeDetectedEvent),
        ["DraftGenerated"] = typeof(DraftGeneratedEvent),
        ["ApprovalRequested"] = typeof(ApprovalRequestedEvent),
        ["ApprovalGranted"] = typeof(ApprovalGrantedEvent),
        ["ApprovalRejected"] = typeof(ApprovalRejectedEvent),
        ["FinalDocumentGenerated"] = typeof(FinalDocumentGeneratedEvent),
        ["DocumentFiled"] = typeof(DocumentFiledEvent),
        ["MasterIndexUpdated"] = typeof(MasterIndexUpdatedEvent),
        ["DraftDeletionCompensated"] = typeof(DraftDeletionCompensatedEvent),
        ["MasterIndexRestored"] = typeof(MasterIndexRestoredEvent)
    };
    
    public SqlEventStore(string connectionString, ILogger<SqlEventStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }
    
    public async Task AppendEventsAsync(
        Guid aggregateId, 
        IEnumerable<IDomainEvent> events, 
        int expectedVersion)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        
        try
        {
            // Optimistic concurrency check
            var currentVersion = await GetCurrentVersionInternalAsync(connection, transaction, aggregateId);
            
            if (currentVersion != expectedVersion)
            {
                throw new ConcurrencyException(
                    $"Concurrency conflict for aggregate {aggregateId}. " +
                    $"Expected version {expectedVersion}, found {currentVersion}");
            }
            
            var version = expectedVersion;
            foreach (var @event in events)
            {
                version++;
                await InsertEventAsync(connection, transaction, @event, version);
                _logger.LogDebug("Appended event {EventType} v{Version} for aggregate {AggregateId}",
                    @event.EventType, version, aggregateId);
            }
            
            await transaction.CommitAsync();
            _logger.LogInformation("Committed {Count} events for aggregate {AggregateId}",
                events.Count(), aggregateId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    private async Task InsertEventAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IDomainEvent @event,
        int version)
    {
        const string sql = @"
            INSERT INTO DocumentEvents (
                EventId, AggregateId, EventType, EventData, 
                Version, OccurredAt, CreatedAt
            ) VALUES (
                @EventId, @AggregateId, @EventType, @EventData,
                @Version, @OccurredAt, GETUTCDATE()
            )";
        
        var eventData = JsonSerializer.Serialize(@event, @event.GetType(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await connection.ExecuteAsync(sql, new
        {
            @event.EventId,
            @event.AggregateId,
            @event.EventType,
            EventData = eventData,
            Version = version,
            @event.OccurredAt
        }, transaction);
    }
    
    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId)
    {
        return await GetEventsAsync(aggregateId, fromVersion: 0);
    }
    
    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(Guid aggregateId, int fromVersion)
    {
        await using var connection = new SqlConnection(_connectionString);
        
        const string sql = @"
            SELECT EventType, EventData, Version 
            FROM DocumentEvents 
            WHERE AggregateId = @AggregateId AND Version > @FromVersion
            ORDER BY Version";
        
        var rows = await connection.QueryAsync<(string EventType, string EventData, int Version)>(
            sql, new { AggregateId = aggregateId, FromVersion = fromVersion });
        
        return rows.Select(row => DeserializeEvent(row.EventType, row.EventData)).ToList();
    }
    
    public async Task<int> GetCurrentVersionAsync(Guid aggregateId)
    {
        await using var connection = new SqlConnection(_connectionString);
        return await GetCurrentVersionInternalAsync(connection, null, aggregateId);
    }
    
    private async Task<int> GetCurrentVersionInternalAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid aggregateId)
    {
        const string sql = @"
            SELECT ISNULL(MAX(Version), 0) 
            FROM DocumentEvents 
            WHERE AggregateId = @AggregateId";
        
        return await connection.ExecuteScalarAsync<int>(sql, new { AggregateId = aggregateId }, transaction);
    }
    
    private IDomainEvent DeserializeEvent(string eventType, string eventData)
    {
        if (!EventTypeMap.TryGetValue(eventType, out var type))
        {
            throw new InvalidOperationException($"Unknown event type: {eventType}");
        }
        
        var @event = JsonSerializer.Deserialize(eventData, type, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        return (IDomainEvent)@event!;
    }
}

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}
```

## Aggregate Root Pattern

```csharp
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _uncommittedEvents = new();
    
    public Guid Id { get; protected set; }
    public int Version { get; protected set; } = -1;
    
    public IReadOnlyList<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents.AsReadOnly();
    
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();
    
    protected void RaiseEvent(IDomainEvent @event)
    {
        ApplyEvent(@event);
        _uncommittedEvents.Add(@event);
    }
    
    protected abstract void ApplyEvent(IDomainEvent @event);
    
    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var @event in history)
        {
            ApplyEvent(@event);
            Version++;
        }
    }
}

public class DocumentApprovalAggregate : AggregateRoot
{
    public string CurrentState { get; private set; } = "Initial";
    public string PhysicalName { get; private set; }
    public List<string> CompletedApprovers { get; private set; } = new();
    public DateTime? ApprovedAt { get; private set; }
    
    protected override void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case DocumentChangeDetectedEvent e:
                Id = e.AggregateId;
                PhysicalName = $"{e.SchemaName}.{e.ObjectName}";
                CurrentState = "DraftPending";
                break;
            case DraftGeneratedEvent:
                CurrentState = "AwaitingApproval";
                break;
            case ApprovalGrantedEvent e:
                CompletedApprovers.Add(e.ApprovedBy);
                ApprovedAt = e.ApprovedAt;
                break;
            case FinalDocumentGeneratedEvent:
                CurrentState = "Filing";
                break;
            case MasterIndexUpdatedEvent:
                CurrentState = "Completed";
                break;
        }
    }
}
```

## Read Model Projections

```csharp
public interface IProjection
{
    Task ProjectAsync(IDomainEvent @event);
}

public class ApprovalDashboardProjection : IProjection
{
    private readonly string _connectionString;
    
    public async Task ProjectAsync(IDomainEvent @event)
    {
        switch (@event)
        {
            case DocumentChangeDetectedEvent e:
                await InsertPendingApproval(e);
                break;
            case ApprovalGrantedEvent e:
                await UpdateApprovalProgress(e);
                break;
            case MasterIndexUpdatedEvent e:
                await MarkCompleted(e);
                break;
        }
    }
    
    private async Task InsertPendingApproval(DocumentChangeDetectedEvent e)
    {
        const string sql = @"
            INSERT INTO DocumentApprovalProjection (
                DocumentId, CurrentState, PhysicalName, DatabaseName,
                SubmittedAt, LastEventVersion, LastUpdatedAt
            ) VALUES (
                @DocumentId, 'DraftPending', @PhysicalName, @DatabaseName,
                @OccurredAt, @Version, GETUTCDATE()
            )";
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new
        {
            DocumentId = e.AggregateId,
            PhysicalName = $"{e.SchemaName}.{e.ObjectName}",
            e.DatabaseName,
            e.OccurredAt,
            e.Version
        });
    }
}
```

## Replaying Events (Recovery)

```csharp
public class EventReplayer
{
    private readonly IEventStore _eventStore;
    private readonly IEnumerable<IProjection> _projections;
    
    public async Task RebuildProjectionsAsync(Guid aggregateId)
    {
        var events = await _eventStore.GetEventsAsync(aggregateId);
        
        foreach (var @event in events)
        {
            foreach (var projection in _projections)
            {
                await projection.ProjectAsync(@event);
            }
        }
    }
    
    public async Task ReplayFromVersionAsync(Guid aggregateId, int fromVersion)
    {
        var events = await _eventStore.GetEventsAsync(aggregateId, fromVersion);
        
        foreach (var @event in events)
        {
            foreach (var projection in _projections)
            {
                await projection.ProjectAsync(@event);
            }
        }
    }
}
```

## DI Registration

```csharp
services.AddSingleton<IEventStore>(sp =>
    new SqlEventStore(
        connectionString,
        sp.GetRequiredService<ILogger<SqlEventStore>>()));

services.AddScoped<IProjection, ApprovalDashboardProjection>();
services.AddScoped<EventReplayer>();
```
