# V2 Integration Implementation Guide

## For: Integration Agent
## Project: C:\Projects\EnterpriseDocumentationPlatform.V2
## Database: IRFS1 on ibidb2003dv (DaQa schema)

---

# STEP 1: Fix Critical Bugs (MUST DO FIRST)

## 1.1 Fix Teams Notification Method Mismatch

**File:** `src/Api/Services/ApprovalTrackingService.cs`

**Find this code (around line 180-200):**
```csharp
await _teamsService.SendDraftReadyNotificationAsync(notification);
```

**Replace with:**
```csharp
await _teamsService.SendDraftApprovalNotificationAsync(
    approval.DocumentId,
    approval.JiraNumber,
    approval.AssignedTo);
```

**Verification:** Check that `ITeamsNotificationService` has a method named `SendDraftApprovalNotificationAsync` with these parameters. If not, use whatever method exists that sends approval notifications.

---

## 1.2 Fix AI Model Default

**File:** `src/Api/Services/ComprehensiveMasterIndexService.cs`

**Find this code (around line 48):**
```csharp
private readonly string _deploymentName = "gpt-35-turbo";
```

**Replace with:**
```csharp
private readonly string _deploymentName = "gpt-4.1";
```

**Alternative:** If using configuration, find where deployment name is set and ensure it pulls from config:
```csharp
_deploymentName = config["OpenAI:Deployment"] ?? "gpt-4.1";
```

---

## 1.3 Build and Verify Fixes

```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2
dotnet build src/Api/Api.csproj

# Check for build errors
# If clean build, continue to Step 2
```

---

# STEP 2: Add BusinessDomain Mapping (No AI Required)

## 2.1 Add Method to ComprehensiveMasterIndexService

**File:** `src/Api/Services/ComprehensiveMasterIndexService.cs`

**Add this private method:**

```csharp
/// <summary>
/// Maps schema name to business domain. No AI call required.
/// </summary>
private void PopulateBusinessDomain(MasterIndexMetadata metadata)
{
    if (string.IsNullOrEmpty(metadata.SchemaName))
    {
        metadata.BusinessDomain = "General";
        return;
    }

    var domainMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Policy schemas
        { "gwpc", "Policy Management" },
        { "gwpcDaily", "Policy Management" },
        { "gwpcMonthly", "Policy Management" },
        { "gwpcWeekly", "Policy Management" },
        
        // Control/Reference
        { "gwControl", "Reference Data" },
        
        // Multi-family
        { "mfp", "Multi-Family Policy" },
        
        // Claims
        { "claims", "Claims Processing" },
        { "claimsDaily", "Claims Processing" },
        
        // Financial
        { "billing", "Billing & Finance" },
        { "finance", "Billing & Finance" },
        { "accounting", "Billing & Finance" },
        
        // Data Quality
        { "DaQa", "Data Quality & Analytics" },
        { "daqa", "Data Quality & Analytics" },
        
        // System
        { "dbo", "Core System" },
        { "audit", "System & Audit" },
        { "log", "System & Audit" },
        
        // ETL
        { "staging", "ETL & Staging" },
        { "etl", "ETL & Staging" },
        { "archive", "Archive & History" }
    };

    metadata.BusinessDomain = domainMap.TryGetValue(metadata.SchemaName, out var domain)
        ? domain
        : "General";
}
```

## 2.2 Call the Method

**Find the main population method** (likely `PopulateMasterIndexFromApprovedDocumentAsync` or similar)

**Add this call after schema/table/column are set:**
```csharp
// After setting SchemaName, TableName, ColumnName
PopulateBusinessDomain(metadata);
```

---

# STEP 3: Add PII Detection (No AI Required)

## 3.1 Add Method to ComprehensiveMasterIndexService

**File:** `src/Api/Services/ComprehensiveMasterIndexService.cs`

**Add this private method:**

