
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Base class for all value objects in the Enterprise Documentation Platform.
/// Provides equality comparison and immutability enforcement.
/// </summary>
public abstract class BaseValueObject : IEquatable<BaseValueObject>
{
    /// <summary>
    /// Gets the atomic values that make up this value object.
    /// Used for equality comparison and hashing.
    /// </summary>
    /// <returns>Collection of atomic values</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (BaseValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public bool Equals(BaseValueObject? other)
    {
        return Equals((object?)other);
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Where(x => x != null)
            .Aggregate(1, (current, obj) => current * 23 + obj!.GetHashCode());
    }

    public static bool operator ==(BaseValueObject? left, BaseValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(BaseValueObject? left, BaseValueObject? right)
    {
        return !Equals(left, right);
    }

    /// <summary>
    /// Creates a shallow copy of the value object.
    /// Since value objects are immutable, this returns the same instance.
    /// </summary>
    public BaseValueObject Copy() => this;
}