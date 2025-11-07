
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for Version entities.
/// Ensures type safety and prevents mixing up different entity IDs.
/// </summary>
public sealed class VersionId : StronglyTypedId<VersionId>
{
    public VersionId(Guid value) : base(value) { }
    public VersionId() : base() { }

    /// <summary>
    /// Creates a VersionId from a string representation.
    /// </summary>
    public static VersionId FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Version ID cannot be null or empty", nameof(value));

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException("Version ID must be a valid GUID", nameof(value));

        return new VersionId(guid);
    }

    /// <summary>
    /// Validates that the provided ID is not empty.
    /// </summary>
    public static VersionId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Version ID cannot be empty GUID", nameof(value));

        return new VersionId(value);
    }

    /// <summary>
    /// Creates a new random VersionId for testing purposes.
    /// </summary>
    public static VersionId ForTesting() => new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    /// <summary>
    /// Generates a new unique VersionId.
    /// </summary>
    public static VersionId NewId() => new(Guid.NewGuid());
}