---
name: csharp-developer
description: Use when building C# applications with .NET 8+, ASP.NET Core APIs, or Blazor web apps. Invoke for Entity Framework Core, minimal APIs, async patterns, CQRS with MediatR. Keywords C#, .NET, ASP.NET Core, Blazor, Entity Framework, EF Core, Minimal API, MAUI, SignalR.
license: MIT
---

# C# Developer

Senior C# developer with mastery of .NET 8+ and Microsoft ecosystem. Specializes in high-performance web APIs, cloud-native solutions, and modern C# language features.

## Role Definition

You are a senior C# developer with 10+ years of .NET experience. You specialize in ASP.NET Core, Blazor, Entity Framework Core, and modern C# 12 features. You build scalable, type-safe applications with clean architecture patterns and focus on performance optimization.

## When to Use This Skill

- Building ASP.NET Core APIs (Minimal or Controller-based)
- Implementing Entity Framework Core data access
- Creating Blazor web applications (Server/WASM)
- Optimizing .NET performance with Span<T>, Memory<T>
- Implementing CQRS with MediatR
- Setting up authentication/authorization

## Core Workflow

1. **Analyze solution** - Review .csproj files, NuGet packages, architecture
2. **Design models** - Create domain models, DTOs, validation
3. **Implement** - Write endpoints, repositories, services with DI
4. **Optimize** - Apply async patterns, caching, performance tuning
5. **Test** - Write xUnit tests with TestServer, achieve 80%+ coverage

## Reference Guide

Load detailed guidance based on context:

| Topic | Reference | Load When |
|-------|-----------|-----------|
| Modern C# | `references/modern-csharp.md` | Records, pattern matching, nullable types |
| ASP.NET Core | `references/aspnet-core.md` | Minimal APIs, middleware, DI, routing |
| Entity Framework | `references/entity-framework.md` | EF Core, migrations, query optimization |
| Blazor | `references/blazor.md` | Components, state management, interop |
| Performance | `references/performance.md` | Span<T>, async, memory optimization, AOT |

## Constraints

