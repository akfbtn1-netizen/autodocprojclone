
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing the security classification of a document.
/// Implements enterprise security classification rules and access controls.
/// </summary>
public sealed class SecurityClassification : BaseValueObject
{
    public string Level { get; private set; }
    public List<string> AccessGroups { get; private set; }
    public bool RequiresPIIHandling { get; private set; }
    public DateTime ClassifiedAt { get; private set; }
    public UserId ClassifiedBy { get; private set; }

    // EF Core constructor - private parameterless constructor
    private SecurityClassification()
    {
        Level = string.Empty;
        AccessGroups = new List<string>();
        RequiresPIIHandling = false;
        ClassifiedAt = DateTime.UtcNow;
        ClassifiedBy = UserId.ForTesting();
    }

    private SecurityClassification(
        string level, 
        List<string> accessGroups, 
        bool requiresPIIHandling,
        DateTime classifiedAt,
        UserId classifiedBy)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        AccessGroups = accessGroups ?? throw new ArgumentNullException(nameof(accessGroups));
        RequiresPIIHandling = requiresPIIHandling;
        ClassifiedAt = classifiedAt;
        ClassifiedBy = classifiedBy ?? throw new ArgumentNullException(nameof(classifiedBy));
    }

    /// <summary>
    /// Creates a Public security classification.
    /// No access restrictions, no PII handling required.
    /// </summary>
    public static SecurityClassification Public(UserId classifiedBy)
    {
        return new SecurityClassification(
            "Public", 
            new List<string> { "Everyone" }, 
            false,
            DateTime.UtcNow,
            classifiedBy);
    }

    /// <summary>
    /// Creates an Internal security classification.
    /// Restricted to company employees.
    /// </summary>
    public static SecurityClassification Internal(UserId classifiedBy, List<string>? accessGroups = null)
    {
        return new SecurityClassification(
            "Internal", 
            accessGroups ?? new List<string> { "Employees" }, 
            false,
            DateTime.UtcNow,
            classifiedBy);
    }

    /// <summary>
    /// Creates a Confidential security classification.
    /// Restricted access, may contain PII.
    /// </summary>
    public static SecurityClassification Confidential(
        UserId classifiedBy, 
        List<string> accessGroups, 
        bool requiresPIIHandling = false)
    {
        if (accessGroups == null || !accessGroups.Any())
            throw new ArgumentException("Confidential documents must specify access groups", nameof(accessGroups));

        return new SecurityClassification(
            "Confidential", 
            accessGroups, 
            requiresPIIHandling,
            DateTime.UtcNow,
            classifiedBy);
    }

    /// <summary>
    /// Creates a Restricted security classification.
    /// Highest security level, always requires PII handling protocols.
    /// </summary>
    public static SecurityClassification Restricted(UserId classifiedBy, List<string> accessGroups)
    {
        if (accessGroups == null || !accessGroups.Any())
            throw new ArgumentException("Restricted documents must specify access groups", nameof(accessGroups));

        return new SecurityClassification(
            "Restricted", 
            accessGroups, 
            true, // Always requires PII handling
            DateTime.UtcNow,
            classifiedBy);
    }

    // Business rule methods
    public bool IsPublic => Level == "Public";
    public bool IsInternal => Level == "Internal";
    public bool IsConfidential => Level == "Confidential";
    public bool IsRestricted => Level == "Restricted";

    /// <summary>
    /// Gets the security level as a numeric value for comparison.
    /// Higher numbers indicate more restrictive access.
    /// </summary>
    public int SecurityLevel => Level switch
    {
        "Public" => 0,
        "Internal" => 1,
        "Confidential" => 2,
        "Restricted" => 3,
        _ => throw new InvalidOperationException($"Unknown security level: {Level}")
    };

    /// <summary>
    /// Determines if a user with specified access groups can access this document.
    /// </summary>
    public bool CanAccess(List<string> userAccessGroups)
    {
        if (userAccessGroups == null || !userAccessGroups.Any())
            return IsPublic;

        return AccessGroups.Any(ag => userAccessGroups.Contains(ag, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines if this classification can be downgraded to the specified level.
    /// Business rule: Can only downgrade classification levels.
    /// </summary>
    public bool CanDowngradeTo(SecurityClassification newClassification)
    {
        return newClassification.SecurityLevel < SecurityLevel;
    }

    /// <summary>
    /// Creates a new classification with updated access groups.
    /// Maintains the same security level and PII requirements.
    /// </summary>
    public SecurityClassification WithAccessGroups(List<string> newAccessGroups, UserId modifiedBy)
    {
        if (newAccessGroups == null || !newAccessGroups.Any())
            throw new ArgumentException("Access groups cannot be empty", nameof(newAccessGroups));

        return new SecurityClassification(
            Level,
            new List<string>(newAccessGroups),
            RequiresPIIHandling,
            DateTime.UtcNow,
            modifiedBy);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Level;
        yield return string.Join(",", AccessGroups.OrderBy(g => g));
        yield return RequiresPIIHandling;
        yield return ClassifiedAt;
        yield return ClassifiedBy.Value;
    }

    public override string ToString() => 
        $"{Level} (Groups: {string.Join(", ", AccessGroups)}, PII: {RequiresPIIHandling})";
}