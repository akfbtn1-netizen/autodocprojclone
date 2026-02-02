---
name: azure-servicebus-masstransit
description: Enterprise messaging patterns with Azure Service Bus and MassTransit for .NET 8+. Covers topics/subscriptions, dead letter queues, saga orchestration, retry patterns, and event-driven architecture. Use when implementing event-driven microservices, saga orchestration, dead letter queue handling, or message retry patterns.
license: MIT
---

# Azure Service Bus + MassTransit Patterns Skill

Production-ready messaging patterns for distributed .NET applications using Azure Service Bus and MassTransit, including saga orchestration, dead letter queue handling, and resilience patterns.

## When to Use This Skill

Activate when:
- Implementing event-driven microservices architecture
- Setting up Azure Service Bus topics and subscriptions
- Building saga/workflow orchestration with MassTransit
- Handling dead letter queues (DLQ)
- Implementing retry and circuit breaker patterns for messaging
- Designing message contracts and routing
- Configuring MassTransit consumers and producers

## Section 1: Azure Service Bus Fundamentals

### 1.1 Core Concepts

```
┌─────────────────────────────────────────────────────────────────┐
│                    Azure Service Bus Namespace                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────┐     Point-to-Point (Queue)                        │
│  │  Queue   │◄────── One message → One consumer                 │
│  └──────────┘                                                   │
│                                                                  │
│  ┌──────────┐     Pub/Sub (Topic)                               │
│  │  Topic   │                                                    │
│  └────┬─────┘                                                   │
│       │                                                          │
│       ├──► Subscription A (Filter: Type='OrderCreated')         │
│       ├──► Subscription B (Filter: Priority='High')             │
│       └──► Subscription C (All messages)                        │
│                                                                  │
│  Dead Letter Queues (DLQ)                                       │
│  └── Every queue/subscription has a DLQ for failed messages     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 When to Use Queues vs Topics

| Scenario | Use | Reason |
|----------|-----|--------|
| Single consumer per message | Queue | Point-to-point delivery |
| Multiple consumers per message | Topic | Pub/sub broadcast |
| Load balancing across workers | Queue | Competing consumers |
| Event notifications | Topic | Multiple subscribers react |
| Command/Request patterns | Queue | Single handler |
| Domain events | Topic | Multiple bounded contexts |

### 1.3 Service Bus Tiers

```csharp
// Standard Tier: Most common for development/production
// - Topics and subscriptions supported
// - 256 KB message size
// - Pay per operation

// Premium Tier: Enterprise production
// - Dedicated resources (messaging units)
// - 100 MB message size
// - Geo-disaster recovery
// - Virtual network integration
```

## Section 2: MassTransit Setup & Configuration

### 2.1 Package Installation

```xml
<!-- Core MassTransit -->
<PackageReference Include="MassTransit" Version="8.2.5" />

<!-- Azure Service Bus Transport -->
<PackageReference Include="MassTransit.Azure.ServiceBus.Core" Version="8.2.5" />

<!-- Entity Framework Core for Saga Persistence -->
<PackageReference Include="MassTransit.EntityFrameworkCore" Version="8.2.5" />

