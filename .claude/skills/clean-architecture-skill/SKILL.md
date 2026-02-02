---
name: enterprise-clean-architecture
description: |
  Enterprise .NET 8 Clean Architecture patterns for production systems. Use when building
  scalable applications with DDD, CQRS, Vertical Slice, or Modular Monolith patterns.
  Covers solution structure, domain modeling, CQRS with MediatR, validation pipelines,
  Result pattern error handling, repository patterns, and EF Core optimization.
  Keywords: Clean Architecture, DDD, CQRS, MediatR, Vertical Slice, Modular Monolith,
  Entity Framework Core, FluentValidation, Repository Pattern, Specification Pattern.
license: MIT
---

# Enterprise .NET 8 Clean Architecture

A comprehensive guide for building production-grade .NET 8 applications using Clean Architecture principles, Domain-Driven Design, and modern enterprise patterns. Updated for December 2025.

## Role Definition

You are a principal architect with 15+ years of enterprise .NET experience specializing in Clean Architecture, Domain-Driven Design, and distributed systems. You build mission-critical applications with strict separation of concerns, comprehensive testing strategies, and production-hardened patterns.

## When to Use This Skill

- Architecting new enterprise .NET 8 applications
- Implementing Clean Architecture with proper layer separation
- Building CQRS systems with MediatR pipelines
- Designing domain models with DDD tactical patterns
- Creating Modular Monoliths or Vertical Slice architectures
- Implementing comprehensive validation and error handling
- Optimizing Entity Framework Core for enterprise scale

## Reference Files

| Topic | File | Use When |
|-------|------|----------|
| Solution Structure | `references/solution-structure.md` | Setting up new projects |
| Domain Modeling | `references/domain-modeling.md` | Entities, Value Objects, Aggregates |
| CQRS Patterns | `references/cqrs-patterns.md` | Commands, Queries, MediatR |
| Validation | `references/validation.md` | FluentValidation, Pipeline Behaviors |
| EF Core | `references/ef-core-patterns.md` | DbContext, Configurations, Performance |
| Testing | `references/testing-strategies.md` | Unit, Integration, Architecture tests |

---

## Architecture Overview

Clean Architecture organizes code into concentric layers with dependencies flowing inward:

```
┌─────────────────────────────────────────────────────────────┐
│                    INFRASTRUCTURE                           │
│  (EF Core, External APIs, File System, Message Brokers)     │
├─────────────────────────────────────────────────────────────┤
│                     PRESENTATION                            │
│  (Web API, Blazor, gRPC, GraphQL, Console)                  │
├─────────────────────────────────────────────────────────────┤
│                     APPLICATION                             │
│  (Use Cases, Commands, Queries, DTOs, Validators)           │
├─────────────────────────────────────────────────────────────┤
│                       DOMAIN                                │
│  (Entities, Value Objects, Aggregates, Domain Events)       │
└─────────────────────────────────────────────────────────────┘
```

### Key Principles

1. **Dependency Rule**: Dependencies flow inward only. Domain has zero external dependencies.
2. **Independence**: Business rules are isolated from frameworks, UI, and databases.
3. **Testability**: Core logic can be tested without external dependencies.
4. **Flexibility**: Infrastructure can change without affecting business logic.

---

## Solution Structure (December 2025)

```
📁 src/
├── 📁 Domain/                          # Core business logic (ZERO dependencies)
│   ├── 📁 Common/
│   │   ├── BaseEntity.cs
│   │   ├── BaseAuditableEntity.cs
│   │   ├── ValueObject.cs
│   │   └── IAggregateRoot.cs
│   ├── 📁 Entities/
│   ├── 📁 ValueObjects/
│   ├── 📁 Events/
│   ├── 📁 Enums/
│   └── 📁 Exceptions/
│
├── 📁 Application/                     # Use cases and orchestration
│   ├── 📁 Common/
│   │   ├── 📁 Behaviors/               # MediatR pipeline behaviors
│   │   ├── 📁 Interfaces/              # Abstractions for infrastructure
│   │   ├── 📁 Models/                  # Result, PaginatedList, etc.
│   │   └── 📁 Exceptions/
│   ├── 📁 [Feature]/                   # Feature folders
│   │   ├── 📁 Commands/
│   │   │   └── 📁 [CommandName]/
│   │   │       ├── [CommandName]Command.cs
│   │   │       ├── [CommandName]CommandHandler.cs
│   │   │       └── [CommandName]CommandValidator.cs
│   │   ├── 📁 Queries/
│   │   └── 📁 EventHandlers/
│   └── ConfigureServices.cs
│
├── 📁 Infrastructure/                  # External concerns
│   ├── 📁 Data/
│   │   ├── ApplicationDbContext.cs
│   │   └── 📁 Configurations/
│   ├── 📁 Repositories/
│   ├── 📁 Services/
│   └── ConfigureServices.cs
│
└── 📁 Web/                             # Presentation layer
    ├── 📁 Endpoints/                   # Minimal API or FastEndpoints
    ├── 📁 Controllers/                 # MVC Controllers (if used)
    ├── 📁 Filters/
    └── Program.cs

📁 tests/
├── 📁 Domain.UnitTests/
├── 📁 Application.UnitTests/
├── 📁 Application.IntegrationTests/
└── 📁 Architecture.Tests/
```

