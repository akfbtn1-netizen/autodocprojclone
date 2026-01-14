using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Enterprise.Documentation.Core.Application.Services.Metadata;

/// <summary>
/// Service to retrieve metadata from the MasterIndex for enhanced documentation generation
/// </summary>
public interface IMetadataEnhancementService
{
    Task<MetadataContext?> GetMetadataContextAsync(string? schemaName, string? tableName, string? columnName, CancellationToken cancellationToken = default);
    Task<List<string>> GetRelatedObjectsAsync(string schemaName, string tableName, CancellationToken cancellationToken = default);
    Task<DataLineageInfo> GetLineageInfoAsync(string schemaName, string tableName, CancellationToken cancellationToken = default);
}

public class MetadataContext
{
    public string? ExistingDescription { get; set; }
    public string? BusinessDomain { get; set; }
    public string? DataClassification { get; set; }
    public int DownstreamDependencies { get; set; }
    public string? SemanticCategory { get; set; }
    public double QualityScore { get; set; }
    public string? UsagePattern { get; set; }
    public bool ContainsPII { get; set; }
    public string? BusinessOwner { get; set; }
    public List<string> RelatedTables { get; set; } = new();
    public Dictionary<string, object> ExtendedProperties { get; set; } = new();
}

public class DataLineageInfo
{
    public List<string> UpstreamObjects { get; set; } = new();
    public List<string> DownstreamObjects { get; set; } = new();
    public int TotalDependencies { get; set; }
    public string ImpactLevel { get; set; } = "LOW"; // LOW, MEDIUM, HIGH, CRITICAL
}

public class MetadataEnhancementService : IMetadataEnhancementService
{
    private readonly ILogger<MetadataEnhancementService> _logger;
    private readonly string _connectionString;

    public MetadataEnhancementService(
        ILogger<MetadataEnhancementService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public async Task<MetadataContext?> GetMetadataContextAsync(
        string? schemaName, 
        string? tableName, 
        string? columnName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var sql = @"
SELECT TOP 1
    Description AS ExistingDescription,
    BusinessDomain,
    DataClassification,
    ISNULL(DownstreamDependencies, 0) AS DownstreamDependencies,
    SemanticCategory,
    ISNULL(QualityScore, 0) AS QualityScore,
    UsagePattern,
    ISNULL(ContainsPII, 0) AS ContainsPII,
    BusinessOwner,
    ExtendedProperties
FROM DaQa.MasterIndex
WHERE (@SchemaName IS NULL OR SchemaName = @SchemaName)
    AND (@TableName IS NULL OR ObjectName = @TableName)
    AND (@ColumnName IS NULL OR ColumnName = @ColumnName OR ColumnName IS NULL)
    AND IsActive = 1
ORDER BY 
    CASE WHEN ColumnName = @ColumnName THEN 1 ELSE 2 END,
    ModifiedDate DESC";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new
            {
                SchemaName = schemaName,
                TableName = tableName,
                ColumnName = columnName
            });

            if (result == null) return null;

            return new MetadataContext
            {
                ExistingDescription = result.ExistingDescription,
                BusinessDomain = result.BusinessDomain,
                DataClassification = result.DataClassification,
                DownstreamDependencies = result.DownstreamDependencies,
                SemanticCategory = result.SemanticCategory,
                QualityScore = result.QualityScore,
                UsagePattern = result.UsagePattern,
                ContainsPII = result.ContainsPII == 1,
                BusinessOwner = result.BusinessOwner,
                ExtendedProperties = ParseExtendedProperties(result.ExtendedProperties)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve metadata context for {Schema}.{Table}.{Column}", 
                schemaName, tableName, columnName);
            return null;
        }
    }

    public async Task<List<string>> GetRelatedObjectsAsync(string schemaName, string tableName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var sql = @"
-- Get objects that reference this table
SELECT DISTINCT 
    SchemaName + '.' + ObjectName AS RelatedObject
FROM DaQa.MasterIndex
WHERE (
    UpstreamObjects LIKE '%' + @SchemaName + '.' + @TableName + '%'
    OR DownstreamObjects LIKE '%' + @SchemaName + '.' + @TableName + '%'
)
AND IsActive = 1
AND NOT (SchemaName = @SchemaName AND ObjectName = @TableName)";

            var results = await connection.QueryAsync<string>(sql, new
            {
                SchemaName = schemaName,
                TableName = tableName
            });

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve related objects for {Schema}.{Table}", schemaName, tableName);
            return new List<string>();
        }
    }

    public async Task<DataLineageInfo> GetLineageInfoAsync(string schemaName, string tableName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var sql = @"
SELECT 
    ISNULL(UpstreamDependencies, 0) AS UpstreamCount,
    ISNULL(DownstreamDependencies, 0) AS DownstreamCount,
    UpstreamObjects,
    DownstreamObjects
FROM DaQa.MasterIndex
WHERE SchemaName = @SchemaName 
    AND ObjectName = @TableName
    AND (ColumnName IS NULL OR ColumnName = '')
    AND IsActive = 1";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new
            {
                SchemaName = schemaName,
                TableName = tableName
            });

            if (result == null)
            {
                return new DataLineageInfo();
            }

            var upstreamCount = (int)result.UpstreamCount;
            var downstreamCount = (int)result.DownstreamCount;
            var totalDependencies = upstreamCount + downstreamCount;

            var impactLevel = totalDependencies switch
            {
                >= 20 => "CRITICAL",
                >= 10 => "HIGH",
                >= 5 => "MEDIUM",
                _ => "LOW"
            };

            return new DataLineageInfo
            {
                UpstreamObjects = ParseObjectList(result.UpstreamObjects),
                DownstreamObjects = ParseObjectList(result.DownstreamObjects),
                TotalDependencies = totalDependencies,
                ImpactLevel = impactLevel
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve lineage info for {Schema}.{Table}", schemaName, tableName);
            return new DataLineageInfo();
        }
    }

    private Dictionary<string, object> ParseExtendedProperties(string? json)
    {
        try
        {
            if (string.IsNullOrEmpty(json)) return new Dictionary<string, object>();
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json) 
                   ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private List<string> ParseObjectList(string? objectList)
    {
        if (string.IsNullOrEmpty(objectList)) return new List<string>();
        
        return objectList.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
    }
}