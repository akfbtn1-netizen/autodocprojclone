using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Visitor that extracts column lineage from UPDATE statements.
/// Maps source columns to target columns through SET clauses.
/// </summary>
public class UpdateStatementVisitor : TSqlConcreteFragmentVisitor
{
    public List<UpdateColumnLineage> ColumnLineages { get; } = new();
    public TableReferenceInfo? TargetTable { get; private set; }
    public List<TableReferenceInfo> ReferencedTables { get; } = new();

    private int _updateIndex;
    private Dictionary<string, TableReferenceInfo> _aliasMap = new();

    public override void Visit(UpdateStatement node)
    {
        _updateIndex++;
        ProcessUpdateStatement(node);
        base.Visit(node);
    }

    private void ProcessUpdateStatement(UpdateStatement node)
    {
        var spec = node.UpdateSpecification;
        if (spec == null)
            return;

        // Build alias map from FROM clause (for UPDATE...FROM)
        if (spec.FromClause != null)
        {
            var tableVisitor = new TableReferenceVisitor();
            spec.FromClause.Accept(tableVisitor);
            _aliasMap = tableVisitor.AliasMap;
            ReferencedTables.AddRange(tableVisitor.Tables);
        }

        // Extract target table
        TargetTable = ExtractTargetTable(spec.Target);
        if (TargetTable != null)
        {
            ReferencedTables.Add(TargetTable);
            var key = TargetTable.Alias ?? TargetTable.TableName;
            _aliasMap[key] = TargetTable;
        }

        // Process SET clauses
        foreach (var setClause in spec.SetClauses)
        {
            ProcessSetClause(setClause);
        }
    }

    private void ProcessSetClause(SetClause setClause)
    {
        if (setClause is AssignmentSetClause assignment)
        {
            ProcessAssignmentSetClause(assignment);
        }
    }

    private void ProcessAssignmentSetClause(AssignmentSetClause assignment)
    {
        // Extract target column
        var targetColumn = ExtractColumnName(assignment.Column);
        if (targetColumn == null)
            return;

        // Extract source columns from the expression
        var sourceExtractor = new ExpressionSourceExtractor(_aliasMap);
        assignment.NewValue.Accept(sourceExtractor);

        // Determine transformation
        string? transformationExpr = null;
        if (!IsSimpleColumnAssignment(assignment.NewValue))
        {
            transformationExpr = GetExpressionDescription(assignment.NewValue);
        }

        ColumnLineages.Add(new UpdateColumnLineage
        {
            TargetTable = TargetTable?.QualifiedName ?? "unknown",
            TargetSchema = TargetTable?.SchemaName ?? "dbo",
            TargetColumn = targetColumn,
            SourceColumns = sourceExtractor.GetDistinctColumns(),
            TransformationExpression = transformationExpr,
            UpdateIndex = _updateIndex,
            Line = assignment.StartLine,
            AssignmentOperator = GetOperatorString(assignment.AssignmentKind)
        });
    }

    private static string? ExtractColumnName(ColumnReferenceExpression? column)
    {
        return column?.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
    }

    private static bool IsSimpleColumnAssignment(ScalarExpression expression)
    {
        return expression is ColumnReferenceExpression;
    }

    private static string GetExpressionDescription(ScalarExpression expression)
    {
        return expression switch
        {
            FunctionCall func => $"{func.FunctionName?.Value}(...)",
            CaseExpression => "CASE...END",
            BinaryExpression => "expression",
            CastCall => "CAST(...)",
            ConvertCall => "CONVERT(...)",
            CoalesceExpression => "COALESCE(...)",
            _ => "expression"
        };
    }

    private static string GetOperatorString(AssignmentKind kind)
    {
        return kind switch
        {
            AssignmentKind.Equals => "=",
            AssignmentKind.AddEquals => "+=",
            AssignmentKind.SubtractEquals => "-=",
            AssignmentKind.MultiplyEquals => "*=",
            AssignmentKind.DivideEquals => "/=",
            AssignmentKind.ModEquals => "%=",
            AssignmentKind.BitwiseAndEquals => "&=",
            AssignmentKind.BitwiseOrEquals => "|=",
            AssignmentKind.BitwiseXorEquals => "^=",
            _ => "="
        };
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

        // UPDATE with alias (UPDATE alias SET ... FROM table alias)
        if (target is VariableTableReference varTable)
        {
            return new TableReferenceInfo
            {
                TableName = varTable.Variable.Name,
                Line = varTable.StartLine
            };
        }

        return null;
    }
}

/// <summary>
/// Represents column lineage from an UPDATE statement.
/// </summary>
public record UpdateColumnLineage
{
    public string TargetTable { get; init; } = string.Empty;
    public string TargetSchema { get; init; } = string.Empty;
    public string TargetColumn { get; init; } = string.Empty;
    public List<SourceColumnInfo> SourceColumns { get; init; } = new();
    public string? TransformationExpression { get; init; }
    public string AssignmentOperator { get; init; } = "=";
    public int UpdateIndex { get; init; }
    public int Line { get; init; }
}
