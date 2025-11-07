
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for AuditLog entities.
/// Ensures type safety and prevents mixing up different entity IDs.
/// </summary>
public sealed class AuditLogId : StronglyTypedId<AuditLogId>
{
    public AuditLogId(Guid value) : base(value) { }
    public AuditLogId() : base() { }

    /// <summary>
    /// Creates an AuditLogId from a string representation.
    /// </summary>
    public static AuditLogId FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("AuditLog ID cannot be null or empty", nameof(value));

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException("AuditLog ID must be a valid GUID", nameof(value));

        return new AuditLogId(guid);
    }

    /// <summary>
    /// Validates that the provided ID is not empty.
    /// </summary>
    public static AuditLogId FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("AuditLog ID cannot be empty GUID", nameof(value));

        return new AuditLogId(value);
    }

    /// <summary>
    /// Creates a new random AuditLogId for testing purposes.
    /// </summary>
    public static AuditLogId ForTesting() => new(Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"));

    /// <summary>
    /// Generates a new unique AuditLogId.
    /// </summary>
    public static AuditLogId NewId() => new(Guid.NewGuid());
}