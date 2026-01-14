// =============================================================================
// Agent #7: Gap Intelligence Agent - Query Pattern Miner
// Mines query patterns from SQL Server DMVs for usage analysis
// =============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Enterprise.Documentation.Core.Application.Services.GapIntelligence;

/// <summary>
/// Mines query patterns from SQL Server DMVs to identify high-usage database objects.
/// Uses sys.dm_exec_query_stats and sys.dm_exec_sql_text for execution statistics.
/// </summary>
public class QueryPatternMiner : IQueryPatternMiner
{
    private readonly ILogger<QueryPatternMiner> _logger;
    private readonly string _connectionString;

    public QueryPatternMiner(ILogger<QueryPatternMiner> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    /// <summary>
    /// Mine query patterns from execution stats for the specified lookback period
    /// </summary>
    public async Task<List<UsageHeatmapEntry>> MineQueryPatternsAsync(int lookbackDays = 30, CancellationToken ct = default)
    {
        _logger.LogInformation("Mining query patterns from DMVs for last {Days} days", lookbackDays);

        using var connection = new SqlConnection(_connectionString);

        try
        {
            // Mine from sys.dm_exec_query_stats joined with sys.dm_exec_sql_text
            var results = await connection.QueryAsync<UsageHeatmapEntry>(@"
                SELECT
                    OBJECT_SCHEMA_NAME(st.objectid, st.dbid) AS SchemaName,
                    OBJECT_NAME(st.objectid, st.dbid) AS ObjectName,
                    'PROCEDURE' AS ObjectType,
                    SUM(qs.execution_count) AS ExecutionCount30d,
                    AVG(qs.total_worker_time / NULLIF(qs.execution_count, 0)) / 1000.0 AS AvgCpuTimeMs,
                    AVG(qs.total_logical_reads / NULLIF(qs.execution_count, 0)) AS AvgLogicalReads
                FROM sys.dm_exec_query_stats qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
                WHERE st.objectid IS NOT NULL
                  AND qs.last_execution_time > DATEADD(DAY, -@Days, GETUTCDATE())
                  AND OBJECT_SCHEMA_NAME(st.objectid, st.dbid) IS NOT NULL
                GROUP BY OBJECT_SCHEMA_NAME(st.objectid, st.dbid), OBJECT_NAME(st.objectid, st.dbid)",
                new { Days = lookbackDays });

            _logger.LogInformation("Found {Count} objects with query stats", results.Count());
            return results.ToList();
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Failed to mine query patterns from DMVs - this may require elevated permissions");
            return new List<UsageHeatmapEntry>();
        }
    }

    /// <summary>
    /// Find high-usage objects that lack documentation
    /// </summary>
    public async Task<List<UndocumentedHotspot>> FindUndocumentedHotspotsAsync(CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);

        var hotspots = await connection.QueryAsync<UndocumentedHotspot>(@"
            SELECT h.SchemaName, h.ObjectName, h.ObjectType, h.HeatScore, h.ExecutionCount30d
            FROM DaQa.UsageHeatmap h
            LEFT JOIN DaQa.MasterIndex m ON h.SchemaName = m.SchemaName AND h.ObjectName = m.ObjectName AND m.IsActive = 1
            WHERE m.IndexId IS NULL AND h.HeatScore > 30
            ORDER BY h.HeatScore DESC");

        _logger.LogInformation("Found {Count} undocumented hotspots", hotspots.Count());
        return hotspots.ToList();
    }
}
