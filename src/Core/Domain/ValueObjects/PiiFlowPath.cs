namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a PII data flow path through the system.
/// Critical for insurance business compliance tracking.
/// </summary>
public record PiiFlowPath
{
    public string SourceColumn { get; init; } = string.Empty;
    public string PiiType { get; init; } = string.Empty;
    public List<PiiFlowStep> Steps { get; init; } = new();
    public string FinalDestination { get; init; } = string.Empty;

    /// <summary>
    /// Check if PII flows to a specific object
    /// </summary>
    public bool FlowsTo(string objectName) =>
        Steps.Any(s => s.ObjectName.Equals(objectName, StringComparison.OrdinalIgnoreCase)) ||
        FinalDestination.Equals(objectName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Get all objects in the flow path
    /// </summary>
    public IEnumerable<string> GetAllObjects()
    {
        yield return SourceColumn;
        foreach (var step in Steps)
            yield return step.ObjectName;
        if (!string.IsNullOrEmpty(FinalDestination))
            yield return FinalDestination;
    }

    public int PathLength => Steps.Count + 1;

    public string ToPathString() =>
        $"{SourceColumn} -> {string.Join(" -> ", Steps.Select(s => s.ObjectName))} -> {FinalDestination}";
}

/// <summary>
/// A single step in a PII flow path
/// </summary>
public record PiiFlowStep
{
    public string ObjectName { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty; // READ, WRITE, TRANSFORM
    public bool IsTransformation { get; init; }
}

/// <summary>
/// Common PII types tracked in the system
/// </summary>
public static class PiiTypes
{
    public const string SSN = "SSN";
    public const string DateOfBirth = "DOB";
    public const string DriversLicense = "DriversLicense";
    public const string Email = "Email";
    public const string Phone = "Phone";
    public const string Address = "Address";
    public const string FinancialAccount = "FinancialAccount";
    public const string MedicalRecord = "MedicalRecord";
    public const string PolicyNumber = "PolicyNumber";
}
