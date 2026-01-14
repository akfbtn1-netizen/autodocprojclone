using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Visitor that extracts column lineage from SELECT statements.
/// Tracks which columns are read from which tables.
/// </summary>
public class SelectStatementVisitor : TSqlConcreteFragmentVisitor
{
    public List<SelectColumnLineage> ColumnLineages { get; } = new();
    public List<TableReferenceInfo> ReferencedTables { get; } = new();

    private readonly Stack<SelectContext> _contextStack = new();
    private int _selectIndex;

    public override void Visit(SelectStatement node)
    {
        _selectIndex++;
        ProcessSelectStatement(node);
        base.Visit(node);
    }

    public override void Visit(QuerySpecification node)
    {
        var context = new SelectContext
        {
            SelectIndex = _selectIndex,
            StartLine = node.StartLine
        };

        // Build table alias map from FROM clause
        if (node.FromClause != null)
        {
            var tableVisitor = new TableReferenceVisitor();
            node.FromClause.Accept(tableVisitor);
            context.AliasMap = tableVisitor.AliasMap;
            ReferencedTables.AddRange(tableVisitor.Tables);
        }

        _contextStack.Push(context);

        // Process SELECT elements
        int columnIndex = 0;
        foreach (var element in node.SelectElements)
        {
            ProcessSelectElement(element, context, columnIndex++);
        }

        _contextStack.Pop();
    }

    private void ProcessSelectStatement(SelectStatement node)
    {
        // Handle CTEs
        if (node.WithCtesAndXmlNamespaces != null)
        {
            foreach (var cte in node.WithCtesAndXmlNamespaces.CommonTableExpressions)
            {
                // CTEs are processed by the QuerySpecification visit
            }
        }
    }

    private void ProcessSelectElement(SelectElement element, SelectContext context, int columnIndex)
    {
        switch (element)
        {
            case SelectStarExpression star:
                ProcessSelectStar(star, context, columnIndex);
                break;

            case SelectScalarExpression scalar:
                ProcessScalarExpression(scalar, context, columnIndex);
                break;
        }
    }

    private void ProcessSelectStar(SelectStarExpression star, SelectContext context, int columnIndex)
    {
        string? qualifier = null;
        if (star.Qualifier != null)
        {
            qualifier = string.Join(".", star.Qualifier.Identifiers.Select(i => i.Value));
        }

        // SELECT * or SELECT alias.* - needs schema resolution
        ColumnLineages.Add(new SelectColumnLineage
        {
            OutputColumnIndex = columnIndex,
            OutputColumnName = "*",
            IsSelectStar = true,
            TableQualifier = qualifier,
            Line = star.StartLine,
            SelectIndex = context.SelectIndex
        });
    }

    private void ProcessScalarExpression(SelectScalarExpression scalar, SelectContext context, int columnIndex)
    {
        var outputAlias = scalar.ColumnName?.Value;
        var sourceColumns = ExtractSourceColumns(scalar.Expression, context);

        var lineage = new SelectColumnLineage
        {
            OutputColumnIndex = columnIndex,
            OutputColumnName = outputAlias ?? DeriveColumnName(scalar.Expression),
            SourceColumns = sourceColumns,
            TransformationExpression = IsSimpleColumnReference(scalar.Expression)
                ? null
                : GetExpressionText(scalar.Expression),
            Line = scalar.StartLine,
            SelectIndex = context.SelectIndex
        };

        ColumnLineages.Add(lineage);
    }

    private List<SourceColumnInfo> ExtractSourceColumns(ScalarExpression expression, SelectContext context)
    {
        var extractor = new ExpressionSourceExtractor(context.AliasMap);
        expression.Accept(extractor);
        return extractor.SourceColumns;
    }

    private static bool IsSimpleColumnReference(ScalarExpression expression)
    {
        return expression is ColumnReferenceExpression;
    }

    private static string DeriveColumnName(ScalarExpression expression)
    {
        return expression switch
        {
            ColumnReferenceExpression col =>
                col.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value ?? "unknown",
            FunctionCall func => func.FunctionName?.Value ?? "func",
            CaseExpression => "case_result",
            _ => "expr"
        };
    }

    private static string? GetExpressionText(ScalarExpression expression)
    {
        // Get a simplified representation of the expression
        return expression switch
        {
            FunctionCall func => $"{func.FunctionName?.Value}(...)",
            CaseExpression => "CASE...END",
            BinaryExpression bin => $"({GetExpressionText(bin.FirstExpression)} op {GetExpressionText(bin.SecondExpression)})",
            CastCall cast => $"CAST({GetExpressionText(cast.Parameter)} AS ...)",
            CoalesceExpression => "COALESCE(...)",
            NullIfExpression => "NULLIF(...)",
            _ => null
        };
    }

    private class SelectContext
    {
        public int SelectIndex { get; init; }
        public int StartLine { get; init; }
        public Dictionary<string, TableReferenceInfo> AliasMap { get; set; } = new();
    }
}

/// <summary>
/// Represents column lineage from a SELECT statement.
/// </summary>
public record SelectColumnLineage
{
    public int OutputColumnIndex { get; init; }
    public string OutputColumnName { get; init; } = string.Empty;
    public List<SourceColumnInfo> SourceColumns { get; init; } = new();
    public string? TransformationExpression { get; init; }
    public bool IsSelectStar { get; init; }
    public string? TableQualifier { get; init; }
    public int Line { get; init; }
    public int SelectIndex { get; init; }
}

/// <summary>
/// Information about a source column in an expression.
/// </summary>
public record SourceColumnInfo
{
    public string ColumnName { get; init; } = string.Empty;
    public string? TableAlias { get; init; }
    public string? ResolvedTable { get; init; }
    public string? ResolvedSchema { get; init; }

    public string FullName => ResolvedSchema != null && ResolvedTable != null
        ? $"{ResolvedSchema}.{ResolvedTable}.{ColumnName}"
        : ResolvedTable != null
            ? $"{ResolvedTable}.{ColumnName}"
            : ColumnName;
}
