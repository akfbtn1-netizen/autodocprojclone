# Domain Modeling Reference

Comprehensive guide to Domain-Driven Design tactical patterns in .NET 8.

## Entity Design

### Base Entity with Domain Events

```csharp
namespace Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; protected set; }
    
    private readonly List<IDomainEvent> _domainEvents = [];
    
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
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

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
```

### Strongly-Typed IDs

```csharp
namespace Domain.Common;

/// <summary>
/// Base for strongly-typed IDs providing type safety.
/// </summary>
public abstract record StronglyTypedId<T>(T Value) where T : notnull
{
    public override string ToString() => Value.ToString() ?? string.Empty;
}

// Usage
public sealed record OrderId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid value) => new(value);
}

public sealed record CustomerId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
}

public sealed record ProductId(Guid Value) : StronglyTypedId<Guid>(Value);
```

### Entity with Strongly-Typed ID

```csharp
namespace Domain.Common;

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected Entity() { }
    protected Entity(TId id) => Id = id;
    
    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
    
    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        if (EqualityComparer<TId>.Default.Equals(Id, default) || 
            EqualityComparer<TId>.Default.Equals(other.Id, default))
            return false;
        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }
    
    public override int GetHashCode() => (GetType().ToString() + Id).GetHashCode();
    
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);
    
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}
```

---

## Value Objects

### Value Object Base Class

```csharp
namespace Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();
    
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;
        
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }
    
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }
    
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }
    
    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
```

### Money Value Object

```csharp
namespace Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }
    
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
    
    public static Money Create(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency is required");
        if (currency.Length != 3)
            throw new DomainException("Currency must be 3-letter ISO code");
        
        return new Money(amount, currency.ToUpperInvariant());
    }
    
    public static Money Zero(string currency) => Create(0, currency);
    public static Money USD(decimal amount) => Create(amount, "USD");
    public static Money EUR(decimal amount) => Create(amount, "EUR");
    
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return Create(Amount + other.Amount, Currency);
    }
    
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return Create(Amount - other.Amount, Currency);
    }
    
    public Money Multiply(decimal factor) => Create(Amount * factor, Currency);
    
    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Cannot operate on {Currency} and {other.Currency}");
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
    
    public override string ToString() => $"{Amount:N2} {Currency}";
}
```

### Email Value Object

```csharp
namespace Domain.ValueObjects;

public sealed class Email : ValueObject
{
    public string Value { get; }
    
    private Email(string value) => Value = value;
    
    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email cannot be empty");
        
        email = email.Trim().ToLowerInvariant();
        
        if (!IsValidEmail(email))
            throw new DomainException($"Invalid email format: {email}");
        
        return new Email(email);
    }
    
    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
    
    public override string ToString() => Value;
    public static implicit operator string(Email email) => email.Value;
}
```

### Address Value Object

```csharp
namespace Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }
    
    private Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }
    
    public static Address Create(string street, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new DomainException("Street is required");
        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City is required");
        if (string.IsNullOrWhiteSpace(country))
            throw new DomainException("Country is required");
        
        return new Address(
            street.Trim(),
            city.Trim(),
            state?.Trim() ?? string.Empty,
            postalCode?.Trim() ?? string.Empty,
            country.Trim().ToUpperInvariant()
        );
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }
    
    public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
```

---

## Aggregate Root Pattern

### Order Aggregate Example

