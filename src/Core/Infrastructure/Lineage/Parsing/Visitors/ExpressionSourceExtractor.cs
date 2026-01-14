using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Extracts all source columns referenced in a scalar expression.
/// Used to trace data lineage through transformations.
/// </summary>
public class ExpressionSourceExtractor : TSqlConcreteFragmentVisitor
{
    private readonly Dictionary<string, TableReferenceInfo> _aliasMap;

    public List<SourceColumnInfo> SourceColumns { get; } = new();

    public ExpressionSourceExtractor(Dictionary<string, TableReferenceInfo>? aliasMap = null)
    {
        _aliasMap = aliasMap ?? new Dictionary<string, TableReferenceInfo>(StringComparer.OrdinalIgnoreCase);
    }

    public override void Visit(ColumnReferenceExpression node)
    {
        var identifiers = node.MultiPartIdentifier?.Identifiers;
        if (identifiers == null || identifiers.Count == 0)
        {
            base.Visit(node);
            return;
        }

        var columnName = identifiers[^1].Value;
        string? tableAlias = identifiers.Count >= 2 ? identifiers[^2].Value : null;
        string? schemaName = identifiers.Count >= 3 ? identifiers[^3].Value : null;

        var sourceInfo = new SourceColumnInfo
        {
            ColumnName = columnName,
            TableAlias = tableAlias
        };

        // Try to resolve alias to actual table
        if (tableAlias != null && _aliasMap.TryGetValue(tableAlias, out var tableInfo))
        {
            sourceInfo = sourceInfo with
            {
                ResolvedTable = tableInfo.TableName,
                ResolvedSchema = tableInfo.SchemaName
            };
        }
        else if (schemaName != null && tableAlias != null)
        {
            // Already has schema.table.column format
            sourceInfo = sourceInfo with
            {
                ResolvedTable = tableAlias,
                ResolvedSchema = schemaName
            };
        }

        SourceColumns.Add(sourceInfo);
        base.Visit(node);
    }

    public override void Visit(FunctionCall node)
    {
        // Process function arguments to extract column references
        foreach (var param in node.Parameters)
        {
            param.Accept(this);
        }
        // Don't call base - we've already processed children
    }

    public override void Visit(CaseExpression node)
    {
        // Process CASE WHEN conditions and results
        if (node.InputExpression != null)
        {
            node.InputExpression.Accept(this);
        }

        foreach (var whenClause in node.WhenClauses)
        {
            whenClause.WhenExpression.Accept(this);
            whenClause.ThenExpression.Accept(this);
        }

        if (node.ElseExpression != null)
        {
            node.ElseExpression.Accept(this);
        }
    }

    public override void Visit(CoalesceExpression node)
    {
        foreach (var expr in node.Expressions)
        {
            expr.Accept(this);
        }
    }

    public override void Visit(NullIfExpression node)
    {
        node.FirstExpression.Accept(this);
        node.SecondExpression.Accept(this);
    }

    public override void Visit(CastCall node)
    {
        node.Parameter.Accept(this);
    }

    public override void Visit(ConvertCall node)
    {
        node.Parameter.Accept(this);
    }

    public override void Visit(BinaryExpression node)
    {
        node.FirstExpression.Accept(this);
        node.SecondExpression.Accept(this);
    }

    public override void Visit(UnaryExpression node)
    {
        node.Expression.Accept(this);
    }

    public override void Visit(ParenthesisExpression node)
    {
        node.Expression.Accept(this);
    }

    public override void Visit(IIfCall node)
    {
        node.Predicate.Accept(this);
        node.ThenExpression.Accept(this);
        node.ElseExpression.Accept(this);
    }

    /// <summary>
    /// Gets distinct source columns by full name.
    /// </summary>
    public List<SourceColumnInfo> GetDistinctColumns()
    {
        return SourceColumns
            .GroupBy(c => c.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }
}
