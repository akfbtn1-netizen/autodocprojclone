// Enhanced StoredProcedureDocumentationService with performance tracking
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using Dapper;

namespace Enterprise.Documentation.Core.Application.Services.Documentation;

/// <summary>
/// Enhanced service with performance baseline tracking and automated regression testing integration
/// </summary>
public interface IPerformanceTrackingService
{
    Task<PerformanceBaseline> EstablishBaselineAsync(string procedureName, CancellationToken cancellationToken = default);
    Task<bool> ValidatePerformanceAsync(string procedureName, CancellationToken cancellationToken = default);
    Task<List<PerformanceAlert>> GetPerformanceAlertsAsync(string procedureName, CancellationToken cancellationToken = default);
}

public class PerformanceBaseline
{
    public string ProcedureName { get; set; } = string.Empty;
    public decimal AvgCpuTime { get; set; }
    public decimal AvgDuration { get; set; }
    public long AvgLogicalReads { get; set; }
    public long AvgPhysicalReads { get; set; }
    public DateTime BaselineDate { get; set; }
    public int SampleSize { get; set; }
}

public class PerformanceAlert
{
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal BaselineValue { get; set; }
    public decimal DeviationPercent { get; set; }
    public DateTime DetectedAt { get; set; }
}

/// <summary>
/// Extension methods for enhanced performance tracking
/// </summary>
public static class PerformanceTrackingExtensions
{
    public static async Task<string> GenerateEnhancedPerformanceNotesAsync(
        this string procedureDefinition,
        string procedureName,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
    var notes = new List<string>();

        // Get actual performance statistics from SQL Server
        var perfStats = await GetProcedurePerformanceStatsAsync(procedureName, connectionString, cancellationToken);
    
    if (perfStats != null)
    {
        notes.Add($"Average execution time: {perfStats.AvgDuration:F2}ms");
        notes.Add($"Average CPU time: {perfStats.AvgCpuTime:F2}ms");
        notes.Add($"Average logical reads: {perfStats.AvgLogicalReads:N0}");
        
        if (perfStats.AvgDuration > 1000)
        {
            notes.Add("⚠️ Long execution time detected - consider optimization");
        }
        
        if (perfStats.AvgLogicalReads > 100000)
        {
            notes.Add("⚠️ High I/O usage - review indexes and query patterns");
        }
    }

    // Static code analysis for performance patterns
    if (procedureDefinition.Contains("CURSOR", StringComparison.OrdinalIgnoreCase))
    {
        notes.Add("🐌 Contains cursor operations - high performance impact on large datasets");
    }

    var joinCount = Regex.Matches(procedureDefinition, @"\bJOIN\b", RegexOptions.IgnoreCase).Count;
    if (joinCount > 5)
    {
        notes.Add($"🔗 Multiple table joins ({joinCount}) - ensure proper indexing strategy");
    }

    if (procedureDefinition.Contains("SELECT *", StringComparison.OrdinalIgnoreCase))
    {
        notes.Add("📋 SELECT * detected - specify required columns for better performance");
    }

    if (Regex.IsMatch(procedureDefinition, @"WHERE.*LIKE.*%.*%", RegexOptions.IgnoreCase))
    {
        notes.Add("🔍 Leading wildcard searches detected - cannot use indexes efficiently");
    }

    return notes.Any() ? string.Join(" ", notes) : "No specific performance considerations identified.";
}

    private static async Task<PerformanceBaseline?> GetProcedurePerformanceStatsAsync(
        string procedureName,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

    return await connection.QueryFirstOrDefaultAsync<PerformanceBaseline>(@"
        SELECT TOP 1
            @ProcedureName as ProcedureName,
            AVG(qs.total_worker_time / qs.execution_count) / 1000.0 as AvgCpuTime,
            AVG(qs.total_elapsed_time / qs.execution_count) / 1000.0 as AvgDuration,
            AVG(qs.total_logical_reads / qs.execution_count) as AvgLogicalReads,
            AVG(qs.total_physical_reads / qs.execution_count) as AvgPhysicalReads,
            MAX(qs.creation_time) as BaselineDate,
            SUM(qs.execution_count) as SampleSize
        FROM sys.dm_exec_query_stats qs
        CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
        WHERE st.text LIKE '%' + @ProcedureName + '%'
        AND st.text NOT LIKE '%sys.dm_exec%'
        GROUP BY qs.sql_handle
        ORDER BY SUM(qs.execution_count) DESC",
        new { ProcedureName = procedureName });
    }
}