```csharp
namespace Domain.Entities;

public sealed class Order : BaseAuditableEntity, IAggregateRoot
{
    private readonly List<OrderItem> _items = [];
    
    // Read-only collection access
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    
    // Properties with private setters
    public OrderId OrderId { get; private set; } = null!;
    public CustomerId CustomerId { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public Address ShippingAddress { get; private set; } = null!;
    public Address? BillingAddress { get; private set; }
    public Money TotalAmount { get; private set; } = null!;
    public DateTime? ShippedAt { get; private set; }
    public string? TrackingNumber { get; private set; }
    
    // EF Core requires parameterless constructor
    private Order() { }
    
    /// <summary>
    /// Factory method - the ONLY way to create an Order.
    /// </summary>
    public static Order Create(CustomerId customerId, Address shippingAddress, Address? billingAddress = null)
    {
        ArgumentNullException.ThrowIfNull(customerId);
        ArgumentNullException.ThrowIfNull(shippingAddress);
        
        var order = new Order
        {
            OrderId = OrderId.New(),
            CustomerId = customerId,
            ShippingAddress = shippingAddress,
            BillingAddress = billingAddress ?? shippingAddress,
            Status = OrderStatus.Pending,
            TotalAmount = Money.Zero("USD")
        };
        
        order.AddDomainEvent(new OrderCreatedEvent(order.OrderId));
        return order;
    }
    
    /// <summary>
    /// Add item through aggregate root to maintain invariants.
    /// </summary>
    public void AddItem(ProductId productId, string productName, Money unitPrice, int quantity)
    {
        GuardAgainstInvalidStatus(OrderStatus.Pending, "Cannot add items");
        
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero");
        
        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        
        if (existingItem is not null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            var item = OrderItem.Create(this, productId, productName, unitPrice, quantity);
            _items.Add(item);
        }
        
        RecalculateTotal();
    }
    
    /// <summary>
    /// Remove item from order.
    /// </summary>
    public void RemoveItem(ProductId productId)
    {
        GuardAgainstInvalidStatus(OrderStatus.Pending, "Cannot remove items");
        
        var item = _items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new DomainException($"Product {productId} not found");
        
        _items.Remove(item);
        RecalculateTotal();
    }
    
    /// <summary>
    /// Confirm the order for processing.
    /// </summary>
    public void Confirm()
    {
        GuardAgainstInvalidStatus(OrderStatus.Pending, "Cannot confirm");
        
        if (!_items.Any())
            throw new DomainException("Cannot confirm order with no items");
        
        Status = OrderStatus.Confirmed;
        AddDomainEvent(new OrderConfirmedEvent(OrderId));
    }
    
    /// <summary>
    /// Ship the order with tracking information.
    /// </summary>
    public void Ship(string trackingNumber)
    {
        GuardAgainstInvalidStatus(OrderStatus.Confirmed, "Cannot ship");
        
        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new DomainException("Tracking number is required");
        
        TrackingNumber = trackingNumber;
        ShippedAt = DateTime.UtcNow;
        Status = OrderStatus.Shipped;
        
        AddDomainEvent(new OrderShippedEvent(OrderId, trackingNumber));
    }
    
    /// <summary>
    /// Cancel the order.
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Shipped || Status == OrderStatus.Delivered)
            throw new DomainException($"Cannot cancel order in {Status} status");
        
        Status = OrderStatus.Cancelled;
        AddDomainEvent(new OrderCancelledEvent(OrderId, reason));
    }
    
    private void RecalculateTotal()
    {
        TotalAmount = _items.Aggregate(
            Money.Zero("USD"),
            (sum, item) => sum.Add(item.TotalPrice)
        );
    }
    
    private void GuardAgainstInvalidStatus(OrderStatus expectedStatus, string action)
    {
        if (Status != expectedStatus)
            throw new DomainException($"{action} - order is {Status}, expected {expectedStatus}");
    }
}
```

### OrderItem Entity (Child of Aggregate)

```csharp
namespace Domain.Entities;

/// <summary>
/// OrderItem can only be modified through Order aggregate root.
/// </summary>
public sealed class OrderItem : BaseEntity
{
    public ProductId ProductId { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }
    public Money TotalPrice => UnitPrice.Multiply(Quantity);
    
    // Back reference to aggregate root
    public int OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    
    private OrderItem() { }
    
    // Internal - only Order can create OrderItem
    internal static OrderItem Create(Order order, ProductId productId, string productName, 
        Money unitPrice, int quantity)
    {
        return new OrderItem
        {
            Order = order,
            ProductId = productId,
            ProductName = productName,
            UnitPrice = unitPrice,
            Quantity = quantity
        };
    }
    
    internal void IncreaseQuantity(int additionalQuantity)
    {
        if (additionalQuantity <= 0)
            throw new DomainException("Quantity must be positive");
        Quantity += additionalQuantity;
    }
    
    internal void DecreaseQuantity(int reduceBy)
    {
        if (reduceBy <= 0)
            throw new DomainException("Quantity must be positive");
        if (Quantity - reduceBy < 0)
            throw new DomainException("Cannot reduce quantity below zero");
        Quantity -= reduceBy;
    }
}
```

