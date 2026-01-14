// TODO [3]: Implement ScriptDom visitor to populate ColumnLineage entities
// TODO [3]: Build LineageService with MassTransit saga orchestration
// TODO [3]: Create PII flow tracking using IsPiiColumn + PiiType
// TODO [3]: Implement impact analysis query ("what breaks if column X changes?")
// TODO [3]: Feed lineage data to Agent #6 GraphRAG knowledge graph
namespace Enterprise.Documentation.Core.Domain.Entities.Lineage;

/// <summary>
/// Represents a column-level lineage record extracted from T-SQL parsing.
/// Tracks data flow at the column granularity for compliance and impact analysis.
/// </summary>
public class ColumnLineage
{
    public int Id { get; private set; }
    public Guid LineageId { get; private set; }
    public string ProcedureSchema { get; private set; } = string.Empty;
    public string ProcedureName { get; private set; } = string.Empty;
    public string? SourceSchema { get; private set; }
    public string? SourceTable { get; private set; }
    public string? SourceColumn { get; private set; }
    public string? TargetSchema { get; private set; }
    public string? TargetTable { get; private set; }
    public string? TargetColumn { get; private set; }
    public OperationType OperationType { get; private set; }
    public string? TransformationExpression { get; private set; }
    public int StatementIndex { get; private set; }
    public int? LineNumber { get; private set; }
    public bool IsPiiColumn { get; private set; }
    public string? PiiType { get; private set; }
    public int RiskWeight { get; private set; }
    public DateTime DiscoveredAt { get; private set; }
    public Guid? SourceScanId { get; private set; }

    private ColumnLineage() { } // EF Core

