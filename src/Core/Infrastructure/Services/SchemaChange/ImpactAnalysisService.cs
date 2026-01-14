// ═══════════════════════════════════════════════════════════════════════════
// Agent #4: Impact Analysis Service
// ScriptDom-based SQL parsing for dependency detection
// ═══════════════════════════════════════════════════════════════════════════
// TODO [4]: Add full ScriptDom visitor implementation for complex queries
// TODO [4]: Integrate with Agent #3 column-level lineage data
// TODO [4]: Add caching for frequently queried dependencies

using System.Data;
using Dapper;
using Enterprise.Documentation.Core.Application.DTOs.SchemaChange;
using Enterprise.Documentation.Core.Application.Interfaces.SchemaChange;
using Enterprise.Documentation.Core.Domain.Entities.SchemaChange;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Enterprise.Documentation.Core.Infrastructure.Services.SchemaChange;

/// <summary>
/// Analyzes impact of schema changes using SQL Server metadata and ScriptDom parsing.
/// </summary>
public class ImpactAnalysisService : IImpactAnalysisService
{
    private readonly IDbConnection _connection;
    private readonly ISchemaChangeRepository _changeRepository;
    private readonly ILogger<ImpactAnalysisService> _logger;

    public ImpactAnalysisService(
        IDbConnection connection,
        ISchemaChangeRepository changeRepository,
        ILogger<ImpactAnalysisService> logger)
    {
        _connection = connection;
        _changeRepository = changeRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ChangeImpactDto>> AnalyzeImpactAsync(Guid changeId, CancellationToken ct = default)
    {
        var change = await _changeRepository.GetByIdAsync(changeId, ct);
        if (change == null)
            return Enumerable.Empty<ChangeImpactDto>();

        _logger.LogInformation("Analyzing impact for change {ChangeId}: {Schema}.{Object}",
            changeId, change.SchemaName, change.ObjectName);

        var impacts = new List<ChangeImpactDto>();

        // Use SQL Server dependency tracking
        var dependents = await FindDependentObjectsAsync(change.SchemaName, change.ObjectName, null, ct);

        foreach (var dependent in dependents)
        {
            var parts = dependent.Split('.');
            if (parts.Length < 2) continue;

            var depSchema = parts[0];
            var depObject = parts[1];
            var depType = await GetObjectTypeAsync(depSchema, depObject, ct);

            // Analyze the specific impact
            var (impactType, severity, description) = await AnalyzeSpecificImpactAsync(
                change, depSchema, depObject, depType, ct);

            impacts.Add(new ChangeImpactDto(
                Guid.NewGuid(),
                depSchema,
                depObject,
                depType,
                impactType,
                severity,
                description,
                null, // OperationType - requires deeper analysis
                null, // AffectedColumn
                null, // LineNumber
                null, // SqlFragment
                GetSuggestedAction(impactType, severity),
                severity >= 4
            ));
        }

        _logger.LogInformation("Found {Count} impacted objects for change {ChangeId}",
            impacts.Count, changeId);

        return impacts;
    }

    public async Task<IEnumerable<string>> FindDependentObjectsAsync(
        string schemaName,
        string objectName,
        string? columnName = null,
        CancellationToken ct = default)
    {
        // Use sys.sql_expression_dependencies for accurate dependency tracking
        var sql = @"
            SELECT DISTINCT
                SCHEMA_NAME(o.schema_id) + '.' + o.name AS DependentObject
            FROM sys.sql_expression_dependencies d
            INNER JOIN sys.objects o ON d.referencing_id = o.object_id
            WHERE d.referenced_schema_name = @SchemaName
              AND d.referenced_entity_name = @ObjectName
              AND (@ColumnName IS NULL OR d.referenced_minor_name = @ColumnName)
            ORDER BY DependentObject";

        var dependents = await _connection.QueryAsync<string>(sql, new
        {
            SchemaName = schemaName,
            ObjectName = objectName,
            ColumnName = columnName
        });

        // Also check foreign key relationships for tables
        if (columnName == null)
        {
            var fkSql = @"
                SELECT DISTINCT
                    SCHEMA_NAME(parent.schema_id) + '.' + parent.name AS DependentObject
                FROM sys.foreign_keys fk
                INNER JOIN sys.objects parent ON fk.parent_object_id = parent.object_id
                INNER JOIN sys.objects referenced ON fk.referenced_object_id = referenced.object_id
                INNER JOIN sys.schemas s ON referenced.schema_id = s.schema_id
                WHERE s.name = @SchemaName AND referenced.name = @ObjectName";

            var fkDependents = await _connection.QueryAsync<string>(fkSql, new
            {
                SchemaName = schemaName,
                ObjectName = objectName
            });

            dependents = dependents.Union(fkDependents);
        }

        return dependents.Distinct();
    }

    public async Task<(int Score, string RiskLevel)> CalculateRiskScoreAsync(
        Guid changeId,
        IEnumerable<ChangeImpactDto> impacts,
        CancellationToken ct = default)
    {
        var change = await _changeRepository.GetByIdAsync(changeId, ct);
        if (change == null)
            return (0, "LOW");

        var impactList = impacts.ToList();
        var score = 0;

        // Base score from change type
        score += change.ChangeType switch
        {
            ChangeType.Drop => 40,
            ChangeType.Alter => 20,
            ChangeType.Create => 5,
            _ => 10
        };

        // Add points for each impacted object
        score += impactList.Count * 5;

        // Add severity points
        score += impactList.Sum(i => i.ImpactSeverity * 3);

        // Add points for breaking changes
        score += impactList.Count(i => i.ImpactType == "BREAKS") * 15;

        // Check if object is in critical schema
        if (change.SchemaName.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
            change.SchemaName.Equals("gwpc", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        // Check object usage frequency (from DMVs)
        var usageScore = await GetObjectUsageScoreAsync(change.SchemaName, change.ObjectName, ct);
        score += usageScore;

        // Cap at 100
        score = Math.Min(score, 100);

        var riskLevel = score switch
        {
            >= 80 => "CRITICAL",
            >= 60 => "HIGH",
            >= 30 => "MEDIUM",
            _ => "LOW"
        };

        return (score, riskLevel);
    }

    #region Private Helpers

    private async Task<string> GetObjectTypeAsync(string schemaName, string objectName, CancellationToken ct)
    {
        var sql = @"
            SELECT o.type_desc
            FROM sys.objects o
            INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
            WHERE s.name = @SchemaName AND o.name = @ObjectName";

        var typeDesc = await _connection.QuerySingleOrDefaultAsync<string>(sql, new
        {
            SchemaName = schemaName,
            ObjectName = objectName
        });

        return typeDesc?.ToUpperInvariant() switch
        {
            "USER_TABLE" => "TABLE",
            "VIEW" => "VIEW",
            "SQL_STORED_PROCEDURE" => "PROCEDURE",
            "SQL_SCALAR_FUNCTION" => "FUNCTION",
            "SQL_INLINE_TABLE_VALUED_FUNCTION" => "FUNCTION",
            "SQL_TABLE_VALUED_FUNCTION" => "FUNCTION",
            _ => "UNKNOWN"
        };
    }

    private async Task<(string ImpactType, int Severity, string Description)> AnalyzeSpecificImpactAsync(
        Domain.Entities.SchemaChange.SchemaChange change,
        string depSchema,
        string depObject,
        string depType,
        CancellationToken ct)
    {
        // DROP changes always break dependents
        if (change.ChangeType == ChangeType.Drop)
        {
            return ("BREAKS", 5, $"Object {depSchema}.{depObject} references dropped object {change.FullObjectName}");
        }

        // ALTER changes may break or invalidate
        if (change.ChangeType == ChangeType.Alter)
        {
            // For now, assume invalidation - TODO [4]: Use ScriptDom to detect breaking changes
            return ("INVALIDATES", 3, $"Object {depSchema}.{depObject} may need recompilation after changes to {change.FullObjectName}");
        }

        // CREATE changes typically don't break anything
        return ("MODIFIES", 1, $"New object {change.FullObjectName} may be referenced by {depSchema}.{depObject}");
    }

    private async Task<int> GetObjectUsageScoreAsync(string schemaName, string objectName, CancellationToken ct)
    {
        // Query DMV for execution stats
        var sql = @"
            SELECT TOP 1
                CASE
                    WHEN qs.execution_count > 10000 THEN 20
                    WHEN qs.execution_count > 1000 THEN 15
                    WHEN qs.execution_count > 100 THEN 10
                    WHEN qs.execution_count > 10 THEN 5
                    ELSE 0
                END AS UsageScore
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            WHERE st.text LIKE '%' + @ObjectName + '%'
            ORDER BY qs.execution_count DESC";

        try
        {
            var score = await _connection.QuerySingleOrDefaultAsync<int?>(sql, new { ObjectName = objectName });
            return score ?? 0;
        }
        catch
        {
            // DMV access may fail - return 0
            return 0;
        }
    }

    private static string GetSuggestedAction(string impactType, int severity)
    {
        return impactType switch
        {
            "BREAKS" => "Review and update the dependent object before deploying this change",
            "INVALIDATES" => "Recompile the dependent object after applying this change",
            "MODIFIES" => "Test the dependent object to ensure expected behavior",
            "PERFORMANCE" => "Monitor performance after applying this change",
            _ => "Review the impact and test thoroughly"
        };
    }

    #endregion
}

/// <summary>
/// ScriptDom visitor for extracting table/column references.
/// TODO [4]: Implement full visitor pattern for complex query analysis.
/// </summary>
public class DependencyVisitor : TSqlFragmentVisitor
{
    public HashSet<string> ReferencedTables { get; } = new();
    public HashSet<string> ReferencedColumns { get; } = new();

    public override void Visit(NamedTableReference node)
    {
        var schemaName = node.SchemaObject.SchemaIdentifier?.Value ?? "dbo";
        var tableName = node.SchemaObject.BaseIdentifier.Value;
        ReferencedTables.Add($"{schemaName}.{tableName}");
        base.Visit(node);
    }

    public override void Visit(ColumnReferenceExpression node)
    {
        if (node.MultiPartIdentifier?.Identifiers.Count >= 1)
        {
            var columnName = node.MultiPartIdentifier.Identifiers.Last().Value;
            ReferencedColumns.Add(columnName);
        }
        base.Visit(node);
    }
}