<!-- For Outbox Pattern -->
<PackageReference Include="MassTransit.EntityFrameworkCore" Version="8.2.5" />
```

### 2.2 Basic Configuration

```csharp
// Program.cs
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    // Set endpoint naming convention (recommended: kebab-case)
    x.SetKebabCaseEndpointNameFormatter();
    
    // Register consumers from assembly
    x.AddConsumers(typeof(Program).Assembly);
    
    // Configure Azure Service Bus transport
    x.UsingAzureServiceBus((context, cfg) =>
    {
        // Connection string from configuration
        cfg.Host(builder.Configuration.GetConnectionString("AzureServiceBus"));
        
        // Configure endpoints automatically
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
app.Run();
```

### 2.3 Advanced Configuration with Options

```csharp
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    
    // Add specific consumers
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<PaymentProcessedConsumer>();
    
    // Add saga state machines
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.AddDbContext<DbContext, OrderSagaDbContext>((provider, builder) =>
            {
                builder.UseSqlServer(connectionString);
            });
        });
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        var connectionString = context.GetRequiredService<IConfiguration>()
            .GetConnectionString("AzureServiceBus");
        
        cfg.Host(connectionString, h =>
        {
            // Retry policy for connection
            h.RetryLimit = 3;
            h.RetryMinBackoff = TimeSpan.FromSeconds(1);
            h.RetryMaxBackoff = TimeSpan.FromSeconds(30);
        });
        
        // Global message retry
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30)
        ));
        
        // Enable outbox for reliable messaging
        cfg.UseEntityFrameworkOutbox<OrderSagaDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
        });
        
        cfg.ConfigureEndpoints(context);
    });
});
```

## Section 3: Message Contracts

### 3.1 Message Design Principles

```csharp
// RULE 1: Messages should be immutable
// RULE 2: Use records for simple messages
// RULE 3: Include CorrelationId for tracing
// RULE 4: Commands = verbs (CreateOrder), Events = past tense (OrderCreated)

namespace Contracts.Commands;

// Command: Request to perform an action (one handler)
public record CreateOrder
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public List<OrderItem> Items { get; init; } = new();
    public DateTime RequestedAt { get; init; }
}

public record OrderItem
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
```

```csharp
namespace Contracts.Events;

// Event: Something that happened (multiple handlers possible)
public record OrderCreated
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record OrderCompleted
{
    public Guid OrderId { get; init; }
    public DateTime CompletedAt { get; init; }
}

public record OrderFailed
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; }
}
```

### 3.2 Message Correlation

```csharp
// Interface for correlated messages (saga support)
public interface CorrelatedBy<T>
{
    T CorrelationId { get; }
}

// Implement correlation for saga messages
public record OrderCreated : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
}
```

## Section 4: Consumers

### 4.1 Basic Consumer

```csharp
using MassTransit;

public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IOrderRepository _orderRepository;

    public OrderCreatedConsumer(
        ILogger<OrderCreatedConsumer> logger,
        IOrderRepository orderRepository)
    {
        _logger = logger;
        _orderRepository = orderRepository;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Processing OrderCreated: {OrderId}, Amount: {Amount}",
            message.OrderId,
            message.Amount);

        try
        {
            await _orderRepository.UpdateStatusAsync(
                message.OrderId, 
                OrderStatus.Created);
            
            // Optionally publish follow-up event
            await context.Publish(new OrderProcessingStarted
            {
                OrderId = message.OrderId,
                StartedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OrderCreated: {OrderId}", message.OrderId);
            throw; // Rethrow to trigger retry/DLQ
        }
    }
}
```

### 4.2 Consumer with Definition (Advanced Configuration)

```csharp
public class OrderCreatedConsumerDefinition : ConsumerDefinition<OrderCreatedConsumer>
{
    public OrderCreatedConsumerDefinition()
    {
        // Custom endpoint name
        EndpointName = "order-created-handler";
        
        // Concurrent message limit
        ConcurrentMessageLimit = 10;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Configure retry for this specific consumer
        endpointConfigurator.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5)
        ));
        
        // Configure circuit breaker
        endpointConfigurator.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 10;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });
    }
}

// Register with definition
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer, OrderCreatedConsumerDefinition>();
});
```

### 4.3 Batch Consumer

```csharp
public class OrderBatchConsumer : IConsumer<Batch<OrderCreated>>
{
    private readonly ILogger<OrderBatchConsumer> _logger;
    private readonly IOrderRepository _repository;

