using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Visitor that extracts all column references from T-SQL AST.
/// </summary>
public class ColumnReferenceVisitor : TSqlConcreteFragmentVisitor
{
    public List<ColumnReferenceInfo> Columns { get; } = new();

    public override void Visit(ColumnReferenceExpression node)
    {
        var column = ExtractColumnInfo(node);
        if (column != null)
        {
            Columns.Add(column);
        }
        base.Visit(node);
    }

    private static ColumnReferenceInfo? ExtractColumnInfo(ColumnReferenceExpression node)
    {
        var identifiers = node.MultiPartIdentifier?.Identifiers;
        if (identifiers == null || identifiers.Count == 0)
            return null;

        var columnName = identifiers[^1].Value;
        string? tableAlias = null;
        string? schemaName = null;

        if (identifiers.Count >= 2)
        {
            tableAlias = identifiers[^2].Value;
        }

        if (identifiers.Count >= 3)
        {
            schemaName = identifiers[^3].Value;
        }

        return new ColumnReferenceInfo
        {
            ColumnName = columnName,
            TableAlias = tableAlias,
            SchemaName = schemaName,
            Line = node.StartLine,
            Column = node.StartColumn,
            ColumnType = node.ColumnType
        };
    }
}

/// <summary>
/// Information about a column reference extracted from AST.
/// </summary>
public record ColumnReferenceInfo
{
    public string ColumnName { get; init; } = string.Empty;
    public string? TableAlias { get; init; }
    public string? SchemaName { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public ColumnType ColumnType { get; init; }

    public string FullReference => SchemaName != null
        ? $"{SchemaName}.{TableAlias}.{ColumnName}"
        : TableAlias != null
            ? $"{TableAlias}.{ColumnName}"
            : ColumnName;
}
