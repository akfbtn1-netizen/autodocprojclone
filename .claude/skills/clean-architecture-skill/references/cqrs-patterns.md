# CQRS Patterns Reference

Complete guide to Command Query Responsibility Segregation with MediatR in .NET 8.

## CQRS Overview

CQRS separates read (Query) and write (Command) operations into distinct models:

```
┌─────────────────┐     ┌─────────────────┐
│    Commands     │     │    Queries      │
│  (Write Model)  │     │  (Read Model)   │
├─────────────────┤     ├─────────────────┤
│ - CreateOrder   │     │ - GetOrder      │
│ - UpdateOrder   │     │ - GetOrdersList │
│ - CancelOrder   │     │ - GetStats      │
└────────┬────────┘     └────────┬────────┘
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│  Domain Model   │     │  Read DTOs /    │
│  (Rich Entities)│     │  Projections    │
└─────────────────┘     └─────────────────┘
```

---

## Core Abstractions

### Command and Query Interfaces

```csharp
namespace Application.Common;

using MediatR;

/// <summary>
/// Marker interface for commands that mutate state.
/// </summary>
public interface ICommand<out TResponse> : IRequest<TResponse> { }

/// <summary>
/// Command with no return value.
/// </summary>
public interface ICommand : IRequest<Unit> { }

/// <summary>
/// Marker interface for queries that read state.
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse> { }

/// <summary>
/// Command handler interface.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse> { }

/// <summary>
/// Command handler with no response.
/// </summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : ICommand { }

/// <summary>
/// Query handler interface.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse> { }
```

---

## Command Implementation

### Command Record

```csharp
namespace Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    AddressDto ShippingAddress,
    AddressDto? BillingAddress,
    List<OrderItemDto> Items
) : ICommand<Result<OrderDto>>;

public sealed record AddressDto(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country
);

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity
);
```

### Command Validator

```csharp
namespace Application.Orders.Commands.CreateOrder;

using FluentValidation;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");
        
        RuleFor(x => x.ShippingAddress)
            .NotNull()
            .WithMessage("Shipping address is required")
            .SetValidator(new AddressDtoValidator());
        
        RuleFor(x => x.BillingAddress)
            .SetValidator(new AddressDtoValidator()!)
            .When(x => x.BillingAddress is not null);
        
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one item is required")
            .Must(items => items.Count <= 50)
            .WithMessage("Cannot exceed 50 items per order");
        
        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemDtoValidator());
    }
}

public sealed class AddressDtoValidator : AbstractValidator<AddressDto>
{
    public AddressDtoValidator()
    {
        RuleFor(x => x.Street)
            .NotEmpty().WithMessage("Street is required")
            .MaximumLength(200).WithMessage("Street cannot exceed 200 characters");
        
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required")
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters");
        
        RuleFor(x => x.State)
            .MaximumLength(50).WithMessage("State cannot exceed 50 characters");
        
        RuleFor(x => x.PostalCode)
            .MaximumLength(20).WithMessage("Postal code cannot exceed 20 characters");
        
        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .Length(2, 3).WithMessage("Country must be 2-3 character ISO code");
    }
}

public sealed class OrderItemDtoValidator : AbstractValidator<OrderItemDto>
{
    public OrderItemDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required");
        
        RuleFor(x => x.ProductName)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters");
        
        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("Unit price must be greater than 0");
        
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 1000).WithMessage("Quantity must be between 1 and 1000");
    }
}
```

### Command Handler

```csharp
namespace Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler(
    IApplicationDbContext context,
    IMapper mapper,
    ILogger<CreateOrderCommandHandler> logger)
    : ICommandHandler<CreateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate business rules (beyond input validation)
        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == new CustomerId(request.CustomerId), cancellationToken);
        
        if (customer is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Customer.NotFound", $"Customer {request.CustomerId} not found"));
        }
        
        // 2. Create value objects (with validation)
        Address shippingAddress;
        try
        {
            shippingAddress = Address.Create(
                request.ShippingAddress.Street,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.Country);
        }
        catch (DomainException ex)
        {
            return Result.Failure<OrderDto>(
                Error.Validation("Address.Invalid", ex.Message));
        }
        
        Address? billingAddress = request.BillingAddress is not null
            ? Address.Create(
                request.BillingAddress.Street,
                request.BillingAddress.City,
                request.BillingAddress.State,
                request.BillingAddress.PostalCode,
                request.BillingAddress.Country)
            : null;
        
        // 3. Create aggregate via factory method
        var order = Order.Create(customer.CustomerId, shippingAddress, billingAddress);
        
        // 4. Add items through aggregate root
        foreach (var item in request.Items)
        {
            order.AddItem(
                new ProductId(item.ProductId),
                item.ProductName,
                Money.USD(item.UnitPrice),
                item.Quantity);
        }
        
        // 5. Persist
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        
        // 6. Log and return
        logger.LogInformation(
            "Created order {OrderId} for customer {CustomerId} with {ItemCount} items",
            order.OrderId, customer.CustomerId, order.Items.Count);
        
        var dto = mapper.Map<OrderDto>(order);
        return Result.Success(dto);
    }
}
```