    public OrderBatchConsumer(
        ILogger<OrderBatchConsumer> logger,
        IOrderRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task Consume(ConsumeContext<Batch<OrderCreated>> context)
    {
        _logger.LogInformation(
            "Processing batch of {Count} orders",
            context.Message.Length);

        var orders = context.Message
            .Select(m => m.Message)
            .ToList();

        await _repository.BulkInsertAsync(orders);
    }
}

// Configuration
x.AddConsumer<OrderBatchConsumer>(cfg =>
{
    cfg.Options<BatchOptions>(options => options
        .SetMessageLimit(100)
        .SetTimeLimit(TimeSpan.FromSeconds(5))
        .SetConcurrencyLimit(10));
});
```

## Section 5: Publishing Messages

### 5.1 Publishing Events

```csharp
public class OrderService
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IPublishEndpoint publishEndpoint,
        ILogger<OrderService> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            Items = request.Items,
            CreatedAt = DateTime.UtcNow
        };

        // Save to database first
        await _orderRepository.SaveAsync(order);

        // Publish event (goes to topic, all subscribers receive)
        await _publishEndpoint.Publish(new OrderCreated
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt
        });

        _logger.LogInformation("Published OrderCreated event for {OrderId}", order.Id);
    }
}
```

### 5.2 Sending Commands

```csharp
public class PaymentController : ControllerBase
{
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public PaymentController(ISendEndpointProvider sendEndpointProvider)
    {
        _sendEndpointProvider = sendEndpointProvider;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment(ProcessPaymentRequest request)
    {
        // Send command to specific queue (one handler)
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(
            new Uri("queue:process-payment"));

        await endpoint.Send(new ProcessPayment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = request.OrderId,
            Amount = request.Amount,
            RequestedAt = DateTime.UtcNow
        });

        return Accepted();
    }
}
```

### 5.3 Request/Response Pattern

```csharp
// Request contract
public record GetOrderStatus
{
    public Guid OrderId { get; init; }
}

// Response contract
public record OrderStatusResult
{
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime LastUpdated { get; init; }
}

// Consumer that responds
public class GetOrderStatusConsumer : IConsumer<GetOrderStatus>
{
    private readonly IOrderRepository _repository;

    public async Task Consume(ConsumeContext<GetOrderStatus> context)
    {
        var order = await _repository.GetByIdAsync(context.Message.OrderId);
        
        await context.RespondAsync(new OrderStatusResult
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            LastUpdated = order.UpdatedAt
        });
    }
}

// Client making request
public class OrderQueryService
{
    private readonly IRequestClient<GetOrderStatus> _client;

    public OrderQueryService(IRequestClient<GetOrderStatus> client)
    {
        _client = client;
    }

    public async Task<OrderStatusResult> GetStatusAsync(Guid orderId)
    {
        var response = await _client.GetResponse<OrderStatusResult>(
            new GetOrderStatus { OrderId = orderId },
            timeout: TimeSpan.FromSeconds(30));

        return response.Message;
    }
}

