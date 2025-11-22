using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text.Json;
using Enterprise.Documentation.Core.Application.Services.MasterIndex;

namespace Enterprise.Documentation.Core.Infrastructure.Services.MasterIndex;

public class MasterIndexService : IMasterIndexService
{
    private readonly ILogger<MasterIndexService> _logger;
    private readonly string _connectionString;

    public MasterIndexService(
        ILogger<MasterIndexService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public async Task<int> PopulateIndexAsync(MasterIndexEntry entry, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Populating MasterIndex for DocId: {DocId}", entry.DocId);

        try
        {
            // Parse schema and table from "schema.table" format
            var (schema, tableName) = ParseSchemaAndTable(entry.Table);

            // Generate file hash
            var fileHash = await CalculateFileHashAsync(entry.LocalFilePath);
            var fileSize = new FileInfo(entry.LocalFilePath).Length;

            // Calculate quality scores
            var (qualityScore, completenessScore, metadataCompleteness) = CalculateQualityScores(entry);

            // Parse stored procedures list
            var storedProceduresJson = ParseStoredProcedures(entry.ModifiedStoredProcedures);

            // Build keywords from description and AI tags
            var keywords = BuildKeywords(entry);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                INSERT INTO DaQa.MasterIndex (
                    -- Core Identity
                    SourceSystem, SourceDocumentID, SourceFilePath, DocumentTitle, DocumentType,
                    Description, Status, WorkflowStatus, ApprovalStatus, ApprovedBy, ApprovedDate,

                    -- Database Lineage
                    SystemName, DatabaseName, SchemaName, TableName, ColumnName,

                    -- Change Tracking
                    CABNumber,

                    -- Business Context
                    BusinessOwner, TechnicalOwner,

                    -- Dependencies
                    StoredProcedures,

                    -- AI-Enhanced Metadata
                    AIGeneratedTags, SemanticCategory, Keywords,

                    -- Quality Metrics
                    QualityScore, CompletenessScore, MetadataCompleteness,
                    LastValidated, ValidationStatus,

                    -- File Metadata
                    FileSize, FileHash,

                    -- Versioning
                    VersionNumber, IsLatestVersion,

                    -- Audit Trail
                    CreatedDate, CreatedBy, ModifiedDate, ModifiedBy,
                    IsDeleted, AccessCount,

                    -- Custom Fields (Excel extras)
                    CustomField1, CustomField2, CustomField3
                )
                VALUES (
                    -- Core Identity
                    @SourceSystem, @SourceDocumentID, @SourceFilePath, @DocumentTitle, @DocumentType,
                    @Description, @Status, @WorkflowStatus, @ApprovalStatus, @ApprovedBy, @ApprovedDate,

                    -- Database Lineage
                    @SystemName, @DatabaseName, @SchemaName, @TableName, @ColumnName,

                    -- Change Tracking
                    @CABNumber,

                    -- Business Context
                    @BusinessOwner, @TechnicalOwner,

                    -- Dependencies
                    @StoredProcedures,

                    -- AI-Enhanced Metadata
                    @AIGeneratedTags, @SemanticCategory, @Keywords,

                    -- Quality Metrics
                    @QualityScore, @CompletenessScore, @MetadataCompleteness,
                    @LastValidated, @ValidationStatus,

                    -- File Metadata
                    @FileSize, @FileHash,

                    -- Versioning
                    @VersionNumber, @IsLatestVersion,

                    -- Audit Trail
                    @CreatedDate, @CreatedBy, @ModifiedDate, @ModifiedBy,
                    @IsDeleted, @AccessCount,

                    -- Custom Fields
                    @CustomField1, @CustomField2, @CustomField3
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var indexId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                // Core Identity
                SourceSystem = "ExcelChangeTracker",
                SourceDocumentID = entry.DocId,
                SourceFilePath = entry.LocalFilePath,
                DocumentTitle = entry.DocumentTitle,
                DocumentType = entry.DocumentType,
                Description = entry.EnhancedDescription ?? entry.Description,
                Status = "Published",
                WorkflowStatus = "Approved",
                ApprovalStatus = "Approved",
                ApprovedBy = entry.ApprovedBy,
                ApprovedDate = entry.ApprovedDate,

                // Database Lineage
                SystemName = "IRFS1",
                DatabaseName = "IRFS1",
                SchemaName = schema,
                TableName = tableName,
                ColumnName = entry.Column,

                // Change Tracking
                CABNumber = entry.CABNumber,

                // Business Context
                BusinessOwner = entry.ReportedBy,
                TechnicalOwner = entry.AssignedTo,

                // Dependencies
                StoredProcedures = storedProceduresJson,

                // AI-Enhanced Metadata
                AIGeneratedTags = entry.AIGeneratedTags != null ? JsonSerializer.Serialize(entry.AIGeneratedTags) : null,
                SemanticCategory = entry.SemanticCategory ?? DetermineSemanticCategory(entry.ChangeType),
                Keywords = keywords,

                // Quality Metrics
                QualityScore = qualityScore,
                CompletenessScore = completenessScore,
                MetadataCompleteness = metadataCompleteness,
                LastValidated = DateTime.UtcNow,
                ValidationStatus = "Valid",

                // File Metadata
                FileSize = fileSize,
                FileHash = fileHash,

                // Versioning
                VersionNumber = "1.0",
                IsLatestVersion = true,

                // Audit Trail
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "AutoDraftService",
                ModifiedDate = DateTime.UtcNow,
                ModifiedBy = entry.ApprovedBy,
                IsDeleted = false,
                AccessCount = 0,

                // Custom Fields (Excel extras)
                CustomField1 = entry.Priority,
                CustomField2 = entry.Severity,
                CustomField3 = entry.Sprint
            });

            _logger.LogInformation("MasterIndex entry created with IndexID: {IndexId} for DocId: {DocId}",
                indexId, entry.DocId);

            return indexId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating MasterIndex for DocId: {DocId}", entry.DocId);
            throw;
        }
    }

