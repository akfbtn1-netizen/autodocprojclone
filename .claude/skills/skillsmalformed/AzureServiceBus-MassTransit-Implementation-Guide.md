# Azure Service Bus + MassTransit - Implementation Guide

## Quick Start Checklist

```
□ Azure Service Bus namespace created (Standard or Premium tier)
□ Connection string available
□ NuGet packages installed
□ MassTransit configured in Program.cs
□ Message contracts defined
□ Consumers implemented
□ Health checks configured
```

---

## Step 1: Package Installation

```xml
<!-- Add to your .csproj file -->
<ItemGroup>
  <!-- Core MassTransit -->
  <PackageReference Include="MassTransit" Version="8.2.5" />
  
  <!-- Azure Service Bus Transport -->
  <PackageReference Include="MassTransit.Azure.ServiceBus.Core" Version="8.2.5" />
  
  <!-- For Saga persistence (optional) -->
  <PackageReference Include="MassTransit.EntityFrameworkCore" Version="8.2.5" />
  
  <!-- For health checks -->
  <PackageReference Include="AspNetCore.HealthChecks.AzureServiceBus" Version="8.0.1" />
</ItemGroup>
```

Or via CLI:
```bash
dotnet add package MassTransit
dotnet add package MassTransit.Azure.ServiceBus.Core
dotnet add package MassTransit.EntityFrameworkCore
```

---

## Step 2: Basic Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key"
  }
}
```

### Program.cs - Minimal Setup
```csharp
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    // Use kebab-case for endpoint names (order-created, not OrderCreated)
    x.SetKebabCaseEndpointNameFormatter();
    
    // Auto-register all consumers in this assembly
    x.AddConsumers(typeof(Program).Assembly);
    
    // Configure Azure Service Bus
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("AzureServiceBus"));
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
app.Run();
```

### Program.cs - Production Setup
```csharp
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    
    // Register specific consumers
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<PaymentProcessedConsumer>();
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("AzureServiceBus"), h =>
        {
            h.RetryLimit = 3;
            h.RetryMinBackoff = TimeSpan.FromSeconds(1);
            h.RetryMaxBackoff = TimeSpan.FromSeconds(30);
        });
        
        // Global retry policy
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30)
        ));
        
        // Circuit breaker
        cfg.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 10;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });
        
        cfg.ConfigureEndpoints(context);
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddAzureServiceBusTopic(
        builder.Configuration.GetConnectionString("AzureServiceBus")!,
        "order-events",
        name: "servicebus");
```

---

## Step 3: Define Message Contracts

### Commands (One Handler)
```csharp
namespace Contracts.Commands;

// Commands use imperative verbs: Create, Process, Reserve, Send
public record CreateOrder
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Amount { get; init; }
    public DateTime RequestedAt { get; init; }
}

public record ProcessPayment
{
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
}
```

### Events (Multiple Handlers)
```csharp
namespace Contracts.Events;

// Events use past tense: Created, Processed, Reserved, Sent
public record OrderCreated
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal Amount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record PaymentProcessed
{
    public Guid OrderId { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public DateTime ProcessedAt { get; init; }
}
```

### Correlated Messages (For Sagas)
```csharp
using MassTransit;

public record OrderCreated : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
}
```

---

## Step 4: Implement Consumers

### Basic Consumer
```csharp
using MassTransit;

public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IOrderRepository _repository;

    public OrderCreatedConsumer(
        ILogger<OrderCreatedConsumer> logger,
        IOrderRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Processing OrderCreated: {OrderId}",
            message.OrderId);

        try
        {
            // Your business logic
            await _repository.SaveAsync(message);
            
            // Optionally publish follow-up event
            await context.Publish(new OrderProcessingStarted
            {
                OrderId = message.OrderId,
                StartedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to process OrderCreated: {OrderId}", 
                message.OrderId);
            throw; // Rethrow to trigger retry/DLQ
        }
    }
}
```

### Consumer with Custom Configuration
```csharp
public class OrderCreatedConsumerDefinition : ConsumerDefinition<OrderCreatedConsumer>
{
    public OrderCreatedConsumerDefinition()
    {
        EndpointName = "order-processing";
        ConcurrentMessageLimit = 10;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Custom retry for this consumer only
        endpointConfigurator.UseMessageRetry(r => 
            r.Intervals(500, 1000, 5000));
    }
}