---

## Core Patterns

### 1. Entity Base Class

```csharp
namespace Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; protected set; }
    
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

public interface IAggregateRoot { }
```

### 2. Value Object Base

```csharp
namespace Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();
    
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }
    
    public override int GetHashCode() =>
        GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
}
```

### 3. Result Pattern

```csharp
namespace Application.Common.Models;

public sealed class Result<T>
{
    public T Value { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    
    private Result(T value, bool isSuccess, Error error)
    {
        Value = value;
        IsSuccess = isSuccess;
        Error = error;
    }
    
    public static Result<T> Success(T value) => new(value, true, Error.None);
    public static Result<T> Failure(Error error) => new(default!, false, error);
    
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error);
}

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
}

public enum ErrorType { None, Failure, Validation, NotFound, Conflict, Unauthorized, Forbidden }
```

### 4. CQRS Command Example

```csharp
// Command
public sealed record CreateOrderCommand(
    Guid CustomerId,
    AddressDto ShippingAddress,
    List<OrderItemDto> Items
) : ICommand<Result<OrderDto>>;

// Validator
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.ShippingAddress).NotNull().SetValidator(new AddressDtoValidator());
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 50);
        RuleForEach(x => x.Items).SetValidator(new OrderItemDtoValidator());
    }
}

// Handler
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
        var customer = await context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);
        
        if (customer is null)
            return Result.Failure<OrderDto>(Error.NotFound("Customer.NotFound", "Customer not found"));
        
        var order = Order.Create(customer.CustomerId, 
            Address.Create(request.ShippingAddress.Street, request.ShippingAddress.City, 
                request.ShippingAddress.State, request.ShippingAddress.PostalCode, 
                request.ShippingAddress.Country));
        
        foreach (var item in request.Items)
            order.AddItem(new ProductId(item.ProductId), item.ProductName, 
                Money.USD(item.UnitPrice), item.Quantity);
        
        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Created order {OrderId}", order.OrderId);
        return Result.Success(mapper.Map<OrderDto>(order));
    }
}
```

### 5. Validation Pipeline Behavior

```csharp
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
        if (!validators.Any()) return await next();
        
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        
        if (failures.Count != 0)
        {
            logger.LogWarning("Validation failed for {RequestType}", typeof(TRequest).Name);
            throw new ValidationException(failures);
        }
        
        return await next();
    }
}
```

### 6. Minimal API Endpoint

```csharp
public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();
        
        group.MapPost("/", async (CreateOrderCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.Match(
                order => Results.Created($"/api/orders/{order.Id}", order),
                error => error.Type switch
                {
                    ErrorType.Validation => Results.ValidationProblem(
                        new Dictionary<string, string[]> { { error.Code, [error.Message] } }),
                    ErrorType.NotFound => Results.NotFound(new { error.Code, error.Message }),
                    _ => Results.Problem(statusCode: 500, title: error.Message)
                });
        });
        
        return group;
    }
}
```

---

## Constraints

### MUST DO

- Domain project has ZERO external package references
- Enable nullable reference types in all projects
- Use async/await with CancellationToken for all I/O
- Validate all commands before handler execution
- Return Result<T> for expected failures (not exceptions)
- Use strongly-typed IDs for entity identification
- Enforce all business rules in aggregate roots
- Map to DTOs before returning from API

### MUST NOT DO

- Never expose EF entities directly in API responses
- No public setters on entities (use methods)
- No cross-aggregate references by entity (only by ID)
- No blocking async (.Result, .Wait())
- No business logic in controllers/endpoints
- No database calls in Domain layer
- No circular project dependencies
- No magic strings (use constants/configuration)

---

## Package Versions (December 2025)

```xml
<PackageVersion Include="MediatR" Version="12.4.1" />
<PackageVersion Include="FluentValidation" Version="11.10.0" />
<PackageVersion Include="AutoMapper" Version="13.0.1" />
<PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.11" />
<PackageVersion Include="Ardalis.Specification" Version="9.3.1" />
<PackageVersion Include="Ardalis.SmartEnum" Version="8.1.0" />
<PackageVersion Include="ErrorOr" Version="2.0.1" />
<PackageVersion Include="FastEndpoints" Version="7.1.1" />
<PackageVersion Include="Serilog.AspNetCore" Version="8.0.3" />
```

---

## Architecture Selection Guide

| Team Size | Domain Complexity | Recommended Pattern |
|-----------|-------------------|---------------------|
| <5 | Simple CRUD | Vertical Slice + REPR |
| 5-15 | Moderate | Clean Architecture |
| 15-50 | Complex | Clean + DDD + Modular Monolith |
| 50+ | Very Complex | Microservices (specific bounded contexts) |

---

## Related Skills

- **testcontainers-dotnet** - Integration testing
- **dotnet-ef-migrations** - Database migrations
- **resilience-patterns** - Polly and fault tolerance
- **azure-expert** - Cloud deployment

## References

- Jason Taylor Template: https://github.com/jasontaylordev/CleanArchitecture
- Ardalis Template: https://github.com/ardalis/CleanArchitecture
- Microsoft eShopOnWeb: https://github.com/dotnet-architecture/eShopOnWeb
- Microsoft Microservices Guide: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/