```csharp
/// <summary>
/// Detects PII based on column name patterns. No AI call required.
/// </summary>
private void DetectPII(MasterIndexMetadata metadata)
{
    // Default to non-PII
    metadata.PIIIndicator = false;
    metadata.ContainsPII = false;
    metadata.SensitivityLevel = "Low";
    metadata.DataClassification = "Internal";

    if (string.IsNullOrEmpty(metadata.ColumnName))
        return;

    var columnLower = metadata.ColumnName.ToLowerInvariant();
    var detectedTypes = new List<string>();

    // PII patterns - add more as needed
    var patterns = new (string Type, string Pattern)[]
    {
        ("SSN", @"(ssn|social.*security|soc.*sec|tax.*id|tin)"),
        ("DOB", @"(birth.*date|dob|date.*birth|birthdate|birth_dt)"),
        ("Email", @"(email|e-mail|e_mail|emailaddr|email_addr)"),
        ("Phone", @"(phone|mobile|cell|telephone|fax|tel_)"),
        ("Address", @"(address|street|city|zip|postal|state|addr_)"),
        ("Name", @"(first.*name|last.*name|full.*name|fname|lname|cust.*name|customer.*name)"),
        ("Account", @"(account.*num|acct.*no|bank.*acct|routing|aba_)"),
        ("License", @"(license|licence|driver.*lic|dl_num|drv_lic)"),
        ("Medical", @"(diagnosis|treatment|medical|health|patient|hipaa)"),
        ("Financial", @"(salary|income|credit.*score|net.*worth|annual.*inc)")
    };

    foreach (var (type, pattern) in patterns)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(
            columnLower, pattern, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            detectedTypes.Add(type);
        }
    }

    if (detectedTypes.Any())
    {
        metadata.PIIIndicator = true;
        metadata.ContainsPII = true;
        metadata.PIITypes = System.Text.Json.JsonSerializer.Serialize(detectedTypes);

        // High sensitivity for critical PII types
        var highSensitivity = new[] { "SSN", "Medical", "Financial", "Account" };
        if (detectedTypes.Any(t => highSensitivity.Contains(t)))
        {
            metadata.SensitivityLevel = "High";
            metadata.DataClassification = "Confidential";
        }
        else
        {
            metadata.SensitivityLevel = "Medium";
            metadata.DataClassification = "Internal";
        }
    }
}
```

## 3.2 Call the Method

**In the main population method, add after PopulateBusinessDomain:**
```csharp
PopulateBusinessDomain(metadata);
DetectPII(metadata);  // ADD THIS
```

---

# STEP 4: Add CompletenessScore Calculation

## 4.1 Add Method to ComprehensiveMasterIndexService

**File:** `src/Api/Services/ComprehensiveMasterIndexService.cs`

**Add this private method:**

```csharp
/// <summary>
/// Calculates completeness score based on populated fields.
/// Call this LAST, after all other population methods.
/// </summary>
private void CalculateCompletenessScore(MasterIndexMetadata metadata)
{
    const int TotalColumns = 119;
    int populated = 0;

    // Use reflection to count non-null, non-empty properties
    var props = metadata.GetType().GetProperties();
    
    var excludeProps = new HashSet<string> 
    { 
        "IndexID", "PopulatedFieldCount", "CompletenessScore", 
        "MetadataCompleteness", "QualityScore", "DataQualityScore"
    };

    foreach (var prop in props)
    {
        if (excludeProps.Contains(prop.Name)) continue;
        
        try
        {
            var value = prop.GetValue(metadata);
            
            if (value != null)
            {
                if (value is string str)
                {
                    if (!string.IsNullOrWhiteSpace(str))
                        populated++;
                }
                else if (value is bool b)
                {
                    populated++; // Bools count as populated even if false
                }
                else
                {
                    populated++;
                }
            }
        }
        catch
        {
            // Skip properties that can't be read
        }
    }

    metadata.CompletenessScore = (int)Math.Round(populated * 100.0 / TotalColumns);
    metadata.MetadataCompleteness = metadata.CompletenessScore;
    metadata.QualityScore = metadata.CompletenessScore; // Simple for now
}
```

## 4.2 Call the Method LAST

**In the main population method, add as the FINAL step before database insert:**
```csharp
// ... all other population logic ...

PopulateBusinessDomain(metadata);
DetectPII(metadata);

// LAST - calculate completeness
CalculateCompletenessScore(metadata);

// Then INSERT into database
```

---

# STEP 5: Add FileSize and FileHash

## 5.1 Add Method to ComprehensiveMasterIndexService

**File:** `src/Api/Services/ComprehensiveMasterIndexService.cs`

**Add using statement at top:**
```csharp
using System.Security.Cryptography;
```

**Add this private method:**

```csharp
/// <summary>
/// Populates file metadata (size and hash) from the document file.
/// </summary>
private async Task PopulateFileMetadataAsync(MasterIndexMetadata metadata, string filePath, CancellationToken ct)
{
    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
    {
        _logger.LogWarning("Cannot populate file metadata - file not found: {Path}", filePath);
        return;
    }

    try
    {
        var fileInfo = new FileInfo(filePath);
        metadata.FileSize = fileInfo.Length;

        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hashBytes = await sha.ComputeHashAsync(stream, ct);
        metadata.FileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to read file metadata for {Path}", filePath);
    }
}
```

## 5.2 Call the Method

**In the main population method, add where you have access to the file path:**
```csharp
await PopulateFileMetadataAsync(metadata, finalDocumentPath, cancellationToken);
```