// Register with definition
x.AddConsumer<OrderCreatedConsumer, OrderCreatedConsumerDefinition>();
```

---

## Step 5: Publishing and Sending Messages

### Publish Events (Goes to Topic - Multiple Subscribers)
```csharp
public class OrderService
{
    private readonly IPublishEndpoint _publishEndpoint;

    public OrderService(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        // Save to database
        var order = await SaveOrderAsync(request);
        
        // Publish event - ALL subscribers receive this
        await _publishEndpoint.Publish(new OrderCreated
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Amount = order.Amount,
            CreatedAt = DateTime.UtcNow
        });
    }
}
```

### Send Commands (Goes to Queue - Single Handler)
```csharp
public class PaymentController : ControllerBase
{
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public PaymentController(ISendEndpointProvider sendEndpointProvider)
    {
        _sendEndpointProvider = sendEndpointProvider;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessPayment(PaymentRequest request)
    {
        // Get endpoint for specific queue
        var endpoint = await _sendEndpointProvider
            .GetSendEndpoint(new Uri("queue:process-payment"));
        
        // Send command - ONE handler receives this
        await endpoint.Send(new ProcessPayment
        {
            OrderId = request.OrderId,
            Amount = request.Amount
        });

        return Accepted();
    }
}
```

### Request/Response Pattern
```csharp
// Register request client in Program.cs
x.AddRequestClient<GetOrderStatus>();

// Use in service
public class OrderQueryService
{
    private readonly IRequestClient<GetOrderStatus> _client;

    public async Task<OrderStatusResult> GetStatusAsync(Guid orderId)
    {
        var response = await _client.GetResponse<OrderStatusResult>(
            new GetOrderStatus { OrderId = orderId },
            timeout: TimeSpan.FromSeconds(30));
        
        return response.Message;
    }
}

// Consumer responds
public class GetOrderStatusConsumer : IConsumer<GetOrderStatus>
{
    public async Task Consume(ConsumeContext<GetOrderStatus> context)
    {
        var status = await GetStatusFromDb(context.Message.OrderId);
        
        await context.RespondAsync(new OrderStatusResult
        {
            OrderId = context.Message.OrderId,
            Status = status
        });
    }
}
```

---

## Step 6: Saga State Machine

### Define State Class
```csharp
public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    
    // Order data
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Tracking
    public bool PaymentReceived { get; set; }
    public string? FailureReason { get; set; }
    