---

## Domain Events

```csharp
namespace Domain.Events;

// Base event
public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// Order events
public sealed record OrderCreatedEvent(OrderId OrderId) : DomainEvent;
public sealed record OrderConfirmedEvent(OrderId OrderId) : DomainEvent;
public sealed record OrderShippedEvent(OrderId OrderId, string TrackingNumber) : DomainEvent;
public sealed record OrderDeliveredEvent(OrderId OrderId) : DomainEvent;
public sealed record OrderCancelledEvent(OrderId OrderId, string Reason) : DomainEvent;

// Customer events
public sealed record CustomerCreatedEvent(CustomerId CustomerId, string Email) : DomainEvent;
public sealed record CustomerEmailChangedEvent(CustomerId CustomerId, string OldEmail, string NewEmail) : DomainEvent;
```

---

## SmartEnum for Status Types

```csharp
using Ardalis.SmartEnum;

namespace Domain.Enums;

public sealed class OrderStatus : SmartEnum<OrderStatus>
{
    public static readonly OrderStatus Pending = new(nameof(Pending), 1);
    public static readonly OrderStatus Confirmed = new(nameof(Confirmed), 2);
    public static readonly OrderStatus Processing = new(nameof(Processing), 3);
    public static readonly OrderStatus Shipped = new(nameof(Shipped), 4);
    public static readonly OrderStatus Delivered = new(nameof(Delivered), 5);
    public static readonly OrderStatus Cancelled = new(nameof(Cancelled), 6);
    public static readonly OrderStatus Refunded = new(nameof(Refunded), 7);
    
    public bool CanTransitionTo(OrderStatus newStatus) => (this, newStatus) switch
    {
        (var s, _) when s == Pending => newStatus == Confirmed || newStatus == Cancelled,
        (var s, _) when s == Confirmed => newStatus == Processing || newStatus == Cancelled,
        (var s, _) when s == Processing => newStatus == Shipped || newStatus == Cancelled,
        (var s, _) when s == Shipped => newStatus == Delivered,
        (var s, _) when s == Delivered => newStatus == Refunded,
        _ => false
    };
    
    public bool IsTerminal => this == Delivered || this == Cancelled || this == Refunded;
    public bool CanModifyItems => this == Pending;
    public bool CanCancel => this != Shipped && this != Delivered && this != Cancelled && this != Refunded;
    
    private OrderStatus(string name, int value) : base(name, value) { }
}
```

---

## Domain Exceptions

```csharp
namespace Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    
    public DomainException(string message, Exception innerException) 
        : base(message, innerException) { }
}

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.") { }
}

public class InvalidOperationDomainException : DomainException
{
    public InvalidOperationDomainException(string operation, string reason)
        : base($"Cannot perform '{operation}': {reason}") { }
}
```

---

## Design Rules Summary

### Aggregate Design Rules

1. **Single Transaction**: One aggregate per transaction
2. **Reference by ID**: Reference other aggregates by ID only
3. **Small Aggregates**: Keep aggregates as small as possible
4. **Consistency Boundary**: Aggregate maintains all invariants
5. **Factory Methods**: Use static factory methods for creation
6. **Domain Events**: Raise events for state changes

### Value Object Rules

1. **Immutable**: No setters, create new instances
2. **Self-Validating**: Validate in constructor/factory
3. **Equality by Value**: Compare all components
4. **No Identity**: No ID property
5. **Replaceable**: Swap whole object, don't modify

### Entity Rules

1. **Private Setters**: Control state changes
2. **Methods for Behavior**: Use methods, not property setters
3. **Protect Collections**: Expose IReadOnly, keep List private
4. **Validate State Changes**: Check invariants in methods
5. **Clear Identity**: Use strongly-typed IDs when possible
