using Microsoft.SqlServer.TransactSql.ScriptDom;
using Enterprise.Documentation.Core.Domain.Entities.Lineage;

namespace Enterprise.Documentation.Core.Infrastructure.Lineage.Parsing.Visitors;

/// <summary>
/// Detects dynamic SQL patterns that cannot be statically analyzed.
/// These procedures require manual review for accurate lineage tracking.
/// </summary>
public class DynamicSqlDetector : TSqlConcreteFragmentVisitor
{
    public List<DynamicSqlInfo> DynamicSqlUsages { get; } = new();

    public override void Visit(ExecuteStatement node)
    {
        var execSpec = node.ExecuteSpecification;
        if (execSpec == null)
        {
            base.Visit(node);
            return;
        }

        switch (execSpec.ExecutableEntity)
        {
            case ExecutableProcedureReference procRef:
                CheckForSpExecuteSql(procRef, node.StartLine);
                break;

            case ExecutableStringList stringList:
                // EXEC ('SELECT ...') or EXEC (@sql)
                RecordDynamicSql(
                    DynamicSqlType.ExecString,
                    "EXEC with string literal or variable",
                    node.StartLine,
                    RiskLevel.High);
                break;
        }

        base.Visit(node);
    }

    public override void Visit(OpenRowsetCall node)
    {
        RecordDynamicSql(
            DynamicSqlType.OpenQuery,
            $"OPENROWSET({GetFirstArgument(node)})",
            node.StartLine,
            RiskLevel.Critical);

        base.Visit(node);
    }

    public override void Visit(OpenQueryCall node)
    {
        RecordDynamicSql(
            DynamicSqlType.OpenQuery,
            $"OPENQUERY(...)",
            node.StartLine,
            RiskLevel.Critical);

        base.Visit(node);
    }

    public override void Visit(ExecuteParameter node)
    {
        // Check for variable parameters that might contain SQL
        if (node.Variable != null)
        {
            var varName = node.Variable.Name;
            if (varName.Contains("sql", StringComparison.OrdinalIgnoreCase) ||
                varName.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                varName.Contains("cmd", StringComparison.OrdinalIgnoreCase))
            {
                RecordDynamicSql(
                    DynamicSqlType.ExecVariable,
                    $"Variable @{varName} may contain dynamic SQL",
                    node.StartLine,
                    RiskLevel.Medium);
            }
        }

        base.Visit(node);
    }

    private void CheckForSpExecuteSql(ExecutableProcedureReference procRef, int line)
    {
        var procName = procRef.ProcedureReference?.ProcedureReference?.Name?.BaseIdentifier?.Value;

        if (string.Equals(procName, "sp_executesql", StringComparison.OrdinalIgnoreCase))
        {
            RecordDynamicSql(
                DynamicSqlType.SpExecuteSql,
                "sp_executesql - parameterized dynamic SQL",
                line,
                RiskLevel.High);
        }
        else if (string.Equals(procName, "sp_execute", StringComparison.OrdinalIgnoreCase))
        {
            RecordDynamicSql(
                DynamicSqlType.SpExecuteSql,
                "sp_execute - prepared statement execution",
                line,
                RiskLevel.Medium);
        }
    }

    private void RecordDynamicSql(DynamicSqlType type, string pattern, int line, RiskLevel risk)
    {
        DynamicSqlUsages.Add(new DynamicSqlInfo
        {
            DynamicSqlType = type,
            DetectedPattern = pattern,
            LineNumber = line,
            RiskLevel = risk
        });
    }

    private static string GetFirstArgument(OpenRowsetCall node)
    {
        if (node.ProviderName != null)
            return node.ProviderName.Value;

        return "...";
    }

    /// <summary>
    /// Checks if any dynamic SQL was detected.
    /// </summary>
    public bool HasDynamicSql => DynamicSqlUsages.Count > 0;

    /// <summary>
    /// Gets the highest risk level detected.
    /// </summary>
    public RiskLevel HighestRiskLevel => DynamicSqlUsages.Count > 0
        ? DynamicSqlUsages.Max(d => d.RiskLevel)
        : RiskLevel.Low;
}

/// <summary>
/// Information about detected dynamic SQL.
/// </summary>
public record DynamicSqlInfo
{
    public DynamicSqlType DynamicSqlType { get; init; }
    public string DetectedPattern { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public RiskLevel RiskLevel { get; init; }

    public string Warning => $"Dynamic SQL detected at line {LineNumber}: {DetectedPattern}";
}