// Register request client
builder.Services.AddMassTransit(x =>
{
    x.AddRequestClient<GetOrderStatus>();
});
```

## Section 6: Saga State Machines

### 6.1 Saga State Class

```csharp
using MassTransit;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    
    // Current state (stored as string for readability)
    public string CurrentState { get; set; } = string.Empty;
    
    // Order data
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Tracking
    public bool PaymentReceived { get; set; }
    public bool InventoryReserved { get; set; }
    public string? FailureReason { get; set; }
    
    // For optimistic concurrency (EF Core)
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
```

### 6.2 State Machine Definition

```csharp
using MassTransit;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    // States
    public State Submitted { get; private set; } = null!;
    public State PaymentPending { get; private set; } = null!;
    public State PaymentReceived { get; private set; } = null!;
    public State InventoryReserved { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;

    // Events
    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReserved { get; private set; } = null!;
    public Event<InventoryReservationFailed> InventoryReservationFailed { get; private set; } = null!;

    public OrderStateMachine()
    {
        // Define state property
        InstanceState(x => x.CurrentState);

        // Configure event correlation
        Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentCompleted, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReserved, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReservationFailed, x => x.CorrelateById(m => m.Message.OrderId));

        // Initial state - when OrderSubmitted arrives
        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.CreatedAt = DateTime.UtcNow;
                })
                .PublishAsync(context => context.Init<ProcessPayment>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    Amount = context.Saga.TotalAmount
                }))
                .TransitionTo(PaymentPending)
        );

        // During PaymentPending state
        During(PaymentPending,
            When(PaymentCompleted)
                .Then(context => context.Saga.PaymentReceived = true)
                .PublishAsync(context => context.Init<ReserveInventory>(new
                {
                    OrderId = context.Saga.CorrelationId
                }))
                .TransitionTo(PaymentReceived),
            
            When(PaymentFailed)
                .Then(context => context.Saga.FailureReason = context.Message.Reason)
                .PublishAsync(context => context.Init<OrderFailed>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    Reason = context.Message.Reason
                }))
                .TransitionTo(Faulted)
        );

        // During PaymentReceived state
        During(PaymentReceived,
            When(InventoryReserved)
                .Then(context =>
                {
                    context.Saga.InventoryReserved = true;
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .PublishAsync(context => context.Init<OrderCompleted>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    CompletedAt = context.Saga.CompletedAt
                }))
                .TransitionTo(Completed)
                .Finalize(),
            
            When(InventoryReservationFailed)
                .Then(context => context.Saga.FailureReason = context.Message.Reason)
                // Compensate - refund payment
                .PublishAsync(context => context.Init<RefundPayment>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    Amount = context.Saga.TotalAmount
                }))
                .PublishAsync(context => context.Init<OrderFailed>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    Reason = "Inventory not available"
                }))
                .TransitionTo(Faulted)
        );

        // Completed instances can be removed
        SetCompletedWhenFinalized();
    }
}
```

### 6.3 Saga Persistence with EF Core

```csharp
public class OrderSagaDbContext : SagaDbContext
{
    public OrderSagaDbContext(DbContextOptions<OrderSagaDbContext> options)
        : base(options)
    {
    }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new OrderStateMap(); }
    }
}

public class OrderStateMap : SagaClassMap<OrderState>
{
    protected override void Configure(EntityTypeBuilder<OrderState> entity, ModelBuilder model)
    {
        entity.ToTable("OrderSaga");
        
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.FailureReason).HasMaxLength(500);
        
        // Optimistic concurrency
        entity.Property(x => x.RowVersion)
            .IsRowVersion();
    }
}

// Registration
builder.Services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.AddDbContext<DbContext, OrderSagaDbContext>((provider, builder) =>
            {
                builder.UseSqlServer(connectionString, m =>
                {
                    m.MigrationsAssembly(typeof(OrderSagaDbContext).Assembly.FullName);
                });
            });
        });
});
```

## Section 7: Dead Letter Queue Handling

### 7.1 Understanding DLQ

```
Message Flow with DLQ:
                                    
     ┌─────────┐    Success    ┌──────────┐
     │ Message │──────────────►│ Consumer │
     └────┬────┘               └──────────┘
          │
          │ Failure (after retries)
          ▼
     ┌─────────────────────┐
     │ Dead Letter Queue   │
     │ - DeadLetterReason  │
     │ - DeadLetterError   │
     │ - Original message  │
     └─────────────────────┘
```

### 7.2 Reasons Messages Go to DLQ

1. **MaxDeliveryCountExceeded**: Message retried too many times (default: 10)
2. **TTLExpiredException**: Message time-to-live expired
3. **HeaderSizeExceeded**: Message headers too large
4. **MessageSizeExceeded**: Message body too large
5. **Application Dead-Lettering**: Consumer explicitly dead-letters

### 7.3 Processing Dead Letter Messages

```csharp
// DLQ Consumer for analysis and reprocessing
public class OrderCreatedDeadLetterConsumer : IConsumer<OrderCreated>
{
    private readonly ILogger<OrderCreatedDeadLetterConsumer> _logger;
    private readonly IDeadLetterRepository _deadLetterRepo;

