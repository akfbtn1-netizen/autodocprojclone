// =============================================================================
// Agent #5: Post-Approval Pipeline - Column Lineage Service
// Extracts column-level lineage from T-SQL stored procedures using ScriptDom
// =============================================================================

using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Diagnostics;

namespace Enterprise.Documentation.Core.Application.Services.PostApproval;

/// <summary>
/// Extracts column-level lineage from T-SQL stored procedures using Microsoft ScriptDom.
/// Implements the tsql-scriptdom-lineage skill patterns.
/// </summary>
public class ColumnLineageService : IColumnLineageService
{
    private readonly ILogger<ColumnLineageService> _logger;
    private readonly string _connectionString;
    private readonly TSql160Parser _parser;

    public ColumnLineageService(
        ILogger<ColumnLineageService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        _parser = new TSql160Parser(initialQuotedIdentifiers: true);
    }

    public async Task<LineageExtractionResult> ExtractLineageAsync(
        string schemaName,
        string procedureName,
        string procedureDefinition,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new LineageExtractionResult
        {
            SchemaName = schemaName,
            ObjectName = procedureName
        };

        try
        {
            _logger.LogInformation("Extracting lineage from {Schema}.{Proc}", schemaName, procedureName);

            // Parse SQL
            using var reader = new StringReader(procedureDefinition);
            var fragment = _parser.Parse(reader, out var errors);

            if (errors.Count > 0)
            {
                result.ParseErrors = errors.Select(e => $"Line {e.Line}: {e.Message}").ToList();
                _logger.LogWarning("Parse errors in {Schema}.{Proc}: {Count} errors",
                    schemaName, procedureName, errors.Count);
            }

            result.LinesAnalyzed = procedureDefinition.Split('\n').Length;

            // Extract lineage using visitors
            var visitor = new LineageExtractionVisitor(schemaName, procedureName);
            fragment.Accept(visitor);

            result.ColumnLineages = visitor.Lineages;
            result.Success = true;

            // Save to database
            await SaveLineageAsync(result, ct);

            _logger.LogInformation("Extracted {Count} lineage entries from {Schema}.{Proc}",
                result.ColumnLineages.Count, schemaName, procedureName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract lineage from {Schema}.{Proc}", schemaName, procedureName);
            result.Success = false;
            result.ParseErrors.Add(ex.Message);
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<ImpactAnalysisResult> AnalyzeImpactAsync(
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken ct = default)
    {
        var result = new ImpactAnalysisResult
        {
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName
        };

        using var connection = new SqlConnection(_connectionString);

        // Find all procedures that touch this column
        var dependencies = await connection.QueryAsync<LineageDependency>(@"
            SELECT DISTINCT
                cl.SourceSchema AS SchemaName,
                cl.SourceName AS ObjectName,
                cl.SourceType AS ObjectType,
                cl.OperationType AS DependencyType,
                1 AS Depth
            FROM DaQa.ColumnLineage cl
            WHERE cl.TargetSchema = @Schema
              AND cl.TargetTable = @Table
              AND cl.TargetColumn = @Column",
            new { Schema = schemaName, Table = tableName, Column = columnName });

        result.Dependencies = dependencies.ToList();
        result.TotalAffectedObjects = result.Dependencies.Count;
        result.AffectedProcedures = result.Dependencies.Count(d => d.ObjectType == "PROCEDURE");
        result.AffectedViews = result.Dependencies.Count(d => d.ObjectType == "VIEW");
        result.AffectedFunctions = result.Dependencies.Count(d => d.ObjectType == "FUNCTION");

        // Calculate risk score
        result.RiskScore = CalculateRiskScore(result);
        result.RiskLevel = result.RiskScore switch
        {
            > 80 => "CRITICAL",
            > 60 => "HIGH",
            > 40 => "MEDIUM",
            _ => "LOW"
        };

        // Generate recommendations
        result.Recommendations = GenerateRecommendations(result);

        return result;
    }

    public async Task<List<LineageDependency>> GetDownstreamDependenciesAsync(
        string schemaName,
        string objectName,
        CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);

        // Recursive CTE to find all downstream dependencies
        var dependencies = await connection.QueryAsync<dynamic>(@"
            WITH DownstreamCTE AS (
                -- Base: direct dependencies
                SELECT DISTINCT
                    cl.TargetSchema AS SchemaName,
                    cl.TargetTable AS ObjectName,
                    'TABLE' AS ObjectType,
                    cl.OperationType AS DependencyType,
                    1 AS Depth,
                    CAST(cl.TargetColumn AS NVARCHAR(MAX)) AS AffectedColumns
                FROM DaQa.ColumnLineage cl
                WHERE cl.SourceSchema = @Schema AND cl.SourceName = @Object

                UNION ALL

                -- Recursive: dependencies of dependencies
                SELECT DISTINCT
                    cl.TargetSchema,
                    cl.TargetTable,
                    'TABLE',
                    cl.OperationType,
                    d.Depth + 1,
                    d.AffectedColumns + ',' + cl.TargetColumn
                FROM DaQa.ColumnLineage cl
                INNER JOIN DownstreamCTE d ON cl.SourceSchema = d.SchemaName AND cl.SourceName = d.ObjectName
                WHERE d.Depth < 5 -- Limit recursion depth
            )
            SELECT SchemaName, ObjectName, ObjectType, DependencyType, Depth,
                   STRING_AGG(AffectedColumns, ',') AS AffectedColumnsStr
            FROM DownstreamCTE
            GROUP BY SchemaName, ObjectName, ObjectType, DependencyType, Depth
            ORDER BY Depth, SchemaName, ObjectName",
            new { Schema = schemaName, Object = objectName });

        return dependencies.Select(d => new LineageDependency
        {
            SchemaName = d.SchemaName,
            ObjectName = d.ObjectName,
            ObjectType = d.ObjectType,
            DependencyType = d.DependencyType,
            Depth = d.Depth,
            AffectedColumns = ((string?)d.AffectedColumnsStr)?.Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList()
                ?? new List<string>()
        }).ToList();
    }

    #region Private Methods

    private async Task SaveLineageAsync(LineageExtractionResult result, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);

        // Delete existing lineage for this procedure
        await connection.ExecuteAsync(@"
            DELETE FROM DaQa.ColumnLineage
            WHERE SourceSchema = @Schema AND SourceName = @Name",
            new { Schema = result.SchemaName, Name = result.ObjectName });

        // Insert new lineage entries
        foreach (var entry in result.ColumnLineages)
        {
            await connection.ExecuteAsync(@"
                INSERT INTO DaQa.ColumnLineage (
                    SourceSchema, SourceName, SourceType,
                    TargetSchema, TargetTable, TargetColumn,
                    OperationType, TransformationExpression,
                    IsPiiColumn, PiiType, RiskWeight,
                    StartLine, EndLine, LastAnalyzed
                ) VALUES (
                    @SourceSchema, @SourceName, 'PROCEDURE',
                    @TargetSchema, @TargetTable, @TargetColumn,
                    @OperationType, @TransformationExpression,
                    @IsPII, @PIIType, @RiskWeight,
                    @StartLine, @EndLine, GETUTCDATE()
                )",
                new
                {
                    SourceSchema = result.SchemaName,
                    SourceName = result.ObjectName,
                    entry.TargetSchema,
                    entry.TargetTable,
                    entry.TargetColumn,
                    entry.OperationType,
                    entry.TransformationExpression,
                    entry.IsPII,
                    entry.PIIType,
                    entry.RiskWeight,
                    entry.StartLine,
                    entry.EndLine
                });
        }
    }

    private int CalculateRiskScore(ImpactAnalysisResult result)
    {
        var score = 0;
        score += result.TotalAffectedObjects * 5;
        score += result.AffectedProcedures * 3;
        score += result.Dependencies.Count(d => d.DependencyType == "UPDATE") * 10;
        score += result.Dependencies.Count(d => d.DependencyType == "DELETE") * 15;
        return Math.Min(score, 100);
    }

    private List<string> GenerateRecommendations(ImpactAnalysisResult result)
    {
        var recommendations = new List<string>();

        if (result.RiskScore > 60)
            recommendations.Add("High impact change - require additional review before deployment");

        if (result.AffectedProcedures > 5)
            recommendations.Add($"Consider batching updates to {result.AffectedProcedures} procedures");

        if (result.Dependencies.Any(d => d.DependencyType == "DELETE"))
            recommendations.Add("DELETE operations detected - verify cascading impact");

        // TODO [5]: Integrate with Agent #4 Schema Change Detector for enhanced recommendations

        return recommendations;
    }

    #endregion
}

#region ScriptDom Visitors

/// <summary>
/// Visitor that extracts column-level lineage from T-SQL AST
/// </summary>
internal class LineageExtractionVisitor : TSqlConcreteFragmentVisitor
{
    private readonly string _sourceSchema;
    private readonly string _sourceName;
    private readonly Dictionary<string, string> _tableAliases = new();

