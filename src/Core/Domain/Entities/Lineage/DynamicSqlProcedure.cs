namespace Enterprise.Documentation.Core.Domain.Entities.Lineage;

/// <summary>
/// Tracks procedures that use dynamic SQL which cannot be statically analyzed.
/// These require manual review for accurate lineage tracking.
/// </summary>
public class DynamicSqlProcedure
{
    public int Id { get; private set; }
    public string SchemaName { get; private set; } = string.Empty;
    public string ProcedureName { get; private set; } = string.Empty;
    public DynamicSqlType DynamicSqlType { get; private set; }
    public string? DetectedPattern { get; private set; }
    public int? LineNumber { get; private set; }
    public RiskLevel RiskLevel { get; private set; }
    public bool ManuallyReviewed { get; private set; }
    public string? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }
    public string? KnownTargets { get; private set; }
    public DateTime DetectedAt { get; private set; }
    public Guid? SourceScanId { get; private set; }

    private DynamicSqlProcedure() { } // EF Core

    public static DynamicSqlProcedure Create(
        string schemaName,
        string procedureName,
        DynamicSqlType dynamicSqlType,
        string? detectedPattern = null,
        int? lineNumber = null)
    {
        var riskLevel = DetermineRiskLevel(dynamicSqlType, detectedPattern);

        return new DynamicSqlProcedure
        {
            SchemaName = schemaName,
            ProcedureName = procedureName,
            DynamicSqlType = dynamicSqlType,
            DetectedPattern = detectedPattern,
            LineNumber = lineNumber,
            RiskLevel = riskLevel,
            DetectedAt = DateTime.UtcNow
        };
    }

    public void MarkAsReviewed(string reviewedBy, string? notes = null, string? knownTargets = null)
    {
        ManuallyReviewed = true;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
        KnownTargets = knownTargets;
    }

    public void UpdateKnownTargets(string knownTargets)
    {
        KnownTargets = knownTargets;
    }

    public void SetSourceScan(Guid scanId)
    {
        SourceScanId = scanId;
    }

    public string FullName => $"{SchemaName}.{ProcedureName}";

    private static RiskLevel DetermineRiskLevel(DynamicSqlType type, string? pattern)
    {
        // Higher risk for patterns that could affect critical operations
        if (pattern?.Contains("DELETE", StringComparison.OrdinalIgnoreCase) == true ||
            pattern?.Contains("DROP", StringComparison.OrdinalIgnoreCase) == true ||
            pattern?.Contains("TRUNCATE", StringComparison.OrdinalIgnoreCase) == true)
        {
            return RiskLevel.Critical;
        }

        return type switch
        {
            DynamicSqlType.ExecString => RiskLevel.High,      // Most dangerous - SQL injection risk
            DynamicSqlType.ExecVariable => RiskLevel.Medium,  // Variable execution
            DynamicSqlType.SpExecuteSql => RiskLevel.High,    // Parameterized but still dynamic
            DynamicSqlType.OpenQuery => RiskLevel.Critical,   // Cross-server execution
            _ => RiskLevel.Medium
        };
    }
}

/// <summary>
/// Types of dynamic SQL patterns detected
/// </summary>
public enum DynamicSqlType
{
    SpExecuteSql,   // EXEC sp_executesql @sql
    ExecString,     // EXEC ('SELECT ...')
    ExecVariable,   // EXEC (@sql)
    OpenQuery,      // OPENQUERY, OPENROWSET
    Other           // Other dynamic patterns
}

/// <summary>
/// Risk levels for dynamic SQL procedures
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}
