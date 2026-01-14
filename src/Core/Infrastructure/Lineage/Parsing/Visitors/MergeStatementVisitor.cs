using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Visitor that extracts column lineage from MERGE statements.
/// Handles WHEN MATCHED (UPDATE), WHEN NOT MATCHED (INSERT) clauses.
/// </summary>
public class MergeStatementVisitor : TSqlConcreteFragmentVisitor
{
    public List<MergeColumnLineage> ColumnLineages { get; } = new();
    public TableReferenceInfo? TargetTable { get; private set; }
    public TableReferenceInfo? SourceTable { get; private set; }

    private int _mergeIndex;
    private Dictionary<string, TableReferenceInfo> _aliasMap = new();

    public override void Visit(MergeStatement node)
    {
        _mergeIndex++;
        ProcessMergeStatement(node);
        base.Visit(node);
    }

    private void ProcessMergeStatement(MergeStatement node)
    {
        var spec = node.MergeSpecification;
        if (spec == null)
            return;

        // Extract target table
        TargetTable = ExtractTableReference(spec.Target);
        if (TargetTable != null)
        {
            var targetKey = TargetTable.Alias ?? TargetTable.TableName;
            _aliasMap[targetKey] = TargetTable;
        }

        // Extract source table/query
        SourceTable = ExtractMergeSource(spec.TableReference);
        if (SourceTable != null)
        {
            var sourceKey = SourceTable.Alias ?? SourceTable.TableName;
            _aliasMap[sourceKey] = SourceTable;
        }

        // Process action clauses
        foreach (var clause in spec.ActionClauses)
        {
            ProcessActionClause(clause);
        }
    }

    private void ProcessActionClause(MergeActionClause clause)
    {
        switch (clause.Action)
        {
            case UpdateMergeAction updateAction:
                ProcessUpdateAction(updateAction, clause.Condition);
                break;

            case InsertMergeAction insertAction:
                ProcessInsertAction(insertAction, clause.Condition);
                break;

            case DeleteMergeAction deleteAction:
                ProcessDeleteAction(deleteAction, clause.Condition);
                break;
        }
    }

    private void ProcessUpdateAction(UpdateMergeAction action, MergeCondition? condition)
    {
        foreach (var setClause in action.SetClauses)
        {
            if (setClause is AssignmentSetClause assignment)
            {
                var targetColumn = ExtractColumnName(assignment.Column);
                if (targetColumn == null)
                    continue;

                var sourceExtractor = new ExpressionSourceExtractor(_aliasMap);
                assignment.NewValue.Accept(sourceExtractor);

                ColumnLineages.Add(new MergeColumnLineage
                {
                    MergeActionType = MergeActionType.WhenMatchedUpdate,
                    TargetTable = TargetTable?.QualifiedName ?? "unknown",
                    TargetSchema = TargetTable?.SchemaName ?? "dbo",
                    TargetColumn = targetColumn,
                    SourceColumns = sourceExtractor.GetDistinctColumns(),
                    TransformationExpression = GetTransformationExpression(assignment.NewValue),
                    ConditionType = GetConditionType(condition),
                    MergeIndex = _mergeIndex,
                    Line = assignment.StartLine
                });
            }
        }
    }