    public static ColumnLineage CreateRead(
        string procedureSchema,
        string procedureName,
        string sourceSchema,
        string sourceTable,
        string sourceColumn,
        int statementIndex = 0,
        int? lineNumber = null)
    {
        return new ColumnLineage
        {
            LineageId = Guid.NewGuid(),
            ProcedureSchema = procedureSchema,
            ProcedureName = procedureName,
            SourceSchema = sourceSchema,
            SourceTable = sourceTable,
            SourceColumn = sourceColumn,
            OperationType = Lineage.OperationType.Read,
            StatementIndex = statementIndex,
            LineNumber = lineNumber,
            RiskWeight = 1,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    public static ColumnLineage CreateInsert(
        string procedureSchema,
        string procedureName,
        string targetSchema,
        string targetTable,
        string targetColumn,
        string? sourceSchema = null,
        string? sourceTable = null,
        string? sourceColumn = null,
        string? transformationExpression = null,
        int statementIndex = 0,
        int? lineNumber = null)
    {
        return new ColumnLineage
        {
            LineageId = Guid.NewGuid(),
            ProcedureSchema = procedureSchema,
            ProcedureName = procedureName,
            SourceSchema = sourceSchema,
            SourceTable = sourceTable,
            SourceColumn = sourceColumn,
            TargetSchema = targetSchema,
            TargetTable = targetTable,
            TargetColumn = targetColumn,
            OperationType = Lineage.OperationType.Insert,
            TransformationExpression = transformationExpression,
            StatementIndex = statementIndex,
            LineNumber = lineNumber,
            RiskWeight = 2,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    public static ColumnLineage CreateUpdate(
        string procedureSchema,
        string procedureName,
        string targetSchema,
        string targetTable,
        string targetColumn,
        string? sourceSchema = null,
        string? sourceTable = null,
        string? sourceColumn = null,
        string? transformationExpression = null,
        int statementIndex = 0,
        int? lineNumber = null)
    {
        return new ColumnLineage
        {
            LineageId = Guid.NewGuid(),
            ProcedureSchema = procedureSchema,
            ProcedureName = procedureName,
            SourceSchema = sourceSchema,
            SourceTable = sourceTable,
            SourceColumn = sourceColumn,
            TargetSchema = targetSchema,
            TargetTable = targetTable,
            TargetColumn = targetColumn,
            OperationType = Lineage.OperationType.Update,
            TransformationExpression = transformationExpression,
            StatementIndex = statementIndex,
            LineNumber = lineNumber,
            RiskWeight = 3,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    public static ColumnLineage CreateDelete(
        string procedureSchema,
        string procedureName,
        string targetSchema,
        string targetTable,
        string targetColumn,
        int statementIndex = 0,
        int? lineNumber = null)
    {
        return new ColumnLineage
        {
            LineageId = Guid.NewGuid(),
            ProcedureSchema = procedureSchema,
            ProcedureName = procedureName,
            TargetSchema = targetSchema,
            TargetTable = targetTable,
            TargetColumn = targetColumn,
            OperationType = Lineage.OperationType.Delete,
            StatementIndex = statementIndex,
            LineNumber = lineNumber,
            RiskWeight = 5,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    public static ColumnLineage CreateMergeUpdate(
        string procedureSchema,
        string procedureName,
        string targetSchema,
        string targetTable,
        string targetColumn,
        string? sourceSchema = null,
        string? sourceTable = null,
        string? sourceColumn = null,
        string? transformationExpression = null,
        int statementIndex = 0,
        int? lineNumber = null)
    {
        return new ColumnLineage
        {
            LineageId = Guid.NewGuid(),
            ProcedureSchema = procedureSchema,
            ProcedureName = procedureName,
            SourceSchema = sourceSchema,
            SourceTable = sourceTable,
            SourceColumn = sourceColumn,
            TargetSchema = targetSchema,
            TargetTable = targetTable,
            TargetColumn = targetColumn,
            OperationType = Lineage.OperationType.MergeUpdate,
            TransformationExpression = transformationExpression,
            StatementIndex = statementIndex,
            LineNumber = lineNumber,
            RiskWeight = 3,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    public static ColumnLineage CreateMergeInsert(
        string procedureSchema,
        string procedureName,
        string targetSchema,
        string targetTable,
        string targetColumn,
        string? sourceSchema = null,
        string? sourceTable = null,
        string? sourceColumn = null,
        string? transformationExpression = null,
        int statementIndex = 0,
        int? lineNumber = null)
    {
        return new ColumnLineage
        {
            LineageId = Guid.NewGuid(),
            ProcedureSchema = procedureSchema,
            ProcedureName = procedureName,
            SourceSchema = sourceSchema,
            SourceTable = sourceTable,
            SourceColumn = sourceColumn,
            TargetSchema = targetSchema,
            TargetTable = targetTable,
            TargetColumn = targetColumn,
            OperationType = Lineage.OperationType.MergeInsert,
            TransformationExpression = transformationExpression,
            StatementIndex = statementIndex,
            LineNumber = lineNumber,
            RiskWeight = 2,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    public void MarkAsPii(string piiType)
    {
        IsPiiColumn = true;
        PiiType = piiType;
    }

    public void SetSourceScan(Guid scanId)
    {
        SourceScanId = scanId;
    }

    public bool IsRead => OperationType == Lineage.OperationType.Read;
    public bool IsInsert => OperationType == Lineage.OperationType.Insert || OperationType == Lineage.OperationType.MergeInsert;
    public bool IsUpdate => OperationType == Lineage.OperationType.Update || OperationType == Lineage.OperationType.MergeUpdate;
    public bool IsDelete => OperationType == Lineage.OperationType.Delete;
    public bool IsMergeTarget => OperationType == Lineage.OperationType.MergeUpdate || OperationType == Lineage.OperationType.MergeInsert;

    public string SourceFullName => SourceColumn != null
        ? $"{SourceSchema}.{SourceTable}.{SourceColumn}"
        : SourceTable != null ? $"{SourceSchema}.{SourceTable}" : string.Empty;

    public string TargetFullName => TargetColumn != null
        ? $"{TargetSchema}.{TargetTable}.{TargetColumn}"
        : TargetTable != null ? $"{TargetSchema}.{TargetTable}" : string.Empty;

    public string ProcedureFullName => $"{ProcedureSchema}.{ProcedureName}";
}
