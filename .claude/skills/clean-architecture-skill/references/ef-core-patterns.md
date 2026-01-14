# Entity Framework Core Patterns Reference

Complete guide to EF Core 8 configuration, optimization, and best practices.

## DbContext Configuration

### Base DbContext

```csharp
namespace Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _dateTime;
    private readonly IMediator _mediator;
    
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser currentUser,
        IDateTime dateTime,
        IMediator mediator)
        : base(options)
    {
        _currentUser = currentUser;
        _dateTime = dateTime;
        _mediator = mediator;
    }
    
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        
        base.OnModelCreating(modelBuilder);
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Audit trail
        UpdateAuditableEntities();
        
        var result = await base.SaveChangesAsync(cancellationToken);
        
        // Dispatch domain events after successful save
        await DispatchDomainEventsAsync();
        
        return result;
    }
    
    private void UpdateAuditableEntities()
    {
        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = _currentUser.UserId;
                    entry.Entity.CreatedAt = _dateTime.UtcNow;
                    break;
                    
                case EntityState.Modified:
                    entry.Entity.LastModifiedBy = _currentUser.UserId;
                    entry.Entity.LastModifiedAt = _dateTime.UtcNow;
                    break;
            }
        }
    }
    
    private async Task DispatchDomainEventsAsync()
    {
        var entities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
        
        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();
        
        entities.ForEach(e => e.ClearDomainEvents());
        
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent);
        }
    }
}
```

---

## Entity Configurations

### Order Configuration

```csharp
namespace Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        
        builder.HasKey(o => o.Id);
        
        // Strongly-typed ID conversion
        builder.Property(o => o.OrderId)
            .HasConversion(
                id => id.Value,
                value => OrderId.From(value))
            .IsRequired();
        
        builder.HasIndex(o => o.OrderId)
            .IsUnique()
            .HasDatabaseName("IX_Orders_OrderId");
        
        builder.Property(o => o.CustomerId)
            .HasConversion(
                id => id.Value,
                value => new CustomerId(value))
            .IsRequired();
        
        // SmartEnum conversion
        builder.Property(o => o.Status)
            .HasConversion(
                s => s.Value,
                v => OrderStatus.FromValue(v))
            .IsRequired();
        
        // Owned entity - Value Object as complex type
        builder.OwnsOne(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.Street)
                .HasColumnName("ShippingStreet")
                .HasMaxLength(200)
                .IsRequired();
                
            address.Property(a => a.City)
                .HasColumnName("ShippingCity")
                .HasMaxLength(100)
                .IsRequired();
                
            address.Property(a => a.State)
                .HasColumnName("ShippingState")
                .HasMaxLength(50);
                
            address.Property(a => a.PostalCode)
                .HasColumnName("ShippingPostalCode")
                .HasMaxLength(20);
                
            address.Property(a => a.Country)
                .HasColumnName("ShippingCountry")
                .HasMaxLength(3)
                .IsRequired();
        });
        
        builder.OwnsOne(o => o.BillingAddress, address =>
        {
            address.Property(a => a.Street)
                .HasColumnName("BillingStreet")
                .HasMaxLength(200);
            address.Property(a => a.City)
                .HasColumnName("BillingCity")
                .HasMaxLength(100);
            address.Property(a => a.State)
                .HasColumnName("BillingState")
                .HasMaxLength(50);
            address.Property(a => a.PostalCode)
                .HasColumnName("BillingPostalCode")
                .HasMaxLength(20);
            address.Property(a => a.Country)
                .HasColumnName("BillingCountry")
                .HasMaxLength(3);
        });
        
        // Money value object
        builder.OwnsOne(o => o.TotalAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalAmount")
                .HasPrecision(18, 2)
                .IsRequired();
                
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });
        
        // Tracking number
        builder.Property(o => o.TrackingNumber)
            .HasMaxLength(100);
        
        // Collection navigation - use backing field
        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Access collection through private field
        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        
        // Customer relationship
        builder.HasOne(o => o.Customer)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Audit fields
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.CreatedBy).HasMaxLength(256);
        builder.Property(o => o.LastModifiedBy).HasMaxLength(256);
        
        // Indexes for common queries
        builder.HasIndex(o => o.CustomerId)
            .HasDatabaseName("IX_Orders_CustomerId");
            
        builder.HasIndex(o => o.Status)
            .HasDatabaseName("IX_Orders_Status");
            
        builder.HasIndex(o => o.CreatedAt)
            .HasDatabaseName("IX_Orders_CreatedAt");
            
        builder.HasIndex(o => new { o.Status, o.CreatedAt })
            .HasDatabaseName("IX_Orders_Status_CreatedAt");
    }
}
```

### OrderItem Configuration

```csharp
namespace Infrastructure.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.ProductId)
            .HasConversion(
                id => id.Value,
                value => new ProductId(value))
            .IsRequired();
        
        builder.Property(i => i.ProductName)
            .HasMaxLength(200)
            .IsRequired();
        
        builder.OwnsOne(i => i.UnitPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("UnitPrice")
                .HasPrecision(18, 2)
                .IsRequired();
                
            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });
        
        builder.Property(i => i.Quantity)
            .IsRequired();
        
        // TotalPrice is computed - don't persist
        builder.Ignore(i => i.TotalPrice);
        
        // Index for product queries
        builder.HasIndex(i => i.ProductId)
            .HasDatabaseName("IX_OrderItems_ProductId");
    }
}
```