    public OrderCreatedDeadLetterConsumer(
        ILogger<OrderCreatedDeadLetterConsumer> logger,
        IDeadLetterRepository deadLetterRepo)
    {
        _logger = logger;
        _deadLetterRepo = deadLetterRepo;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        // Extract DLQ metadata
        var deadLetterReason = context.Headers.Get<string>("DeadLetterReason");
        var deadLetterError = context.Headers.Get<string>("DeadLetterErrorDescription");
        var originalEnqueueTime = context.Headers.Get<DateTime?>("x-original-enqueue-time");

        _logger.LogWarning(
            "Dead letter message received. OrderId: {OrderId}, Reason: {Reason}, Error: {Error}",
            context.Message.OrderId,
            deadLetterReason,
            deadLetterError);

        // Store for analysis
        await _deadLetterRepo.SaveAsync(new DeadLetterRecord
        {
            MessageId = context.MessageId ?? Guid.NewGuid(),
            MessageType = nameof(OrderCreated),
            CorrelationId = context.Message.OrderId,
            DeadLetterReason = deadLetterReason,
            DeadLetterError = deadLetterError,
            MessageBody = System.Text.Json.JsonSerializer.Serialize(context.Message),
            ReceivedAt = DateTime.UtcNow
        });

        // Optionally attempt reprocessing based on reason
        if (deadLetterReason == "MaxDeliveryCountExceeded")
        {
            // Could send to a manual review queue
            await context.Send(new Uri("queue:manual-review"), context.Message);
        }
    }
}

// Configure DLQ consumer
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedDeadLetterConsumer>();
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString);
        
        // Configure DLQ endpoint
        cfg.ReceiveEndpoint("order-created/$deadletterqueue", e =>
        {
            e.ConfigureConsumer<OrderCreatedDeadLetterConsumer>(context);
        });
    });
});
```

### 7.4 Explicitly Dead-Lettering Messages

```csharp
public class OrderValidationConsumer : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var message = context.Message;

        // Validation that can't be retried
        if (message.TotalAmount <= 0)
        {
            // Explicitly send to DLQ with reason
            await context.Send(
                new Uri($"queue:{context.ReceiveContext.InputAddress.AbsolutePath}/$deadletterqueue"),
                message,
                sendContext =>
                {
                    sendContext.Headers.Set("DeadLetterReason", "ValidationFailed");
                    sendContext.Headers.Set("DeadLetterErrorDescription", 
                        "Order amount must be greater than zero");
                });
            
            return; // Don't process further
        }

        // Continue normal processing
        await ProcessOrderAsync(message);
    }
}
```

### 7.5 Azure Function for DLQ Processing

```csharp
// Automatic DLQ processing with Azure Functions
public class DeadLetterProcessor
{
    private readonly ILogger<DeadLetterProcessor> _logger;

    public DeadLetterProcessor(ILogger<DeadLetterProcessor> logger)
    {
        _logger = logger;
    }