    private void ProcessInsertAction(InsertMergeAction action, MergeCondition? condition)
    {
        var targetColumns = new List<string>();

        // Get target columns
        if (action.Columns != null)
        {
            foreach (var col in action.Columns)
            {
                var colName = col.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (colName != null)
                {
                    targetColumns.Add(colName);
                }
            }
        }

        // Get source values
        var sourceValues = new List<List<SourceColumnInfo>>();
        if (action.Source is ValuesInsertSource valuesSource)
        {
            foreach (var row in valuesSource.RowValues)
            {
                var rowSources = new List<SourceColumnInfo>();
                foreach (var value in row.ColumnValues)
                {
                    var extractor = new ExpressionSourceExtractor(_aliasMap);
                    value.Accept(extractor);
                    rowSources.AddRange(extractor.SourceColumns);
                }
                sourceValues.Add(rowSources);
            }
        }

        // Map columns to sources
        for (int i = 0; i < targetColumns.Count; i++)
        {
            var targetColumn = targetColumns[i];
            var sources = sourceValues.Count > 0 && i < sourceValues[0].Count
                ? sourceValues[0].Where((_, idx) => idx == i).ToList()
                : new List<SourceColumnInfo>();

            // Try to get individual source for this column position
            if (sourceValues.Count > 0 && sourceValues[0].Count > i)
            {
                sources = new List<SourceColumnInfo> { sourceValues[0][i] };
            }

            ColumnLineages.Add(new MergeColumnLineage
            {
                MergeActionType = MergeActionType.WhenNotMatchedInsert,
                TargetTable = TargetTable?.QualifiedName ?? "unknown",
                TargetSchema = TargetTable?.SchemaName ?? "dbo",
                TargetColumn = targetColumn,
                SourceColumns = sources,
                ConditionType = GetConditionType(condition),
                MergeIndex = _mergeIndex,
                Line = action.StartLine
            });
        }
    }

    private void ProcessDeleteAction(DeleteMergeAction action, MergeCondition? condition)
    {
        // DELETE doesn't have column-level lineage, but we note the operation
        ColumnLineages.Add(new MergeColumnLineage
        {
            MergeActionType = MergeActionType.WhenMatchedDelete,
            TargetTable = TargetTable?.QualifiedName ?? "unknown",
            TargetSchema = TargetTable?.SchemaName ?? "dbo",
            TargetColumn = "*", // All columns affected
            SourceColumns = new List<SourceColumnInfo>(),
            ConditionType = GetConditionType(condition),
            MergeIndex = _mergeIndex,
            Line = action.StartLine,
            IsDelete = true
        });
    }

    private static string? ExtractColumnName(ColumnReferenceExpression? column)
    {
        return column?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
    }

    private static TableReferenceInfo? ExtractTableReference(TableReference? target)
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

    private static TableReferenceInfo? ExtractMergeSource(TableReference? source)
    {
        return source switch
        {
            NamedTableReference namedTable => ExtractTableReference(namedTable),
            QueryDerivedTable queryDerived => new TableReferenceInfo
            {
                TableName = "(subquery)",
                Alias = queryDerived.Alias?.Value,
                IsSubquery = true,
                Line = queryDerived.StartLine
            },
            _ => null
        };
    }

    private static string? GetTransformationExpression(ScalarExpression expression)
    {
        if (expression is ColumnReferenceExpression)
            return null;

        return expression switch
        {
            FunctionCall func => $"{func.FunctionName?.Value}(...)",
            CaseExpression => "CASE...END",
            _ => "expression"
        };
    }

    private static string GetConditionType(MergeCondition? condition)
    {
        return condition switch
        {
            null => "DEFAULT",
            { SearchCondition: null } => condition.IsNot ? "NOT MATCHED" : "MATCHED",
            _ => "CONDITIONAL"
        };
    }
}

/// <summary>
/// Represents column lineage from a MERGE statement.
/// </summary>
public record MergeColumnLineage
{
    public MergeActionType MergeActionType { get; init; }
    public string TargetTable { get; init; } = string.Empty;
    public string TargetSchema { get; init; } = string.Empty;
    public string TargetColumn { get; init; } = string.Empty;
    public List<SourceColumnInfo> SourceColumns { get; init; } = new();
    public string? TransformationExpression { get; init; }
    public string ConditionType { get; init; } = string.Empty;
    public int MergeIndex { get; init; }
    public int Line { get; init; }
    public bool IsDelete { get; init; }
}

/// <summary>
/// Types of MERGE actions
/// </summary>
public enum MergeActionType
{
    WhenMatchedUpdate,
    WhenMatchedDelete,
    WhenNotMatchedInsert,
    WhenNotMatchedBySourceUpdate,
    WhenNotMatchedBySourceDelete
}
