namespace Enterprise.Documentation.Core.Domain.Entities.Lineage;

/// <summary>
/// Pre-computed impact analysis scores for columns.
/// Used for quick risk assessment before schema changes.
/// </summary>
public class ColumnRiskScore
{
    public int Id { get; private set; }
    public string SchemaName { get; private set; } = string.Empty;
    public string TableName { get; private set; } = string.Empty;
    public string ColumnName { get; private set; } = string.Empty;
    public int DirectDependentCount { get; private set; }
    public int TransitiveDependentCount { get; private set; }
    public int ReadOperations { get; private set; }
    public int WriteOperations { get; private set; }
    public int DeleteOperations { get; private set; }
    public int AffectedProcedures { get; private set; }
    public int AffectedViews { get; private set; }
    public int PiiExposureCount { get; private set; }
    public int RiskScore { get; private set; }
    public DateTime LastCalculatedAt { get; private set; }
    public Guid? SourceScanId { get; private set; }

    private ColumnRiskScore() { } // EF Core

    public static ColumnRiskScore Create(
        string schemaName,
        string tableName,
        string columnName)
    {
        return new ColumnRiskScore
        {
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
            LastCalculatedAt = DateTime.UtcNow
        };
    }

    public void UpdateMetrics(
        int directDependents,
        int transitiveDependents,
        int readOps,
        int writeOps,
        int deleteOps,
        int affectedProcs,
        int affectedViews,
        int piiExposure)
    {
        DirectDependentCount = directDependents;
        TransitiveDependentCount = transitiveDependents;
        ReadOperations = readOps;
        WriteOperations = writeOps;
        DeleteOperations = deleteOps;
        AffectedProcedures = affectedProcs;
        AffectedViews = affectedViews;
        PiiExposureCount = piiExposure;

        // Calculate composite risk score
        // Formula: READ*1 + INSERT/UPDATE*3 + DELETE*5 + PII*10 + Dependents*2
        RiskScore = (readOps * 1) +
                    (writeOps * 3) +
                    (deleteOps * 5) +
                    (piiExposure * 10) +
                    (directDependents * 2);

        LastCalculatedAt = DateTime.UtcNow;
    }

    public void SetSourceScan(Guid scanId)
    {
        SourceScanId = scanId;
    }

    public ImpactLevel ImpactLevel => RiskScore switch
    {
        >= 100 => Lineage.ImpactLevel.Critical,
        >= 50 => Lineage.ImpactLevel.High,
        >= 20 => Lineage.ImpactLevel.Medium,
        _ => Lineage.ImpactLevel.Low
    };

    public string FullColumnName => $"{SchemaName}.{TableName}.{ColumnName}";
}

/// <summary>
/// Impact levels for change assessment
/// </summary>
public enum ImpactLevel
{
    Low,
    Medium,
    High,
    Critical
}