    [FunctionName("ProcessOrderDLQ")]
    public async Task Run(
        [ServiceBusTrigger(
            "order-events", 
            "order-processor/$deadletterqueue",
            Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogWarning(
            "DLQ Message: {Subject}, DeadLetterReason: {Reason}",
            message.Subject,
            message.DeadLetterReason);

        // Analyze and decide action
        var action = AnalyzeDeadLetter(message);

        switch (action)
        {
            case DlqAction.Requeue:
                // Send back to main queue for retry
                await RequeueMessageAsync(message);
                break;
                
            case DlqAction.Archive:
                // Move to cold storage
                await ArchiveMessageAsync(message);
                break;
                
            case DlqAction.Alert:
                // Notify operations team
                await SendAlertAsync(message);
                break;
        }

        // Complete the DLQ message
        await messageActions.CompleteMessageAsync(message);
    }
}
```

## Section 8: Resilience Patterns

### 8.1 Retry Configuration

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString);
        
        // Global retry policy
        cfg.UseMessageRetry(r =>
        {
            // Exponential backoff
            r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(60),
                intervalDelta: TimeSpan.FromSeconds(2));
            
            // Only retry specific exceptions
            r.Handle<SqlException>();
            r.Handle<HttpRequestException>();
            r.Handle<TimeoutException>();
            
            // Ignore certain exceptions (send to DLQ immediately)
            r.Ignore<ValidationException>();
            r.Ignore<ArgumentException>();
        });
        
        cfg.ConfigureEndpoints(context);
    });
});
```

### 8.2 Circuit Breaker

```csharp
cfg.UseCircuitBreaker(cb =>
{
    // Track failures over this period
    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
    
    // Trip circuit after this many failures
    cb.TripThreshold = 15;
    
    // Minimum throughput before circuit can trip
    cb.ActiveThreshold = 10;
    
    // How long circuit stays open
    cb.ResetInterval = TimeSpan.FromMinutes(5);
});
```

### 8.3 Rate Limiting

```csharp
cfg.UseRateLimit(1000, TimeSpan.FromSeconds(1)); // 1000 msg/sec

// Per-endpoint rate limit
cfg.ReceiveEndpoint("high-volume-queue", e =>
{
    e.UseRateLimit(100, TimeSpan.FromSeconds(1));
    e.ConfigureConsumer<HighVolumeConsumer>(context);
});
```

### 8.4 Combining with Polly

```csharp
// For HTTP calls within consumers
builder.Services.AddHttpClient<IExternalPaymentService, ExternalPaymentService>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
```

## Section 9: Topic/Subscription Patterns

### 9.1 Topic Configuration

```csharp
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<OrderAuditConsumer>();
    x.AddConsumer<OrderNotificationConsumer>();
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString);
        
        // All three consumers subscribe to same topic
        // Each gets a copy of every OrderCreated message
        cfg.ConfigureEndpoints(context);
    });
});
```

### 9.2 Subscription Filters

```csharp
cfg.ReceiveEndpoint("high-priority-orders", e =>
{
    // SQL filter on subscription
    e.ConfigureConsumeTopology = false; // Manual configuration
    
    e.Subscribe<OrderCreated>(s =>
    {
        // Only receive orders over $1000
        s.Filter = new SqlFilter("TotalAmount > 1000");
    });
    
    e.ConfigureConsumer<HighPriorityOrderConsumer>(context);
});
```

### 9.3 Partitioning for Ordering

```csharp
// Ensure messages for same order processed in order
public class OrderConsumerDefinition : ConsumerDefinition<OrderConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Partition by OrderId - same order always same partition
        var partition = endpointConfigurator.CreatePartitioner(8);
        
        consumerConfigurator.Message<OrderCreated>(m =>
            m.UsePartitioner(partition, ctx => ctx.Message.OrderId));
        
        consumerConfigurator.Message<OrderUpdated>(m =>
            m.UsePartitioner(partition, ctx => ctx.Message.OrderId));
    }
}
```

## Section 10: Outbox Pattern

### 10.1 Problem: Dual-Write Consistency

```
Without Outbox (DANGEROUS):
1. Save order to database     ✓
2. Publish OrderCreated event ✗ (network failure)
Result: Database updated, but no event published!

With Outbox (SAFE):
1. Save order + outbox message to database (single transaction) ✓
2. Background process publishes from outbox ✓
Result: Guaranteed consistency
```

### 10.2 Outbox Configuration

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
    {
        // Use SQL Server
        o.UseSqlServer();
        
        // Query delay for delivery
        o.QueryDelay = TimeSpan.FromSeconds(1);
        
        // Enable for bus (publish/send)
        o.UseBusOutbox();
        
        // Disable inbox (for consumers)
        // o.DisableInboxCleanupService();
    });
    
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(connectionString);
        
        // Enable outbox on all endpoints
        cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
        });
        
        cfg.ConfigureEndpoints(context);
    });
});
```

### 10.3 Using Outbox in Service

```csharp
public class OrderService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        
        try
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                TotalAmount = request.TotalAmount
            };
            
            _dbContext.Orders.Add(order);
            
            // This publish goes to outbox table, not directly to broker
            await _publishEndpoint.Publish(new OrderCreated
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount
            });
            
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            
            // Outbox delivery service will publish to broker
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

## Section 11: Monitoring & Observability

