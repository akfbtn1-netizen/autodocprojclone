# Metadata Extraction Implementation Guide

## Overview

This guide provides step-by-step instructions for implementing the metadata extraction strategy to maximize MasterIndex column population from ~30% to 70%+.

---

## Phase 1: Quick Wins (Week 1)

### 1.1 Add BusinessDomain Mapping (No AI)

**File:** `ComprehensiveMasterIndexService.cs`
**Location:** Add after Phase 3 (DatabaseMetadata)

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// PHASE 3B: Business Domain Mapping (NEW)
// ═══════════════════════════════════════════════════════════════════════════
private void Phase3B_BusinessDomainMapping(MasterIndexMetadata metadata)
{
    var domainMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "gwpc", "Policy Management" },
        { "gwpcDaily", "Policy Management" },
        { "gwpcMonthly", "Policy Management" },
        { "gwControl", "Reference Data" },
        { "mfp", "Multi-Family Policy" },
        { "claims", "Claims Processing" },
        { "claimsDaily", "Claims Processing" },
        { "billing", "Billing & Finance" },
        { "finance", "Billing & Finance" },
        { "DaQa", "Data Quality & Analytics" },
        { "audit", "System & Audit" },
        { "dbo", "Core System" },
        { "staging", "ETL & Staging" },
        { "archive", "Archive & History" }
    };

    if (!string.IsNullOrEmpty(metadata.SchemaName) && 
        domainMap.TryGetValue(metadata.SchemaName, out var domain))
    {
        metadata.BusinessDomain = domain;
        metadata.PopulatedFieldCount++;
    }
    else
    {
        metadata.BusinessDomain = "General";
        metadata.PopulatedFieldCount++;
    }
}
```

**Call it:** Add to `PopulateMasterIndexFromApprovedDocumentAsync`:
```csharp
await Phase3_DatabaseMetadataAsync(metadata, cancellationToken);
Phase3B_BusinessDomainMapping(metadata);  // NEW
await Phase4_BusinessContextAsync(metadata, filePath, cancellationToken);
```

---

### 1.2 Add PII Detection (Pattern Matching)

**File:** `ComprehensiveMasterIndexService.cs`
**Location:** Add new phase or integrate into Phase 7 (Classification)

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// PHASE 7B: PII Detection (NEW)
// ═══════════════════════════════════════════════════════════════════════════
private void Phase7B_PIIDetection(MasterIndexMetadata metadata)
{
    if (string.IsNullOrEmpty(metadata.ColumnName))
    {
        metadata.PIIIndicator = false;
        metadata.ContainsPII = false;
        metadata.PopulatedFieldCount += 2;
        return;
    }

    var piiPatterns = new (string Type, string Pattern)[]
    {
        ("SSN", @"(ssn|social.*security|soc.*sec|tax.*id)"),
        ("DOB", @"(birth.*date|dob|date.*birth|birthdate)"),
        ("Email", @"(email|e-mail|e_mail|emailaddr)"),
        ("Phone", @"(phone|mobile|cell|telephone|fax)"),
        ("Address", @"(address|street|city|zip|postal|state)"),
        ("Name", @"(first.*name|last.*name|full.*name|fname|lname|customer.*name)"),
        ("Account", @"(account.*num|acct.*no|bank.*account|routing)"),
        ("License", @"(license|licence|driver.*lic|dl_)"),
        ("Medical", @"(diagnosis|treatment|medical|health|patient)"),
        ("Financial", @"(salary|income|credit.*score|net.*worth)")
    };

    var columnLower = metadata.ColumnName.ToLowerInvariant();
    var detectedPII = new List<string>();

    foreach (var (type, pattern) in piiPatterns)
    {
        if (Regex.IsMatch(columnLower, pattern, RegexOptions.IgnoreCase))
        {
            detectedPII.Add(type);
        }
    }

    metadata.PIIIndicator = detectedPII.Any();
    metadata.ContainsPII = detectedPII.Any();
    metadata.PIITypes = detectedPII.Any() 
        ? JsonSerializer.Serialize(detectedPII) 
        : null;
    
    // Set sensitivity based on PII
    if (detectedPII.Any())
    {
        metadata.SensitivityLevel = detectedPII.Contains("SSN") || 
                                    detectedPII.Contains("Medical") || 
                                    detectedPII.Contains("Financial")
            ? "High"
            : "Medium";
        
        metadata.DataClassification = metadata.SensitivityLevel == "High" 
            ? "Confidential" 
            : "Internal";
    }
    else
    {
        metadata.SensitivityLevel = "Low";
        metadata.DataClassification = "Internal";
    }

    metadata.PopulatedFieldCount += 5; // PIIIndicator, ContainsPII, PIITypes, SensitivityLevel, DataClassification
}
```