---

## Query Implementation

### Query Record

```csharp
namespace Application.Orders.Queries.GetOrder;

public sealed record GetOrderQuery(Guid OrderId) : IQuery<Result<OrderDto>>;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string Status,
    AddressDto ShippingAddress,
    AddressDto? BillingAddress,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt,
    DateTime? ShippedAt,
    string? TrackingNumber,
    List<OrderItemDto> Items
);
```

### Query Handler

```csharp
namespace Application.Orders.Queries.GetOrder;

public sealed class GetOrderQueryHandler(
    IApplicationDbContext context,
    IMapper mapper)
    : IQueryHandler<GetOrderQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        GetOrderQuery request,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .AsNoTracking()  // Read-only query optimization
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == OrderId.From(request.OrderId), cancellationToken);
        
        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Order.NotFound", $"Order {request.OrderId} not found"));
        }
        
        var dto = mapper.Map<OrderDto>(order);
        return Result.Success(dto);
    }
}
```

### Paginated Query

```csharp
namespace Application.Orders.Queries.GetOrdersList;

public sealed record GetOrdersListQuery(
    Guid? CustomerId = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 10,
    string? SortBy = null,
    bool SortDescending = false
) : IQuery<Result<PaginatedList<OrderSummaryDto>>>;

public sealed record OrderSummaryDto(
    Guid OrderId,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? ShippedAt
);

public sealed class GetOrdersListQueryHandler(
    IApplicationDbContext context)
    : IQueryHandler<GetOrdersListQuery, Result<PaginatedList<OrderSummaryDto>>>
{
    public async Task<Result<PaginatedList<OrderSummaryDto>>> Handle(
        GetOrdersListQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .AsQueryable();
        
        // Apply filters
        if (request.CustomerId.HasValue)
            query = query.Where(o => o.CustomerId == new CustomerId(request.CustomerId.Value));
        
        if (!string.IsNullOrEmpty(request.Status) && 
            OrderStatus.TryFromName(request.Status, out var status))
            query = query.Where(o => o.Status == status);
        
        if (request.FromDate.HasValue)
            query = query.Where(o => o.CreatedAt >= request.FromDate.Value);
        
        if (request.ToDate.HasValue)
            query = query.Where(o => o.CreatedAt <= request.ToDate.Value);
        
        // Apply sorting
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "createdat" => request.SortDescending 
                ? query.OrderByDescending(o => o.CreatedAt) 
                : query.OrderBy(o => o.CreatedAt),
            "total" => request.SortDescending 
                ? query.OrderByDescending(o => o.TotalAmount.Amount) 
                : query.OrderBy(o => o.TotalAmount.Amount),
            "status" => request.SortDescending 
                ? query.OrderByDescending(o => o.Status.Value) 
                : query.OrderBy(o => o.Status.Value),
            _ => query.OrderByDescending(o => o.CreatedAt)
        };
        
        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);
        
        // Apply pagination and project to DTO
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OrderSummaryDto(
                o.OrderId.Value,
                o.Customer.Name,
                o.Status.Name,
                o.TotalAmount.Amount,
                o.Items.Count,
                o.CreatedAt,
                o.ShippedAt))
            .ToListAsync(cancellationToken);
        
        var result = new PaginatedList<OrderSummaryDto>(
            items, totalCount, request.PageNumber, request.PageSize);
        
        return Result.Success(result);
    }
}
```

---

## Pipeline Behaviors

### Validation Behavior

```csharp
namespace Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next();
        
        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();
        
        if (failures.Count != 0)
        {
            logger.LogWarning(
                "Validation failed for {RequestType}: {Errors}",
                typeof(TRequest).Name,
                string.Join(", ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));
            
            throw new ValidationException(failures);
        }
        
        return await next();
    }
}
```

### Logging Behavior

```csharp
namespace Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = currentUser.UserId ?? "Anonymous";
        
        logger.LogInformation(
            "Handling {RequestName} for user {UserId} {@Request}",
            requestName, userId, request);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            logger.LogInformation(
                "Handled {RequestName} for user {UserId} in {ElapsedMs}ms",
                requestName, userId, stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            logger.LogError(ex,
                "Error handling {RequestName} for user {UserId} after {ElapsedMs}ms",
                requestName, userId, stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
}
```

### Performance Behavior

```csharp
namespace Application.Common.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var response = await next();
        
        stopwatch.Stop();
        
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        
        if (elapsedMs > SlowRequestThresholdMs)
        {
            logger.LogWarning(
                "SLOW REQUEST: {RequestName} ({ElapsedMs}ms) by {UserId} {@Request}",
                typeof(TRequest).Name, 
                elapsedMs, 
                currentUser.UserId ?? "Anonymous",
                request);
        }
        
        return response;
    }
}
```