### 11.1 OpenTelemetry Integration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("MassTransit")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("MassTransit")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

### 11.2 Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddAzureServiceBusTopic(
        connectionString,
        topicName: "order-events",
        name: "servicebus-topic")
    .AddAzureServiceBusQueue(
        connectionString,
        queueName: "order-commands",
        name: "servicebus-queue");

// MassTransit bus health
builder.Services.AddMassTransit(x =>
{
    x.AddHealthChecks();
});
```

### 11.3 Logging Consumer Activity

```csharp
public class LoggingObserver : IConsumeObserver
{
    private readonly ILogger<LoggingObserver> _logger;

    public LoggingObserver(ILogger<LoggingObserver> logger)
    {
        _logger = logger;
    }

    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        _logger.LogInformation(
            "Consuming {MessageType}, MessageId: {MessageId}",
            typeof(T).Name,
            context.MessageId);
        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        _logger.LogInformation(
            "Consumed {MessageType}, MessageId: {MessageId}",
            typeof(T).Name,
            context.MessageId);
        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        _logger.LogError(
            exception,
            "Consume fault {MessageType}, MessageId: {MessageId}",
            typeof(T).Name,
            context.MessageId);
        return Task.CompletedTask;
    }
}

// Register observer
builder.Services.AddMassTransit(x =>
{
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingObserver>());
    });
});
```

## Section 12: Testing

### 12.1 In-Memory Test Harness

```csharp
public class OrderConsumerTests
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

        try
        {
            // Publish test message
            await harness.Bus.Publish(new OrderCreated
            {
                OrderId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                TotalAmount = 100.00m
            });

            // Assert message was consumed
            Assert.True(await harness.Consumed.Any<OrderCreated>());

            // Get consumer harness for specific assertions
            var consumerHarness = harness.GetConsumerHarness<OrderCreatedConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<OrderCreated>());
        }
        finally
        {
            await harness.Stop();
        }
    }
}
```

### 12.2 Saga Testing

```csharp
public class OrderStateMachineTests
{
    [Fact]
    public async Task Should_transition_to_payment_pending()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
        var orderId = Guid.NewGuid();

        await harness.Bus.Publish(new OrderSubmitted
        {
            OrderId = orderId,
            CustomerId = Guid.NewGuid(),
            TotalAmount = 100.00m
        });

        // Assert saga instance created and in correct state
        Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == orderId));
        
        var instance = sagaHarness.Created.ContainsInState(orderId, 
            sagaHarness.StateMachine, sagaHarness.StateMachine.PaymentPending);
        Assert.NotNull(instance);
    }
}
```

## Quick Reference: Common Patterns

### Publishing vs Sending

```csharp
// PUBLISH: Event (0 to many consumers, goes to topic)
await _publishEndpoint.Publish(new OrderCreated { ... });

// SEND: Command (exactly one consumer, goes to queue)
var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri("queue:process-payment"));
await endpoint.Send(new ProcessPayment { ... });
```

### Message Type Conventions

```csharp
// Commands: Verb + Noun (imperative)
CreateOrder, ProcessPayment, ReserveInventory, SendNotification

// Events: Noun + Verb (past tense)  
OrderCreated, PaymentProcessed, InventoryReserved, NotificationSent

// Queries (Request/Response)
GetOrderStatus → OrderStatusResult
```

### Endpoint Naming

```csharp
// MassTransit creates endpoints automatically:
// Consumer: OrderCreatedConsumer → queue: order-created
// Saga: OrderStateMachine → queue: order-state

// Override with definition:
public class MyConsumerDefinition : ConsumerDefinition<MyConsumer>
{
    public MyConsumerDefinition()
    {
        EndpointName = "custom-endpoint-name";
    }
}
```

---

## Version History

- **1.0.0** (2026-01-03): Initial release with comprehensive patterns

## Important Note: MassTransit v9 Licensing

MassTransit v8 remains open-source under Apache 2.0. Starting Q1 2026, MassTransit v9 will require a commercial license. Plan accordingly for enterprise deployments.
