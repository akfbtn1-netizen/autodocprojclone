// =============================================================================
// Agent #5: Post-Approval Pipeline - MasterIndex Population Service
// Populates the 115-column MasterIndex table with approved document metadata
// =============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Enterprise.Documentation.Core.Application.Services.PostApproval;

/// <summary>
/// Populates the 115-column MasterIndex table with approved document metadata.
/// Only called after document approval, never during draft creation.
/// </summary>
public class MasterIndexPopulationService : IMasterIndexPopulationService
{
    private readonly ILogger<MasterIndexPopulationService> _logger;
    private readonly string _connectionString;

    public MasterIndexPopulationService(
        ILogger<MasterIndexPopulationService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<PopulationResult> PopulateAsync(
        int approvalId,
        FinalizedMetadata metadata,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Populating MasterIndex for approval {ApprovalId}, document {DocId}",
            approvalId, metadata.DocumentId);

        var result = new PopulationResult();

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Check if entry already exists for this object
            var existingId = await connection.QuerySingleOrDefaultAsync<int?>(@"
                SELECT IndexId FROM DaQa.MasterIndex
                WHERE SchemaName = @Schema AND ObjectName = @Object AND IsActive = 1",
                new { Schema = metadata.SchemaName, Object = metadata.ObjectName });

            if (existingId.HasValue)
            {
                // Update existing entry
                result = await UpdateAsync(existingId.Value, metadata, ct);
                result.IsUpdate = true;
            }
            else
            {
                // Insert new entry
                var masterIndexId = await InsertMasterIndexAsync(connection, metadata, ct);
                result.MasterIndexId = masterIndexId;
                result.Success = true;
                result.ColumnsPopulated = await CountPopulatedColumns(connection, masterIndexId);
            }

            // Update DocumentApprovals with MasterIndex link
            await connection.ExecuteAsync(@"
                UPDATE DaQa.DocumentApprovals
                SET MasterIndexPopulatedAt = GETUTCDATE(),
                    FinalizedMetadata = @Metadata
                WHERE ApprovalID = @ApprovalId",
                new { ApprovalId = approvalId, Metadata = JsonSerializer.Serialize(metadata) });

            // Update Shadow Metadata table
            await UpdateShadowMetadataAsync(connection, metadata);

            _logger.LogInformation("MasterIndex populated: ID={MasterIndexId}, Columns={Columns}",
                result.MasterIndexId, result.ColumnsPopulated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to populate MasterIndex for {DocId}", metadata.DocumentId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<PopulationResult> UpdateAsync(
        int masterIndexId,
        FinalizedMetadata metadata,
        CancellationToken ct = default)
    {
        var result = new PopulationResult { MasterIndexId = masterIndexId, IsUpdate = true };

        try
        {
            using var connection = new SqlConnection(_connectionString);

            await connection.ExecuteAsync(@"
                UPDATE DaQa.MasterIndex SET
                    -- Document fields
                    DocumentId = @DocumentId,
                    DocumentPath = @DocumentPath,
                    DocumentType = @DocumentType,

                    -- AI-enriched fields
                    Description = @Description,
                    Purpose = @Purpose,
                    BusinessDomain = @BusinessDomain,
                    Category = @Category,
                    Tags = @Tags,
                    SemanticCategory = @SemanticCategory,

                    -- Classification
                    DataClassification = @DataClassification,
                    ContainsPII = @ContainsPII,
                    PIITypes = @PIITypes,
                    ComplianceCategory = @ComplianceCategory,

                    -- Technical
                    ComplexityScore = @ComplexityScore,
                    HasDynamicSql = @HasDynamicSql,
                    HasCursors = @HasCursors,
                    HasTransactions = @HasTransactions,
                    HasErrorHandling = @HasErrorHandling,

                    -- Change tracking
                    JiraNumber = @JiraNumber,
                    CABNumber = @CABNumber,
                    BracketedCode = @BracketedCode,

                    -- Approval
                    ApprovalStatus = 'Approved',
                    ApprovedBy = @ApprovedBy,
                    ApprovedAt = @ApprovedAt,
                    ApproverComments = @ApproverComments,

                    -- Embedding
                    SemanticEmbedding = @SemanticEmbedding,

                    -- Cost tracking
                    TokensUsed = @TokensUsed,
                    GenerationCostUSD = @GenerationCost,
                    AIModel = @AIModel,

                    -- Timestamps
                    ModifiedDate = GETUTCDATE(),
                    LastValidated = GETUTCDATE()
                WHERE IndexId = @MasterIndexId",
                BuildUpdateParameters(masterIndexId, metadata));

            result.Success = true;
            result.ColumnsPopulated = await CountPopulatedColumns(connection, masterIndexId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update MasterIndex {Id}", masterIndexId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task LinkDocumentAsync(
        int masterIndexId,
        string documentId,
        string documentPath,
        CancellationToken ct = default)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(@"
            UPDATE DaQa.MasterIndex
            SET DocumentId = @DocumentId,
                DocumentPath = @DocumentPath,
                ModifiedDate = GETUTCDATE()
            WHERE IndexId = @MasterIndexId",
            new { MasterIndexId = masterIndexId, DocumentId = documentId, DocumentPath = documentPath });
    }

    #region Private Methods

    private async Task<int> InsertMasterIndexAsync(
        SqlConnection connection,
        FinalizedMetadata metadata,
        CancellationToken ct)
    {
        // Insert with key columns - subset of 115 total columns
        // TODO [5]: Add remaining columns as MasterIndex schema evolves
        var sql = @"
            INSERT INTO DaQa.MasterIndex (
                -- Identity
                DatabaseName, SchemaName, ObjectName, ObjectType, ColumnName,

                -- Document
                DocumentId, DocumentPath, DocumentType,

                -- Descriptions
                Description, Purpose, TechnicalNotes,

                -- Classification
                BusinessDomain, Category, Tags, SemanticCategory,
                DataClassification, ContainsPII, PIITypes, ComplianceCategory,

                -- Technical
                ComplexityScore, TechnicalComplexity,
                HasDynamicSql, HasCursors, HasTransactions, HasErrorHandling,

                -- Parameters (JSON)
                ParametersJson, TablesAccessedJson, ColumnsModifiedJson,

                -- Change tracking
                JiraNumber, CABNumber, ChangeDescription, BracketedCode,

                -- Approval
                ApprovalStatus, ApprovedBy, ApprovedAt, ApproverComments,

                -- Embedding
                SemanticEmbedding,

                -- Cost
                TokensUsed, GenerationCostUSD, AIModel,

                -- Status
                IsActive, IsGenerated, WorkflowStatus,

                -- Timestamps
                CreatedDate, ModifiedDate, LastValidated
            )
            OUTPUT INSERTED.IndexId
            VALUES (
                @DatabaseName, @SchemaName, @ObjectName, @ObjectType, @ColumnName,
                @DocumentId, @DocumentPath, @DocumentType,
                @Description, @Purpose, @TechnicalNotes,
                @BusinessDomain, @Category, @Tags, @SemanticCategory,
                @DataClassification, @ContainsPII, @PIITypes, @ComplianceCategory,
                @ComplexityScore, @TechnicalComplexity,
                @HasDynamicSql, @HasCursors, @HasTransactions, @HasErrorHandling,
                @ParametersJson, @TablesAccessedJson, @ColumnsModifiedJson,
                @JiraNumber, @CABNumber, @ChangeDescription, @BracketedCode,
                'Approved', @ApprovedBy, @ApprovedAt, @ApproverComments,
                @SemanticEmbedding,
                @TokensUsed, @GenerationCost, @AIModel,
                1, 1, 'Approved',
                GETUTCDATE(), GETUTCDATE(), GETUTCDATE()
            )";

        return await connection.QuerySingleAsync<int>(sql, BuildInsertParameters(metadata));
    }

    private object BuildInsertParameters(FinalizedMetadata m)
    {
        return new
        {
            DatabaseName = "IRFS1",
            m.SchemaName,
            m.ObjectName,
            m.ObjectType,
            m.ColumnName,
            m.DocumentId,
            DocumentPath = $@"C:\Temp\Documentation-Catalog\{m.SchemaName}\{m.ObjectType}s\{m.DocumentId}.docx",
            m.DocumentType,
            m.Description,
            m.Purpose,
            TechnicalNotes = m.ChangeDescription,
            BusinessDomain = m.Classification?.BusinessDomain ?? "General",
            Category = m.ObjectType,
            Tags = m.Classification?.DomainTags != null ? string.Join(",", m.Classification.DomainTags) : null,
            SemanticCategory = m.Classification?.SemanticCategory,
            DataClassification = m.Classification?.DataClassification ?? "Internal",
            ContainsPII = m.Classification?.ContainsPII ?? false,
            PIITypes = m.Classification?.PIITypes != null ? string.Join(",", m.Classification.PIITypes) : null,
            ComplianceCategory = m.Classification?.ComplianceCategory,
            m.ComplexityScore,
            TechnicalComplexity = m.ComplexityTier,
            m.HasDynamicSql,
            m.HasCursors,
            m.HasTransactions,
            m.HasErrorHandling,
            ParametersJson = m.Parameters.Any() ? JsonSerializer.Serialize(m.Parameters) : null,
            TablesAccessedJson = m.TablesAccessed.Any() ? JsonSerializer.Serialize(m.TablesAccessed) : null,
            ColumnsModifiedJson = m.ColumnsModified.Any() ? JsonSerializer.Serialize(m.ColumnsModified) : null,
            m.JiraNumber,
            m.CABNumber,
            m.ChangeDescription,
            m.BracketedCode,
            m.ApprovedBy,
            m.ApprovedAt,
            ApproverComments = m.ApproverComments,
            SemanticEmbedding = m.SemanticEmbedding != null
                ? ConvertEmbeddingToBytes(m.SemanticEmbedding) : null,
            m.TokensUsed,
            GenerationCost = m.GenerationCostUSD,
            AIModel = m.AIModel ?? "gpt-4"
        };
    }

    private object BuildUpdateParameters(int masterIndexId, FinalizedMetadata m)
    {
        return new
        {
            MasterIndexId = masterIndexId,
            m.DocumentId,
            DocumentPath = $@"C:\Temp\Documentation-Catalog\{m.SchemaName}\{m.ObjectType}s\{m.DocumentId}.docx",
            m.DocumentType,
            m.Description,
            m.Purpose,
            BusinessDomain = m.Classification?.BusinessDomain ?? "General",
            Category = m.ObjectType,
            Tags = m.Classification?.DomainTags != null ? string.Join(",", m.Classification.DomainTags) : null,
            SemanticCategory = m.Classification?.SemanticCategory,
            DataClassification = m.Classification?.DataClassification ?? "Internal",
            ContainsPII = m.Classification?.ContainsPII ?? false,
            PIITypes = m.Classification?.PIITypes != null ? string.Join(",", m.Classification.PIITypes) : null,
            ComplianceCategory = m.Classification?.ComplianceCategory,
            m.ComplexityScore,
            m.HasDynamicSql,
            m.HasCursors,
            m.HasTransactions,
            m.HasErrorHandling,
            m.JiraNumber,
            m.CABNumber,
            m.BracketedCode,
            m.ApprovedBy,
            m.ApprovedAt,
            ApproverComments = m.ApproverComments,
            SemanticEmbedding = m.SemanticEmbedding != null
                ? ConvertEmbeddingToBytes(m.SemanticEmbedding) : null,
            m.TokensUsed,
            GenerationCost = m.GenerationCostUSD,
            AIModel = m.AIModel ?? "gpt-4"
        };
    }

    private byte[] ConvertEmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private async Task<int> CountPopulatedColumns(SqlConnection connection, int masterIndexId)
    {
        // Count non-null columns
        var result = await connection.QuerySingleAsync<dynamic>(@"
            SELECT
                CASE WHEN DocumentId IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN Description IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN BusinessDomain IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN SemanticEmbedding IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN ApprovedBy IS NOT NULL THEN 1 ELSE 0 END +
                15 AS PopulatedCount -- Base fields always populated
            FROM DaQa.MasterIndex WHERE IndexId = @Id",
            new { Id = masterIndexId });

        return (int)result.PopulatedCount;
    }

    private async Task UpdateShadowMetadataAsync(SqlConnection connection, FinalizedMetadata metadata)
    {
        await connection.ExecuteAsync(@"
            MERGE DaQa.DocumentShadowMetadata AS target
            USING (SELECT @DocumentId AS DocumentId) AS source
            ON target.DocumentId = source.DocumentId
            WHEN MATCHED THEN UPDATE SET
                SyncStatus = 'CURRENT',
                ContentHash = @ContentHash,
                MasterIndexId = @MasterIndexId,
                LastModified = GETUTCDATE(),
                LastSynced = GETUTCDATE(),
                TokensUsed = @TokensUsed,
                GenerationCostUSD = @GenerationCost,
                AIModel = @AIModel,
                ApprovedAt = @ApprovedAt,
                ApprovedBy = @ApprovedBy
            WHEN NOT MATCHED THEN INSERT (
                DocumentId, FilePath, SyncStatus, ContentHash, MasterIndexId,
                LastModified, LastSynced, TokensUsed, GenerationCostUSD, AIModel,
                GeneratedAt, ApprovedAt, ApprovedBy
            ) VALUES (
                @DocumentId, @FilePath, 'CURRENT', @ContentHash, @MasterIndexId,
                GETUTCDATE(), GETUTCDATE(), @TokensUsed, @GenerationCost, @AIModel,
                @GeneratedAt, @ApprovedAt, @ApprovedBy
            );",
            new
            {
                metadata.DocumentId,
                FilePath = $@"C:\Temp\Documentation-Catalog\{metadata.SchemaName}\{metadata.ObjectType}s\{metadata.DocumentId}.docx",
                ContentHash = metadata.ContentHash ?? "",
                metadata.MasterIndexId,
                metadata.TokensUsed,
                GenerationCost = metadata.GenerationCostUSD,
                AIModel = metadata.AIModel ?? "gpt-4",
                GeneratedAt = metadata.ExtractedAt,
                metadata.ApprovedAt,
                metadata.ApprovedBy
            });
    }

    #endregion
}