    public List<ColumnLineageEntry> Lineages { get; } = new();

    public LineageExtractionVisitor(string sourceSchema, string sourceName)
    {
        _sourceSchema = sourceSchema;
        _sourceName = sourceName;
    }

    public override void Visit(InsertStatement node)
    {
        var target = ExtractTableName(node.InsertSpecification.Target);
        if (target == null) return;

        // Extract columns being inserted
        foreach (var col in node.InsertSpecification.Columns)
        {
            var columnName = col.MultiPartIdentifier.Identifiers.LastOrDefault()?.Value;
            if (columnName != null)
            {
                AddLineage(target.Schema, target.Table, columnName, "INSERT", node.StartLine, node.StartLine);
            }
        }

        base.Visit(node);
    }

    public override void Visit(UpdateStatement node)
    {
        var target = ExtractTableName(node.UpdateSpecification.Target);
        if (target == null) return;

        foreach (var setClause in node.UpdateSpecification.SetClauses)
        {
            if (setClause is AssignmentSetClause assignment)
            {
                var columnName = assignment.Column?.MultiPartIdentifier.Identifiers.LastOrDefault()?.Value;
                if (columnName != null)
                {
                    // Try to extract source columns from the expression
                    var sourceExpr = ExtractExpression(assignment.NewValue);
                    AddLineage(target.Schema, target.Table, columnName, "UPDATE", node.StartLine, node.StartLine, sourceExpr);
                }
            }
        }

        base.Visit(node);
    }