---

## Value Converters

### Custom Value Converters

```csharp
namespace Infrastructure.Data.Converters;

/// <summary>
/// Generic converter for strongly-typed IDs.
/// </summary>
public class StronglyTypedIdConverter<TId, TValue> : ValueConverter<TId, TValue>
    where TId : StronglyTypedId<TValue>
    where TValue : notnull
{
    public StronglyTypedIdConverter()
        : base(
            id => id.Value,
            value => (TId)Activator.CreateInstance(typeof(TId), value)!)
    {
    }
}

/// <summary>
/// Converter for SmartEnum types.
/// </summary>
public class SmartEnumConverter<TEnum, TValue> : ValueConverter<TEnum, TValue>
    where TEnum : SmartEnum<TEnum, TValue>
    where TValue : IEquatable<TValue>, IComparable<TValue>
{
    public SmartEnumConverter()
        : base(
            @enum => @enum.Value,
            value => SmartEnum<TEnum, TValue>.FromValue(value))
    {
    }
}

/// <summary>
/// Converter for Email value object.
/// </summary>
public class EmailConverter : ValueConverter<Email, string>
{
    public EmailConverter()
        : base(
            email => email.Value,
            value => Email.Create(value))
    {
    }
}
```

### Applying Converters Globally

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    // Apply converters for all strongly-typed IDs
    configurationBuilder.Properties<OrderId>()
        .HaveConversion<StronglyTypedIdConverter<OrderId, Guid>>();
    
    configurationBuilder.Properties<CustomerId>()
        .HaveConversion<StronglyTypedIdConverter<CustomerId, Guid>>();
    
    configurationBuilder.Properties<ProductId>()
        .HaveConversion<StronglyTypedIdConverter<ProductId, Guid>>();
    
    // Apply converter for all SmartEnums
    configurationBuilder.Properties<OrderStatus>()
        .HaveConversion<SmartEnumConverter<OrderStatus, int>>();
    
    // String length conventions
    configurationBuilder.Properties<string>()
        .HaveMaxLength(500);
    
    // Decimal precision
    configurationBuilder.Properties<decimal>()
        .HavePrecision(18, 2);
}
```

---

## Performance Optimization

### Query Optimization Patterns

```csharp
// 1. Use AsNoTracking for read-only queries
var orders = await context.Orders
    .AsNoTracking()
    .Where(o => o.Status == OrderStatus.Pending)
    .ToListAsync(cancellationToken);

// 2. Project to DTOs instead of loading full entities
var orderDtos = await context.Orders
    .AsNoTracking()
    .Where(o => o.CustomerId == customerId)
    .Select(o => new OrderDto
    {
        Id = o.OrderId.Value,
        Status = o.Status.Name,
        Total = o.TotalAmount.Amount,
        ItemCount = o.Items.Count
    })
    .ToListAsync(cancellationToken);

// 3. Use AsSplitQuery for multiple collections
var ordersWithItems = await context.Orders
    .Include(o => o.Items)
    .Include(o => o.Customer)
    .AsSplitQuery()  // Prevents cartesian explosion
    .ToListAsync(cancellationToken);

// 4. Explicit loading when needed
var order = await context.Orders.FindAsync(orderId);
await context.Entry(order)
    .Collection(o => o.Items)
    .LoadAsync(cancellationToken);

// 5. Filtered includes (EF Core 5+)
var ordersWithPendingItems = await context.Orders
    .Include(o => o.Items.Where(i => i.Quantity > 0))
    .ToListAsync(cancellationToken);

// 6. Pagination with keyset (cursor) for large datasets
var lastOrderId = request.LastOrderId;
var orders = await context.Orders
    .AsNoTracking()
    .Where(o => o.Id > lastOrderId)
    .OrderBy(o => o.Id)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
```

### Avoiding N+1 Queries

```csharp
// BAD: N+1 query problem
var orders = await context.Orders.ToListAsync();
foreach (var order in orders)
{
    // Each iteration triggers a new query!
    var items = order.Items; // Lazy loading causes N additional queries
}

// GOOD: Eager loading with Include
var orders = await context.Orders
    .Include(o => o.Items)
    .ToListAsync();

// GOOD: Projection to avoid loading entities
var orderSummaries = await context.Orders
    .Select(o => new
    {
        o.OrderId,
        o.Status,
        ItemCount = o.Items.Count,
        TotalValue = o.Items.Sum(i => i.UnitPrice.Amount * i.Quantity)
    })
    .ToListAsync();
```

### Bulk Operations (EF Core 7+)

```csharp
// Bulk delete - single SQL statement
await context.Orders
    .Where(o => o.Status == OrderStatus.Cancelled && o.CreatedAt < cutoffDate)
    .ExecuteDeleteAsync(cancellationToken);

