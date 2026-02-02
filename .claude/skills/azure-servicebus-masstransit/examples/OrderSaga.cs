// OrderSaga.cs - Complete Saga State Machine Example
// Demonstrates: State machine, compensation, persistence

using MassTransit;

namespace Enterprise.Messaging.Sagas;

#region Contracts

// Commands (sent to specific queue)
public record SubmitOrder
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public List<OrderLineItem> Items { get; init; } = new();
    public decimal TotalAmount { get; init; }
}

public record ProcessPayment
{
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
}

public record ReserveInventory
{
    public Guid OrderId { get; init; }
    public List<OrderLineItem> Items { get; init; } = new();
}

public record RefundPayment
{
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record ReleaseInventory
{
    public Guid OrderId { get; init; }
}

// Events (published to topic)
public record OrderSubmitted : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime SubmittedAt { get; init; }
}

public record PaymentCompleted : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public DateTime CompletedAt { get; init; }
}

public record PaymentFailed : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; }
}

public record InventoryReserved : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public string ReservationId { get; init; } = string.Empty;
    public DateTime ReservedAt { get; init; }
}

public record InventoryReservationFailed : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<string> UnavailableItems { get; init; } = new();
}

public record OrderCompleted : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public DateTime CompletedAt { get; init; }
}

public record OrderFailed : CorrelatedBy<Guid>
{
    public Guid CorrelationId => OrderId;
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime FailedAt { get; init; }
}

