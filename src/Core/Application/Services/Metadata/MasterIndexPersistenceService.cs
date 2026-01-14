using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.Json;

namespace Enterprise.Documentation.Core.Application.Services.Metadata;

public interface IMasterIndexPersistenceService
{
    Task<int> SaveMetadataAsync(
        MasterIndexMetadata metadata,
        CancellationToken ct = default);
    
    Task<bool> UpdateDocumentPathAsync(
        string docId,
        string generatedDocPath,
        string generatedDocUrl,
        CancellationToken ct = default);
}

public class MasterIndexPersistenceService : IMasterIndexPersistenceService
{
    private readonly ILogger<MasterIndexPersistenceService> _logger;
    private readonly string _connectionString;

    public MasterIndexPersistenceService(
        ILogger<MasterIndexPersistenceService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public async Task<int> SaveMetadataAsync(
        MasterIndexMetadata metadata,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Saving metadata to MasterIndex for DocId: {DocId}", metadata.DocId);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Check if record exists
            var existingId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT IndexID FROM DaQa.MasterIndex WHERE DocId = @DocId",
                new { metadata.DocId });

            int indexId;
            
            if (existingId.HasValue)
            {
                // Update existing record
                indexId = existingId.Value;
                await UpdateMetadataAsync(connection, indexId, metadata);
                _logger.LogInformation("Updated existing MasterIndex record {IndexId}", indexId);
            }
            else
            {
                // Insert new record
                indexId = await InsertMetadataAsync(connection, metadata);
                _logger.LogInformation("Inserted new MasterIndex record {IndexId}", indexId);
            }

            return indexId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save metadata for DocId: {DocId}", metadata.DocId);
            throw;
        }
    }

    private async Task<int> InsertMetadataAsync(SqlConnection connection, MasterIndexMetadata metadata)
    {
        const string sql = @"
            INSERT INTO DaQa.MasterIndex (
                SourceSystem,
                DocId,
                DocumentTitle,
                DocumentType,
                Description,
                BusinessDomain,
                BusinessProcess,
                SystemName,
                DatabaseName,
                SchemaName,
                TableName,
                ColumnName,
                VersionNumber,
                IsLatestVersion,
                Status,
                Keywords,
                Tags,
                RelatedTables,
                Dependencies,
                StoredProcedures,
                AIGeneratedTags,
                SemanticCategory,
                MetadataCompleteness,
                TechnicalComplexity,
                CriticalityLevel,
                CABNumber,
                BusinessDefinition,
                TechnicalDefinition,
                CreatedDate,
                CreatedBy,
                ModifiedDate,
                ModifiedBy
            )
            OUTPUT INSERTED.IndexID
            VALUES (
                'DocumentationPlatformV2',
                @DocId,
                @DocumentTitle,
                @DocumentType,
                @Description,
                @BusinessDomain,
                @BusinessProcess,
                @SystemName,
                @DatabaseName,
                @SchemaName,
                @TableName,
                @ColumnName,
                @VersionNumber,
                1, -- IsLatestVersion
                'Active',
                @Keywords,
                @Tags,
                @RelatedTables,
                @Dependencies,
                @StoredProcedures,
                @AIGeneratedTags,
                @SemanticCategory,
                @MetadataCompleteness,
                @TechnicalComplexity,
                @CriticalityLevel,
                @CABNumber,
                @BusinessDefinition,
                @TechnicalDefinition,
                GETDATE(),
                @CreatedBy,
                GETDATE(),
                @ModifiedBy
            )";

        var indexId = await connection.QuerySingleAsync<int>(sql, new
        {
            metadata.DocId,
            DocumentTitle = $"{metadata.SchemaName}.{metadata.ObjectName} - {metadata.JiraNumber}",
            metadata.DocumentType,
            Description = metadata.Purpose,
            BusinessDomain = metadata.DomainTags != null && metadata.DomainTags.Any() 
                ? metadata.DomainTags[0] : null,
            metadata.BusinessProcess,
            SystemName = "IRFS1",
            metadata.DatabaseName,
            metadata.SchemaName,
            metadata.TableName,
            metadata.ColumnName,
            VersionNumber = metadata.Version,
            Keywords = metadata.Keywords != null 
                ? string.Join(", ", metadata.Keywords.Take(50)) : null, // Limit to avoid overflow
            Tags = metadata.DomainTags != null 
                ? JsonSerializer.Serialize(metadata.DomainTags) : null,
            RelatedTables = metadata.DependentTables != null 
                ? JsonSerializer.Serialize(metadata.DependentTables) : null,
            Dependencies = metadata.DependentProcedures != null 
                ? JsonSerializer.Serialize(metadata.DependentProcedures) : null,
            StoredProcedures = $"{metadata.SchemaName}.{metadata.ObjectName}",
            AIGeneratedTags = metadata.DomainTags != null 
                ? string.Join(", ", metadata.DomainTags) : null,
            SemanticCategory = metadata.BusinessProcess,
            MetadataCompleteness = metadata.ConfidenceScore,
            TechnicalComplexity = metadata.ComplexityLevel,
            CriticalityLevel = metadata.BusinessImpactLevel,
            CABNumber = metadata.JiraNumber,
            BusinessDefinition = metadata.BusinessImpact,
            TechnicalDefinition = metadata.TechnicalSummary,
            CreatedBy = metadata.Author,
            ModifiedBy = metadata.Author
        });

        return indexId;
    }

    private async Task UpdateMetadataAsync(SqlConnection connection, int indexId, MasterIndexMetadata metadata)
    {
        const string sql = @"
            UPDATE DaQa.MasterIndex
            SET
                DocumentTitle = @DocumentTitle,
                DocumentType = @DocumentType,
                Description = @Description,
                BusinessDomain = @BusinessDomain,
                BusinessProcess = @BusinessProcess,
                SchemaName = @SchemaName,
                TableName = @TableName,
                ColumnName = @ColumnName,
                VersionNumber = @VersionNumber,
                Keywords = @Keywords,
                Tags = @Tags,
                RelatedTables = @RelatedTables,
                Dependencies = @Dependencies,
                StoredProcedures = @StoredProcedures,
                AIGeneratedTags = @AIGeneratedTags,
                SemanticCategory = @SemanticCategory,
                MetadataCompleteness = @MetadataCompleteness,
                TechnicalComplexity = @TechnicalComplexity,
                CriticalityLevel = @CriticalityLevel,
                BusinessDefinition = @BusinessDefinition,
                TechnicalDefinition = @TechnicalDefinition,
                ModifiedDate = GETDATE(),
                ModifiedBy = @ModifiedBy
            WHERE IndexID = @IndexId";

        await connection.ExecuteAsync(sql, new
        {
            IndexId = indexId,
            DocumentTitle = $"{metadata.SchemaName}.{metadata.ObjectName} - {metadata.JiraNumber}",
            metadata.DocumentType,
            Description = metadata.Purpose,
            BusinessDomain = metadata.DomainTags != null && metadata.DomainTags.Any() 
                ? metadata.DomainTags[0] : null,
            metadata.BusinessProcess,
            metadata.SchemaName,
            metadata.TableName,
            metadata.ColumnName,
            VersionNumber = metadata.Version,
            Keywords = metadata.Keywords != null 
                ? string.Join(", ", metadata.Keywords.Take(50)) : null,
            Tags = metadata.DomainTags != null 
                ? JsonSerializer.Serialize(metadata.DomainTags) : null,
            RelatedTables = metadata.DependentTables != null 
                ? JsonSerializer.Serialize(metadata.DependentTables) : null,
            Dependencies = metadata.DependentProcedures != null 
                ? JsonSerializer.Serialize(metadata.DependentProcedures) : null,
            StoredProcedures = $"{metadata.SchemaName}.{metadata.ObjectName}",
            AIGeneratedTags = metadata.DomainTags != null 
                ? string.Join(", ", metadata.DomainTags) : null,
            SemanticCategory = metadata.BusinessProcess,
            MetadataCompleteness = metadata.ConfidenceScore,
            TechnicalComplexity = metadata.ComplexityLevel,
            CriticalityLevel = metadata.BusinessImpactLevel,
            BusinessDefinition = metadata.BusinessImpact,
            TechnicalDefinition = metadata.TechnicalSummary,
            ModifiedBy = metadata.Author
        });
    }

    public async Task<bool> UpdateDocumentPathAsync(
        string docId,
        string generatedDocPath,
        string generatedDocUrl,
        CancellationToken ct = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                UPDATE DaQa.MasterIndex
                SET
                    GeneratedDocPath = @GeneratedDocPath,
                    GeneratedDocURL = @GeneratedDocURL,
                    ModifiedDate = GETDATE()
                WHERE DocId = @DocId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                DocId = docId,
                GeneratedDocPath = generatedDocPath,
                GeneratedDocURL = generatedDocUrl
            });

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update document path for DocId: {DocId}", docId);
            return false;
        }
    }
}