### Transaction Behavior (Commands Only)

```csharp
namespace Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(
    IApplicationDbContext context,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>  // Only for commands
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        // Check if already in transaction
        if (context.Database.CurrentTransaction is not null)
            return await next();
        
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken);
        
        try
        {
            logger.LogDebug("Beginning transaction for {RequestName}", requestName);
            
            var response = await next();
            
            await transaction.CommitAsync(cancellationToken);
            
            logger.LogDebug("Committed transaction for {RequestName}", requestName);
            
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, 
                "Transaction failed for {RequestName}, rolling back", 
                requestName);
            
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

---

## MediatR Registration

```csharp
namespace Application;

using Microsoft.Extensions.DependencyInjection;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = typeof(ConfigureServices).Assembly;
        
        // AutoMapper
        services.AddAutoMapper(assembly);
        
        // FluentValidation
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        
        // MediatR with behaviors (ORDER MATTERS!)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            
            // 1. Unhandled exception logging (outermost)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
            
            // 2. Request/response logging
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            
            // 3. Validation (fail fast before processing)
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            
            // 4. Performance monitoring
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
            
            // 5. Transaction wrapping for commands (innermost for data operations)
            // Note: Register TransactionBehavior specifically for commands if needed
        });
        
        return services;
    }
}
```

---

## Domain Event Handlers

### Event Handler Example

```csharp
namespace Application.Orders.EventHandlers;

public sealed class OrderCreatedEventHandler(
    IEmailService emailService,
    IApplicationDbContext context,
    ILogger<OrderCreatedEventHandler> logger)
    : INotificationHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Order {OrderId} created, sending confirmation email", 
            notification.OrderId);
        
        var order = await context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.OrderId == notification.OrderId, cancellationToken);
        
        if (order is null)
        {
            logger.LogWarning("Order {OrderId} not found for event handling", 
                notification.OrderId);
            return;
        }
        
        await emailService.SendOrderConfirmationAsync(
            order.Customer.Email,
            order.OrderId.Value,
            order.TotalAmount.ToString(),
            cancellationToken);
        
        logger.LogInformation("Order confirmation email sent for {OrderId}", 
            notification.OrderId);
    }
}

public sealed class OrderShippedEventHandler(
    INotificationService notificationService,
    ILogger<OrderShippedEventHandler> logger)
    : INotificationHandler<OrderShippedEvent>
{
    public async Task Handle(OrderShippedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Order {OrderId} shipped with tracking {TrackingNumber}",
            notification.OrderId, notification.TrackingNumber);
        
        await notificationService.SendShippingNotificationAsync(
            notification.OrderId.Value,
            notification.TrackingNumber,
            cancellationToken);
    }
}
```

---

## AutoMapper Profiles

```csharp
namespace Application.Common.Mappings;

using AutoMapper;

public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        // Order -> OrderDto
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Id, opt => opt.MapFrom(s => s.OrderId.Value))
            .ForMember(d => d.CustomerId, opt => opt.MapFrom(s => s.CustomerId.Value))
            .ForMember(d => d.CustomerName, opt => opt.MapFrom(s => s.Customer.Name))
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.Name))
            .ForMember(d => d.TotalAmount, opt => opt.MapFrom(s => s.TotalAmount.Amount))
            .ForMember(d => d.Currency, opt => opt.MapFrom(s => s.TotalAmount.Currency))
            .ForMember(d => d.ShippingAddress, opt => opt.MapFrom(s => s.ShippingAddress))
            .ForMember(d => d.BillingAddress, opt => opt.MapFrom(s => s.BillingAddress));
        
        // Address -> AddressDto
        CreateMap<Address, AddressDto>();
        
        // OrderItem -> OrderItemDto
        CreateMap<OrderItem, OrderItemDto>()
            .ForMember(d => d.ProductId, opt => opt.MapFrom(s => s.ProductId.Value))
            .ForMember(d => d.UnitPrice, opt => opt.MapFrom(s => s.UnitPrice.Amount));
    }
}
```

---

## Best Practices Summary

### Commands
- Use for write operations only
- Return Result<T> for expected failures
- Wrap in transactions
- Raise domain events
- Include comprehensive validation

### Queries
- Use for read operations only
- Use AsNoTracking() for performance
- Project to DTOs in query (not AutoMapper for complex queries)
- Include pagination for lists
- Consider read replicas for scale

### Pipeline Behaviors
- Order matters (outermost to innermost)
- Keep behaviors focused (single responsibility)
- Use generic constraints to target command/query
- Log appropriately for debugging

### General
- Keep handlers thin - delegate to domain
- One handler per command/query
- Co-locate related files (command, validator, handler)
- Use dependency injection
- Test handlers with integration tests
