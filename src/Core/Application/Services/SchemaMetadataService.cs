using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Core.Application.Services;

/// <summary>
/// Service for retrieving and managing database schema metadata
/// </summary>
public class SchemaMetadataService : ISchemaMetadataService
{
    private readonly ILogger<SchemaMetadataService> _logger;

    public SchemaMetadataService(ILogger<SchemaMetadataService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Retrieves comprehensive metadata for a database object
    /// </summary>
    public async Task<SchemaMetadata> GetMetadataAsync(string schemaName, string objectName, CancellationToken cancellationToken = default)
    {
        try
        {
            // In a real implementation, this would query the database information schema
            // For now, providing mock implementation based on naming conventions
            
            var metadata = new SchemaMetadata
            {
                SchemaName = schemaName,
                ObjectName = objectName,
                ObjectType = DetermineObjectType(objectName),
                CreatedDate = DateTime.UtcNow.AddDays(-30), // Mock creation date
                ModifiedDate = DateTime.UtcNow.AddDays(-5), // Mock modification date
                Description = await GenerateDescription(schemaName, objectName),
                Dependencies = await GetDependencies(schemaName, objectName),
                Columns = await GetColumns(schemaName, objectName),
                Indexes = await GetIndexes(schemaName, objectName),
                Permissions = await GetPermissions(schemaName, objectName)
            };

            _logger.LogInformation("Retrieved metadata for {Schema}.{Object}", schemaName, objectName);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metadata for {Schema}.{Object}", schemaName, objectName);
            throw;
        }
    }

    /// <summary>
    /// Gets schema statistics and health metrics
    /// </summary>
    public async Task<SchemaStats> GetSchemaStatsAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        try
        {
            // Mock implementation - in reality would query system views
            var stats = new SchemaStats
            {
                SchemaName = schemaName,
                TotalObjects = await CountObjectsInSchema(schemaName),
                TableCount = await CountObjectsByType(schemaName, "TABLE"),
                ViewCount = await CountObjectsByType(schemaName, "VIEW"),
                ProcedureCount = await CountObjectsByType(schemaName, "PROCEDURE"),
                FunctionCount = await CountObjectsByType(schemaName, "FUNCTION"),
                LastAnalyzed = DateTime.UtcNow
            };

            _logger.LogInformation("Retrieved stats for schema {Schema}: {Objects} objects", schemaName, stats.TotalObjects);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stats for schema {Schema}", schemaName);
            throw;
        }
    }

    private string DetermineObjectType(string objectName)
    {
        var lowerName = objectName.ToLower();
        
        if (lowerName.StartsWith("sp_") || lowerName.StartsWith("proc_"))
            return "PROCEDURE";
        if (lowerName.StartsWith("fn_") || lowerName.StartsWith("func_"))
            return "FUNCTION";
        if (lowerName.StartsWith("vw_") || lowerName.StartsWith("view_"))
            return "VIEW";
        if (lowerName.StartsWith("tbl_") || lowerName.StartsWith("table_"))
            return "TABLE";
        if (lowerName.StartsWith("idx_") || lowerName.StartsWith("ix_"))
            return "INDEX";
        
        return "TABLE"; // Default assumption
    }

    private async Task<string> GenerateDescription(string schemaName, string objectName)
    {
        // Generate description based on naming patterns and conventions
        var objectType = DetermineObjectType(objectName);
        var cleanName = objectName.Replace("sp_", "").Replace("fn_", "").Replace("vw_", "").Replace("tbl_", "");
        
        return objectType switch
        {
            "PROCEDURE" => $"Stored procedure for {FormatName(cleanName)} operations in {schemaName} schema",
            "FUNCTION" => $"User-defined function for {FormatName(cleanName)} calculations",
            "VIEW" => $"View providing {FormatName(cleanName)} data access",
            "TABLE" => $"Table storing {FormatName(cleanName)} information",
            _ => $"Database object {objectName} in {schemaName} schema"
        };
    }

