// =============================================================================
// T-SQL ScriptDom Lineage Extraction - Complete Implementation Examples
// =============================================================================
// Part of: tsql-scriptdom-lineage skill
// Version: 1.0.0
// =============================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DocumentationPlatform.LineageExtraction;

// =============================================================================
// SECTION 1: Core Parsing Infrastructure
// =============================================================================

/// <summary>
/// Factory for creating version-appropriate T-SQL parsers
/// </summary>
public static class TsqlParserFactory
{
    public static TSqlParser Create(SqlServerVersion version, bool quotedIdentifiers = true)
    {
        return version switch
        {
            SqlServerVersion.Sql2008 => new TSql100Parser(quotedIdentifiers),
            SqlServerVersion.Sql2012 => new TSql110Parser(quotedIdentifiers),
            SqlServerVersion.Sql2014 => new TSql120Parser(quotedIdentifiers),
            SqlServerVersion.Sql2016 => new TSql130Parser(quotedIdentifiers),
            SqlServerVersion.Sql2017 => new TSql140Parser(quotedIdentifiers),
            SqlServerVersion.Sql2019 => new TSql150Parser(quotedIdentifiers),
            SqlServerVersion.Sql2022 => new TSql160Parser(quotedIdentifiers),
            _ => new TSql160Parser(quotedIdentifiers) // Default to latest
        };
    }
    
    public static TSqlParser CreateForConnectionString(string connectionString)
    {
        // Auto-detect SQL Server version from connection
        var version = DetectSqlServerVersion(connectionString);
        return Create(version);
    }
    
    private static SqlServerVersion DetectSqlServerVersion(string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        
        var versionString = conn.ServerVersion;
        var majorVersion = int.Parse(versionString.Split('.')[0]);
        
        return majorVersion switch
        {
            >= 16 => SqlServerVersion.Sql2022,
            15 => SqlServerVersion.Sql2019,
            14 => SqlServerVersion.Sql2017,
            13 => SqlServerVersion.Sql2016,
            12 => SqlServerVersion.Sql2014,
            11 => SqlServerVersion.Sql2012,
            10 => SqlServerVersion.Sql2008,
            _ => SqlServerVersion.Sql2022
        };
    }
}

public enum SqlServerVersion
{
    Sql2008,
    Sql2012,
    Sql2014,
    Sql2016,
    Sql2017,
    Sql2019,
    Sql2022
}

/// <summary>
/// Result of parsing T-SQL with error information
/// </summary>
public record ParseResult
{
    public TSqlFragment Fragment { get; init; }
    public IReadOnlyList<ParseError> Errors { get; init; }
    public bool Success => Errors.Count == 0;
    public string OriginalSql { get; init; }
}

/// <summary>
/// Central parsing service with caching and error handling
/// </summary>
public class TsqlParsingService
{
    private readonly TSqlParser _parser;
    private readonly Dictionary<int, ParseResult> _parseCache = new();
    
    public TsqlParsingService(SqlServerVersion version = SqlServerVersion.Sql2022)
    {
        _parser = TsqlParserFactory.Create(version);
    }
    
    public ParseResult Parse(string sql)
    {
        var hash = sql.GetHashCode();
        
        if (_parseCache.TryGetValue(hash, out var cached))
            return cached;
        
        using var reader = new StringReader(sql);
        var fragment = _parser.Parse(reader, out var errors);
        
        var result = new ParseResult
        {
            Fragment = fragment,
            Errors = errors.ToList(),
            OriginalSql = sql
        };
        
        _parseCache[hash] = result;
        return result;
    }
    
    public void ClearCache() => _parseCache.Clear();
}

// =============================================================================
// SECTION 2: Schema Metadata Provider
// =============================================================================

/// <summary>
/// Interface for providing database schema metadata
/// </summary>
public interface ISchemaMetadataProvider
{
    IReadOnlyList<string> GetTableColumns(string schema, string table);
    bool ColumnExists(string schema, string table, string column);
    ColumnMetadata GetColumnMetadata(string schema, string table, string column);
    IReadOnlyList<TableMetadata> GetAllTables();
    Task RefreshCacheAsync();
}

public record ColumnMetadata
{
    public string Schema { get; init; }
    public string Table { get; init; }
    public string Column { get; init; }
    public string DataType { get; init; }
    public int? MaxLength { get; init; }
    public bool IsNullable { get; init; }
    public bool IsComputed { get; init; }
    public string ComputedDefinition { get; init; }
}

public record TableMetadata
{
    public string Schema { get; init; }
    public string Table { get; init; }
    public string ObjectType { get; init; } // TABLE, VIEW
    public IReadOnlyList<ColumnMetadata> Columns { get; init; }
}

/// <summary>
/// SQL Server implementation of schema metadata provider
/// </summary>
public class SqlServerMetadataProvider : ISchemaMetadataProvider
{
    private readonly string _connectionString;
    private Dictionary<string, TableMetadata> _tableCache = new();
    private bool _cacheLoaded = false;
    
