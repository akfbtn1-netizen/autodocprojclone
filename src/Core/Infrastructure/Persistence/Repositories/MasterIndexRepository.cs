using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dapper-based repository for MasterIndex catalog operations.
/// Uses direct SQL with Dapper for high-performance read/write operations.
/// NO Entity Framework dependencies - pure Dapper implementation.
/// </summary>
public class MasterIndexRepository : IMasterIndexRepository
{
    private readonly string _connectionString;
    private readonly ILogger<MasterIndexRepository> _logger;

    public MasterIndexRepository(
        IConfiguration configuration,
        ILogger<MasterIndexRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured in appsettings");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("MasterIndexRepository initialized with Dapper (no EF)");
    }

    // ===== WRITE OPERATIONS (NEW) =====

    public async Task<MasterIndex> AddAsync(MasterIndex entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO IRFS1.DaQa.MasterIndex (
                JiraNumber, ObjectName, Tier, Priority, BusinessImpact, TechnicalComplexity,
                DataSensitivity, DocumentTitle, DocumentStatus, DocumentPath, ReviewedBy,
                BusinessOwner, TechnicalOwner, StakeholderGroup, BusinessJustification,
                CreatedDate, ModifiedDate, IsActive
            )
            OUTPUT INSERTED.IndexID
            VALUES (
                @JiraNumber, @ObjectName, @Tier, @Priority, @BusinessImpact, @TechnicalComplexity,
                @DataSensitivity, @DocumentTitle, @DocumentStatus, @DocumentPath, @ReviewedBy,
                @BusinessOwner, @TechnicalOwner, @StakeholderGroup, @BusinessJustification,
                GETUTCDATE(), GETUTCDATE(), 1
            )";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var indexId = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
            
            entity.Id = indexId;
            
            _logger.LogInformation("Added MasterIndex entry {IndexId} for {Schema}.{Object}", 
                indexId, entity.SchemaName, entity.ObjectName);
            
            return entity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add MasterIndex entry for {Schema}.{Object}", 
                entity.SchemaName, entity.ObjectName);
            throw;
        }
    }

    public async Task UpdateAsync(MasterIndex entity, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE IRFS1.DaQa.MasterIndex
            SET JiraNumber = @JiraNumber,
                ObjectName = @ObjectName,
                Tier = @Tier,
                Priority = @Priority,
                BusinessImpact = @BusinessImpact,
                TechnicalComplexity = @TechnicalComplexity,
                DataSensitivity = @DataSensitivity,
                DocumentTitle = @DocumentTitle,
                DocumentStatus = @DocumentStatus,
                DocumentPath = @DocumentPath,
                ReviewedBy = @ReviewedBy,
                BusinessOwner = @BusinessOwner,
                TechnicalOwner = @TechnicalOwner,
                StakeholderGroup = @StakeholderGroup,
                BusinessJustification = @BusinessJustification,
                ModifiedDate = GETUTCDATE()
            WHERE IndexID = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(sql, entity, cancellationToken: cancellationToken));
            
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"MasterIndex entry {entity.Id} not found");
            }
            
            _logger.LogInformation("Updated MasterIndex entry {IndexId}", entity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MasterIndex entry {IndexId}", entity.Id);
            throw;
        }
    }

    public async Task UpdateFieldsAsync(
        int indexId, 
        Dictionary<string, object?> fieldsToUpdate, 
        CancellationToken cancellationToken = default)
    {
        if (fieldsToUpdate == null || fieldsToUpdate.Count == 0)
        {
            throw new ArgumentException("No fields provided for update", nameof(fieldsToUpdate));
        }

        // Build dynamic SET clause
        var setClauses = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("@IndexID", indexId);

        foreach (var field in fieldsToUpdate)
        {
            setClauses.Add($"{field.Key} = @{field.Key}");
            parameters.Add($"@{field.Key}", field.Value);
        }

        // Always update ModifiedDate
        setClauses.Add("ModifiedDate = GETUTCDATE()");

        var sql = $@"
            UPDATE IRFS1.DaQa.MasterIndex
            SET {string.Join(", ", setClauses)}
            WHERE IndexID = @IndexID";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"MasterIndex entry {indexId} not found");
            }
            
            _logger.LogInformation("Updated {FieldCount} fields for MasterIndex entry {IndexId}", 
                fieldsToUpdate.Count, indexId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update fields for MasterIndex entry {IndexId}", indexId);
            throw;
        }
    }

    public async Task DeleteAsync(int indexId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE IRFS1.DaQa.MasterIndex
            SET IsActive = 0,
                ModifiedDate = GETUTCDATE()
            WHERE IndexID = @IndexId";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(sql, new { IndexId = indexId }, cancellationToken: cancellationToken));
            
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"MasterIndex entry {indexId} not found");
            }
            
            _logger.LogInformation("Soft deleted MasterIndex entry {IndexId}", indexId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete MasterIndex entry {IndexId}", indexId);
            throw;
        }
    }

    // ===== READ OPERATIONS =====
    
    // Original interface method signatures (for backwards compatibility)
    public async Task<MasterIndex?> GetByIdAsync(int id)
    {
        return await GetByIdAsync(id, CancellationToken.None);
    }

    public async Task<MasterIndex?> GetByJiraAndObjectAsync(string jiraNumber, string documentType, string objectName, string schemaName)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE JiraNumber = @JiraNumber 
              AND DocumentType = @DocumentType
              AND ObjectName = @ObjectName 
              AND SchemaName = @SchemaName
              AND IsActive = 1";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            return await connection.QuerySingleOrDefaultAsync<MasterIndex>(sql, new 
            { 
                JiraNumber = jiraNumber, 
                DocumentType = documentType, 
                ObjectName = objectName, 
                SchemaName = schemaName 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MasterIndex by Jira {Jira} and object {Object}", jiraNumber, objectName);
            throw;
        }
    }

    public async Task<List<MasterIndex>> GetByStatusAsync(string status)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE Status = @Status AND IsActive = 1
            ORDER BY ModifiedDate DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            var results = await connection.QueryAsync<MasterIndex>(sql, new { Status = status });
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries by status {Status}", status);
            throw;
        }
    }

    public async Task<List<MasterIndex>> GetByTierAsync(int tier)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE Tier = @Tier AND IsActive = 1
            ORDER BY ModifiedDate DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            var results = await connection.QueryAsync<MasterIndex>(sql, new { Tier = tier });
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries by tier {Tier}", tier);
            throw;
        }
    }

    // Original AddAsync without CancellationToken
    public async Task<MasterIndex> AddAsync(MasterIndex entity)
    {
        return await AddAsync(entity, CancellationToken.None);
    }

    // Original UpdateAsync without CancellationToken
    public async Task UpdateAsync(MasterIndex entity)
    {
        await UpdateAsync(entity, CancellationToken.None);
    }

    // Original DeleteAsync without CancellationToken
    public async Task DeleteAsync(int id)
    {
        await DeleteAsync(id, CancellationToken.None);
    }

    // Original ExistsAsync with different signature
    public async Task<bool> ExistsAsync(string jiraNumber, string documentType, string objectName, string schemaName)
    {
        const string sql = @"
            SELECT CAST(CASE WHEN EXISTS(
                SELECT 1 FROM IRFS1.DaQa.MasterIndex
                WHERE JiraNumber = @JiraNumber 
                  AND DocumentType = @DocumentType
                  AND ObjectName = @ObjectName 
                  AND SchemaName = @SchemaName
                  AND IsActive = 1
            ) THEN 1 ELSE 0 END AS BIT)";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            return await connection.ExecuteScalarAsync<bool>(sql, new 
            { 
                JiraNumber = jiraNumber, 
                DocumentType = documentType, 
                ObjectName = objectName, 
                SchemaName = schemaName 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence for Jira {Jira}", jiraNumber);
            throw;
        }
    }
    
    public async Task<MasterIndex?> GetByIdAsync(int indexId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE IndexID = @IndexId AND IsActive = 1";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            return await connection.QuerySingleOrDefaultAsync<MasterIndex>(
                new CommandDefinition(sql, new { IndexId = indexId }, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MasterIndex entry {IndexId}", indexId);
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetAllAsync(
        int pageNumber = 1, 
        int pageSize = 100, 
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE IsActive = 1
            ORDER BY IndexID
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var offset = (pageNumber - 1) * pageSize;
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, new { Offset = offset, PageSize = pageSize }, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all MasterIndex entries");
            throw;
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM IRFS1.DaQa.MasterIndex WHERE IsActive = 1";
        
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            return await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get total count");
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetByDatabaseAsync(
        string databaseName, 
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE DatabaseName = @DatabaseName AND IsActive = 1
            ORDER BY SchemaName, ObjectName";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, new { DatabaseName = databaseName }, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries for database {Database}", databaseName);
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetByTableAsync(
        string databaseName,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE DatabaseName = @DatabaseName
              AND SchemaName = @SchemaName
              AND ObjectName = @TableName
              AND IsActive = 1
            ORDER BY ColumnName";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, new { DatabaseName = databaseName, SchemaName = schemaName, TableName = tableName }, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries for table {Schema}.{Table}", schemaName, tableName);
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetTablesBySchemaAsync(
        string databaseName,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT DISTINCT DatabaseName, SchemaName, ObjectName
            FROM IRFS1.DaQa.MasterIndex
            WHERE DatabaseName = @DatabaseName
              AND SchemaName = @SchemaName
              AND IsActive = 1
            ORDER BY ObjectName";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, new { DatabaseName = databaseName, SchemaName = schemaName }, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tables for schema {Schema}", schemaName);
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetColumnsByTableAsync(
        string databaseName,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE DatabaseName = @DatabaseName
              AND SchemaName = @SchemaName
              AND ObjectName = @TableName
              AND ColumnName IS NOT NULL
              AND IsActive = 1
            ORDER BY ColumnName";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, new { DatabaseName = databaseName, SchemaName = schemaName, TableName = tableName }, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get columns for table {Schema}.{Table}", schemaName, tableName);
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetGeneratedDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE DocumentationUrl IS NOT NULL
              AND IsActive = 1
            ORDER BY ModifiedDate DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get generated documents");
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetByApprovalStatusAsync(
        string approvalStatus,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE ApprovalStatus = @ApprovalStatus
              AND IsActive = 1
            ORDER BY ModifiedDate DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, new { ApprovalStatus = approvalStatus }, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries by approval status {Status}", approvalStatus);
            throw;
        }
    }

    public async Task<IReadOnlyList<MasterIndex>> GetByWorkflowStatusAsync(
        string workflowStatus,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE WorkflowStatus = @WorkflowStatus
              AND IsActive = 1
            ORDER BY ModifiedDate DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(sql, new { WorkflowStatus = workflowStatus }, cancellationToken: cancellationToken));
            
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entries by workflow status {Status}", workflowStatus);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(
        string databaseName,
        string schemaName,
        string tableName,
        string? columnName = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT CAST(CASE WHEN EXISTS(
                SELECT 1 FROM IRFS1.DaQa.MasterIndex
                WHERE DatabaseName = @DatabaseName
                  AND SchemaName = @SchemaName
                  AND ObjectName = @TableName
                  AND (@ColumnName IS NULL OR ColumnName = @ColumnName)
                  AND IsActive = 1
            ) THEN 1 ELSE 0 END AS BIT)";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new 
                { 
                    DatabaseName = databaseName,
                    SchemaName = schemaName,
                    TableName = tableName,
                    ColumnName = columnName
                }, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence");
            throw;
        }
    }

    // ===== STUB IMPLEMENTATIONS FOR REMAINING INTERFACE METHODS =====
    // TODO: Implement these following the same Dapper patterns above
    
    public async Task<IReadOnlyList<MasterIndex>> SearchAsync(
        string searchTerm,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM IRFS1.DaQa.MasterIndex
            WHERE IsActive = 1
              AND (
                PhysicalName LIKE @SearchPattern
                OR LogicalName LIKE @SearchPattern
                OR [Description] LIKE @SearchPattern
                OR SchemaName LIKE @SearchPattern
                OR DatabaseName LIKE @SearchPattern
                OR ObjectType LIKE @SearchPattern
                OR Category LIKE @SearchPattern
                OR BusinessDomain LIKE @SearchPattern
                OR TechnicalSummary LIKE @SearchPattern
                OR BusinessPurpose LIKE @SearchPattern
                OR ColumnName LIKE @SearchPattern
              )
            ORDER BY
                CASE WHEN PhysicalName LIKE @SearchPattern THEN 0 ELSE 1 END,
                ModifiedDate DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var offset = (pageNumber - 1) * pageSize;
            var searchPattern = $"%{searchTerm}%";

            var results = await connection.QueryAsync<MasterIndex>(
                new CommandDefinition(
                    sql,
                    new { SearchPattern = searchPattern, Offset = offset, PageSize = pageSize },
                    cancellationToken: cancellationToken));

            _logger.LogInformation("Search for '{SearchTerm}' returned {Count} results", searchTerm, results.Count());
            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search MasterIndex for term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    public Task<IReadOnlyList<MasterIndex>> GetByBusinessDomainAsync(string businessDomain, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetByBusinessDomainAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetByCategoryAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetByTagsAsync(string tags, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetByTagsAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetWithUpstreamDependenciesAsync(string tableName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetWithUpstreamDependenciesAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetWithDownstreamDependenciesAsync(string tableName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetWithDownstreamDependenciesAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetRelatedTablesAsync(string databaseName, string schemaName, string tableName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetRelatedTablesAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetEntriesWithPIIAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetEntriesWithPIIAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetByDataClassificationAsync(string classification, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetByDataClassificationAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetLowQualityEntriesAsync(decimal qualityThreshold = 70, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetLowQualityEntriesAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetEntriesNeedingValidationAsync(int daysThreshold = 90, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetEntriesNeedingValidationAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetBySemanticCategoryAsync(string semanticCategory, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetBySemanticCategoryAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetWithOptimizationSuggestionsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetWithOptimizationSuggestionsAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetByComplexityAsync(string complexityLevel, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetByComplexityAsync - implement using Dapper");
    }

    public Task<Dictionary<string, int>> GetCountByBusinessDomainAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetCountByBusinessDomainAsync - implement using Dapper");
    }

    public Task<Dictionary<string, int>> GetCountByDataClassificationAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetCountByDataClassificationAsync - implement using Dapper");
    }

    public Task<Dictionary<string, decimal>> GetAverageQualityScoreByDomainAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetAverageQualityScoreByDomainAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetModifiedSinceAsync(DateTime sinceDate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetModifiedSinceAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetWithSchemaChangesSinceAsync(DateTime sinceDate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetWithSchemaChangesSinceAsync - implement using Dapper");
    }

    public Task<IReadOnlyList<MasterIndex>> GetByIdsAsync(IEnumerable<int> indexIds, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetByIdsAsync - implement using Dapper");
    }
}