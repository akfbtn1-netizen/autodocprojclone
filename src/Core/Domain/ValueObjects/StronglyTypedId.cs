
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Base class for strongly-typed identifiers in the Enterprise Documentation Platform.
/// Prevents primitive obsession and provides type safety for entity IDs.
/// </summary>
/// <typeparam name="T">The type of the strongly-typed ID</typeparam>
public abstract class StronglyTypedId<T> : BaseValueObject where T : StronglyTypedId<T>
{
    public Guid Value { get; }

    protected StronglyTypedId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ID cannot be empty", nameof(value));
        
        Value = value;
    }

    protected StronglyTypedId() : this(Guid.NewGuid())
    {
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    /// <summary>
    /// Creates a new instance with a specific GUID value.
    /// Used for deserialization and testing.
    /// </summary>
    public static TId From<TId>(Guid value) where TId : StronglyTypedId<TId>, new()
    {
        return (TId)Activator.CreateInstance(typeof(TId), value)!;
    }

    /// <summary>
    /// Creates a new instance with a new GUID value.
    /// </summary>
    public static TId New<TId>() where TId : StronglyTypedId<TId>, new()
    {
        return new TId();
    }

    /// <summary>
    /// Implicit conversion to Guid for database operations.
    /// </summary>
    public static implicit operator Guid(StronglyTypedId<T> id) => id.Value;

    /// <summary>
    /// Implicit conversion to string for serialization.
    /// </summary>
    public static implicit operator string(StronglyTypedId<T> id) => id.Value.ToString();
}