---

### 1.3 Add CompletenessScore Calculation

**File:** `ComprehensiveMasterIndexService.cs`
**Location:** Call at the very end before INSERT

```csharp
// ═══════════════════════════════════════════════════════════════════════════
// PHASE 15: Calculate Completeness (NEW - call last)
// ═══════════════════════════════════════════════════════════════════════════
private void Phase15_CalculateCompleteness(MasterIndexMetadata metadata)
{
    // Count non-null, non-empty properties
    var props = typeof(MasterIndexMetadata).GetProperties();
    int populated = 0;
    int total = 0;

    var excludeProps = new HashSet<string> 
    { 
        "IndexID", "PopulatedFieldCount", "CompletenessScore", "MetadataCompleteness"
    };

    foreach (var prop in props)
    {
        if (excludeProps.Contains(prop.Name)) continue;
        
        total++;
        var value = prop.GetValue(metadata);
        
        if (value != null)
        {
            if (value is string str && !string.IsNullOrWhiteSpace(str))
                populated++;
            else if (value is not string)
                populated++;
        }
    }

    metadata.CompletenessScore = (int)Math.Round(populated * 100.0 / 119); // 119 total columns
    metadata.MetadataCompleteness = metadata.CompletenessScore;
    metadata.QualityScore = metadata.CompletenessScore; // Simple for now
}
```

---

### 1.4 Add FileSize and FileHash

**File:** `ComprehensiveMasterIndexService.cs`
**Location:** Phase 2 (Document Analysis) 

```csharp
// In Phase2_DocumentAnalysisAsync, add:
private async Task Phase2_DocumentAnalysisAsync(
    MasterIndexMetadata metadata,
    string filePath,
    CancellationToken ct)
{
    if (!File.Exists(filePath))
    {
        _logger.LogWarning("Document not found: {Path}", filePath);
        return;
    }

    var fileInfo = new FileInfo(filePath);
    
    // File metadata
    metadata.FileSize = fileInfo.Length;
    metadata.PopulatedFieldCount++;

    // File hash
    using (var stream = File.OpenRead(filePath))
    using (var sha = SHA256.Create())
    {
        var hashBytes = await sha.ComputeHashAsync(stream, ct);
        metadata.FileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        metadata.PopulatedFieldCount++;
    }

    // Existing document analysis...
    // (keep existing code for DocumentTitle, Description extraction)
}
```

**Add using:** `using System.Security.Cryptography;`

---

## Phase 2: AI-Powered Metadata (Week 2)

### 2.1 Create MetadataAIService

**File:** `Services/MetadataAIService.cs` (NEW)