    public async Task UpdateDocumentationLinkAsync(string docId, string sharePointUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating SharePoint URL for DocId: {DocId}", docId);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                UPDATE DaQa.MasterIndex
                SET GeneratedDocURL = @SharePointUrl,
                    ModifiedDate = @ModifiedDate
                WHERE SourceDocumentID = @DocId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                SharePointUrl = sharePointUrl,
                DocId = docId,
                ModifiedDate = DateTime.UtcNow
            });

            if (rowsAffected == 0)
            {
                _logger.LogWarning("No MasterIndex entry found for DocId: {DocId}", docId);
            }
            else
            {
                _logger.LogInformation("Updated SharePoint URL for DocId: {DocId}", docId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SharePoint URL for DocId: {DocId}", docId);
            throw;
        }
    }

    private (string schema, string tableName) ParseSchemaAndTable(string tableFullName)
    {
        if (string.IsNullOrWhiteSpace(tableFullName))
            return (string.Empty, string.Empty);

        var parts = tableFullName.Split('.');
        if (parts.Length == 2)
            return (parts[0].Trim(), parts[1].Trim());

        // If no schema, assume it's just the table name
        return (string.Empty, tableFullName.Trim());
    }

    private async Task<string> CalculateFileHashAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    private (int qualityScore, int completenessScore, int metadataCompleteness) CalculateQualityScores(MasterIndexEntry entry)
    {
        int totalFields = 0;
        int completedFields = 0;

        // Core required fields (always counted)
        totalFields += 6;
        if (!string.IsNullOrWhiteSpace(entry.DocId)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.DocumentTitle)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.DocumentType)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.Description)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.Documentation)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.CABNumber)) completedFields++;

        // Optional enhanced fields
        totalFields += 8;
        if (!string.IsNullOrWhiteSpace(entry.JiraNumber)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.Column)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.ModifiedStoredProcedures)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.EnhancedDescription)) completedFields++;
        if (entry.AIGeneratedTags?.Any() == true) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.Priority)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.Severity)) completedFields++;
        if (!string.IsNullOrWhiteSpace(entry.Sprint)) completedFields++;

        var completenessScore = (int)Math.Round((double)completedFields / totalFields * 100);

        // Quality score factors in AI enhancement
        var hasAIEnhancement = !string.IsNullOrWhiteSpace(entry.EnhancedDescription) ||
                               (entry.AIGeneratedTags?.Any() == true);
        var qualityScore = completenessScore;
        if (hasAIEnhancement)
            qualityScore = Math.Min(100, qualityScore + 10); // Bonus for AI enhancement

        // Metadata completeness (subset of most important fields)
        int metadataFields = 5;
        int completedMetadata = 0;
        if (!string.IsNullOrWhiteSpace(entry.ReportedBy)) completedMetadata++;
        if (!string.IsNullOrWhiteSpace(entry.AssignedTo)) completedMetadata++;
        if (!string.IsNullOrWhiteSpace(entry.ChangeType)) completedMetadata++;
        if (!string.IsNullOrWhiteSpace(entry.ModifiedStoredProcedures)) completedMetadata++;
        if (entry.AIGeneratedTags?.Any() == true) completedMetadata++;

        var metadataCompleteness = (int)Math.Round((double)completedMetadata / metadataFields * 100);

        return (qualityScore, completenessScore, metadataCompleteness);
    }

    private string? ParseStoredProcedures(string? modifiedStoredProcedures)
    {
        if (string.IsNullOrWhiteSpace(modifiedStoredProcedures))
            return null;

        // Split by comma and create JSON array
        var procedures = modifiedStoredProcedures
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim())
            .ToList();

        return procedures.Any() ? JsonSerializer.Serialize(procedures) : null;
    }

    private string BuildKeywords(MasterIndexEntry entry)
    {
        var keywords = new List<string>();

        // Add from document type
        keywords.Add(entry.DocumentType.ToLower());
        keywords.Add(entry.ChangeType.ToLower());

        // Add from table/column
        var (schema, tableName) = ParseSchemaAndTable(entry.Table);
        if (!string.IsNullOrWhiteSpace(tableName))
            keywords.Add(tableName.ToLower());
        if (!string.IsNullOrWhiteSpace(entry.Column))
            keywords.Add(entry.Column.ToLower());

        // Add from CAB and Jira
        if (!string.IsNullOrWhiteSpace(entry.CABNumber))
            keywords.Add(entry.CABNumber.ToLower());
        if (!string.IsNullOrWhiteSpace(entry.JiraNumber))
            keywords.Add(entry.JiraNumber.ToLower());

        // Add AI-generated tags
        if (entry.AIGeneratedTags?.Any() == true)
            keywords.AddRange(entry.AIGeneratedTags.Select(t => t.ToLower()));

        // Add words from description (simple keyword extraction)
        var descriptionWords = entry.Description
            .Split(new[] { ' ', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3) // Only words longer than 3 chars
            .Select(w => w.ToLower())
            .Distinct()
            .Take(10); // Limit to top 10 words
        keywords.AddRange(descriptionWords);

        return JsonSerializer.Serialize(keywords.Distinct().ToList());
    }

    private string DetermineSemanticCategory(string changeType)
    {
        return changeType switch
        {
            "Business Request" => "New Feature",
            "Enhancement" => "Improvement",
            "Defect Fix" => "Bug Fix",
            "Anomaly" => "Data Quality Issue",
            "EDW-Research" => "Research",
            "EDW-Q&A" => "Question/Answer",
            "Research" => "Investigation",
            _ => "General Change"
        };
    }
}