    private async Task<List<string>> GetDependencies(string schemaName, string objectName)
    {
        // Mock dependencies based on common patterns
        var dependencies = new List<string>();
        
        var objectType = DetermineObjectType(objectName);
        if (objectType == "PROCEDURE" || objectType == "VIEW")
        {
            // Add some common table dependencies
            dependencies.Add($"{schemaName}.Users");
            dependencies.Add($"{schemaName}.Audit_Log");
        }
        
        return dependencies;
    }

    private async Task<List<ColumnInfo>> GetColumns(string schemaName, string objectName)
    {
        var columns = new List<ColumnInfo>();
        var objectType = DetermineObjectType(objectName);
        
        if (objectType == "TABLE" || objectType == "VIEW")
        {
            // Add standard columns based on object type
            columns.Add(new ColumnInfo { Name = "Id", DataType = "int", IsNullable = false, IsPrimaryKey = true });
            columns.Add(new ColumnInfo { Name = "Name", DataType = "nvarchar", MaxLength = 255, IsNullable = false });
            columns.Add(new ColumnInfo { Name = "CreatedDate", DataType = "datetime2", IsNullable = false });
            columns.Add(new ColumnInfo { Name = "ModifiedDate", DataType = "datetime2", IsNullable = true });
        }
        
        return columns;
    }

    private async Task<List<string>> GetIndexes(string schemaName, string objectName)
    {
        var indexes = new List<string>();
        
        if (DetermineObjectType(objectName) == "TABLE")
        {
            indexes.Add($"PK_{objectName}_Id");
            indexes.Add($"IX_{objectName}_Name");
            indexes.Add($"IX_{objectName}_CreatedDate");
        }
        
        return indexes;
    }

    private async Task<List<string>> GetPermissions(string schemaName, string objectName)
    {
        // Mock permissions
        return new List<string>
        {
            "db_datareader: SELECT",
            "db_datawriter: INSERT, UPDATE, DELETE",
            "db_owner: ALL"
        };
    }

    private async Task<int> CountObjectsInSchema(string schemaName)
    {
        // Mock count based on schema name
        return schemaName.ToLower() switch
        {
            "dbo" => 150,
            "audit" => 25,
            "security" => 40,
            "reporting" => 85,
            _ => 30
        };
    }

    private async Task<int> CountObjectsByType(string schemaName, string objectType)
    {
        var total = await CountObjectsInSchema(schemaName);
        return objectType.ToUpper() switch
        {
            "TABLE" => total / 2,
            "VIEW" => total / 4,
            "PROCEDURE" => total / 6,
            "FUNCTION" => total / 8,
            _ => 0
        };
    }

    private string FormatName(string name)
    {
        // Convert underscore_case to readable format
        return string.Join(" ", name.Split('_').Select(word => 
            char.ToUpper(word[0]) + word.Substring(1).ToLower()));
    }

    // Interface wrapper methods
    public async Task<List<string>> GetSchemasAsync()
    {
        // Return mock schemas
        return new List<string> { "dbo", "reporting", "staging", "archive" };
    }

    public async Task<List<string>> GetTablesAsync(string schemaName)
    {
        // Return mock table names
        return new List<string> { "Users", "Products", "Orders", "Audit_Log" };
    }

    public async Task<List<string>> GetStoredProceduresAsync(string schemaName)
    {
        // Return mock procedure names
        return new List<string> { "sp_GetUsers", "sp_ProcessOrder", "sp_GenerateReport" };
    }

    public async Task<List<ColumnInfo>> GetColumnsAsync(string schemaName, string tableName)
    {
        // Call existing method
        return await GetColumns(schemaName, tableName);
    }

    public async Task<string?> GetTableDescriptionAsync(string schemaName, string tableName)
    {
        // Call existing method
        return await GenerateDescription(schemaName, tableName);
    }

    public async Task<string?> GetProcedureDescriptionAsync(string schemaName, string procedureName)
    {
        // Call existing method
        return await GenerateDescription(schemaName, procedureName);
    }

    /// <summary>
    /// Extract metadata for a specific schema object (alias for GetMetadataAsync)
    /// </summary>
    public async Task<SchemaMetadata> ExtractMetadataAsync(string schemaName, string objectName)
    {
        return await GetMetadataAsync(schemaName, objectName);
    }
}