```csharp
using System.Text.Json;
using Azure.AI.OpenAI;

namespace EnterpriseDocumentation.Api.Services;

public interface IMetadataAIService
{
    Task<SemanticClassification> ClassifySemanticCategoryAsync(
        string schema, string table, string column, string description, CancellationToken ct);
    Task<string[]> GenerateTagsAsync(
        string schema, string table, string column, string description, CancellationToken ct);
    Task<ComplianceClassification> ClassifyComplianceAsync(
        string schema, string table, string column, bool containsPII, CancellationToken ct);
}

public class MetadataAIService : IMetadataAIService
{
    private readonly OpenAIClient _client;
    private readonly string _deploymentName;
    private readonly ILogger<MetadataAIService> _logger;

    public MetadataAIService(
        OpenAIClient client,
        IConfiguration config,
        ILogger<MetadataAIService> logger)
    {
        _client = client;
        _deploymentName = config["OpenAI:Deployment"] ?? "gpt-4.1";
        _logger = logger;
    }

    public async Task<SemanticClassification> ClassifySemanticCategoryAsync(
        string schema, string table, string column, string description, CancellationToken ct)
    {
        var prompt = $@"You are a database metadata classifier for Tennessee Farmers Insurance.

Classify this database object into exactly ONE semantic category:

Schema: {schema}
Table: {table}
Column: {column ?? "N/A"}
Description: {description ?? "No description"}

Categories:
- Policy Management: Policy lifecycle, endorsements, renewals, cancellations
- Claims Processing: Claims, losses, payments, reserves
- Billing & Finance: Premiums, invoices, payments, accounting
- Customer Data: Policyholders, contacts, demographics
- Agent & Producer: Agent info, commissions, hierarchies
- Reference Data: Lookup tables, codes, configurations
- Underwriting: Risk assessment, rating, eligibility
- Reporting & Analytics: Aggregated data, metrics, KPIs
- System & Audit: Logs, timestamps, technical metadata
- Document Management: Documents, attachments, correspondence

Respond with JSON only:
{{""category"": ""selected category"", ""confidence"": 0.0-1.0}}";

        try
        {
            var response = await _client.GetChatCompletionsAsync(
                new ChatCompletionsOptions(_deploymentName, new[]
                {
                    new ChatRequestUserMessage(prompt)
                })
                {
                    Temperature = 0.1f,
                    MaxTokens = 100,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject
                }, ct);

            var json = response.Value.Choices[0].Message.Content;
            return JsonSerializer.Deserialize<SemanticClassification>(json) 
                ?? new SemanticClassification { Category = "General", Confidence = 0.5 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI classification failed, using fallback");
            return new SemanticClassification { Category = "General", Confidence = 0.0 };
        }
    }

    public async Task<string[]> GenerateTagsAsync(
        string schema, string table, string column, string description, CancellationToken ct)
    {
        var prompt = $@"Generate 5-10 searchable tags for this database object:

Schema: {schema}
Table: {table}
Column: {column ?? "N/A"}
Description: {description ?? "No description"}

Rules:
- Lowercase only
- Include business and technical terms
- Include abbreviations users might search

Respond with JSON only:
{{""tags"": [""tag1"", ""tag2"", ...]}}";

        try
        {
            var response = await _client.GetChatCompletionsAsync(
                new ChatCompletionsOptions(_deploymentName, new[]
                {
                    new ChatRequestUserMessage(prompt)
                })
                {
                    Temperature = 0.3f,
                    MaxTokens = 150,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject
                }, ct);

            var json = response.Value.Choices[0].Message.Content;
            var result = JsonSerializer.Deserialize<TagsResult>(json);
            return result?.Tags ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI tag generation failed");
            return Array.Empty<string>();
        }
    }

    public async Task<ComplianceClassification> ClassifyComplianceAsync(
        string schema, string table, string column, bool containsPII, CancellationToken ct)
    {
        var prompt = $@"Identify applicable compliance frameworks for this insurance company data:

Schema: {schema}
Table: {table}
Column: {column ?? "N/A"}
Contains PII: {containsPII}

Frameworks: SOX, HIPAA, PCI-DSS, GLBA, State Insurance Regulations, GDPR, CCPA

Respond with JSON only:
{{""complianceTags"": [""SOX"", ""GLBA""], ""retentionYears"": 7}}";

        try
        {
            var response = await _client.GetChatCompletionsAsync(
                new ChatCompletionsOptions(_deploymentName, new[]
                {
                    new ChatRequestUserMessage(prompt)
                })
                {
                    Temperature = 0.1f,
                    MaxTokens = 100,
                    ResponseFormat = ChatCompletionsResponseFormat.JsonObject
                }, ct);

            var json = response.Value.Choices[0].Message.Content;
            return JsonSerializer.Deserialize<ComplianceClassification>(json)
                ?? new ComplianceClassification();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI compliance classification failed");
            return new ComplianceClassification();
        }
    }
}

public class SemanticClassification
{
    public string Category { get; set; } = "General";
    public double Confidence { get; set; }
}

public class TagsResult
{
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class ComplianceClassification
{
    public string[] ComplianceTags { get; set; } = Array.Empty<string>();
    public int RetentionYears { get; set; } = 7;
}
```

---

### 2.2 Register MetadataAIService

**File:** `Program.cs`

```csharp
// Add with other service registrations
builder.Services.AddScoped<IMetadataAIService, MetadataAIService>();
```

---

### 2.3 Integrate AI into ComprehensiveMasterIndexService

**File:** `ComprehensiveMasterIndexService.cs`

