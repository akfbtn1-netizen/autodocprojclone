# Testing Strategies Reference

Comprehensive guide to testing Clean Architecture .NET 8 applications.

## Testing Pyramid

```
            ┌───────────────┐
            │   E2E Tests   │  ← Few, slow, verify complete flows
            └───────────────┘
          ┌───────────────────┐
          │ Integration Tests │  ← Moderate, test boundaries
          └───────────────────┘
        ┌───────────────────────┐
        │    Unit Tests         │  ← Many, fast, test logic
        └───────────────────────┘
```

---

## Domain Unit Tests

### Entity Tests

```csharp
namespace Domain.UnitTests.Entities;

using FluentAssertions;

public class OrderTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateOrder()
    {
        // Arrange
        var customerId = new CustomerId(Guid.NewGuid());
        var address = Address.Create("123 Main St", "Seattle", "WA", "98101", "USA");
        
        // Act
        var order = Order.Create(customerId, address);
        
        // Assert
        order.Should().NotBeNull();
        order.CustomerId.Should().Be(customerId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<OrderCreatedEvent>();
    }
    
    [Fact]
    public void AddItem_WhenPending_ShouldAddAndRecalculate()
    {
        // Arrange
        var order = CreateTestOrder();
        
        // Act
        order.AddItem(new ProductId(Guid.NewGuid()), "Product", Money.USD(29.99m), 2);
        
        // Assert
        order.Items.Should().ContainSingle();
        order.TotalAmount.Amount.Should().Be(59.98m);
    }
    
    [Fact]
    public void Confirm_WithNoItems_ShouldThrow()
    {
        var order = CreateTestOrder();
        
        var act = () => order.Confirm();
        
        act.Should().Throw<DomainException>().WithMessage("*no items*");
    }
    
    private static Order CreateTestOrder() =>
        Order.Create(new CustomerId(Guid.NewGuid()), 
            Address.Create("123 Main St", "Seattle", "WA", "98101", "USA"));
}
```

### Value Object Tests

```csharp
public class MoneyTests
{
    [Fact]
    public void Add_WithSameCurrency_ShouldReturnSum()
    {
        var result = Money.USD(10m).Add(Money.USD(5m));
        result.Amount.Should().Be(15m);
    }
    
    [Fact]
    public void Add_WithDifferentCurrency_ShouldThrow()
    {
        var act = () => Money.USD(10m).Add(Money.EUR(5m));
        act.Should().Throw<DomainException>();
    }
    
    [Fact]
    public void Equals_SameValues_ShouldBeTrue()
    {
        Money.USD(100m).Should().Be(Money.USD(100m));
    }
}
```

---

## Integration Tests with Testcontainers

### Test Infrastructure

```csharp
namespace Application.IntegrationTests;

using Testcontainers.MsSql;
using Respawn;

[Collection("Database")]
public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private static MsSqlContainer? _container;
    private static IServiceProvider? _serviceProvider;
    private static Respawner? _respawner;
    
    protected IServiceScope Scope { get; private set; } = null!;
    protected IMediator Mediator => Scope.ServiceProvider.GetRequiredService<IMediator>();
    protected ApplicationDbContext DbContext => Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    public async Task InitializeAsync()
    {
        if (_container is null)
        {
            _container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();
            await _container.StartAsync();
            
            var services = new ServiceCollection();
            services.AddApplicationServices();
            services.AddDbContext<ApplicationDbContext>(o => 
                o.UseSqlServer(_container.GetConnectionString()));
            services.AddScoped<ICurrentUser, TestCurrentUser>();
            
            _serviceProvider = services.BuildServiceProvider();
            
            using var scope = _serviceProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Database.MigrateAsync();
            
            _respawner = await Respawner.CreateAsync(_container.GetConnectionString());
        }
        
        Scope = _serviceProvider!.CreateScope();
    }
    
    public async Task DisposeAsync()
    {
        if (_respawner is not null && _container is not null)
            await _respawner.ResetAsync(_container.GetConnectionString());
        Scope?.Dispose();
    }
}
```

### Command Handler Tests

```csharp
public class CreateOrderCommandTests : BaseIntegrationTest
{
    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateOrder()
    {
        var customer = await CreateTestCustomerAsync();
        
        var command = new CreateOrderCommand(
            customer.CustomerId.Value,
            new AddressDto("456 Oak Ave", "Portland", "OR", "97201", "USA"),
            null,
            [new OrderItemDto(Guid.NewGuid(), "Product", 29.99m, 2)]);
        
        var result = await Mediator.Send(command);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(59.98m);
    }
    
    [Fact]
    public async Task Handle_WithNonExistentCustomer_ShouldReturnNotFound()
    {
        var command = new CreateOrderCommand(
            Guid.NewGuid(),
            new AddressDto("123 Main St", "Seattle", "WA", "98101", "USA"),
            null,
            [new OrderItemDto(Guid.NewGuid(), "Product", 10m, 1)]);
        
        var result = await Mediator.Send(command);
        
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
```

---

## Architecture Tests

```csharp
using NetArchTest.Rules;

public class ArchitectureTests
{
    [Fact]
    public void Domain_ShouldNot_DependOnOtherProjects()
    {
        var assembly = typeof(Domain.Entities.Order).Assembly;
        
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAll("Application", "Infrastructure", "Web")
            .GetResult();
        
        result.IsSuccessful.Should().BeTrue();
    }
    
    [Fact]
    public void Application_ShouldNot_DependOnInfrastructure()
    {
        var assembly = typeof(Application.ConfigureServices).Assembly;
        
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("Infrastructure")
            .GetResult();
        
        result.IsSuccessful.Should().BeTrue();
    }
    
    [Fact]
    public void Handlers_ShouldHave_SingleDependencyOnMediatR()
    {
        var assembly = typeof(Application.ConfigureServices).Assembly;
        
        var result = Types.InAssembly(assembly)
            .That().HaveNameEndingWith("Handler")
            .Should().ImplementInterface(typeof(IRequestHandler<,>))
            .GetResult();
        
        result.IsSuccessful.Should().BeTrue();
    }
}
```

---

## Test Utilities

### Fluent Builder

```csharp
public class OrderBuilder
{
    private CustomerId _customerId = new(Guid.NewGuid());
    private Address _address = Address.Create("123 Main", "City", "ST", "12345", "USA");
    private readonly List<(ProductId, string, Money, int)> _items = [];
    
    public OrderBuilder WithCustomer(CustomerId id) { _customerId = id; return this; }
    public OrderBuilder WithItem(string name, decimal price, int qty)
    {
        _items.Add((new ProductId(Guid.NewGuid()), name, Money.USD(price), qty));
        return this;
    }
    
    public Order Build()
    {
        var order = Order.Create(_customerId, _address);
        foreach (var (pid, name, price, qty) in _items)
            order.AddItem(pid, name, price, qty);
        return order;
    }
}
```

---

## Best Practices

| Type | Scope | Speed | When to Use |
|------|-------|-------|-------------|
| Unit | Domain logic | Fast | Business rules, value objects |
| Integration | Handlers, DB | Medium | Use cases, persistence |
| Architecture | Structure | Fast | Dependency rules |
| E2E | Full stack | Slow | Critical paths only |