    public override void Visit(DeleteStatement node)
    {
        var target = ExtractTableName(node.DeleteSpecification.Target);
        if (target == null) return;

        AddLineage(target.Schema, target.Table, "*", "DELETE", node.StartLine, node.StartLine);
        base.Visit(node);
    }

    public override void Visit(MergeStatement node)
    {
        var target = ExtractTableName(node.MergeSpecification.Target);
        if (target == null) return;

        // Process MERGE actions
        foreach (var action in node.MergeSpecification.ActionClauses)
        {
            var operation = action.Action switch
            {
                InsertMergeAction => "MERGE_INSERT",
                UpdateMergeAction => "MERGE_UPDATE",
                DeleteMergeAction => "MERGE_DELETE",
                _ => "MERGE"
            };

            if (action.Action is UpdateMergeAction updateAction)
            {
                foreach (var setClause in updateAction.SetClauses)
                {
                    if (setClause is AssignmentSetClause assignment)
                    {
                        var columnName = assignment.Column?.MultiPartIdentifier.Identifiers.LastOrDefault()?.Value;
                        if (columnName != null)
                        {
                            AddLineage(target.Schema, target.Table, columnName, operation, node.StartLine, node.StartLine);
                        }
                    }
                }
            }
        }

        base.Visit(node);
    }

    public override void Visit(SelectStatement node)
    {
        // Track SELECT for READ lineage
        if (node.QueryExpression is QuerySpecification querySpec && querySpec.FromClause != null)
        {
            // Register table aliases
            foreach (var tableRef in querySpec.FromClause.TableReferences)
            {
                RegisterTableAlias(tableRef);
            }
        }

        base.Visit(node);
    }

    public override void Visit(ColumnReferenceExpression node)
    {
        // Track column reads
        var parts = node.MultiPartIdentifier?.Identifiers;
        if (parts == null || parts.Count == 0) return;

        var columnName = parts.Last().Value;
        var tableOrAlias = parts.Count >= 2 ? parts[parts.Count - 2].Value : null;

        if (tableOrAlias != null)
        {
            var resolved = ResolveTableAlias(tableOrAlias);
            if (resolved != null)
            {
                AddLineage(resolved.Schema, resolved.Table, columnName, "READ", node.StartLine, node.StartLine);
            }
        }

        base.Visit(node);
    }

