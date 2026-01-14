// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Schema Change Detector - Column Change Entity
// Tracks column-level modifications
// ═══════════════════════════════════════════════════════════════════════════

namespace Enterprise.Documentation.Core.Domain.Entities.SchemaChange;

/// <summary>
/// Records column-level changes for detailed tracking and lineage impact.
/// </summary>
public class ColumnChange
{
    public Guid ColumnChangeId { get; private set; }
    public Guid ChangeId { get; private set; }

    // Column identification
    public string SchemaName { get; private set; } = string.Empty;
    public string TableName { get; private set; } = string.Empty;
    public string ColumnName { get; private set; } = string.Empty;

    // Change type
    public ColumnChangeType ChangeType { get; private set; }

    // Before state
    public string? OldDataType { get; private set; }
    public int? OldMaxLength { get; private set; }
    public byte? OldPrecision { get; private set; }
    public byte? OldScale { get; private set; }
    public bool? OldIsNullable { get; private set; }
    public string? OldDefaultValue { get; private set; }
    public bool? OldIsIdentity { get; private set; }
    public bool? OldIsPii { get; private set; }
    public string? OldPiiType { get; private set; }

    // After state
    public string? NewDataType { get; private set; }
    public int? NewMaxLength { get; private set; }
    public byte? NewPrecision { get; private set; }
    public byte? NewScale { get; private set; }
    public bool? NewIsNullable { get; private set; }
    public string? NewDefaultValue { get; private set; }
    public bool? NewIsIdentity { get; private set; }
    public bool? NewIsPii { get; private set; }
    public string? NewPiiType { get; private set; }

    // Lineage impact counts
    public int ReadCount { get; private set; }
    public int UpdateCount { get; private set; }
    public int InsertCount { get; private set; }
    public int DeleteCount { get; private set; }

    public DateTime DetectedAt { get; private set; }

    private ColumnChange() { } // EF Core / Dapper

    public static ColumnChange CreateAdd(
        Guid changeId,
        string schemaName,
        string tableName,
        string columnName,
        string dataType,
        bool isNullable)
    {
        return new ColumnChange
        {
            ColumnChangeId = Guid.NewGuid(),
            ChangeId = changeId,
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
            ChangeType = ColumnChangeType.Add,
            NewDataType = dataType,
            NewIsNullable = isNullable,
            DetectedAt = DateTime.UtcNow
        };
    }

    public static ColumnChange CreateDrop(
        Guid changeId,
        string schemaName,
        string tableName,
        string columnName,
        string oldDataType)
    {
        return new ColumnChange
        {
            ColumnChangeId = Guid.NewGuid(),
            ChangeId = changeId,
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
            ChangeType = ColumnChangeType.Drop,
            OldDataType = oldDataType,
            DetectedAt = DateTime.UtcNow
        };
    }

    public static ColumnChange CreateModify(
        Guid changeId,
        string schemaName,
        string tableName,
        string columnName)
    {
        return new ColumnChange
        {
            ColumnChangeId = Guid.NewGuid(),
            ChangeId = changeId,
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
            ChangeType = ColumnChangeType.Modify,
            DetectedAt = DateTime.UtcNow
        };
    }

    public void SetOldState(
        string dataType,
        int? maxLength,
        bool isNullable,
        string? defaultValue = null,
        bool isPii = false,
        string? piiType = null)
    {
        OldDataType = dataType;
        OldMaxLength = maxLength;
        OldIsNullable = isNullable;
        OldDefaultValue = defaultValue;
        OldIsPii = isPii;
        OldPiiType = piiType;
    }

    public void SetNewState(
        string dataType,
        int? maxLength,
        bool isNullable,
        string? defaultValue = null,
        bool isPii = false,
        string? piiType = null)
    {
        NewDataType = dataType;
        NewMaxLength = maxLength;
        NewIsNullable = isNullable;
        NewDefaultValue = defaultValue;
        NewIsPii = isPii;
        NewPiiType = piiType;
    }

    public void SetLineageImpact(int reads, int updates, int inserts, int deletes)
    {
        ReadCount = reads;
        UpdateCount = updates;
        InsertCount = inserts;
        DeleteCount = deletes;
    }

    public string FullColumnName => $"{SchemaName}.{TableName}.{ColumnName}";

    public int TotalUsageCount => ReadCount + UpdateCount + InsertCount + DeleteCount;
}