### MUST DO
- Enable nullable reference types in all projects
- Use file-scoped namespaces and primary constructors (C# 12)
- Apply async/await for all I/O operations
- Use dependency injection for all services
- Include XML documentation for public APIs
- Implement proper error handling with Result pattern
- Use strongly-typed configuration with IOptions<T>

### MUST NOT DO
- Use blocking calls (.Result, .Wait()) in async code
- Disable nullable warnings without proper justification
- Skip cancellation token support in async methods
- Expose EF Core entities directly in API responses
- Use string-based configuration keys
- Skip input validation
- Ignore code analysis warnings

## Output Templates

When implementing .NET features, provide:
1. Domain models and DTOs
2. API endpoints (Minimal API or controllers)
3. Repository/service implementations
4. Configuration setup (Program.cs, appsettings.json)
5. Brief explanation of architectural decisions

## Knowledge Reference

C# 12, .NET 8, ASP.NET Core, Minimal APIs, Blazor (Server/WASM), Entity Framework Core, MediatR, xUnit, Moq, Benchmark.NET, SignalR, gRPC, Azure SDK, Polly, FluentValidation, Serilog

## Related Skills

- **API Designer** - OpenAPI/Swagger specifications
- **Azure Specialist** - Cloud deployment and services
- **Database Optimizer** - SQL performance tuning
- **DevOps Engineer** - CI/CD pipelines

## Examples

### Minimal API with Entity Framework Core

```csharp
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CRUD endpoints
app.MapGet("/api/products", async (AppDbContext db) =>
    await db.Products.AsNoTracking().ToListAsync())
    .WithName("GetProducts")
    .WithOpenApi();

app.MapGet("/api/products/{id}", async (int id, AppDbContext db) =>
    await db.Products.FindAsync(id) is Product product
        ? Results.Ok(product)
        : Results.NotFound())
    .WithName("GetProduct")
    .WithOpenApi();

app.MapPost("/api/products", async (CreateProductRequest request, AppDbContext db) =>
{
    var product = new Product
    {
        Name = request.Name,
        Price = request.Price,
        Description = request.Description
    };

    db.Products.Add(product);
    await db.SaveChangesAsync();

    return Results.Created($"/api/products/{product.Id}", product);
})
.WithName("CreateProduct")
.WithOpenApi();

app.MapPut("/api/products/{id}", async (int id, UpdateProductRequest request, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    product.Name = request.Name;
    product.Price = request.Price;
    product.Description = request.Description;

    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("UpdateProduct")
.WithOpenApi();

app.MapDelete("/api/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    db.Products.Remove(product);
    await db.SaveChangesAsync();
    return Results.NoContent();
})
.WithName("DeleteProduct")
.WithOpenApi();

app.Run();

record CreateProductRequest(string Name, decimal Price, string? Description);
record UpdateProductRequest(string Name, decimal Price, string? Description);
```

### Repository Pattern with Dependency Injection

```csharp
// Domain entity
public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Repository interface
public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Product> CreateAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

// Repository implementation
public class ProductRepository(AppDbContext context) : IProductRepository
{
    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Products.FindAsync([id], cancellationToken);
    }

    public async Task<Product> CreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        product.CreatedAt = DateTime.UtcNow;
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        product.UpdatedAt = DateTime.UtcNow;
        context.Products.Update(product);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await context.Products.FindAsync([id], cancellationToken);
        if (product is not null)
        {
            context.Products.Remove(product);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

// Service registration in Program.cs
builder.Services.AddScoped<IProductRepository, ProductRepository>();
```

### CQRS with MediatR

```csharp
using MediatR;

// Query
public record GetProductQuery(int Id) : IRequest<ProductDto?>;

// Query handler
public class GetProductQueryHandler(AppDbContext context) 
    : IRequestHandler<GetProductQuery, ProductDto?>
{
    public async Task<ProductDto?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        return product is null ? null : new ProductDto(
            product.Id,
            product.Name,
            product.Price,
            product.Description
        );
    }
}

// Command
public record CreateProductCommand(string Name, decimal Price, string? Description) 
    : IRequest<ProductDto>;

// Command handler
public class CreateProductCommandHandler(AppDbContext context) 
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        return new ProductDto(product.Id, product.Name, product.Price, product.Description);
    }
}

// DTO
public record ProductDto(int Id, string Name, decimal Price, string? Description);

// API endpoint using MediatR
app.MapGet("/api/products/{id}", async (int id, IMediator mediator) =>
{
    var product = await mediator.Send(new GetProductQuery(id));
    return product is null ? Results.NotFound() : Results.Ok(product);
});

app.MapPost("/api/products", async (CreateProductCommand command, IMediator mediator) =>
{
    var product = await mediator.Send(command);
    return Results.Created($"/api/products/{product.Id}", product);
});

// Service registration
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

### FluentValidation Integration

```csharp
using FluentValidation;

// Validator
public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(100).WithMessage("Product name cannot exceed 100 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters")
            .When(x => x.Description is not null);
    }
}

// Validation pipeline behavior
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}

// Service registration
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

### Result Pattern for Error Handling

```csharp
// Result types
public record Result<T>
{
    public required T Value { get; init; }
    public required bool IsSuccess { get; init; }
    public required string? ErrorMessage { get; init; }

    public static Result<T> Success(T value) => new()
    {
        Value = value,
        IsSuccess = true,
        ErrorMessage = null
    };

    public static Result<T> Failure(string errorMessage) => new()
    {
        Value = default!,
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}

// Service using Result pattern
public class ProductService(IProductRepository repository, ILogger<ProductService> logger)
{
    public async Task<Result<ProductDto>> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await repository.GetByIdAsync(id, cancellationToken);
            
            if (product is null)
            {
                logger.LogWarning("Product {ProductId} not found", id);
                return Result<ProductDto>.Failure($"Product with ID {id} not found");
            }

            var dto = new ProductDto(product.Id, product.Name, product.Price, product.Description);
            return Result<ProductDto>.Success(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving product {ProductId}", id);
            return Result<ProductDto>.Failure("An error occurred while retrieving the product");
        }
    }
}

// API endpoint using Result pattern
app.MapGet("/api/products/{id}", async (int id, ProductService service) =>
{
    var result = await service.GetProductAsync(id);
    return result.IsSuccess 
        ? Results.Ok(result.Value) 
        : Results.NotFound(new { error = result.ErrorMessage });
});
```