    private void RegisterTableAlias(TableReference tableRef)
    {
        if (tableRef is NamedTableReference named)
        {
            var alias = named.Alias?.Value;
            var tableName = named.SchemaObject?.Identifiers.LastOrDefault()?.Value;
            var schemaName = named.SchemaObject?.Identifiers.Count >= 2
                ? named.SchemaObject.Identifiers[^2].Value : "dbo";

            if (alias != null && tableName != null)
            {
                _tableAliases[alias] = $"{schemaName}.{tableName}";
            }
        }
        else if (tableRef is JoinTableReference join)
        {
            RegisterTableAlias(join.FirstTableReference);
            RegisterTableAlias(join.SecondTableReference);
        }
    }

    private (string Schema, string Table)? ResolveTableAlias(string aliasOrTable)
    {
        if (_tableAliases.TryGetValue(aliasOrTable, out var fullName))
        {
            var parts = fullName.Split('.');
            return (parts[0], parts[1]);
        }
        return (aliasOrTable.Contains('.') ? aliasOrTable.Split('.')[0] : "dbo",
                aliasOrTable.Contains('.') ? aliasOrTable.Split('.')[1] : aliasOrTable);
    }

    private (string Schema, string Table)? ExtractTableName(TableReference? tableRef)
    {
        if (tableRef is NamedTableReference named)
        {
            var tableName = named.SchemaObject?.Identifiers.LastOrDefault()?.Value;
            var schemaName = named.SchemaObject?.Identifiers.Count >= 2
                ? named.SchemaObject.Identifiers[^2].Value : "dbo";
            return (schemaName, tableName ?? "");
        }
        return null;
    }

    private string? ExtractExpression(ScalarExpression? expr)
    {
        if (expr == null) return null;
        // Simplified - return the SQL fragment text
        // TODO [5]: Implement full expression parsing for complex transformations
        return expr.ToString();
    }

    private void AddLineage(string schema, string table, string column, string operation,
        int? startLine = null, int? endLine = null, string? transformation = null)
    {
        // Detect PII
        var isPii = IsPiiColumn(column);
        var piiType = isPii ? DetectPiiType(column) : null;

        Lineages.Add(new ColumnLineageEntry
        {
            SourceSchema = _sourceSchema,
            SourceTable = _sourceName,
            SourceColumn = "*",
            TargetSchema = schema,
            TargetTable = table,
            TargetColumn = column,
            OperationType = operation,
            TransformationExpression = transformation,
            IsPII = isPii,
            PIIType = piiType,
            RiskWeight = CalculateRiskWeight(operation, isPii),
            StartLine = startLine,
            EndLine = endLine
        });
    }

    private bool IsPiiColumn(string columnName)
    {
        var piiPatterns = new[] { "ssn", "email", "phone", "dob", "birth", "address", "salary", "password", "taxid", "ssnum", "tin" };
        return piiPatterns.Any(p => columnName.ToLower().Contains(p));
    }

    private string? DetectPiiType(string columnName)
    {
        var lower = columnName.ToLower();
        if (lower.Contains("ssn") || lower.Contains("ssnum") || lower.Contains("taxid") || lower.Contains("tin")) return "SSN";
        if (lower.Contains("email")) return "EMAIL";
        if (lower.Contains("phone")) return "PHONE";
        if (lower.Contains("dob") || lower.Contains("birth")) return "DOB";
        if (lower.Contains("address")) return "ADDRESS";
        if (lower.Contains("salary")) return "FINANCIAL";
        return "OTHER";
    }

    private int CalculateRiskWeight(string operation, bool isPii)
    {
        var baseWeight = operation switch
        {
            "READ" => 1,
            "INSERT" => 2,
            "UPDATE" => 3,
            "DELETE" => 5,
            "MERGE_UPDATE" => 4,
            "MERGE_DELETE" => 5,
            _ => 2
        };
        return isPii ? baseWeight * 2 : baseWeight;
    }
}

#endregion
