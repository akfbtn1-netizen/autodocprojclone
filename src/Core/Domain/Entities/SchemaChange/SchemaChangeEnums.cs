// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Enums
// ═══════════════════════════════════════════════════════════════════════════

namespace Enterprise.Documentation.Core.Domain.Entities.SchemaChange;

/// <summary>
/// Type of database object affected by the change.
/// </summary>
public enum ObjectType
{
    Table,
    View,
    Procedure,
    Function,
    Index,
    Constraint,
    Trigger,
    Schema,
    Unknown
}

/// <summary>
/// Type of schema modification.
/// </summary>
public enum ChangeType
{
    Create,
    Alter,
    Drop,
    Rename
}

/// <summary>
/// How the change was detected.
/// </summary>
public enum DetectionMethod
{
    DdlTrigger,
    Polling,
    Manual,
    Startup
}

/// <summary>
/// Risk level of a schema change based on impact analysis.
/// </summary>
public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Processing status of a detected change.
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Analyzing,
    Assessed,
    Acknowledged,
    AutoProcessed,
    Failed
}

/// <summary>
/// Type of impact on dependent objects.
/// </summary>
public enum ImpactType
{
    Breaks,        // Will cause errors
    Invalidates,   // Needs recompilation
    Modifies,      // Changes behavior
    Performance    // May affect performance
}

/// <summary>
/// Saga states for detection run workflow.
/// </summary>
public enum DetectionRunState
{
    Pending,
    Snapshotting,
    Comparing,
    Analyzing,
    Notifying,
    Complete,
    Failed,
    Cancelled
}

/// <summary>
/// Column change type for detailed tracking.
/// </summary>
public enum ColumnChangeType
{
    Add,
    Drop,
    Modify,
    Rename
}