public record OrderLineItem
{
    public string ProductId { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

#endregion

#region Saga State

/// <summary>
/// Saga instance state - persisted between events
/// </summary>
public class OrderState : SagaStateMachineInstance
{
    /// <summary>
    /// Unique identifier for this saga instance
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Current state name (e.g., "Submitted", "PaymentPending")
    /// </summary>
    public string CurrentState { get; set; } = string.Empty;

    // Order details
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Tracking flags
    public bool PaymentReceived { get; set; }
    public bool InventoryReserved { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? InventoryReservationId { get; set; }
    public string? FailureReason { get; set; }

    // Optimistic concurrency (EF Core)
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

#endregion

#region State Machine

/// <summary>
/// Order processing saga state machine
/// Handles: Order submission → Payment → Inventory → Completion
/// With compensation for failures
/// </summary>
public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    // Declare states
    public State Submitted { get; private set; } = null!;
    public State PaymentPending { get; private set; } = null!;
    public State PaymentReceived { get; private set; } = null!;
    public State InventoryPending { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;
    public State Compensating { get; private set; } = null!;

    // Declare events
    public Event<OrderSubmitted> OrderSubmittedEvent { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompletedEvent { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailedEvent { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReservedEvent { get; private set; } = null!;
    public Event<InventoryReservationFailed> InventoryReservationFailedEvent { get; private set; } = null!;

    public OrderStateMachine(ILogger<OrderStateMachine> logger)
    {
        // Map CurrentState property to state
        InstanceState(x => x.CurrentState);

        // Configure event correlation
        ConfigureEvents();

        // Define state transitions
        ConfigureInitialState(logger);
        ConfigurePaymentPendingState(logger);
        ConfigurePaymentReceivedState(logger);
        ConfigureCompensation(logger);
        ConfigureCompletion();
    }

    private void ConfigureEvents()
    {
        Event(() => OrderSubmittedEvent, x => 
            x.CorrelateById(m => m.Message.OrderId));
        
        Event(() => PaymentCompletedEvent, x => 
            x.CorrelateById(m => m.Message.OrderId));
        
        Event(() => PaymentFailedEvent, x => 
            x.CorrelateById(m => m.Message.OrderId));
        
        Event(() => InventoryReservedEvent, x => 
            x.CorrelateById(m => m.Message.OrderId));
        
        Event(() => InventoryReservationFailedEvent, x => 
            x.CorrelateById(m => m.Message.OrderId));
    }

    private void ConfigureInitialState(ILogger logger)
    {
        Initially(
            When(OrderSubmittedEvent)
                .Then(context =>
                {
                    logger.LogInformation(
                        "Order {OrderId} submitted by customer {CustomerId}",
                        context.Saga.CorrelationId,
                        context.Message.CustomerId);

                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.SubmittedAt = context.Message.SubmittedAt;
                })
                .TransitionTo(Submitted)
                .Then(context =>
                {
                    logger.LogInformation(
                        "Order {OrderId} transitioning to PaymentPending",
                        context.Saga.CorrelationId);
                })
                // Send command to payment service
                .SendAsync(
                    new Uri("queue:process-payment"),
                    context => context.Init<ProcessPayment>(new
                    {
                        OrderId = context.Saga.CorrelationId,
                        Amount = context.Saga.TotalAmount,
                        PaymentMethod = "CreditCard"
                    }))
                .TransitionTo(PaymentPending)
        );
    }

    private void ConfigurePaymentPendingState(ILogger logger)
    {
        During(PaymentPending,
            // Payment succeeded - proceed to inventory
            When(PaymentCompletedEvent)
                .Then(context =>
                {
                    logger.LogInformation(
                        "Payment completed for order {OrderId}, transaction {TransactionId}",
                        context.Saga.CorrelationId,
                        context.Message.TransactionId);

                    context.Saga.PaymentReceived = true;
                    context.Saga.PaymentTransactionId = context.Message.TransactionId;
                })
                .TransitionTo(PaymentReceived)
                // Send command to inventory service
                .SendAsync(
                    new Uri("queue:reserve-inventory"),
                    context => context.Init<ReserveInventory>(new
                    {
                        OrderId = context.Saga.CorrelationId,
                        Items = new List<OrderLineItem>() // Would come from order details
                    }))
                .TransitionTo(InventoryPending),

            // Payment failed - go to faulted state
            When(PaymentFailedEvent)
                .Then(context =>
                {
                    logger.LogWarning(
                        "Payment failed for order {OrderId}: {Reason}",
                        context.Saga.CorrelationId,
                        context.Message.Reason);

                    context.Saga.FailureReason = context.Message.Reason;
                })
                // Publish failure event for other services
                .PublishAsync(context => context.Init<OrderFailed>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    Reason = $"Payment failed: {context.Message.Reason}",
                    FailedAt = DateTime.UtcNow
                }))
                .TransitionTo(Faulted)
        );
    }

    private void ConfigurePaymentReceivedState(ILogger logger)
    {
        During(InventoryPending,
            // Inventory reserved - complete order
            When(InventoryReservedEvent)
                .Then(context =>
                {
                    logger.LogInformation(
                        "Inventory reserved for order {OrderId}",
                        context.Saga.CorrelationId);

                    context.Saga.InventoryReserved = true;
                    context.Saga.InventoryReservationId = context.Message.ReservationId;
                    context.Saga.CompletedAt = DateTime.UtcNow;
                })
                .PublishAsync(context => context.Init<OrderCompleted>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    CompletedAt = context.Saga.CompletedAt
                }))
                .TransitionTo(Completed)
                .Finalize(),

            // Inventory failed - compensate by refunding payment
            When(InventoryReservationFailedEvent)
                .Then(context =>
                {
                    logger.LogWarning(
                        "Inventory reservation failed for order {OrderId}: {Reason}",
                        context.Saga.CorrelationId,
                        context.Message.Reason);

                    context.Saga.FailureReason = context.Message.Reason;
                })
                .TransitionTo(Compensating)
                // Compensate - refund the payment
                .SendAsync(
                    new Uri("queue:refund-payment"),
                    context => context.Init<RefundPayment>(new
                    {
                        OrderId = context.Saga.CorrelationId,
                        Amount = context.Saga.TotalAmount,
                        Reason = "Inventory not available"
                    }))
                // Publish failure event
                .PublishAsync(context => context.Init<OrderFailed>(new
                {
                    OrderId = context.Saga.CorrelationId,
                    Reason = $"Inventory failed: {context.Message.Reason}",
                    FailedAt = DateTime.UtcNow
                }))
                .TransitionTo(Faulted)
        );
    }

    private void ConfigureCompensation(ILogger logger)
    {
        // Handle any cleanup needed during compensation
        During(Compensating,
            Ignore(PaymentCompletedEvent),
            Ignore(PaymentFailedEvent),
            Ignore(InventoryReservedEvent),
            Ignore(InventoryReservationFailedEvent)
        );
    }

    private void ConfigureCompletion()
    {
        // Remove completed sagas from repository
        SetCompletedWhenFinalized();
    }
}

#endregion

#region Saga Definition (Optional Advanced Configuration)

public class OrderStateMachineDefinition : SagaDefinition<OrderState>
{
    public OrderStateMachineDefinition()
    {
        // Concurrent message limit
        Endpoint(e => e.ConcurrentMessageLimit = 16);
    }

    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<OrderState> sagaConfigurator,
        IRegistrationContext context)
    {
        // Partition messages by correlation ID for ordering
        var partition = endpointConfigurator.CreatePartitioner(16);

        sagaConfigurator.Message<OrderSubmitted>(x =>
            x.UsePartitioner(partition, m => m.Message.OrderId));

        sagaConfigurator.Message<PaymentCompleted>(x =>
            x.UsePartitioner(partition, m => m.Message.OrderId));

        sagaConfigurator.Message<PaymentFailed>(x =>
            x.UsePartitioner(partition, m => m.Message.OrderId));

        sagaConfigurator.Message<InventoryReserved>(x =>
            x.UsePartitioner(partition, m => m.Message.OrderId));

        sagaConfigurator.Message<InventoryReservationFailed>(x =>
            x.UsePartitioner(partition, m => m.Message.OrderId));
    }
}

#endregion

#region EF Core Configuration

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MassTransit.EntityFrameworkCoreIntegration;

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
        entity.ToTable("OrderSaga", "saga");

        entity.HasKey(x => x.CorrelationId);

        entity.Property(x => x.CurrentState)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(x => x.PaymentTransactionId)
            .HasMaxLength(100);

        entity.Property(x => x.InventoryReservationId)
            .HasMaxLength(100);

        entity.Property(x => x.FailureReason)
            .HasMaxLength(500);

        // Optimistic concurrency
        entity.Property(x => x.RowVersion)
            .IsRowVersion();

        // Indexes for common queries
        entity.HasIndex(x => x.CurrentState);
        entity.HasIndex(x => x.CustomerId);
        entity.HasIndex(x => x.SubmittedAt);
    }
}

#endregion

#region Registration Example

/*
// Program.cs registration:

builder.Services.AddDbContext<OrderSagaDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    // Add saga with EF Core persistence
    x.AddSagaStateMachine<OrderStateMachine, OrderState, OrderStateMachineDefinition>()
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

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(serviceBusConnectionString);

        // Retry configuration
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15)));

        // Outbox for reliable messaging
        cfg.UseEntityFrameworkOutbox<OrderSagaDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
        });

        cfg.ConfigureEndpoints(context);
    });
});
*/

#endregion