// Bulk update - single SQL statement
await context.Products
    .Where(p => p.Category == "Electronics")
    .ExecuteUpdateAsync(
        s => s.SetProperty(p => p.Price, p => p.Price * 1.1m),
        cancellationToken);

// Bulk update with multiple properties
await context.Orders
    .Where(o => o.Status == OrderStatus.Shipped && o.ShippedAt < DateTime.UtcNow.AddDays(-30))
    .ExecuteUpdateAsync(
        s => s
            .SetProperty(o => o.Status, OrderStatus.Delivered)
            .SetProperty(o => o.LastModifiedAt, DateTime.UtcNow),
        cancellationToken);
```

### Compiled Queries

```csharp
// Define compiled query as static field
private static readonly Func<ApplicationDbContext, Guid, Task<Order?>> GetOrderByIdQuery =
    EF.CompileAsyncQuery(
        (ApplicationDbContext context, Guid orderId) =>
            context.Orders
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderId == OrderId.From(orderId)));

// Use compiled query
public async Task<Order?> GetOrderByIdAsync(Guid orderId)
{
    return await GetOrderByIdQuery(_context, orderId);
}
```

---

## Repository Pattern with Specification

### Generic Repository

```csharp
namespace Infrastructure.Repositories;

using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;

public class Repository<T> : RepositoryBase<T>, IRepository<T>
    where T : class, IAggregateRoot
{
    private readonly ApplicationDbContext _context;
    
    public Repository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }
    
    public async Task<T?> GetByIdAsync<TId>(TId id, CancellationToken ct = default)
        where TId : notnull
    {
        return await _context.Set<T>().FindAsync(new object[] { id }, ct);
    }
    
    public IQueryable<T> AsQueryable() => _context.Set<T>().AsQueryable();
}

public class ReadRepository<T> : RepositoryBase<T>, IReadRepository<T>
    where T : class
{
    public ReadRepository(ApplicationDbContext context) : base(context) { }
}
```

### Specification Examples

```csharp
namespace Domain.Specifications;

using Ardalis.Specification;

public sealed class OrdersByCustomerSpec : Specification<Order>
{
    public OrdersByCustomerSpec(CustomerId customerId)
    {
        Query
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt);
    }
}

public sealed class PendingOrdersSpec : Specification<Order>
{
    public PendingOrdersSpec()
    {
        Query
            .Where(o => o.Status == OrderStatus.Pending)
            .Include(o => o.Customer)
            .OrderBy(o => o.CreatedAt);
    }
}

public sealed class OrderWithItemsSpec : SingleResultSpecification<Order>
{
    public OrderWithItemsSpec(OrderId orderId)
    {
        Query
            .Where(o => o.OrderId == orderId)
            .Include(o => o.Items)
            .Include(o => o.Customer);
    }
}

public sealed class OrdersPaginatedSpec : Specification<Order>
{
    public OrdersPaginatedSpec(int pageNumber, int pageSize, OrderStatus? status = null)
    {
        if (status.HasValue)
        {
            Query.Where(o => o.Status == status.Value);
        }
        
        Query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);
    }
}
```

---

## Database Initialization

```csharp
namespace Infrastructure.Data;

public class ApplicationDbContextInitialiser
{
    private readonly ILogger<ApplicationDbContextInitialiser> _logger;
    private readonly ApplicationDbContext _context;
    
    public ApplicationDbContextInitialiser(
        ILogger<ApplicationDbContextInitialiser> logger,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }
    
    public async Task InitialiseAsync()
    {
        try
        {
            await _context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while migrating the database");
            throw;
        }
    }
    
    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }
    
    private async Task TrySeedAsync()
    {
        // Seed default data
        if (!await _context.Customers.AnyAsync())
        {
            var customer = Customer.Create(
                Email.Create("test@example.com"),
                "Test Customer",
                Address.Create("123 Main St", "Seattle", "WA", "98101", "USA"));
            
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Seeded default customer");
        }
    }
}
```

---

## Infrastructure DI Registration

```csharp
namespace Infrastructure;

public static class ConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");
        
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(30);
            });
            
            // Add interceptors
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            
            // Development settings
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });
        
        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
        
        // Services
        services.AddTransient<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        
        // Initializer
        services.AddScoped<ApplicationDbContextInitialiser>();
        
        return services;
    }
}
```

---

## Best Practices Summary

### Configuration
- Use `IEntityTypeConfiguration<T>` for each entity
- Apply configurations from assembly
- Use owned entities for value objects
- Configure proper cascade delete behavior

### Performance
- Always use `AsNoTracking()` for read queries
- Project to DTOs instead of loading entities
- Use `AsSplitQuery()` for multiple includes
- Use bulk operations for mass updates/deletes
- Consider compiled queries for hot paths
- Add proper indexes for common queries

### Patterns
- Use Repository pattern for aggregates only
- Use Specification pattern for reusable queries
- Dispatch domain events after SaveChanges
- Implement audit trail in SaveChangesAsync

### Avoid
- Lazy loading in web applications
- Loading more data than needed
- N+1 query patterns
- Blocking async operations
