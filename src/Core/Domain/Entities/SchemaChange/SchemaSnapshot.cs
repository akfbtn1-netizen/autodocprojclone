// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Schema Snapshot Entity
// Point-in-time capture of database schema state
// ═══════════════════════════════════════════════════════════════════════════

namespace Enterprise.Documentation.Core.Domain.Entities.SchemaChange;

/// <summary>
/// Captures a point-in-time snapshot of database schema for comparison.
/// </summary>
public class SchemaSnapshot
{
    public Guid SnapshotId { get; private set; }

    // Identification
    public string SnapshotName { get; private set; } = string.Empty;
    public string SnapshotType { get; private set; } = string.Empty;  // FULL, SCHEMA, OBJECT, BASELINE
    public string? SchemaFilter { get; private set; }

    // Content (compressed)
    public byte[] SnapshotData { get; private set; } = Array.Empty<byte>();

    // Counts
    public int ObjectCount { get; private set; }
    public int TableCount { get; private set; }
    public int ViewCount { get; private set; }
    public int ProcedureCount { get; private set; }
    public int FunctionCount { get; private set; }

    // Metadata
    public DateTime TakenAt { get; private set; }
    public string TakenBy { get; private set; } = string.Empty;
    public string? DatabaseVersion { get; private set; }

    // Comparison
    public bool IsBaseline { get; private set; }
    public Guid? PreviousSnapshotId { get; private set; }
    public string? DiffFromPreviousJson { get; private set; }

    // Retention
    public DateTime? ExpiresAt { get; private set; }
    public bool IsArchived { get; private set; }

    private SchemaSnapshot() { } // EF Core / Dapper

    public static SchemaSnapshot CreateFull(string takenBy, byte[] data, int tables, int views, int procs, int funcs)
    {
        return new SchemaSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            SnapshotName = $"Full_Snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            SnapshotType = "FULL",
            SnapshotData = data,
            ObjectCount = tables + views + procs + funcs,
            TableCount = tables,
            ViewCount = views,
            ProcedureCount = procs,
            FunctionCount = funcs,
            TakenAt = DateTime.UtcNow,
            TakenBy = takenBy
        };
    }

    public static SchemaSnapshot CreateForSchema(string schemaName, string takenBy, byte[] data)
    {
        return new SchemaSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            SnapshotName = $"Schema_{schemaName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            SnapshotType = "SCHEMA",
            SchemaFilter = schemaName,
            SnapshotData = data,
            TakenAt = DateTime.UtcNow,
            TakenBy = takenBy
        };
    }

    public static SchemaSnapshot CreateBaseline(string takenBy, byte[] data)
    {
        return new SchemaSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            SnapshotName = $"Baseline_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            SnapshotType = "BASELINE",
            SnapshotData = data,
            TakenAt = DateTime.UtcNow,
            TakenBy = takenBy,
            IsBaseline = true
        };
    }

    public void SetCounts(int tables, int views, int procs, int funcs)
    {
        TableCount = tables;
        ViewCount = views;
        ProcedureCount = procs;
        FunctionCount = funcs;
        ObjectCount = tables + views + procs + funcs;
    }

    public void SetDatabaseVersion(string version)
    {
        DatabaseVersion = version;
    }

    public void LinkToPrevious(Guid previousId, string diffJson)
    {
        PreviousSnapshotId = previousId;
        DiffFromPreviousJson = diffJson;
    }

    public void SetExpiration(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
    }

    public void Archive()
    {
        IsArchived = true;
    }

    public void MarkAsBaseline()
    {
        IsBaseline = true;
    }
}