```csharp
// Add to constructor
private readonly IMetadataAIService _aiService;

public ComprehensiveMasterIndexService(
    IConfiguration config,
    ILogger<ComprehensiveMasterIndexService> logger,
    IMetadataAIService aiService)  // ADD THIS
{
    _connectionString = config.GetConnectionString("DefaultConnection");
    _logger = logger;
    _aiService = aiService;  // ADD THIS
}

// Add new AI phase
private async Task Phase16_AIEnrichmentAsync(MasterIndexMetadata metadata, CancellationToken ct)
{
    _logger.LogInformation("Phase 16: AI Enrichment for {DocId}", metadata.DocId);

    // Semantic Category
    var semantic = await _aiService.ClassifySemanticCategoryAsync(
        metadata.SchemaName,
        metadata.TableName,
        metadata.ColumnName,
        metadata.Description,
        ct);
    
    metadata.SemanticCategory = semantic.Category;
    metadata.PopulatedFieldCount++;

    // AI Tags
    var tags = await _aiService.GenerateTagsAsync(
        metadata.SchemaName,
        metadata.TableName,
        metadata.ColumnName,
        metadata.Description,
        ct);
    
    if (tags.Any())
    {
        metadata.AIGeneratedTags = JsonSerializer.Serialize(tags);
        metadata.PopulatedFieldCount++;
    }

    // Compliance (only if we detected PII or it's financial)
    if (metadata.PIIIndicator == true || 
        metadata.BusinessDomain == "Billing & Finance")
    {
        var compliance = await _aiService.ClassifyComplianceAsync(
            metadata.SchemaName,
            metadata.TableName,
            metadata.ColumnName,
            metadata.PIIIndicator ?? false,
            ct);
        
        if (compliance.ComplianceTags?.Any() == true)
        {
            metadata.ComplianceTags = JsonSerializer.Serialize(compliance.ComplianceTags);
            metadata.RetentionPolicy = $"{compliance.RetentionYears} years";
            metadata.PopulatedFieldCount += 2;
        }
    }
}
```

---

## Phase 3: Shadow Metadata / CustomProperties (Week 2-3)

### 3.1 Add CustomPropertiesHelper

**File:** `Helpers/CustomPropertiesHelper.cs` (NEW)

See full implementation in `references/CUSTOM-PROPERTIES.md`

### 3.2 Integrate into ApprovalTrackingService

**File:** `ApprovalTrackingService.cs`

Add after final document creation (around line 200+):

```csharp
// After: File.Copy(draftPath, finalPath, overwrite: true);

// Embed shadow metadata
_logger.LogInformation("Embedding shadow metadata for {DocId}", approval.DocumentId);

var shadowMetadata = new ShadowMetadata
{
    DocId = approval.DocumentId,
    JiraNumber = approval.JiraNumber,
    DocumentType = GetDocumentTypeFromDocId(approval.DocumentId),
    Version = "1.0",
    GeneratedDate = DateTime.UtcNow,
    SchemaName = parsedDocInfo.Schema,
    TableName = parsedDocInfo.Table,
    ColumnName = parsedDocInfo.Column,
    DatabaseName = "IRFS1",
    BusinessDomain = await GetBusinessDomainAsync(parsedDocInfo.Schema),
    SemanticCategory = masterIndexRecord?.SemanticCategory ?? "General",
    DataClassification = masterIndexRecord?.DataClassification ?? "Internal",
    PIIIndicator = masterIndexRecord?.PIIIndicator ?? false,
    ApprovalStatus = "Approved",
    ApprovedBy = approval.ApprovedBy,
    Keywords = masterIndexRecord?.Keywords
};

CustomPropertiesHelper.EmbedShadowMetadata(finalPath, shadowMetadata);
```

---

## Phase 4: Keywords Extraction (Week 3)

### 4.1 Extract Keywords from Document

**File:** `ComprehensiveMasterIndexService.cs`

```csharp
private void Phase4B_KeywordExtraction(MasterIndexMetadata metadata)
{
    var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Add schema/table/column
    if (!string.IsNullOrEmpty(metadata.SchemaName))
        keywords.Add(metadata.SchemaName);
    if (!string.IsNullOrEmpty(metadata.TableName))
        keywords.Add(metadata.TableName);
    if (!string.IsNullOrEmpty(metadata.ColumnName))
        keywords.Add(metadata.ColumnName);
    
    // Add business domain
    if (!string.IsNullOrEmpty(metadata.BusinessDomain))
        keywords.Add(metadata.BusinessDomain);

    // Extract from description (simple word extraction)
    if (!string.IsNullOrEmpty(metadata.Description))
    {
        var words = Regex.Split(metadata.Description, @"\W+")
            .Where(w => w.Length > 3)
            .Where(w => !IsStopWord(w))
            .Take(15);
        
        foreach (var word in words)
            keywords.Add(word);
    }

    // Add stored procedure name if present
    if (!string.IsNullOrEmpty(metadata.StoredProcedures))
    {
        var procs = metadata.StoredProcedures
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim());
        
        foreach (var proc in procs)
            keywords.Add(proc);
    }

    metadata.Keywords = string.Join(", ", keywords.Take(25));
    metadata.PopulatedFieldCount++;
}

private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
{
    "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her",
    "was", "one", "our", "out", "has", "have", "been", "were", "this", "that",
    "with", "they", "from", "which", "their", "will", "would", "there", "what"
};

private static bool IsStopWord(string word) => StopWords.Contains(word);
```