---

# STEP 6: Build and Test

## 6.1 Build the Project

```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2
dotnet build src/Api/Api.csproj
```

**Expected:** Build succeeds with no errors.

## 6.2 Run the API

```powershell
dotnet run --project src/Api/Api.csproj
```

**Expected:** API starts on port 5195.

## 6.3 Verify with Swagger

1. Open: http://localhost:5195/swagger
2. Check that endpoints are accessible

---

# STEP 7: Test End-to-End Pipeline

## 7.1 Trigger a Document Generation

Option A: Add a test row to Excel spreadsheet with Status = "Completed"

Option B: Insert directly to database:
```sql
INSERT INTO DaQa.DocumentChanges (
    UniqueKey, JiraNumber, Status, ChangeType, 
    TableName, ColumnName, Description, ReportedBy, AssignedTo,
    CreatedAt, UpdatedAt
)
VALUES (
    CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', 'TEST-' + CONVERT(VARCHAR, GETDATE())), 2),
    'TEST-0001',
    'Completed',
    'Enhancement',
    'irf_policy',
    'test_column',
    'Test document for pipeline validation',
    'Integration Test',
    'developer@tfic.com',
    GETUTCDATE(),
    GETUTCDATE()
);
```

## 7.2 Monitor Processing

Watch the console output for:
- ExcelChangeIntegratorService detecting the change
- DocumentChangeWatcherService processing
- DocGeneratorQueueProcessor generating document
- DraftGenerationService creating draft
- Teams notification (if fixed correctly)

## 7.3 Verify MasterIndex Population

```sql
SELECT 
    DocId,
    BusinessDomain,
    PIIIndicator,
    SensitivityLevel,
    DataClassification,
    CompletenessScore,
    FileSize,
    FileHash,
    CreatedDate
FROM DaQa.MasterIndex
WHERE DocId LIKE 'TEST-%' OR CreatedDate > DATEADD(HOUR, -1, GETUTCDATE())
ORDER BY CreatedDate DESC;
```

**Expected:** New columns should be populated.

---

# STEP 8: Validation Checklist

## 8.1 Code Changes Verification

| File | Change | Status |
|------|--------|--------|
| ApprovalTrackingService.cs | Teams method fix | ☐ |
| ComprehensiveMasterIndexService.cs | Model default fix | ☐ |
| ComprehensiveMasterIndexService.cs | PopulateBusinessDomain added | ☐ |
| ComprehensiveMasterIndexService.cs | DetectPII added | ☐ |
| ComprehensiveMasterIndexService.cs | CalculateCompletenessScore added | ☐ |
| ComprehensiveMasterIndexService.cs | PopulateFileMetadataAsync added | ☐ |

## 8.2 Runtime Verification

| Test | Expected | Status |
|------|----------|--------|
| API starts | Port 5195 listening | ☐ |
| Build succeeds | No errors | ☐ |
| Test document created | File exists in Drafts folder | ☐ |
| MasterIndex populated | New record with BusinessDomain set | ☐ |
| CompletenessScore > 30 | Higher than before | ☐ |
| Teams notification sent | No method mismatch error | ☐ |

---

# STEP 9: Report Results

After completing all steps, report:

1. **Build Status:** Success/Failure + any errors
2. **Files Modified:** List of files changed
3. **Test Results:** 
   - Document generated? Y/N
   - MasterIndex record created? Y/N
   - BusinessDomain populated? Y/N
   - CompletenessScore value?
4. **Errors Encountered:** Any exceptions or issues
5. **Teams Notification:** Sent successfully? Y/N

---

# TROUBLESHOOTING

## Build Errors

**Missing using statements:**
```csharp
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
```

**Property doesn't exist on MasterIndexMetadata:**
Check the actual property names in your `MasterIndexMetadata` class. They may differ slightly (e.g., `PIIFlag` vs `PIIIndicator`).

## Runtime Errors

**Teams notification still fails:**
Search for all methods in `ITeamsNotificationService` and use the correct one.

**AI calls timeout:**
The quick wins (Steps 2-5) don't require AI. If AI phases are slow, they can be disabled temporarily.

## Database Errors

**Column doesn't exist:**
Verify MasterIndex table has all 119 columns. Run:
```sql
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'DaQa' AND TABLE_NAME = 'MasterIndex'
ORDER BY ORDINAL_POSITION;
```

---

# NEXT STEPS (After This Works)

1. **Add AI-powered metadata** (SemanticCategory, AIGeneratedTags)
2. **Add CustomProperties shadow metadata** to documents
3. **Add keyword extraction** from document content
4. **Implement search index** for discovery

These are covered in the metadata-extraction-strategy skill.
