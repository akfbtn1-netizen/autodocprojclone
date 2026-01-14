using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Visitor that extracts all table references from T-SQL AST.
/// Builds a map of aliases to actual table names.
/// </summary>
public class TableReferenceVisitor : TSqlConcreteFragmentVisitor
{
    public List<TableReferenceInfo> Tables { get; } = new();
    public Dictionary<string, TableReferenceInfo> AliasMap { get; } = new(StringComparer.OrdinalIgnoreCase);

    public override void Visit(NamedTableReference node)
    {
        var tableInfo = ExtractTableInfo(node);
        if (tableInfo != null)
        {
            Tables.Add(tableInfo);

            // Add to alias map
            var key = tableInfo.Alias ?? tableInfo.TableName;
            AliasMap[key] = tableInfo;
        }
        base.Visit(node);
    }

    public override void Visit(QueryDerivedTable node)
    {
        // Handle subqueries with aliases
        if (node.Alias != null)
        {
            var info = new TableReferenceInfo
            {
                TableName = $"(subquery)",
                Alias = node.Alias.Value,
                IsSubquery = true,
                Line = node.StartLine
            };
            Tables.Add(info);
            AliasMap[info.Alias] = info;
        }
        base.Visit(node);
    }

    public override void Visit(CommonTableExpression node)
    {
        // Track CTE as a virtual table
        var cteName = node.ExpressionName.Value;
        var info = new TableReferenceInfo
        {
            TableName = cteName,
            Alias = cteName,
            IsCte = true,
            Line = node.StartLine
        };
        Tables.Add(info);
        AliasMap[cteName] = info;

        base.Visit(node);
    }

    private static TableReferenceInfo? ExtractTableInfo(NamedTableReference node)
    {
        var schemaObject = node.SchemaObject;
        if (schemaObject == null)
            return null;

        var identifiers = schemaObject.Identifiers;
        if (identifiers == null || identifiers.Count == 0)
            return null;

        var tableName = identifiers[^1].Value;
        string? schemaName = null;
        string? databaseName = null;
        string? serverName = null;

        if (identifiers.Count >= 2)
            schemaName = identifiers[^2].Value;
        if (identifiers.Count >= 3)
            databaseName = identifiers[^3].Value;
        if (identifiers.Count >= 4)
            serverName = identifiers[^4].Value;

        return new TableReferenceInfo
        {
            TableName = tableName,
            SchemaName = schemaName ?? "dbo",
            DatabaseName = databaseName,
            ServerName = serverName,
            Alias = node.Alias?.Value,
            Line = node.StartLine
        };
    }

    /// <summary>
    /// Resolves a table alias to its actual table info.
    /// </summary>
    public TableReferenceInfo? ResolveAlias(string alias)
    {
        return AliasMap.TryGetValue(alias, out var info) ? info : null;
    }
}

/// <summary>
/// Information about a table reference extracted from AST.
/// </summary>
public record TableReferenceInfo
{
    public string TableName { get; init; } = string.Empty;
    public string? SchemaName { get; init; }
    public string? DatabaseName { get; init; }
    public string? ServerName { get; init; }
    public string? Alias { get; init; }
    public bool IsSubquery { get; init; }
    public bool IsCte { get; init; }
    public int Line { get; init; }

    public string FullName
    {
        get
        {
            if (ServerName != null)
                return $"{ServerName}.{DatabaseName}.{SchemaName}.{TableName}";
            if (DatabaseName != null)
                return $"{DatabaseName}.{SchemaName}.{TableName}";
            if (SchemaName != null)
                return $"{SchemaName}.{TableName}";
            return TableName;
        }
    }

    public string QualifiedName => SchemaName != null
        ? $"{SchemaName}.{TableName}"
        : TableName;
}