---

## Validation & Testing

### Test Query: Check Population Rates

```sql
SELECT 
    COUNT(*) AS TotalDocs,
    
    -- Tier 1 (Critical)
    COUNT(CASE WHEN DocumentTitle IS NOT NULL THEN 1 END) AS HasTitle,
    COUNT(CASE WHEN BusinessDomain IS NOT NULL THEN 1 END) AS HasDomain,
    COUNT(CASE WHEN SemanticCategory IS NOT NULL THEN 1 END) AS HasCategory,
    COUNT(CASE WHEN Keywords IS NOT NULL THEN 1 END) AS HasKeywords,
    COUNT(CASE WHEN DataClassification IS NOT NULL THEN 1 END) AS HasClassification,
    
    -- Tier 2 (High Value)
    COUNT(CASE WHEN PIIIndicator IS NOT NULL THEN 1 END) AS HasPII,
    COUNT(CASE WHEN AIGeneratedTags IS NOT NULL THEN 1 END) AS HasAITags,
    COUNT(CASE WHEN ComplianceTags IS NOT NULL THEN 1 END) AS HasCompliance,
    
    -- Quality
    AVG(CompletenessScore) AS AvgCompleteness,
    MIN(CompletenessScore) AS MinCompleteness,
    MAX(CompletenessScore) AS MaxCompleteness
    
FROM DaQa.MasterIndex
WHERE IsDeleted = 0;
```

### Test: Verify Shadow Metadata

```csharp
// After document creation, verify:
var readBack = CustomPropertiesHelper.ReadShadowMetadata(finalPath);
Debug.Assert(readBack.DocId == approval.DocumentId);
Debug.Assert(!string.IsNullOrEmpty(readBack.BusinessDomain));
Debug.Assert(!string.IsNullOrEmpty(readBack.SemanticCategory));
```

---

## Deployment Checklist

### Week 1 Deployment
- [ ] Add BusinessDomain mapping to ComprehensiveMasterIndexService
- [ ] Add PII detection patterns
- [ ] Add CompletenessScore calculation
- [ ] Add FileSize/FileHash extraction
- [ ] Test with existing approval workflow
- [ ] Verify population rates increased

### Week 2 Deployment
- [ ] Create MetadataAIService
- [ ] Register in Program.cs
- [ ] Add AI phase to ComprehensiveMasterIndexService
- [ ] Test AI calls don't block workflow on failure
- [ ] Monitor token usage

### Week 3 Deployment
- [ ] Add CustomPropertiesHelper
- [ ] Integrate shadow metadata into ApprovalTrackingService
- [ ] Add keyword extraction
- [ ] Verify CustomProperties readable in Word
- [ ] Test full end-to-end flow

---

## Expected Results

| Metric | Before | After Week 1 | After Week 3 |
|--------|--------|--------------|--------------|
| Avg CompletenessScore | ~30% | ~45% | ~70% |
| BusinessDomain populated | 0% | 100% | 100% |
| PIIIndicator populated | 0% | 100% | 100% |
| SemanticCategory populated | 0% | 0% | 90%+ |
| AIGeneratedTags populated | 0% | 0% | 90%+ |
| Shadow metadata in docs | 0 fields | 0 fields | 15 fields |

---

## Troubleshooting

### AI Calls Failing
- Check OpenAI deployment name in config
- Verify API key is valid
- Check token limits aren't exceeded
- Fallbacks should prevent workflow failure

### CompletenessScore Not Updating
- Ensure Phase15 is called last
- Check property names match MasterIndexMetadata

### CustomProperties Not Appearing
- Verify DocumentFormat.OpenXml NuGet installed
- Check file isn't open when writing
- Use Word > File > Info > Properties > Advanced to view
