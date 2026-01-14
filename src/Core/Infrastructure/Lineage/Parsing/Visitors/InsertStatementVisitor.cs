using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Visitor that extracts column lineage from INSERT statements.
/// Maps source columns to target columns through INSERT...SELECT or VALUES.
/// </summary>
public class InsertStatementVisitor : TSqlConcreteFragmentVisitor
{
    public List<InsertColumnLineage> ColumnLineages { get; } = new();
    public TableReferenceInfo? TargetTable { get; private set; }
    public List<TableReferenceInfo> SourceTables { get; } = new();

    private int _insertIndex;

    public override void Visit(InsertStatement node)
    {
        _insertIndex++;
        ProcessInsertStatement(node);
        base.Visit(node);
    }

    private void ProcessInsertStatement(InsertStatement node)
    {
        var spec = node.InsertSpecification;
        if (spec == null)
            return;

        // Extract target table
        TargetTable = ExtractTargetTable(spec.Target);
        if (TargetTable == null)
            return;

        // Get target columns (explicit or implicit)
        var targetColumns = ExtractTargetColumns(spec);

        // Process the INSERT source
        switch (spec.InsertSource)
        {
            case SelectInsertSource selectSource:
                ProcessSelectInsertSource(selectSource, targetColumns);
                break;

            case ValuesInsertSource valuesSource:
                ProcessValuesInsertSource(valuesSource, targetColumns);
                break;

            case ExecuteInsertSource execSource:
                ProcessExecuteInsertSource(execSource, targetColumns);
                break;
        }
    }

    private void ProcessSelectInsertSource(SelectInsertSource source, List<string> targetColumns)
    {
        // Extract tables from SELECT
        var tableVisitor = new TableReferenceVisitor();
        source.Select.Accept(tableVisitor);
        SourceTables.AddRange(tableVisitor.Tables);

        // Extract SELECT columns
        var selectVisitor = new SelectStatementVisitor();
        source.Select.Accept(selectVisitor);

        // Map SELECT columns to INSERT target columns
        for (int i = 0; i < selectVisitor.ColumnLineages.Count && i < targetColumns.Count; i++)
        {
            var selectLineage = selectVisitor.ColumnLineages[i];
            var targetColumn = targetColumns[i];

            ColumnLineages.Add(new InsertColumnLineage
            {
                TargetTable = TargetTable!.QualifiedName,
                TargetSchema = TargetTable.SchemaName ?? "dbo",
                TargetColumn = targetColumn,
                SourceColumns = selectLineage.SourceColumns,
                TransformationExpression = selectLineage.TransformationExpression,
                InsertIndex = _insertIndex,
                Line = source.StartLine
            });
        }
    }

    private void ProcessValuesInsertSource(ValuesInsertSource source, List<string> targetColumns)
    {
        // For VALUES, we can't trace lineage to other tables
        // but we can note which columns are being inserted
        foreach (var targetColumn in targetColumns)
        {
            ColumnLineages.Add(new InsertColumnLineage
            {
                TargetTable = TargetTable!.QualifiedName,
                TargetSchema = TargetTable.SchemaName ?? "dbo",
                TargetColumn = targetColumn,
                SourceColumns = new List<SourceColumnInfo>(), // Literal values
                TransformationExpression = "VALUES(...)",
                InsertIndex = _insertIndex,
                Line = source.StartLine,
                IsLiteralSource = true
            });
        }
    }

    private void ProcessExecuteInsertSource(ExecuteInsertSource source, List<string> targetColumns)
    {
        // INSERT...EXEC - dynamic source
        foreach (var targetColumn in targetColumns)
        {
            ColumnLineages.Add(new InsertColumnLineage
            {
                TargetTable = TargetTable!.QualifiedName,
                TargetSchema = TargetTable.SchemaName ?? "dbo",
                TargetColumn = targetColumn,
                SourceColumns = new List<SourceColumnInfo>(),
                TransformationExpression = "EXEC ...",
                InsertIndex = _insertIndex,
                Line = source.StartLine,
                IsDynamicSource = true
            });
        }
    }

    private static TableReferenceInfo? ExtractTargetTable(TableReference? target)
    {
        if (target is NamedTableReference namedTable)
        {
            var schemaObject = namedTable.SchemaObject;
            if (schemaObject?.Identifiers == null || schemaObject.Identifiers.Count == 0)
                return null;

            var identifiers = schemaObject.Identifiers;
            return new TableReferenceInfo
            {
                TableName = identifiers[^1].Value,
                SchemaName = identifiers.Count >= 2 ? identifiers[^2].Value : "dbo",
                DatabaseName = identifiers.Count >= 3 ? identifiers[^3].Value : null,
                Alias = namedTable.Alias?.Value,
                Line = namedTable.StartLine
            };
        }

        return null;
    }

    private static List<string> ExtractTargetColumns(InsertSpecification spec)
    {
        var columns = new List<string>();

        if (spec.Columns != null && spec.Columns.Count > 0)
        {
            // Explicit column list: INSERT INTO t (col1, col2)
            foreach (var col in spec.Columns)
            {
                var colName = col.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (colName != null)
                {
                    columns.Add(colName);
                }
            }
        }
        else
        {
            // No explicit columns - would need schema metadata to determine
            // For now, use positional markers
            if (spec.InsertSource is SelectInsertSource selectSource)
            {
                var selectVisitor = new SelectStatementVisitor();
                selectSource.Select.Accept(selectVisitor);

                for (int i = 0; i < selectVisitor.ColumnLineages.Count; i++)
                {
                    var lineage = selectVisitor.ColumnLineages[i];
                    columns.Add(lineage.OutputColumnName ?? $"column_{i + 1}");
                }
            }
        }

        return columns;
    }
}

/// <summary>
/// Represents column lineage from an INSERT statement.
/// </summary>
public record InsertColumnLineage
{
    public string TargetTable { get; init; } = string.Empty;
    public string TargetSchema { get; init; } = string.Empty;
    public string TargetColumn { get; init; } = string.Empty;
    public List<SourceColumnInfo> SourceColumns { get; init; } = new();
    public string? TransformationExpression { get; init; }
    public int InsertIndex { get; init; }
    public int Line { get; init; }
    public bool IsLiteralSource { get; init; }
    public bool IsDynamicSource { get; init; }
}
