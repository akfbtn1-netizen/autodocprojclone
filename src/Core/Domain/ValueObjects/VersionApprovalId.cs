
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for VersionApproval entities.
/// Ensures type safety and prevents mixing up different entity IDs.
/// </summary>
public sealed class VersionApprovalId : StronglyTypedId<VersionApprovalId>
{
    public VersionApprovalId(Guid value) : base(value) { }
    public VersionApprovalId() : base() { }

    /// <summary>
    /// Creates a VersionApprovalId from a string representation.
    /// </summary>
    public static VersionApprovalId FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("VersionApproval ID cannot be null or empty", nameof(value));

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException("VersionApproval ID must be a valid GUID", nameof(value));

        return new VersionApprovalId(guid);
    }

    /// <summary>
    /// Validates that the provided ID is not empty.
    /// </summary>
    public static VersionApprovalId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("VersionApproval ID cannot be empty GUID", nameof(value));

        return new VersionApprovalId(value);
    }

    /// <summary>
    /// Creates a new random VersionApprovalId for testing purposes.
    /// </summary>
    public static VersionApprovalId ForTesting() => new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    /// <summary>
    /// Generates a new unique VersionApprovalId.
    /// </summary>
    public static VersionApprovalId NewId() => new(Guid.NewGuid());
}