    // Concurrency (EF Core)
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

### Define State Machine
```csharp
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State Submitted { get; private set; } = null!;
    public State PaymentPending { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmittedEvent { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompletedEvent { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailedEvent { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        // Configure event correlation
        Event(() => OrderSubmittedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentCompletedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailedEvent, x => x.CorrelateById(m => m.Message.OrderId));

        // Initial state
        Initially(
            When(OrderSubmittedEvent)
                .Then(ctx =>
                {
                    ctx.Saga.CustomerId = ctx.Message.CustomerId;
                    ctx.Saga.TotalAmount = ctx.Message.Amount;
                    ctx.Saga.CreatedAt = DateTime.UtcNow;
                })
                .SendAsync(new Uri("queue:process-payment"),
                    ctx => ctx.Init<ProcessPayment>(new
                    {
                        OrderId = ctx.Saga.CorrelationId,
                        Amount = ctx.Saga.TotalAmount
                    }))
                .TransitionTo(PaymentPending)
        );

        // Payment pending state
        During(PaymentPending,
            When(PaymentCompletedEvent)
                .Then(ctx => ctx.Saga.PaymentReceived = true)
                .PublishAsync(ctx => ctx.Init<OrderCompleted>(new
                {
                    OrderId = ctx.Saga.CorrelationId
                }))
                .TransitionTo(Completed)
                .Finalize(),
            
            When(PaymentFailedEvent)
                .Then(ctx => ctx.Saga.FailureReason = ctx.Message.Reason)
                .PublishAsync(ctx => ctx.Init<OrderFailed>(new
                {
                    OrderId = ctx.Saga.CorrelationId,
                    Reason = ctx.Message.Reason
                }))
                .TransitionTo(Faulted)
        );

        SetCompletedWhenFinalized();
    }
}
```

### Register Saga with EF Core Persistence
```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.AddDbContext<DbContext, OrderSagaDbContext>((provider, builder) =>
            {
                builder.UseSqlServer(connectionString);
            });
        });
});
```

---

## Step 7: Dead Letter Queue Handling

### DLQ Consumer
```csharp
public class OrderCreatedDlqConsumer : IConsumer<OrderCreated>
{
    private readonly ILogger<OrderCreatedDlqConsumer> _logger;

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var reason = context.Headers.Get<string>("DeadLetterReason");
        var error = context.Headers.Get<string>("DeadLetterErrorDescription");
        
        _logger.LogWarning(
            "DLQ Message - OrderId: {OrderId}, Reason: {Reason}, Error: {Error}",
            context.Message.OrderId,
            reason,
            error);

        // Store for analysis, alert ops team, etc.
        await SaveDeadLetterAsync(context.Message, reason, error);
    }
}

// Register DLQ endpoint
x.UsingAzureServiceBus((context, cfg) =>
{
    cfg.ReceiveEndpoint("order-created/$deadletterqueue", e =>
    {
        e.ConfigureConsumer<OrderCreatedDlqConsumer>(context);
    });
});
```

---

## Step 8: Outbox Pattern (Reliable Messaging)

```csharp
// Add EF Core Outbox for guaranteed delivery
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(1);
    });
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString);
        
        cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
        });
        
        cfg.ConfigureEndpoints(context);
    });
});
```

---

## Step 9: Testing

### Unit Test with Test Harness
```csharp
public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task Should_consume_order_created()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderCreatedConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Publish test message
        await harness.Bus.Publish(new OrderCreated
        {
            OrderId = Guid.NewGuid(),
            Amount = 100.00m
        });

        // Assert
        Assert.True(await harness.Consumed.Any<OrderCreated>());
    }
}
```

---

## Quick Reference

### Publish vs Send
| Method | Target | Receivers | Use Case |
|--------|--------|-----------|----------|
| `Publish` | Topic | 0 to many | Events, notifications |
| `Send` | Queue | Exactly 1 | Commands, requests |

### Message Naming
| Type | Convention | Example |
|------|------------|---------|
| Command | Verb + Noun | `CreateOrder`, `ProcessPayment` |
| Event | Noun + Verb (past) | `OrderCreated`, `PaymentProcessed` |

### Endpoint Naming
```
Consumer: OrderCreatedConsumer → queue: order-created
Saga: OrderStateMachine → queue: order-state
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Messages not being consumed | Check endpoint name matches, verify connection string |
| Retry not working | Add `-ErrorAction Stop` equivalent - throw exceptions |
| Saga not persisting | Verify EF Core DbContext registered, run migrations |
| DLQ filling up | Check consumer for unhandled exceptions |
| Duplicate messages | Implement idempotency, use outbox pattern |

---

## Production Checklist

```
□ Connection string in secure configuration (Key Vault)
□ Retry policy configured with exponential backoff
□ Circuit breaker enabled
□ Dead letter queue monitoring/alerting
□ Health checks exposed
□ Structured logging with correlation IDs
□ Outbox pattern for critical operations
□ Saga persistence with optimistic concurrency
□ Integration tests passing
□ Load testing completed
```

---

## Version History

- 1.0.0 (2026-01-03): Initial implementation guide
