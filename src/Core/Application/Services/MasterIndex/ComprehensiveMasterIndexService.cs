// src/Core/Application/Services/MasterIndex/ComprehensiveMasterIndexService.cs

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net.Http.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Enterprise.Documentation.Core.Application.Services.AI;

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
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string? _openAIApiKey;
    private readonly string? _openAIEndpoint;
    private readonly string? _openAIModel;
    private readonly IMetadataAIService _aiService;

    public ComprehensiveMasterIndexService(
        ILogger<ComprehensiveMasterIndexService> logger,
        IConfiguration configuration,
        HttpClient httpClient,
        IMetadataAIService aiService)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
        _aiService = aiService;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection required");
        
        // Optional OpenAI configuration for metadata inference
        _openAIApiKey = configuration["AzureOpenAI:ApiKey"];
        _openAIEndpoint = configuration["AzureOpenAI:Endpoint"];
        _openAIModel = configuration["AzureOpenAI:Model"] ?? "gpt-4.1";
        
        if (!string.IsNullOrEmpty(_openAIApiKey))
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", _openAIApiKey);
        }
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

        // Phase 15: AI-powered metadata inference (if enabled)
        await Phase15_AIInferenceAsync(metadata, docId, jiraNumber, cancellationToken);
        
        // Phase 16: AI-powered metadata enrichment (NEW)
        await Phase16_AIEnrichmentAsync(metadata, cancellationToken);

        // Insert into database with AI-inferred metadata
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
            // File-level metadata
            var fileInfo = new FileInfo(filePath);
            metadata.DocumentSize = fileInfo.Length;
            metadata.FileSize = fileInfo.Length;
            metadata.FileHash = CalculateFileHash(filePath);
            
            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            
            if (body != null)
            {
                metadata.DocumentText = body.InnerText;
                metadata.DocumentFormat = "DOCX";
                metadata.PageCount = EstimatePageCount(body.InnerText);
                metadata.WordCount = CountWords(body.InnerText);
                metadata.CharacterCount = body.InnerText.Length;
                metadata.SectionCount = body.Elements<Paragraph>()
                    .Count(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("Heading") == true);
                metadata.TableCount = body.Elements<Table>().Count();
                metadata.ImageCount = body.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Count();
                metadata.EstimatedReadingTimeMinutes = CalculateReadingTime(metadata.WordCount ?? 0);
                metadata.PopulatedFieldCount += 12;
            }
            else
            {
                metadata.PopulatedFieldCount += 3; // FileSize, FileHash, DocumentSize
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not analyze document structure for {FilePath}", filePath);
            // Still populate basic file metadata if document parsing fails
            var fileInfo = new FileInfo(filePath);
            metadata.DocumentSize = fileInfo.Length;
            metadata.FileSize = fileInfo.Length;
            metadata.FileHash = CalculateFileHash(filePath);
            metadata.PopulatedFieldCount += 3;
        }
    }

    // Calculate SHA256 hash of the file for integrity verification
    private string CalculateFileHash(string filePath)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLower();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not calculate hash for file {FilePath}", filePath);
            return string.Empty;
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

        metadata.Category = DetermineCategoryFromType(metadata.DocumentType ?? "Unknown");
        metadata.BusinessDomain = DetermineBusinessDomain(metadata.DocumentText, metadata.DocumentType);
        metadata.PopulatedFieldCount += 2;
    }

    // Business Domain mapping based on document analysis
    private string DetermineBusinessDomain(string? documentText, string? documentType)
    {
        // Default mappings based on document type
        var typeBasedDomain = documentType switch
        {
            "TechnicalDesign" => "Technology",
            "BusinessRequest" => "Business",
            "DatabaseChange" => "Data Management",
            "Procedure" => "Operations",
            _ => "General"
        };

        // Content-based domain detection
        if (!string.IsNullOrEmpty(documentText))
        {
            var lowerText = documentText.ToLower();
            
            // Finance domain indicators
            if (lowerText.Contains("financial") || lowerText.Contains("accounting") || 
                lowerText.Contains("budget") || lowerText.Contains("cost") ||
                lowerText.Contains("payment") || lowerText.Contains("invoice"))
                return "Finance";
                
            // HR domain indicators  
            if (lowerText.Contains("employee") || lowerText.Contains("payroll") ||
                lowerText.Contains("human resources") || lowerText.Contains("benefits") ||
                lowerText.Contains("performance") || lowerText.Contains("training"))
                return "Human Resources";
                
            // Operations domain indicators
            if (lowerText.Contains("workflow") || lowerText.Contains("process") ||
                lowerText.Contains("procedure") || lowerText.Contains("operations") ||
                lowerText.Contains("manufacturing") || lowerText.Contains("production"))
                return "Operations";
                
            // Sales domain indicators
            if (lowerText.Contains("sales") || lowerText.Contains("customer") ||
                lowerText.Contains("revenue") || lowerText.Contains("marketing") ||
                lowerText.Contains("campaign") || lowerText.Contains("lead"))
                return "Sales & Marketing";
                
            // IT domain indicators
            if (lowerText.Contains("database") || lowerText.Contains("system") ||
                lowerText.Contains("server") || lowerText.Contains("network") ||
                lowerText.Contains("application") || lowerText.Contains("software"))
                return "Information Technology";
                
            // Compliance domain indicators
            if (lowerText.Contains("compliance") || lowerText.Contains("audit") ||
                lowerText.Contains("regulation") || lowerText.Contains("policy") ||
                lowerText.Contains("governance") || lowerText.Contains("risk"))
                return "Compliance & Risk";
        }

        return typeBasedDomain;
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

        // Determine sensitivity with enhanced PII detection
        var detectedPIITypes = DetectPIIPatterns(metadata.DocumentText);
        if (detectedPIITypes.Any())
        {
            metadata.SensitivityLevel = "High";
            metadata.PIIFlag = true;
            metadata.PIITypes = string.Join(", ", detectedPIITypes);
        }
        else if (text.Contains("customer") || text.Contains("employee"))
        {
            metadata.SensitivityLevel = "Medium";
        }
        else
        {
            metadata.SensitivityLevel = "Low";
        }

        metadata.PopulatedFieldCount += detectedPIITypes.Any() ? 4 : 3;
    }

    // Enhanced PII detection with pattern matching
    private List<string> DetectPIIPatterns(string? documentText)
    {
        if (string.IsNullOrEmpty(documentText)) return new List<string>();
        
        var detectedTypes = new List<string>();
        var lowerText = documentText.ToLower();
        
        // Social Security Number patterns
        if (Regex.IsMatch(documentText, @"\b\d{3}-\d{2}-\d{4}\b") || 
            lowerText.Contains("ssn") || lowerText.Contains("social security"))
            detectedTypes.Add("SSN");
            
        // Credit Card patterns
        if (Regex.IsMatch(documentText, @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b") ||
            lowerText.Contains("credit card") || lowerText.Contains("card number"))
            detectedTypes.Add("Credit Card");
            
        // Email patterns
        if (Regex.IsMatch(documentText, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"))
            detectedTypes.Add("Email");
            
        // Phone number patterns
        if (Regex.IsMatch(documentText, @"\b\d{3}[-.\s]?\d{3}[-.\s]?\d{4}\b") ||
            Regex.IsMatch(documentText, @"\(\d{3}\)\s?\d{3}[-.\s]?\d{4}"))
            detectedTypes.Add("Phone Number");
            
        // Date of birth patterns
        if (lowerText.Contains("dob") || lowerText.Contains("date of birth") ||
            lowerText.Contains("birth date") || Regex.IsMatch(documentText, @"\b\d{1,2}/\d{1,2}/\d{4}\b"))
            detectedTypes.Add("Date of Birth");
            
        // Financial information
        if (lowerText.Contains("salary") || lowerText.Contains("income") || 
            lowerText.Contains("wage") || lowerText.Contains("compensation"))
            detectedTypes.Add("Financial Info");
            
        // Medical information
        if (lowerText.Contains("medical") || lowerText.Contains("health") ||
            lowerText.Contains("diagnosis") || lowerText.Contains("treatment"))
            detectedTypes.Add("Medical Info");
            
        // Government ID
        if (lowerText.Contains("driver license") || lowerText.Contains("passport") ||
            lowerText.Contains("license number") || lowerText.Contains("id number"))
            detectedTypes.Add("Government ID");
            
        // Bank account information
        if (lowerText.Contains("bank account") || lowerText.Contains("routing number") ||
            lowerText.Contains("account number") || Regex.IsMatch(documentText, @"\b\d{9,18}\b"))
            detectedTypes.Add("Bank Account");
            
        // Authentication credentials
        if (lowerText.Contains("password") || lowerText.Contains("pin") ||
            lowerText.Contains("secret key") || lowerText.Contains("access token"))
            detectedTypes.Add("Credentials");

        return detectedTypes;
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
        // Calculate comprehensive quality score based on completeness
        var qualityFactors = new Dictionary<string, bool>
        {
            ["BusinessDefinition"] = !string.IsNullOrEmpty(metadata.BusinessDefinition),
            ["TechnicalDefinition"] = !string.IsNullOrEmpty(metadata.TechnicalDefinition), 
            ["BusinessPurpose"] = !string.IsNullOrEmpty(metadata.BusinessPurpose),
            ["BusinessDomain"] = !string.IsNullOrEmpty(metadata.BusinessDomain),
            ["DataClassification"] = !string.IsNullOrEmpty(metadata.DataClassification),
            ["SensitivityLevel"] = !string.IsNullOrEmpty(metadata.SensitivityLevel),
            ["DocumentationType"] = !string.IsNullOrEmpty(metadata.DocumentType),
            ["Category"] = !string.IsNullOrEmpty(metadata.Category),
            ["RelatedObjects"] = !string.IsNullOrEmpty(metadata.RelatedStoredProcedures),
            ["PIIFlag"] = metadata.PIIFlag.HasValue
        };

        var completedFactors = qualityFactors.Count(kv => kv.Value);
        var totalFactors = qualityFactors.Count;
        
        // Calculate percentage-based completeness score
        metadata.CompletenessScore = (decimal)((completedFactors * 100.0) / totalFactors);
        
        // Quality score combines completeness with content quality
        var contentQualityBonus = 0m;
        if (metadata.WordCount > 100) contentQualityBonus += 10;  // Substantial content
        if (metadata.SectionCount > 3) contentQualityBonus += 5;   // Well-structured  
        if (!string.IsNullOrEmpty(metadata.PIITypes)) contentQualityBonus += 5; // Security awareness
        
        metadata.QualityScore = Math.Min(100, metadata.CompletenessScore.GetValueOrDefault(0) + contentQualityBonus);
        metadata.DocumentationQuality = metadata.QualityScore >= 85 ? "Excellent" :
                                       metadata.QualityScore >= 70 ? "Good" :
                                       metadata.QualityScore >= 50 ? "Fair" : "Poor";
        metadata.PopulatedFieldCount += 3;
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
        var tags = new List<string> { metadata.DocumentType ?? "Unknown" };
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
        metadata.MetadataCompleteness = (int?)Math.Round(metadata.CompletenessScore.GetValueOrDefault(0));
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
    // PHASE 16: AI-POWERED METADATA ENRICHMENT (NEW)
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase16_AIEnrichmentAsync(MasterIndexMetadata metadata, CancellationToken ct)
    {
        _logger.LogInformation("Phase 16: AI Enrichment for {DocId}", metadata.DocumentTitle);
        
        try
        {
            // Only run AI enrichment if we have basic metadata
            if (string.IsNullOrEmpty(metadata.SchemaName) || string.IsNullOrEmpty(metadata.TableName))
            {
                _logger.LogInformation("Skipping AI enrichment - insufficient metadata");
                return;
            }

            // AI-powered semantic classification
            if (string.IsNullOrEmpty(metadata.SemanticCategory))
            {
                var semanticClass = await _aiService.ClassifySemanticCategoryAsync(
                    metadata.SchemaName,
                    metadata.TableName,
                    metadata.ColumnName ?? string.Empty,
                    metadata.BusinessDefinition ?? metadata.TechnicalDefinition ?? string.Empty,
                    ct);

                metadata.SemanticCategory = semanticClass.Category;
                metadata.SemanticConfidence = (decimal)semanticClass.Confidence;
                metadata.PopulatedFieldCount += 2;
                
                _logger.LogInformation("AI classified semantic category: {Category} ({Confidence}%)", 
                    semanticClass.Category, (semanticClass.Confidence * 100).ToString("F1"));
            }

            // AI-generated tags
            if (string.IsNullOrEmpty(metadata.AIGeneratedTags))
            {
                var tags = await _aiService.GenerateTagsAsync(
                    metadata.SchemaName,
                    metadata.TableName,
                    metadata.ColumnName ?? string.Empty,
                    metadata.BusinessDefinition ?? metadata.TechnicalDefinition ?? string.Empty,
                    ct);

                if (tags.Length > 0)
                {
                    metadata.AIGeneratedTags = string.Join(", ", tags);
                    metadata.PopulatedFieldCount++;
                    
                    _logger.LogInformation("AI generated tags: {Tags}", metadata.AIGeneratedTags);
                }
            }

            // AI-powered compliance classification
            if (string.IsNullOrEmpty(metadata.ComplianceTags))
            {
                var complianceClass = await _aiService.ClassifyComplianceAsync(
                    metadata.SchemaName,
                    metadata.TableName,
                    metadata.ColumnName ?? string.Empty,
                    metadata.PIIFlag == true,
                    ct);

                if (complianceClass.ComplianceTags.Length > 0)
                {
                    metadata.ComplianceTags = string.Join(", ", complianceClass.ComplianceTags);
                    metadata.RetentionPeriod = $"{complianceClass.RetentionYears} years";
                    metadata.PopulatedFieldCount += 2;
                    
                    _logger.LogInformation("AI classified compliance: {Tags}, retention: {Years} years", 
                        metadata.ComplianceTags, complianceClass.RetentionYears);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI enrichment failed (non-critical) - continuing with existing metadata");
        }
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
            SourceSystem, SourceDocumentID, SourceFilePath,
            DocumentTitle, DocumentType, Description,
            SystemName, DatabaseName, SchemaName, TableName, ColumnName,
            DataType, DataClassification, Sensitivity,
            QualityScore, CompletenessScore, ValidationStatus,
            VersionNumber, IsLatestVersion, Status, WorkflowStatus, ApprovalStatus,
            ApprovedBy, ApprovedDate,
            CreatedDate, CreatedBy, ModifiedDate, ModifiedBy,
            BusinessDefinition, TechnicalDefinition,
            SensitivityLevel, MetadataCompleteness
        ) VALUES (
            @SourceSystem, @SourceDocumentID, @SourceFilePath,
            @DocumentTitle, @DocumentType, @Description,
            @SystemName, @DatabaseName, @SchemaName, @TableName, @ColumnName,
            @DataType, @DataClassification, @Sensitivity,
            @QualityScore, @CompletenessScore, @ValidationStatus,
            @VersionNumber, @IsLatestVersion, @Status, @WorkflowStatus, @ApprovalStatus,
            @ApprovedBy, @ApprovedDate,
            @CreatedDate, @CreatedBy, @ModifiedDate, @ModifiedBy,
            @BusinessDefinition, @TechnicalDefinition,
            @SensitivityLevel, @MetadataCompleteness
        )";
    
    var parameters = new
    {
        SourceSystem = "DocumentationPlatform",
        SourceDocumentID = metadata.SourceDocumentID,
        SourceFilePath = metadata.SourceFilePath,
        DocumentTitle = metadata.DocumentTitle,
        DocumentType = metadata.DocumentType,
        Description = metadata.BusinessDefinition ?? metadata.TechnicalDefinition ?? "Auto-generated documentation",
        SystemName = "IRFS1",
        DatabaseName = metadata.DatabaseName ?? "IRFS1",
        SchemaName = metadata.SchemaName,
        TableName = metadata.TableName,
        ColumnName = metadata.ColumnName,
        DataType = metadata.DataType,
        DataClassification = metadata.DataClassification,
        Sensitivity = metadata.SensitivityLevel,
        QualityScore = metadata.QualityScore ?? 85,
        CompletenessScore = metadata.CompletenessScore ?? 85,
        ValidationStatus = "Validated",
        VersionNumber = "1.0",
        IsLatestVersion = true,
        Status = "Active",
        WorkflowStatus = "Completed",
        ApprovalStatus = "Approved",
        ApprovedBy = metadata.CreatedBy ?? "System",
        ApprovedDate = DateTime.UtcNow,
        CreatedDate = DateTime.UtcNow,
        CreatedBy = metadata.CreatedBy ?? "System",
        ModifiedDate = DateTime.UtcNow,
        ModifiedBy = metadata.ModifiedBy ?? "System",
        BusinessDefinition = metadata.BusinessDefinition,
        TechnicalDefinition = metadata.TechnicalDefinition,
        SensitivityLevel = metadata.SensitivityLevel,
        MetadataCompleteness = metadata.MetadataCompleteness ?? 85
    };
    
    await connection.ExecuteAsync(sql, parameters);
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

    // ═══════════════════════════════════════════════════════════════════
    // PHASE 15: AI-POWERED METADATA INFERENCE
    // ═══════════════════════════════════════════════════════════════════
    private async Task Phase15_AIInferenceAsync(
        MasterIndexMetadata metadata,
        string docId,
        string jiraNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_openAIApiKey) || string.IsNullOrEmpty(_openAIEndpoint))
        {
            _logger.LogInformation("OpenAI not configured - skipping AI metadata inference");
            return;
        }

        try
        {
            _logger.LogInformation("Starting AI-powered metadata inference for {DocId}", docId);

            var inferredMetadata = await InferMetadataWithAIAsync(
                metadata.TableName ?? "Unknown",
                metadata.ColumnName,
                metadata.BusinessDefinition ?? metadata.TechnicalDefinition ?? "Database change",
                ct);

            // Only populate if confidence > 0.8 and field is not already populated
            foreach (var (fieldName, (value, confidence)) in inferredMetadata)
            {
                if (confidence >= 0.8 && !string.IsNullOrWhiteSpace(value))
                {
                    var updated = UpdateMetadataField(metadata, fieldName, value);
                    if (updated)
                    {
                        _logger.LogInformation("AI inferred {Field} with {Confidence}% confidence: {Value}", 
                            fieldName, (confidence * 100).ToString("F1"), value.Substring(0, Math.Min(50, value.Length)) + "...");
                    }
                }
                else
                {
                    _logger.LogDebug("Skipping {Field} - confidence {Confidence}% below threshold or empty value", 
                        fieldName, (confidence * 100).ToString("F1"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI metadata inference failed (non-critical) - continuing with existing metadata");
        }
    }

    private async Task<Dictionary<string, (string value, double confidence)>> InferMetadataWithAIAsync(
        string tableName,
        string? columnName,
        string changeDescription,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, (string, double)>();
        
        try
        {
            var prompt = $@"Analyze this database change and infer metadata. Only provide values you are >80% confident about.

Table: {tableName}
Column: {columnName ?? "N/A"}
Change Description: {changeDescription}

Infer these fields with confidence scores (0.0-1.0):
1. BusinessDefinition: Brief business purpose (1-2 sentences)
2. TechnicalDefinition: Technical description (1-2 sentences)  
3. DataClassification: Public, Internal, Confidential, or Restricted
4. Sensitivity: Low, Medium, High, or Critical

Respond ONLY in this JSON format (no markdown, no preamble):
{{
  ""BusinessDefinition"": {{""value"": ""..."", ""confidence"": 0.0}},
  ""TechnicalDefinition"": {{""value"": ""..."", ""confidence"": 0.0}},
  ""DataClassification"": {{""value"": ""..."", ""confidence"": 0.0}},
  ""Sensitivity"": {{""value"": ""..."", ""confidence"": 0.0}}
}}

If confidence < 0.8, set value to null.";

            var openAIRequest = new
            {
                model = _openAIModel,
                messages = new[]
                {
                    new { role = "system", content = "You are a database metadata expert. Analyze database changes and infer business metadata with high confidence only." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.2,  // Low temperature for consistent results
                max_tokens = 800
            };

            var azureUrl = $"{_openAIEndpoint!.TrimEnd('/')}/openai/deployments/{_openAIModel}/chat/completions?api-version=2024-08-01-preview";
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
            
            var response = await _httpClient.PostAsJsonAsync(azureUrl, openAIRequest, timeoutCts.Token);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI API request failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent.Substring(0, Math.Min(200, errorContent.Length)));
                return results;
            }

            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            var openAIResponse = System.Text.Json.JsonSerializer.Deserialize<OpenAIResponse>(result);

            if (openAIResponse?.Choices?.Length > 0)
            {
                var content = openAIResponse.Choices[0].Message.Content;
                var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, AIInferenceResult>>(content);
                
                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        if (kvp.Value != null && kvp.Value.Confidence >= 0.8 && !string.IsNullOrWhiteSpace(kvp.Value.Value))
                        {
                            results[kvp.Key] = (kvp.Value.Value, kvp.Value.Confidence);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI metadata inference failed (non-critical)");
        }
        
        return results;
    }

    private bool UpdateMetadataField(MasterIndexMetadata metadata, string fieldName, string value)
    {
        return fieldName switch
        {
            "BusinessDefinition" when string.IsNullOrEmpty(metadata.BusinessDefinition) => 
                SetField(() => metadata.BusinessDefinition = value),
            "TechnicalDefinition" when string.IsNullOrEmpty(metadata.TechnicalDefinition) => 
                SetField(() => metadata.TechnicalDefinition = value),
            "DataClassification" when string.IsNullOrEmpty(metadata.DataClassification) => 
                SetField(() => metadata.DataClassification = value),
            "Sensitivity" when string.IsNullOrEmpty(metadata.SensitivityLevel) => 
                SetField(() => metadata.SensitivityLevel = value),
            _ => false
        };
        
        static bool SetField(System.Action setter)
        {
            setter();
            return true;
        }
    }
}

// Supporting classes for OpenAI integration
public class OpenAIResponse
{
    public OpenAIChoice[]? Choices { get; set; }
}

public class OpenAIChoice
{
    public OpenAIMessage Message { get; set; } = new();
}

public class OpenAIMessage
{
    public string Content { get; set; } = string.Empty;
}

public class AIInferenceResult
{
    public string Value { get; set; } = string.Empty;
    public double Confidence { get; set; }
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
    public long? FileSize { get; set; }
    public string? FileHash { get; set; }

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
    public string? BusinessDomain { get; set; }

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
    public string? PIITypes { get; set; }

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
    public decimal? CompletenessScore { get; set; }
    public int? MetadataCompleteness { get; set; }
    public DateTime? LastReviewedDate { get; set; }

    // Audit Trail (Phase 14)
    public string? DocumentVersion { get; set; }
    public string? ChangeReason { get; set; }
    public string? AuditTrail { get; set; }

    // AI-Powered Metadata (Phase 16)
    public string? SemanticCategory { get; set; }
    public decimal? SemanticConfidence { get; set; }
    public string? AIGeneratedTags { get; set; }

    // Tracking
    public int PopulatedFieldCount { get; set; } = 0;
}