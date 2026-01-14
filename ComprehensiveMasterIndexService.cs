// src/Core/Application/Services/MasterIndex/ComprehensiveMasterIndexService.cs

using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Enterprise.Documentation.Core.Application.Services.MasterIndex;

public interface IComprehensiveMasterIndexService
{
    Task<string> PopulateMasterIndexFromApprovedDocumentAsync(
        string docId,
        string filePath,
        string jiraNumber,
        CancellationToken cancellationToken = default);
}

public class ComprehensiveMasterIndexService : IComprehensiveMasterIndexService
{
    private readonly ILogger<ComprehensiveMasterIndexService> _logger;
    private readonly string _connectionString;

    public ComprehensiveMasterIndexService(
        ILogger<ComprehensiveMasterIndexService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
    }

    public async Task<string> PopulateMasterIndexFromApprovedDocumentAsync(
        string docId,
        string filePath,
        string jiraNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive MasterIndex population for {DocId}", docId);

        var indexId = Guid.NewGuid().ToString();
        var metadata = new MasterIndexMetadata { IndexID = indexId };

        // Execute 14 phases
        await Phase1_SourceSystemDataAsync(metadata, docId, filePath, jiraNumber, cancellationToken);
        await Phase2_DocumentAnalysisAsync(metadata, filePath, cancellationToken);
        await Phase3_DatabaseMetadataAsync(metadata, cancellationToken);
        await Phase4_BusinessContextAsync(metadata, filePath, cancellationToken);
        await Phase5_TechnicalDetailsAsync(metadata, filePath, cancellationToken);
        Phase6_OwnershipData(metadata);
        await Phase7_ClassificationAsync(metadata, filePath, cancellationToken);
        await Phase8_RelationshipsAsync(metadata, jiraNumber, cancellationToken);
        Phase9_QualityMetrics(metadata);
        Phase10_UsageTracking(metadata);
        Phase11_LifecycleData(metadata);
        await Phase12_ComplianceAsync(metadata, cancellationToken);
        Phase13_PerformanceStats(metadata);
        Phase14_AuditTrail(metadata);

        // Insert into database
        await InsertMasterIndexAsync(metadata, cancellationToken);

        _logger.LogInformation("MasterIndex population complete: {Completeness}% ({Populated}/116 fields)",
            metadata.CompletenessScore, metadata.PopulatedFieldCount);

        return indexId;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 1: Source System Data
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase1_SourceSystemDataAsync(
        MasterIndexMetadata metadata,
        string docId,
        string filePath,
        string jiraNumber,
        CancellationToken ct)
    {
        metadata.SourceSystem = "DocumentationAutomation";
        metadata.SourceDocumentID = jiraNumber;
        metadata.SourceFilePath = filePath;
        metadata.DocumentTitle = docId;
        metadata.DocumentType = DetermineDocumentType(docId);
        metadata.SourceCreatedDate = File.GetCreationTime(filePath);
        metadata.SourceModifiedDate = File.GetLastWriteTime(filePath);
        metadata.PopulatedFieldCount += 7;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 2: Document Analysis
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase2_DocumentAnalysisAsync(
        MasterIndexMetadata metadata,
        string filePath,
        CancellationToken ct)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            
            if (body != null)
            {
                metadata.DocumentText = body.InnerText;
                metadata.DocumentFormat = "DOCX";
                metadata.DocumentSize = new FileInfo(filePath).Length;
                metadata.PageCount = EstimatePageCount(body.InnerText);
                metadata.WordCount = CountWords(body.InnerText);
                metadata.CharacterCount = body.InnerText.Length;
                metadata.SectionCount = body.Elements<Paragraph>()
                    .Count(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("Heading") == true);
                metadata.TableCount = body.Elements<Table>().Count();
                metadata.ImageCount = body.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Count();
                metadata.EstimatedReadingTimeMinutes = CalculateReadingTime(metadata.WordCount);
                metadata.PopulatedFieldCount += 10;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not analyze document structure for {FilePath}", filePath);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 3: Database Metadata
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase3_DatabaseMetadataAsync(
        MasterIndexMetadata metadata,
        CancellationToken ct)
    {
        // Extract from DocumentChanges
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var dbInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT TableName, ColumnName, SchemaName = 'dbo'
            FROM DaQa.DocumentChanges
            WHERE DocId = @DocId",
            new { DocId = metadata.DocumentTitle });

        if (dbInfo != null)
        {
            metadata.SchemaName = dbInfo.SchemaName ?? "dbo";
            metadata.TableName = dbInfo.TableName;
            metadata.ColumnName = dbInfo.ColumnName;
            metadata.DatabaseName = "IRFS1";
            metadata.PopulatedFieldCount += 4;

            // Get detailed column info if column specified
            if (!string.IsNullOrEmpty(metadata.ColumnName) && !string.IsNullOrEmpty(metadata.TableName))
            {
                var columnInfo = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table AND COLUMN_NAME = @Column",
                    new { Schema = metadata.SchemaName, Table = metadata.TableName, Column = metadata.ColumnName });

                if (columnInfo != null)
                {
                    metadata.DataType = columnInfo.DATA_TYPE;
                    metadata.DataLength = columnInfo.CHARACTER_MAXIMUM_LENGTH;
                    metadata.IsNullable = columnInfo.IS_NULLABLE == "YES";
                    metadata.PopulatedFieldCount += 3;
                }

                // Check if primary key
                var isPK = await connection.QueryFirstOrDefaultAsync<int>(@"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table 
                    AND COLUMN_NAME = @Column AND CONSTRAINT_NAME LIKE 'PK%'",
                    new { Schema = metadata.SchemaName, Table = metadata.TableName, Column = metadata.ColumnName });

                if (isPK > 0)
                {
                    metadata.IsPrimaryKey = true;
                    metadata.PopulatedFieldCount += 1;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 4: Business Context
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase4_BusinessContextAsync(
        MasterIndexMetadata metadata,
        string filePath,
        CancellationToken ct)
    {
        // Extract from document text
        if (!string.IsNullOrEmpty(metadata.DocumentText))
        {
            // Look for Executive Summary section
            var summaryPattern = @"EXECUTIVE SUMMARY\s*(.*?)\s*(?:DATABASE OBJECT DETAILS|$)";
            var match = Regex.Match(metadata.DocumentText, summaryPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                metadata.BusinessDefinition = match.Groups[1].Value.Trim();
                metadata.PopulatedFieldCount += 1;
            }

            // Extract purpose from Database Object Details
            var purposePattern = @"Purpose:\s*(.*?)(?:\n\n|$)";
            var purposeMatch = Regex.Match(metadata.DocumentText, purposePattern, RegexOptions.Singleline);
            if (purposeMatch.Success)
            {
                metadata.BusinessPurpose = purposeMatch.Groups[1].Value.Trim();
                metadata.PopulatedFieldCount += 1;
            }
        }

        metadata.Category = DetermineCategoryFromType(metadata.DocumentType);
        metadata.PopulatedFieldCount += 1;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 5: Technical Details
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase5_TechnicalDetailsAsync(
        MasterIndexMetadata metadata,
        string filePath,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(metadata.DocumentText))
        {
            // Extract code blocks
            var codePattern = @"CODE CHANGES(.*?)(?:Code Explanation:|$)";
            var codeMatch = Regex.Match(metadata.DocumentText, codePattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (codeMatch.Success)
            {
                metadata.TechnicalDefinition = codeMatch.Groups[1].Value.Trim();
                metadata.PopulatedFieldCount += 1;
            }

            // Extract stored procedures mentioned
            var spPattern = @"(usp_|sp_)\w+";
            var spMatches = Regex.Matches(metadata.DocumentText, spPattern);
            if (spMatches.Any())
            {
                metadata.RelatedStoredProcedures = string.Join(", ", spMatches.Select(m => m.Value).Distinct());
                metadata.PopulatedFieldCount += 1;
            }

            // Extract sample queries if present
            var queryPattern = @"USAGE EXAMPLES(.*?)(?:\n\n|$)";
            var queryMatch = Regex.Match(metadata.DocumentText, queryPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (queryMatch.Success)
            {
                metadata.SampleQueries = queryMatch.Groups[1].Value.Trim();
                metadata.PopulatedFieldCount += 1;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 6: Ownership Data
    // ═══════════════════════════════════════════════════════════════════
    private void Phase6_OwnershipData(MasterIndexMetadata metadata)
    {
        metadata.CreatedBy = "DocumentationAutomation";
        metadata.CreatedDate = DateTime.UtcNow;
        metadata.ModifiedBy = "System";
        metadata.ModifiedDate = DateTime.UtcNow;
        metadata.PopulatedFieldCount += 4;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 7: Classification
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase7_ClassificationAsync(
        MasterIndexMetadata metadata,
        string filePath,
        CancellationToken ct)
    {
        var text = metadata.DocumentText?.ToLower() ?? "";

        // Determine data classification
        if (text.Contains("confidential") || text.Contains("restricted"))
            metadata.DataClassification = "Confidential";
        else if (text.Contains("internal"))
            metadata.DataClassification = "Internal";
        else
            metadata.DataClassification = "Public";

        // Determine sensitivity
        var piiKeywords = new[] { "ssn", "social security", "credit card", "password", "salary", "dob", "date of birth" };
        if (piiKeywords.Any(k => text.Contains(k)))
        {
            metadata.SensitivityLevel = "High";
            metadata.PIIFlag = true;
        }
        else if (text.Contains("customer") || text.Contains("employee"))
        {
            metadata.SensitivityLevel = "Medium";
        }
        else
        {
            metadata.SensitivityLevel = "Low";
        }

        metadata.PopulatedFieldCount += 3;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 8: Relationships
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase8_RelationshipsAsync(
        MasterIndexMetadata metadata,
        string jiraNumber,
        CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        // Find related documents by same Jira number
        var related = await connection.QueryAsync<string>(@"
            SELECT DocId
            FROM DaQa.DocumentChanges
            WHERE JiraNumber = @Jira AND DocId != @DocId",
            new { Jira = jiraNumber, DocId = metadata.DocumentTitle });

        if (related.Any())
        {
            metadata.RelatedDocuments = string.Join(",", related);
            metadata.PopulatedFieldCount += 1;
        }

        // Store Jira reference
        metadata.JiraTicket = jiraNumber;
        metadata.PopulatedFieldCount += 1;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 9: Quality Metrics
    // ═══════════════════════════════════════════════════════════════════
    private void Phase9_QualityMetrics(MasterIndexMetadata metadata)
    {
        // Calculate quality score based on completeness
        var hasBusinessDef = !string.IsNullOrEmpty(metadata.BusinessDefinition);
        var hasTechnicalDef = !string.IsNullOrEmpty(metadata.TechnicalDefinition);
        var hasCode = !string.IsNullOrEmpty(metadata.RelatedStoredProcedures);
        var hasClassification = !string.IsNullOrEmpty(metadata.DataClassification);

        var qualityFactors = new[] { hasBusinessDef, hasTechnicalDef, hasCode, hasClassification };
        metadata.QualityScore = (decimal)(qualityFactors.Count(x => x) * 25);
        metadata.DocumentationQuality = metadata.QualityScore >= 75 ? "High" : metadata.QualityScore >= 50 ? "Medium" : "Low";
        metadata.PopulatedFieldCount += 2;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 10: Usage Tracking
    // ═══════════════════════════════════════════════════════════════════
    private void Phase10_UsageTracking(MasterIndexMetadata metadata)
    {
        metadata.UsageCount = 0;
        metadata.LastAccessedDate = null;
        metadata.PopularityScore = 0;
        metadata.PopulatedFieldCount += 3;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 11: Lifecycle Data
    // ═══════════════════════════════════════════════════════════════════
    private void Phase11_LifecycleData(MasterIndexMetadata metadata)
    {
        metadata.ApprovalStatus = "Approved";
        metadata.LifecycleStage = "Active";
        metadata.IsActive = true;
        metadata.IsCertified = false;
        metadata.Status = "Active";
        metadata.PopulatedFieldCount += 5;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 12: Compliance
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase12_ComplianceAsync(
        MasterIndexMetadata metadata,
        CancellationToken ct)
    {
        // Set retention based on classification
        metadata.RetentionPeriod = metadata.DataClassification switch
        {
            "Confidential" => "7 years",
            "Internal" => "5 years",
            _ => "3 years"
        };

        // Add compliance tags
        var tags = new List<string> { metadata.DocumentType };
        if (metadata.PIIFlag == true)
            tags.Add("PII");
        if (metadata.SensitivityLevel == "High")
            tags.Add("HighSensitivity");

        metadata.ComplianceTags = string.Join(",", tags);
        metadata.PopulatedFieldCount += 2;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 13: Performance Stats
    // ═══════════════════════════════════════════════════════════════════
    private void Phase13_PerformanceStats(MasterIndexMetadata metadata)
    {
        metadata.CompletenessScore = (int)Math.Round((metadata.PopulatedFieldCount / 116.0) * 100);
        metadata.MetadataCompleteness = metadata.CompletenessScore;
        metadata.LastReviewedDate = DateTime.UtcNow;
        metadata.PopulatedFieldCount += 3;
    }

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 14: Audit Trail
    // ═══════════════════════════════════════════════════════════════════
    private void Phase14_AuditTrail(MasterIndexMetadata metadata)
    {
        metadata.DocumentVersion = "1.0";
        metadata.ChangeReason = "Initial approval and metadata population";
        metadata.AuditTrail = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Document approved and indexed";
        metadata.PopulatedFieldCount += 3;
    }

    // ═══════════════════════════════════════════════════════════════════
    // DATABASE INSERT
    // ═══════════════════════════════════════════════════════════════════
    private async Task InsertMasterIndexAsync(MasterIndexMetadata metadata, CancellationToken ct)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            INSERT INTO DaQa.MasterIndex (
                IndexID, SourceSystem, SourceDocumentID, SourceFilePath, SourceCreatedDate, SourceModifiedDate,
                DocumentTitle, DocumentType, DocumentFormat, DocumentSize, DocumentText,
                PageCount, WordCount, CharacterCount, SectionCount, TableCount, ImageCount,
                EstimatedReadingTimeMinutes, SchemaName, DatabaseName, TableName, ColumnName,
                DataType, DataLength, IsNullable, IsPrimaryKey, BusinessDefinition, BusinessPurpose,
                Category, TechnicalDefinition, RelatedStoredProcedures, SampleQueries,
                CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, DataClassification,
                SensitivityLevel, PIIFlag, RelatedDocuments, JiraTicket, QualityScore,
                DocumentationQuality, UsageCount, LastAccessedDate, PopularityScore,
                ApprovalStatus, LifecycleStage, IsActive, IsCertified, Status,
                RetentionPeriod, ComplianceTags, CompletenessScore, MetadataCompleteness,
                LastReviewedDate, DocumentVersion, ChangeReason, AuditTrail
            ) VALUES (
                @IndexID, @SourceSystem, @SourceDocumentID, @SourceFilePath, @SourceCreatedDate, @SourceModifiedDate,
                @DocumentTitle, @DocumentType, @DocumentFormat, @DocumentSize, @DocumentText,
                @PageCount, @WordCount, @CharacterCount, @SectionCount, @TableCount, @ImageCount,
                @EstimatedReadingTimeMinutes, @SchemaName, @DatabaseName, @TableName, @ColumnName,
                @DataType, @DataLength, @IsNullable, @IsPrimaryKey, @BusinessDefinition, @BusinessPurpose,
                @Category, @TechnicalDefinition, @RelatedStoredProcedures, @SampleQueries,
                @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate, @DataClassification,
                @SensitivityLevel, @PIIFlag, @RelatedDocuments, @JiraTicket, @QualityScore,
                @DocumentationQuality, @UsageCount, @LastAccessedDate, @PopularityScore,
                @ApprovalStatus, @LifecycleStage, @IsActive, @IsCertified, @Status,
                @RetentionPeriod, @ComplianceTags, @CompletenessScore, @MetadataCompleteness,
                @LastReviewedDate, @DocumentVersion, @ChangeReason, @AuditTrail
            )";

        await connection.ExecuteAsync(sql, metadata);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════
    private string DetermineDocumentType(string docId)
    {
        if (docId.StartsWith("BR-")) return "BusinessRequest";
        if (docId.StartsWith("EN-")) return "Enhancement";
        if (docId.StartsWith("DF-")) return "DefectFix";
        if (docId.StartsWith("SP-")) return "StoredProcedure";
        return "Unknown";
    }

    private string DetermineCategoryFromType(string documentType)
    {
        return documentType switch
        {
            "BusinessRequest" => "Business Logic",
            "Enhancement" => "System Improvement",
            "DefectFix" => "Bug Fix",
            "StoredProcedure" => "Database Object",
            _ => "General"
        };
    }

    private int EstimatePageCount(string text)
    {
        const int wordsPerPage = 500;
        var wordCount = CountWords(text);
        return Math.Max(1, (int)Math.Ceiling(wordCount / (double)wordsPerPage));
    }

    private int CountWords(string text)
    {
        return text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private int CalculateReadingTime(int wordCount)
    {
        const int wordsPerMinute = 200;
        return Math.Max(1, (int)Math.Ceiling(wordCount / (double)wordsPerMinute));
    }
}

// ═══════════════════════════════════════════════════════════════════
// MODEL
// ═══════════════════════════════════════════════════════════════════
public class MasterIndexMetadata
{
    // Identity (Phase 1)
    public string IndexID { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string? SourceDocumentID { get; set; }
    public string? SourceFilePath { get; set; }
    public DateTime? SourceCreatedDate { get; set; }
    public DateTime? SourceModifiedDate { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentType { get; set; }

    // Document Analysis (Phase 2)
    public string? DocumentFormat { get; set; }
    public long? DocumentSize { get; set; }
    public string? DocumentText { get; set; }
    public int? PageCount { get; set; }
    public int? WordCount { get; set; }
    public int? CharacterCount { get; set; }
    public int? SectionCount { get; set; }
    public int? TableCount { get; set; }
    public int? ImageCount { get; set; }
    public int? EstimatedReadingTimeMinutes { get; set; }

    // Database Metadata (Phase 3)
    public string? SchemaName { get; set; }
    public string? DatabaseName { get; set; }
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public string? DataType { get; set; }
    public int? DataLength { get; set; }
    public bool? IsNullable { get; set; }
    public bool? IsPrimaryKey { get; set; }

    // Business Context (Phase 4)
    public string? BusinessDefinition { get; set; }
    public string? BusinessPurpose { get; set; }
    public string? Category { get; set; }

    // Technical Details (Phase 5)
    public string? TechnicalDefinition { get; set; }
    public string? RelatedStoredProcedures { get; set; }
    public string? SampleQueries { get; set; }

    // Ownership (Phase 6)
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    // Classification (Phase 7)
    public string? DataClassification { get; set; }
    public string? SensitivityLevel { get; set; }
    public bool? PIIFlag { get; set; }

    // Relationships (Phase 8)
    public string? RelatedDocuments { get; set; }
    public string? JiraTicket { get; set; }

    // Quality Metrics (Phase 9)
    public decimal? QualityScore { get; set; }
    public string? DocumentationQuality { get; set; }

    // Usage Tracking (Phase 10)
    public long? UsageCount { get; set; }
    public DateTime? LastAccessedDate { get; set; }
    public decimal? PopularityScore { get; set; }

    // Lifecycle (Phase 11)
    public string? ApprovalStatus { get; set; }
    public string? LifecycleStage { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsCertified { get; set; }
    public string? Status { get; set; }

    // Compliance (Phase 12)
    public string? RetentionPeriod { get; set; }
    public string? ComplianceTags { get; set; }

    // Performance Stats (Phase 13)
    public int? CompletenessScore { get; set; }
    public int? MetadataCompleteness { get; set; }
    public DateTime? LastReviewedDate { get; set; }

    // Audit Trail (Phase 14)
    public string? DocumentVersion { get; set; }
    public string? ChangeReason { get; set; }
    public string? AuditTrail { get; set; }

    // Tracking
    public int PopulatedFieldCount { get; set; } = 0;
}
