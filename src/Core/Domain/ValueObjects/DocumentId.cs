
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for Document entities.
/// Ensures type safety and prevents mixing up different entity IDs.
/// </summary>
public sealed class DocumentId : StronglyTypedId<DocumentId>
{
    public DocumentId(Guid value) : base(value) { }
    public DocumentId() : base() { }

    /// <summary>
    /// Creates a DocumentId from a string representation.
    /// Used for API endpoints and user input.
    /// </summary>
    public static DocumentId FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(value));

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"Invalid Document ID format: {value}", nameof(value));

        return new DocumentId(guid);
    }

    /// <summary>
    /// Creates a DocumentId for testing purposes with a predictable value.
    /// </summary>
    public static DocumentId ForTesting(int seed = 1)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new DocumentId(new Guid(bytes));
    }
}

/// <summary>
/// Strongly-typed identifier for Template entities.
/// </summary>
public sealed class TemplateId : StronglyTypedId<TemplateId>
{
    public TemplateId(Guid value) : base(value) { }
    public TemplateId() : base() { }

    public static TemplateId FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Template ID cannot be null or empty", nameof(value));

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"Invalid Template ID format: {value}", nameof(value));

        return new TemplateId(guid);
    }

    public static TemplateId ForTesting(int seed = 1)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new TemplateId(new Guid(bytes));
    }
}

/// <summary>
/// Strongly-typed identifier for User entities.
/// </summary>
public sealed class UserId : StronglyTypedId<UserId>
{
    public UserId(Guid value) : base(value) { }
    public UserId() : base() { }

    public static UserId FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("User ID cannot be null or empty", nameof(value));

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"Invalid User ID format: {value}", nameof(value));

        return new UserId(guid);
    }

    public static UserId ForTesting(int seed = 1)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new UserId(new Guid(bytes));
    }
}

/// <summary>
/// Strongly-typed identifier for Agent entities.
/// </summary>
public sealed class AgentId : StronglyTypedId<AgentId>
{
    public AgentId(Guid value) : base(value) { }
    public AgentId() : base() { }

    public static AgentId FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(value));

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"Invalid Agent ID format: {value}", nameof(value));

        return new AgentId(guid);
    }

    public static AgentId ForTesting(int seed = 1)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new AgentId(new Guid(bytes));
    }
}