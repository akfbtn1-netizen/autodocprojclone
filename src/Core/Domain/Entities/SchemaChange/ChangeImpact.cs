// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Change Impact Entity
// Tracks impact on dependent objects
// ═══════════════════════════════════════════════════════════════════════════

namespace Enterprise.Documentation.Core.Domain.Entities.SchemaChange;

/// <summary>
/// Records the impact of a schema change on a dependent object.
/// </summary>
public class ChangeImpact
{
    public Guid ImpactId { get; private set; }
    public Guid ChangeId { get; private set; }

    // Affected object
    public string AffectedSchema { get; private set; } = string.Empty;
    public string AffectedObject { get; private set; } = string.Empty;
    public ObjectType AffectedObjectType { get; private set; }

    // Impact details
    public ImpactType ImpactType { get; private set; }
    public int ImpactSeverity { get; private set; }  // 1-5
    public string? ImpactDescription { get; private set; }

    // Operation context (from lineage)
    public string? OperationType { get; private set; }  // READ, UPDATE, INSERT, DELETE
    public string? AffectedColumn { get; private set; }

    // Code reference
    public int? LineNumber { get; private set; }
    public string? SqlFragment { get; private set; }

    // Resolution
    public string? SuggestedAction { get; private set; }
    public bool RequiresManualReview { get; private set; }

    public DateTime AnalyzedAt { get; private set; }

    private ChangeImpact() { } // EF Core / Dapper

    public static ChangeImpact Create(
        Guid changeId,
        string affectedSchema,
        string affectedObject,
        ObjectType objectType,
        ImpactType impactType,
        int severity,
        string? description = null)
    {
        return new ChangeImpact
        {
            ImpactId = Guid.NewGuid(),
            ChangeId = changeId,
            AffectedSchema = affectedSchema,
            AffectedObject = affectedObject,
            AffectedObjectType = objectType,
            ImpactType = impactType,
            ImpactSeverity = Math.Clamp(severity, 1, 5),
            ImpactDescription = description,
            AnalyzedAt = DateTime.UtcNow,
            RequiresManualReview = severity >= 4
        };
    }

    public void SetCodeReference(int lineNumber, string sqlFragment)
    {
        LineNumber = lineNumber;
        SqlFragment = sqlFragment;
    }

    public void SetOperationContext(string operationType, string? column = null)
    {
        OperationType = operationType;
        AffectedColumn = column;
    }

    public void SetSuggestedAction(string action)
    {
        SuggestedAction = action;
    }

    public string FullAffectedName => $"{AffectedSchema}.{AffectedObject}";
}