    public SqlServerMetadataProvider(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public async Task RefreshCacheAsync()
    {
        var tables = new Dictionary<string, TableMetadata>(StringComparer.OrdinalIgnoreCase);
        
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        
        var sql = @"
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                t.type_desc AS ObjectType,
                c.name AS ColumnName,
                ty.name AS DataType,
                c.max_length AS MaxLength,
                c.is_nullable AS IsNullable,
                c.is_computed AS IsComputed,
                cc.definition AS ComputedDefinition
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.columns c ON t.object_id = c.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            LEFT JOIN sys.computed_columns cc ON c.object_id = cc.object_id AND c.column_id = cc.column_id
            
            UNION ALL
            
            SELECT 
                s.name AS SchemaName,
                v.name AS TableName,
                'VIEW' AS ObjectType,
                c.name AS ColumnName,
                ty.name AS DataType,
                c.max_length AS MaxLength,
                c.is_nullable AS IsNullable,
                0 AS IsComputed,
                NULL AS ComputedDefinition
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            INNER JOIN sys.columns c ON v.object_id = c.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            
            ORDER BY SchemaName, TableName, c.column_id";
        
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        
        var currentTable = "";
        var columns = new List<ColumnMetadata>();
        string currentSchema = null, currentObjectType = null;
        
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            var objectType = reader.GetString(2);
            var key = $"{schema}.{table}";
            
            if (key != currentTable && currentTable != "")
            {
                tables[currentTable] = new TableMetadata
                {
                    Schema = currentSchema,
                    Table = currentTable.Split('.')[1],
                    ObjectType = currentObjectType,
                    Columns = columns.ToList()
                };
                columns.Clear();
            }
            
            currentTable = key;
            currentSchema = schema;
            currentObjectType = objectType;
            
            columns.Add(new ColumnMetadata
            {
                Schema = schema,
                Table = table,
                Column = reader.GetString(3),
                DataType = reader.GetString(4),
                MaxLength = reader.IsDBNull(5) ? null : reader.GetInt16(5),
                IsNullable = reader.GetBoolean(6),
                IsComputed = reader.GetBoolean(7),
                ComputedDefinition = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        
        // Don't forget the last table
        if (currentTable != "")
        {
            tables[currentTable] = new TableMetadata
            {
                Schema = currentSchema,
                Table = currentTable.Split('.')[1],
                ObjectType = currentObjectType,
                Columns = columns.ToList()
            };
        }
        
        _tableCache = tables;
        _cacheLoaded = true;
    }
    
    public IReadOnlyList<string> GetTableColumns(string schema, string table)
    {
        EnsureCacheLoaded();
        
        var key = $"{schema ?? "dbo"}.{table}";
        if (_tableCache.TryGetValue(key, out var metadata))
        {
            return metadata.Columns.Select(c => c.Column).ToList();
        }
        
        return Array.Empty<string>();
    }
    
    public bool ColumnExists(string schema, string table, string column)
    {
        var columns = GetTableColumns(schema, table);
        return columns.Contains(column, StringComparer.OrdinalIgnoreCase);
    }
    
    public ColumnMetadata GetColumnMetadata(string schema, string table, string column)
    {
        EnsureCacheLoaded();
        
        var key = $"{schema ?? "dbo"}.{table}";
        if (_tableCache.TryGetValue(key, out var metadata))
        {
            return metadata.Columns.FirstOrDefault(c => 
                c.Column.Equals(column, StringComparison.OrdinalIgnoreCase));
        }
        
        return null;
    }
    
    public IReadOnlyList<TableMetadata> GetAllTables()
    {
        EnsureCacheLoaded();
        return _tableCache.Values.ToList();
    }
    
    private void EnsureCacheLoaded()
    {
        if (!_cacheLoaded)
        {
            RefreshCacheAsync().GetAwaiter().GetResult();
        }
    }
}

// =============================================================================
// SECTION 3: Lineage Data Models
// =============================================================================

/// <summary>
/// Represents a column in the lineage graph
/// </summary>
public record LineageColumn
{
    public string Schema { get; init; }
    public string Table { get; init; }
    public string Column { get; init; }
    public string FullyQualifiedName => $"{Schema ?? "dbo"}.{Table}.{Column}";
    
    public override string ToString() => FullyQualifiedName;
}

/// <summary>
/// Represents an edge in the lineage graph (data flow from source to target)
/// </summary>
public record LineageEdge
{
    public LineageColumn Source { get; init; }
    public LineageColumn Target { get; init; }
    public TransformationType TransformationType { get; init; }
    public string TransformationExpression { get; init; }
    public string SourceObject { get; init; } // Procedure/View that creates this lineage
    public int SourceLine { get; init; }
    public decimal Confidence { get; init; } = 1.0m;
}

public enum TransformationType
{
    Direct,           // Direct column reference
    Aggregation,      // SUM, COUNT, AVG, etc.
    CaseExpression,   // CASE WHEN
    Function,         // CAST, CONVERT, string functions, etc.
    Arithmetic,       // +, -, *, /
    Concatenation,    // String concatenation
    Coalesce,         // COALESCE, ISNULL
    Join,             // From JOIN condition (filter dependency)
    Where,            // From WHERE clause (filter dependency)
    Unknown
}

/// <summary>
/// Complete lineage extraction result
/// </summary>
public record LineageExtractionResult
{
    public string ObjectName { get; init; }
    public string ObjectType { get; init; }
    public IReadOnlyList<LineageEdge> Edges { get; init; }
    public IReadOnlyList<string> Warnings { get; init; }
    public IReadOnlyList<DynamicSqlUsage> DynamicSqlUsages { get; init; }
    public IReadOnlyList<TempTableInfo> TempTables { get; init; }
    public bool HasDynamicSql => DynamicSqlUsages?.Count > 0;
    public decimal OverallConfidence { get; init; }
    public TimeSpan ExtractionDuration { get; init; }
}

public record DynamicSqlUsage
{
    public DynamicSqlType Type { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string SqlFragment { get; init; }
}

public enum DynamicSqlType
{
    ExecString,
    SpExecuteSql,
    OpenRowset,
    OpenQuery
}

public record TempTableInfo
{
    public string Name { get; init; }
    public IReadOnlyList<TempColumnInfo> Columns { get; init; }
    public bool IsGlobal { get; init; }
    public bool IsTableVariable { get; init; }
    public int DefinitionLine { get; init; }
}

public record TempColumnInfo
{
    public string Name { get; init; }
    public string DataType { get; init; }
}

// =============================================================================
// SECTION 4: Core Lineage Visitor
// =============================================================================

/// <summary>
/// Main visitor for extracting column-level lineage from T-SQL
/// </summary>
public class LineageExtractorVisitor : TSqlConcreteFragmentVisitor
{
    private readonly ISchemaMetadataProvider _schemaProvider;
    private readonly List<LineageEdge> _edges = new();
    private readonly List<string> _warnings = new();
    private readonly List<DynamicSqlUsage> _dynamicSqlUsages = new();
    private readonly Dictionary<string, TempTableInfo> _tempTables = new();
    private readonly Dictionary<string, CteDefinition> _cteDefinitions = new();
    
    // Context tracking
    private readonly Stack<QueryContext> _contextStack = new();
    private Dictionary<string, TableInfo> _currentAliasMap = new();
    private string _currentObjectName;
    
    public LineageExtractorVisitor(ISchemaMetadataProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }
    
    public IReadOnlyList<LineageEdge> Edges => _edges;
    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<DynamicSqlUsage> DynamicSqlUsages => _dynamicSqlUsages;
    public IReadOnlyDictionary<string, TempTableInfo> TempTables => _tempTables;
    
    public void SetObjectContext(string objectName)
    {
        _currentObjectName = objectName;
    }
    
    // -------------------------------------------------------------------------
    // CTE Handling
    // -------------------------------------------------------------------------
    
    public override void Visit(WithCtesAndXmlNamespaces node)
    {
        // First pass: collect all CTE definitions
        foreach (var cte in node.CommonTableExpressions)
        {
            var cteName = cte.ExpressionName.Value;
            var columns = ExtractCteColumns(cte);
            
            _cteDefinitions[cteName] = new CteDefinition
            {
                Name = cteName,
                Columns = columns,
                QueryExpression = cte.QueryExpression,
                DefinitionLine = cte.StartLine
            };
        }
        
        base.Visit(node);
    }
    
    private List<string> ExtractCteColumns(CommonTableExpression cte)
    {
        if (cte.Columns.Count > 0)
        {
            return cte.Columns.Select(c => c.Value).ToList();
        }
        
        // Derive from SELECT list
        var columns = new List<string>();
        var selectVisitor = new SelectListColumnExtractor();
        cte.QueryExpression.Accept(selectVisitor);
        return selectVisitor.OutputColumns;
    }
    
    // -------------------------------------------------------------------------
    // Temp Table Tracking
    // -------------------------------------------------------------------------
    
    public override void Visit(CreateTableStatement node)
    {
        var tableName = node.SchemaObjectName.BaseIdentifier.Value;
        
        if (tableName.StartsWith("#"))
        {
            var columns = node.Definition.ColumnDefinitions
                .Select(c => new TempColumnInfo
                {
                    Name = c.ColumnIdentifier.Value,
                    DataType = GetDataTypeName(c.DataType)
                })
                .ToList();
            
            _tempTables[tableName] = new TempTableInfo
            {
                Name = tableName,
                Columns = columns,
                IsGlobal = tableName.StartsWith("##"),
                DefinitionLine = node.StartLine
            };
        }
        
        base.Visit(node);
    }
    
    public override void Visit(DeclareTableVariableStatement node)
    {
        var varName = node.Body.VariableName.Value;
        
        var columns = node.Body.Definition.ColumnDefinitions
            .Select(c => new TempColumnInfo
            {
                Name = c.ColumnIdentifier.Value,
                DataType = GetDataTypeName(c.DataType)
            })
            .ToList();
        
        _tempTables[varName] = new TempTableInfo
        {
            Name = varName,
            Columns = columns,
            IsTableVariable = true,
            DefinitionLine = node.StartLine
        };
        
        base.Visit(node);
    }
    
    // -------------------------------------------------------------------------
    // INSERT Statement
    // -------------------------------------------------------------------------
    
    public override void Visit(InsertStatement node)
    {
        var target = node.InsertSpecification.Target;
        var targetTable = ExtractTableName(target);
        var targetColumns = ExtractInsertColumns(node.InsertSpecification);
        
        _contextStack.Push(new QueryContext
        {
            TargetTable = targetTable,
            TargetColumns = targetColumns,
            StatementType = StatementType.Insert,
            StatementLine = node.StartLine
        });
        
        // Process the source (SELECT, VALUES, or EXECUTE)
        if (node.InsertSpecification.InsertSource is SelectInsertSource selectSource)
        {
            ProcessSelectForLineage(selectSource.Select, targetTable, targetColumns);
        }
        
        base.Visit(node);
        _contextStack.Pop();
    }
    
    private List<string> ExtractInsertColumns(InsertSpecification spec)
    {
        if (spec.Columns.Count > 0)
        {
            return spec.Columns.Select(c => c.MultiPartIdentifier.Identifiers.Last().Value).ToList();
        }
        
        // No explicit columns - get from target table schema
        var targetTable = ExtractTableName(spec.Target);
        var parts = targetTable.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];
        
        return _schemaProvider.GetTableColumns(schema, table).ToList();
    }
    
    // -------------------------------------------------------------------------
    // UPDATE Statement
    // -------------------------------------------------------------------------
    
    public override void Visit(UpdateStatement node)
    {
        var targetTable = ExtractUpdateTarget(node);
        
        // Build alias map from FROM clause
        _currentAliasMap = BuildAliasMap(node.UpdateSpecification.FromClause);
        
        // Add the target table to alias map
        if (node.UpdateSpecification.Target is NamedTableReference namedTarget)
        {
            var alias = namedTarget.Alias?.Value ?? ExtractTableName(namedTarget);
            _currentAliasMap[alias] = ExtractTableInfo(namedTarget);
        }
        
        _contextStack.Push(new QueryContext
        {
            TargetTable = targetTable,
            StatementType = StatementType.Update,
            StatementLine = node.StartLine
        });
        
        // Process SET clauses
        foreach (var setClause in node.UpdateSpecification.SetClauses)
        {
            if (setClause is AssignmentSetClause assignment)
            {
                ProcessUpdateAssignment(targetTable, assignment);
            }
        }
        
        base.Visit(node);
        _contextStack.Pop();
    }
    
    private void ProcessUpdateAssignment(string targetTable, AssignmentSetClause assignment)
    {
        var targetColumn = assignment.Column.MultiPartIdentifier.Identifiers.Last().Value;
        
        // Extract source columns from the expression
        var sourceColumns = ExtractColumnsFromExpression(assignment.NewValue);
        var transformationType = DetermineTransformationType(assignment.NewValue);
        
        foreach (var sourceCol in sourceColumns)
        {
            var resolved = ResolveColumn(sourceCol, _currentAliasMap);
            
            _edges.Add(new LineageEdge
            {
                Source = resolved,
                Target = new LineageColumn
                {
                    Schema = ExtractSchema(targetTable),
                    Table = ExtractTableOnly(targetTable),
                    Column = targetColumn
                },
                TransformationType = transformationType,
                SourceObject = _currentObjectName,
                SourceLine = assignment.StartLine
            });
        }
    }
    
    // -------------------------------------------------------------------------
    // MERGE Statement
    // -------------------------------------------------------------------------
    
    public override void Visit(MergeStatement node)
    {
        var targetTable = ExtractTableName(node.MergeSpecification.Target);
        
        // Build alias map
        _currentAliasMap = new Dictionary<string, TableInfo>(StringComparer.OrdinalIgnoreCase);
        
        // Add target
        if (node.MergeSpecification.Target is NamedTableReference target)
        {
            var alias = target.Alias?.Value ?? "target";
            _currentAliasMap[alias] = ExtractTableInfo(target);
        }
        
        // Add source
        var sourceAlias = "source";
        if (node.MergeSpecification.TableReference is NamedTableReference sourceTable)
        {
            sourceAlias = sourceTable.Alias?.Value ?? "source";
            _currentAliasMap[sourceAlias] = ExtractTableInfo(sourceTable);
        }
        
        // Process action clauses
        foreach (var clause in node.MergeSpecification.ActionClauses)
        {
            ProcessMergeActionClause(clause, targetTable);
        }
        
        base.Visit(node);
    }
    
    private void ProcessMergeActionClause(MergeActionClause clause, string targetTable)
    {
        switch (clause.Action)
        {
            case InsertMergeAction insert:
                ProcessMergeInsert(targetTable, insert, clause.StartLine);
                break;
                
            case UpdateMergeAction update:
                ProcessMergeUpdate(targetTable, update, clause.StartLine);
                break;
                
            case DeleteMergeAction:
                // DELETE doesn't create column lineage
                break;
        }
    }
    
    private void ProcessMergeInsert(string targetTable, InsertMergeAction insert, int line)
    {
        var targetColumns = insert.Columns.Count > 0
            ? insert.Columns.Select(c => c.MultiPartIdentifier.Identifiers.Last().Value).ToList()
            : _schemaProvider.GetTableColumns(ExtractSchema(targetTable), ExtractTableOnly(targetTable)).ToList();
        
        var sourceValues = insert.Source as ValuesInsertSource;
        if (sourceValues?.RowValues.Count > 0)
        {
            var row = sourceValues.RowValues[0];
            for (int i = 0; i < Math.Min(targetColumns.Count, row.ColumnValues.Count); i++)
            {
                var sourceColumns = ExtractColumnsFromExpression(row.ColumnValues[i]);
                foreach (var sourceCol in sourceColumns)
                {
                    var resolved = ResolveColumn(sourceCol, _currentAliasMap);
                    _edges.Add(new LineageEdge
                    {
                        Source = resolved,
                        Target = new LineageColumn
                        {
                            Schema = ExtractSchema(targetTable),
                            Table = ExtractTableOnly(targetTable),
                            Column = targetColumns[i]
                        },
                        TransformationType = DetermineTransformationType(row.ColumnValues[i]),
                        SourceObject = _currentObjectName,
                        SourceLine = line
                    });
                }
            }
        }
    }
    
    private void ProcessMergeUpdate(string targetTable, UpdateMergeAction update, int line)
    {
        foreach (var setClause in update.SetClauses)
        {
            if (setClause is AssignmentSetClause assignment)
            {
                var targetColumn = assignment.Column.MultiPartIdentifier.Identifiers.Last().Value;
                var sourceColumns = ExtractColumnsFromExpression(assignment.NewValue);
                
                foreach (var sourceCol in sourceColumns)
                {
                    var resolved = ResolveColumn(sourceCol, _currentAliasMap);
                    _edges.Add(new LineageEdge
                    {
                        Source = resolved,
                        Target = new LineageColumn
                        {
                            Schema = ExtractSchema(targetTable),
                            Table = ExtractTableOnly(targetTable),
                            Column = targetColumn
                        },
                        TransformationType = DetermineTransformationType(assignment.NewValue),
                        SourceObject = _currentObjectName,
                        SourceLine = line
                    });
                }
            }
        }
    }
    
    // -------------------------------------------------------------------------
    // Dynamic SQL Detection
    // -------------------------------------------------------------------------
    
    public override void Visit(ExecuteStatement node)
    {
        if (node.ExecuteSpecification.ExecutableEntity is ExecutableProcedureReference procRef)
        {
            var procName = procRef.ProcedureReference?.ProcedureReference?.Name?.BaseIdentifier?.Value;
            
            if (procName?.Equals("sp_executesql", StringComparison.OrdinalIgnoreCase) == true)
            {
                _dynamicSqlUsages.Add(new DynamicSqlUsage
                {
                    Type = DynamicSqlType.SpExecuteSql,
                    Line = node.StartLine,
                    Column = node.StartColumn
                });
                
                _warnings.Add($"Line {node.StartLine}: sp_executesql detected - lineage cannot be statically determined");
            }
        }
        else if (node.ExecuteSpecification.ExecutableEntity is ExecutableStringList stringList)
        {
            _dynamicSqlUsages.Add(new DynamicSqlUsage
            {
                Type = DynamicSqlType.ExecString,
                Line = node.StartLine,
                Column = node.StartColumn
            });
            
            _warnings.Add($"Line {node.StartLine}: EXEC with string literal - potential SQL injection risk and lineage gap");
        }
        
        base.Visit(node);
    }
    
    public override void Visit(OpenRowsetTableReference node)
    {
        _dynamicSqlUsages.Add(new DynamicSqlUsage
        {
            Type = DynamicSqlType.OpenRowset,
            Line = node.StartLine,
            Column = node.StartColumn
        });
        
        _warnings.Add($"Line {node.StartLine}: OPENROWSET detected - external data source, lineage may be incomplete");
        
        base.Visit(node);
    }
    
    // -------------------------------------------------------------------------
    // Helper Methods
    // -------------------------------------------------------------------------
    
    private void ProcessSelectForLineage(SelectStatement select, string targetTable, List<string> targetColumns)
    {
        if (select.QueryExpression is QuerySpecification spec)
        {
            _currentAliasMap = BuildAliasMap(spec.FromClause);
            
            int columnIndex = 0;
            foreach (var element in spec.SelectElements)
            {
                if (element is SelectStarExpression star)
                {
                    ProcessSelectStar(star, targetTable, targetColumns, ref columnIndex);
                }
                else if (element is SelectScalarExpression scalar)
                {
                    var targetColumn = columnIndex < targetColumns.Count 
                        ? targetColumns[columnIndex] 
                        : scalar.ColumnName?.Value ?? $"Column{columnIndex}";
                    
                    ProcessScalarExpression(scalar.Expression, targetTable, targetColumn, select.StartLine);
                    columnIndex++;
                }
            }
        }
    }
    
    private void ProcessSelectStar(SelectStarExpression star, string targetTable, 
        List<string> targetColumns, ref int columnIndex)
    {
        string tableQualifier = null;
        if (star.Qualifier != null)
        {
            tableQualifier = string.Join(".", star.Qualifier.Identifiers.Select(i => i.Value));
        }
        
        var tablesToExpand = tableQualifier != null
            ? _currentAliasMap.Where(kvp => kvp.Key.Equals(tableQualifier, StringComparison.OrdinalIgnoreCase))
            : _currentAliasMap;
        
        foreach (var (alias, tableInfo) in tablesToExpand)
        {
            var columns = _schemaProvider.GetTableColumns(tableInfo.Schema, tableInfo.Table);
            foreach (var col in columns)
            {
                var targetColumn = columnIndex < targetColumns.Count 
                    ? targetColumns[columnIndex] 
                    : col;
                
                _edges.Add(new LineageEdge
                {
                    Source = new LineageColumn
                    {
                        Schema = tableInfo.Schema,
                        Table = tableInfo.Table,
                        Column = col
                    },
                    Target = new LineageColumn
                    {
                        Schema = ExtractSchema(targetTable),
                        Table = ExtractTableOnly(targetTable),
                        Column = targetColumn
                    },
                    TransformationType = TransformationType.Direct,
                    SourceObject = _currentObjectName
                });
                
                columnIndex++;
            }
        }
    }
    
    private void ProcessScalarExpression(ScalarExpression expr, string targetTable, 
        string targetColumn, int line)
    {
        var sourceColumns = ExtractColumnsFromExpression(expr);
        var transformationType = DetermineTransformationType(expr);
        
        foreach (var sourceCol in sourceColumns)
        {
            var resolved = ResolveColumn(sourceCol, _currentAliasMap);
            
            _edges.Add(new LineageEdge
            {
                Source = resolved,
                Target = new LineageColumn
                {
                    Schema = ExtractSchema(targetTable),
                    Table = ExtractTableOnly(targetTable),
                    Column = targetColumn
                },
                TransformationType = transformationType,
                SourceObject = _currentObjectName,
                SourceLine = line
            });
        }
    }
    
    private List<ColumnReferenceExpression> ExtractColumnsFromExpression(ScalarExpression expr)
    {
        var visitor = new ColumnReferenceCollector();
        expr.Accept(visitor);
        return visitor.Columns;
    }
    
    private LineageColumn ResolveColumn(ColumnReferenceExpression colRef, 
        Dictionary<string, TableInfo> aliasMap)
    {
        var identifiers = colRef.MultiPartIdentifier?.Identifiers;
        if (identifiers == null || identifiers.Count == 0)
        {
            return new LineageColumn { Column = "Unknown" };
        }
        
        var columnName = identifiers.Last().Value;
        
        // Check for table qualifier
        if (identifiers.Count >= 2)
        {
            var tableAlias = identifiers[identifiers.Count - 2].Value;
            
            if (aliasMap.TryGetValue(tableAlias, out var tableInfo))
            {
                return new LineageColumn
                {
                    Schema = tableInfo.Schema,
                    Table = tableInfo.Table,
                    Column = columnName
                };
            }
            
            // Check if it's a CTE
            if (_cteDefinitions.TryGetValue(tableAlias, out var cte))
            {
                return new LineageColumn
                {
                    Schema = "CTE",
                    Table = cte.Name,
                    Column = columnName
                };
            }
        }
        
        // Unqualified column - search all tables
        return ResolveUnqualifiedColumn(columnName, aliasMap);
    }
    
    private LineageColumn ResolveUnqualifiedColumn(string columnName, 
        Dictionary<string, TableInfo> aliasMap)
    {
        var matches = new List<LineageColumn>();
        
        foreach (var (alias, tableInfo) in aliasMap)
        {
            if (_schemaProvider.ColumnExists(tableInfo.Schema, tableInfo.Table, columnName))
            {
                matches.Add(new LineageColumn
                {
                    Schema = tableInfo.Schema,
                    Table = tableInfo.Table,
                    Column = columnName
                });
            }
        }
        
        if (matches.Count == 0)
        {
            _warnings.Add($"Unable to resolve column: {columnName}");
            return new LineageColumn { Column = columnName };
        }
        
        if (matches.Count > 1)
        {
            _warnings.Add($"Ambiguous column reference: {columnName} found in {string.Join(", ", matches.Select(m => m.Table))}");
        }
        
        return matches.First();
    }
    
    private Dictionary<string, TableInfo> BuildAliasMap(FromClause fromClause)
    {
        var map = new Dictionary<string, TableInfo>(StringComparer.OrdinalIgnoreCase);
        
        if (fromClause == null) return map;
        
        foreach (var tableRef in fromClause.TableReferences)
        {
            CollectTableReferences(tableRef, map);
        }
        
        return map;
    }
    
    private void CollectTableReferences(TableReference tableRef, Dictionary<string, TableInfo> map)
    {
        switch (tableRef)
        {
            case NamedTableReference named:
                var tableName = named.SchemaObject.BaseIdentifier.Value;
                var schema = named.SchemaObject.SchemaIdentifier?.Value ?? "dbo";
                var alias = named.Alias?.Value ?? tableName;
                
                // Check if it's a CTE reference
                if (_cteDefinitions.ContainsKey(tableName))
                {
                    map[alias] = new TableInfo { Schema = "CTE", Table = tableName };
                }
                // Check if it's a temp table
                else if (_tempTables.ContainsKey(tableName))
                {
                    map[alias] = new TableInfo { Schema = "tempdb", Table = tableName };
                }
                else
                {
                    map[alias] = new TableInfo { Schema = schema, Table = tableName };
                }
                break;
                
            case JoinTableReference join:
                CollectTableReferences(join.FirstTableReference, map);
                CollectTableReferences(join.SecondTableReference, map);
                break;
                
            case QueryDerivedTable derived:
                var derivedAlias = derived.Alias?.Value ?? $"derived_{map.Count}";
                map[derivedAlias] = new TableInfo { Schema = "derived", Table = derivedAlias };
                break;
        }
    }
    
    private TransformationType DetermineTransformationType(ScalarExpression expr)
    {
        return expr switch
        {
            ColumnReferenceExpression => TransformationType.Direct,
            FunctionCall func when IsAggregateFunction(func.FunctionName.Value) => TransformationType.Aggregation,
            FunctionCall => TransformationType.Function,
            CastCall => TransformationType.Function,
            ConvertCall => TransformationType.Function,
            CaseExpression => TransformationType.CaseExpression,
            CoalesceExpression => TransformationType.Coalesce,
            NullIfExpression => TransformationType.Coalesce,
            BinaryExpression bin when IsArithmetic(bin.BinaryExpressionType) => TransformationType.Arithmetic,
            BinaryExpression bin when bin.BinaryExpressionType == BinaryExpressionType.Add 
                && IsStringType(bin) => TransformationType.Concatenation,
            _ => TransformationType.Unknown
        };
    }
    
    private bool IsAggregateFunction(string functionName)
    {
        var aggregates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SUM", "COUNT", "AVG", "MIN", "MAX", "STDEV", "VAR", 
            "COUNT_BIG", "STDEVP", "VARP", "GROUPING", "CHECKSUM_AGG"
        };
        return aggregates.Contains(functionName);
    }
    
    private bool IsArithmetic(BinaryExpressionType type)
    {
        return type is BinaryExpressionType.Add or BinaryExpressionType.Subtract
            or BinaryExpressionType.Multiply or BinaryExpressionType.Divide
            or BinaryExpressionType.Modulo;
    }
    
    private bool IsStringType(BinaryExpression expr) => false; // Simplified
    
    private string ExtractTableName(TableReference tableRef)
    {
        if (tableRef is NamedTableReference named)
        {
            var schema = named.SchemaObject.SchemaIdentifier?.Value ?? "dbo";
            var table = named.SchemaObject.BaseIdentifier.Value;
            return $"{schema}.{table}";
        }
        return "Unknown";
    }
    
    private TableInfo ExtractTableInfo(NamedTableReference named)
    {
        return new TableInfo
        {
            Schema = named.SchemaObject.SchemaIdentifier?.Value ?? "dbo",
            Table = named.SchemaObject.BaseIdentifier.Value
        };
    }
    
    private string ExtractUpdateTarget(UpdateStatement node)
    {
        return ExtractTableName(node.UpdateSpecification.Target);
    }
    
    private string ExtractSchema(string fqn)
    {
        var parts = fqn.Split('.');
        return parts.Length > 1 ? parts[0] : "dbo";
    }
    
    private string ExtractTableOnly(string fqn)
    {
        var parts = fqn.Split('.');
        return parts.Length > 1 ? parts[1] : parts[0];
    }
    
    private string GetDataTypeName(DataTypeReference dataType)
    {
        if (dataType is SqlDataTypeReference sqlType)
        {
            return sqlType.SqlDataTypeOption.ToString();
        }
        return dataType?.ToString() ?? "Unknown";
    }
}

// Supporting types
public record TableInfo
{
    public string Schema { get; init; }
    public string Table { get; init; }
}

public record CteDefinition
{
    public string Name { get; init; }
    public List<string> Columns { get; init; }
    public QueryExpression QueryExpression { get; init; }
    public int DefinitionLine { get; init; }
}

public class QueryContext
{
    public string TargetTable { get; init; }
    public List<string> TargetColumns { get; init; }
    public StatementType StatementType { get; init; }
    public int StatementLine { get; init; }
}

public enum StatementType { Select, Insert, Update, Delete, Merge }

// =============================================================================
// SECTION 5: Helper Visitors
// =============================================================================

/// <summary>
/// Collects all column references from an expression
/// </summary>
public class ColumnReferenceCollector : TSqlConcreteFragmentVisitor
{
    public List<ColumnReferenceExpression> Columns { get; } = new();
    
    public override void Visit(ColumnReferenceExpression node)
    {
        Columns.Add(node);
        base.Visit(node);
    }
}

/// <summary>
/// Extracts output column names from SELECT list
/// </summary>
public class SelectListColumnExtractor : TSqlConcreteFragmentVisitor
{
    public List<string> OutputColumns { get; } = new();
    private int _columnIndex = 0;
    
    public override void Visit(SelectScalarExpression node)
    {
        var name = node.ColumnName?.Value;
        
        if (name == null && node.Expression is ColumnReferenceExpression colRef)
        {
            name = colRef.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
        }
        
        OutputColumns.Add(name ?? $"Column{_columnIndex}");
        _columnIndex++;
        
        base.Visit(node);
    }
}

// =============================================================================
// SECTION 6: Main Extraction Service
// =============================================================================

/// <summary>
/// High-level service for lineage extraction
/// </summary>
public class LineageExtractionService
{
    private readonly TsqlParsingService _parser;
    private readonly ISchemaMetadataProvider _schemaProvider;
    
    public LineageExtractionService(string connectionString)
    {
        _parser = new TsqlParsingService();
        _schemaProvider = new SqlServerMetadataProvider(connectionString);
    }
    
    public async Task<LineageExtractionResult> ExtractLineageAsync(string sql, string objectName = null)
    {
        var startTime = DateTime.UtcNow;
        
        // Ensure schema cache is loaded
        await _schemaProvider.RefreshCacheAsync();
        
        // Parse SQL
        var parseResult = _parser.Parse(sql);
        
        if (!parseResult.Success)
        {
            return new LineageExtractionResult
            {
                ObjectName = objectName,
                Warnings = parseResult.Errors.Select(e => $"Parse error at line {e.Line}: {e.Message}").ToList(),
                Edges = Array.Empty<LineageEdge>(),
                OverallConfidence = 0
            };
        }
        
        // Extract lineage
        var visitor = new LineageExtractorVisitor(_schemaProvider);
        visitor.SetObjectContext(objectName);
        parseResult.Fragment.Accept(visitor);
        
        // Calculate confidence
        var confidence = CalculateConfidence(visitor);
        
        return new LineageExtractionResult
        {
            ObjectName = objectName,
            ObjectType = DetermineObjectType(parseResult.Fragment),
            Edges = visitor.Edges,
            Warnings = visitor.Warnings,
            DynamicSqlUsages = visitor.DynamicSqlUsages,
            TempTables = visitor.TempTables.Values.ToList(),
            OverallConfidence = confidence,
            ExtractionDuration = DateTime.UtcNow - startTime
        };
    }
    
    private decimal CalculateConfidence(LineageExtractorVisitor visitor)
    {
        var confidence = 1.0m;
        
        // Reduce confidence for dynamic SQL
        confidence -= visitor.DynamicSqlUsages.Count * 0.1m;
        
        // Reduce confidence for unresolved columns
        var unresolvedCount = visitor.Warnings.Count(w => w.Contains("Unable to resolve"));
        confidence -= unresolvedCount * 0.05m;
        
        // Reduce confidence for ambiguous columns
        var ambiguousCount = visitor.Warnings.Count(w => w.Contains("Ambiguous"));
        confidence -= ambiguousCount * 0.03m;
        
        return Math.Max(0, confidence);
    }
    
    private string DetermineObjectType(TSqlFragment fragment)
    {
        if (fragment is TSqlScript script && script.Batches.Count > 0)
        {
            var firstStatement = script.Batches[0].Statements.FirstOrDefault();
            return firstStatement switch
            {
                CreateProcedureStatement => "PROCEDURE",
                CreateFunctionStatement => "FUNCTION",
                CreateViewStatement => "VIEW",
                CreateTriggerStatement => "TRIGGER",
                _ => "SCRIPT"
            };
        }
        return "UNKNOWN";
    }
}
