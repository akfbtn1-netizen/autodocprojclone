# Troubleshooting Guide

## Issue Status Summary (Updated)

| Issue | Status | Action |
|-------|--------|--------|
| Port 5195 conflict | ✅ RESOLVED | Fixed - kill process if recurs |
| Teams notification mismatch | ❌ BROKEN | Needs code fix |
| AI content quality | 🔄 IMPROVING | Ongoing prompt refinement |
| Model mismatch (gpt-35-turbo) | ❌ NOT FIXED | ComprehensiveMasterIndexService:48 |
| Excel writeback disabled | ✅ BY DESIGN | Prevents file corruption |

---

## Critical Issues

### 1. Port 5195 Already In Use

**Status:** ✅ RESOLVED (but may recur)

**Symptom:**
```
System.IO.IOException: Failed to bind to address http://127.0.0.1:5195: address already in use.
```

**Cause:** Another instance of the API is running, or a previous instance didn't shut down cleanly.

**Solution:**
```powershell
# Find the process using port 5195
netstat -ano | findstr :5195

# Kill the process (replace <PID> with actual process ID)
taskkill /PID <PID> /F

# Or use PowerShell one-liner
Get-NetTCPConnection -LocalPort 5195 | Select-Object -Property OwningProcess | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }
```

**Alternative:** Change port in `launchSettings.json`:
```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://localhost:5196"
    }
  }
}
```

---

### 2. Background Services Fail With TaskCanceledException

**Symptom:**
```
BackgroundService failed
System.Threading.Tasks.TaskCanceledException: A task was canceled.
```

**Cause:** This is a cascade failure from the port issue. When the API host fails to start, all BackgroundServices receive cancellation.

**Solution:** Fix the port issue first. The services will start normally once the host binds successfully.

---

### 3. Excel File Corruption / Writeback Issues

**Symptom:** Excel file becomes corrupted or DocId not appearing in Excel.

**Cause:** Concurrent access to Excel file while it's open in Excel application.

**Solution:** Excel writeback is intentionally DISABLED in `ExcelChangeIntegratorService.cs` lines 383-384:
```csharp
_logger.LogInformation("Excel writeback DISABLED - preventing file corruption");
return; // EXIT IMMEDIATELY
```

**Workaround:** DocId is stored in database and can be retrieved from there.

---

### 4. Teams Notification Method Mismatch

**Symptom:** Teams notifications not sending from ApprovalTrackingService.

**Cause:** Interface mismatch between services:
```csharp
// TeamsNotificationService has:
Task SendDraftApprovalNotificationAsync(string docId, string jiraNumber, string assignedTo)

// ApprovalTrackingService calls:
await _teamsService.SendDraftReadyNotificationAsync(notification); // WRONG METHOD!
```

**Solution:** Update ApprovalTrackingService to use correct method signature:
```csharp
await _teamsService.SendDraftApprovalNotificationAsync(
    docDetails.DocId,
    docDetails.JiraNumber ?? "N/A",
    approval.RequestedBy ?? "Unknown");
```

---

### 5. AI Content Quality Issues

**Symptom:** Generated documentation has generic, shallow content.

**Causes:**
1. Generic system prompt without domain context
2. MaxTokens too low (1500) for complex procedures
3. No few-shot examples of good documentation
4. Old API version being used

**Solutions:**

1. **Add domain context to prompt:**
```csharp
var systemPrompt = @"You are documenting database changes for Tennessee Farmers Insurance, 
a property and casualty insurance company. Focus on policy lifecycle, premium calculations, 
agent commissions, and financial reporting.";
```

2. **Increase MaxTokens:**
```csharp
MaxTokens = 3000 // For complex procedures
```

3. **Update API version in OpenAIEnhancementService:**
```csharp
// Change from:
api-version=2023-12-01-preview
// To:
api-version=2024-08-01-preview
```

---

### 6. Metadata Not Populating

**Symptom:** MasterIndex records created but many fields are NULL.

**Causes:**
1. ComprehensiveMasterIndexService defaults to wrong model (gpt-35-turbo)
2. AI inference failures silently logged
3. Field mapping mismatches

**Diagnosis:**
```powershell
# Check logs for inference failures
Select-String -Path "*.log" -Pattern "AI metadata inference failed"
```

**Solution:** Update model in config:
```json
"AzureOpenAI": {
  "Model": "gpt-4.1"
}
```

---

### 7. Document Generation Fails Silently

**Symptom:** Queue shows "Completed" but no document generated.

**Cause:** Python template execution fails but error is caught.

**Diagnosis:**
1. Check `C:\Temp\Documentation-Catalog\Drafts\` for files
2. Look for Python errors in logs:
```powershell
Select-String -Path "*.log" -Pattern "Failed to generate Word document"
```

**Common Python Issues:**
- `python-docx` not installed: `pip install python-docx`
- Template file missing from `C:\Projects\EnterpriseDocumentationPlatform.V2\Templates\`
- JSON data file write permission denied

---

### 8. BAS Marker Not Found

**Symptom:** Code extraction returns empty, no marked code in document.

**Cause:** BAS markers in stored procedure don't match expected format.

**Expected Format:**
```sql
-- Begin BAS-9818
[code changes here]
-- End BAS-9818
```

**Regex Pattern Used:**
```csharp
@"-{1,}\s*Begin\s*\[?\s*BAS\s*-?\s*{ticketNumber}\s*\]?"
```

**Common Issues:**
- Missing dashes: `Begin BAS-9818` (needs `-- Begin BAS-9818`)
- Wrong ticket format: `BAS9818` vs `BAS-9818`
- Extra spaces or characters

---

### 9. ApprovalWorkflow Entry Not Created

**Symptom:** Draft generated but not appearing in approval page.

**Cause:** `CreateApprovalWorkflowEntryAsync` in DocGeneratorQueueProcessor failed.

**Diagnosis:**
```sql
SELECT TOP 10 * FROM DaQa.ApprovalWorkflow ORDER BY RequestedDate DESC;
```

**Common Causes:**
- Database connection timeout
- Missing required columns in INSERT
- Duplicate key violation

---

### 10. Configuration Not Loading

**Symptom:** Services use default values instead of config file values.

**Cause:** Wrong environment configuration loaded.

**Check Environment:**
```powershell
echo $env:ASPNETCORE_ENVIRONMENT
```

**appsettings Loading Order:**
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (overrides)

**Force Development:**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

---

## Diagnostic Queries

### Check Recent Document Changes
```sql
SELECT TOP 20 DocId, JiraNumber, Status, TableName, ChangeType, CreatedAt
FROM DaQa.DocumentChanges
ORDER BY CreatedAt DESC;
```

### Check Queue Status
```sql
SELECT Status, COUNT(*) as Count
FROM DaQa.DocumentationQueue
GROUP BY Status;

SELECT TOP 10 * FROM DaQa.DocumentationQueue WHERE Status = 'Failed';
```

### Check Approval Status
```sql
SELECT ApprovalStatus, COUNT(*) as Count
FROM DaQa.ApprovalWorkflow
GROUP BY ApprovalStatus;
```

### Check Workflow Events
```sql
SELECT TOP 50 EventType, Status, Message, Timestamp
FROM DaQa.WorkflowEvents
ORDER BY Timestamp DESC;
```

### Check MasterIndex Completeness
```sql
SELECT 
    DocId,
    CASE WHEN BusinessDefinition IS NOT NULL THEN 1 ELSE 0 END +
    CASE WHEN TechnicalDefinition IS NOT NULL THEN 1 ELSE 0 END +
    CASE WHEN Keywords IS NOT NULL THEN 1 ELSE 0 END as PopulatedFields,
    MetadataCompleteness
FROM DaQa.MasterIndex
ORDER BY CreatedDate DESC;
```

---

## Service Health Checks

### Verify All Services Running
When API starts successfully, you should see:
```
ExcelChangeIntegrator started. Watching: [path]
Poll interval: 1 minutes
DocumentChangeWatcher started. Poll interval: 1 minutes
DocGeneratorQueueProcessor started with 00:01:00 polling interval
```

### Test API Endpoints
```powershell
# Health check
curl http://localhost:5195/weatherforecast

# Swagger UI
Start-Process "http://localhost:5195/swagger"

# Pending approvals
curl http://localhost:5195/api/approvals/pending
```

---

## Log Locations

| Log Type | Location |
|----------|----------|
| Console Output | Terminal where `dotnet run` executed |
| Application Insights | Azure portal (if configured) |
| Workflow Events | `DaQa.WorkflowEvents` table |
| Notification Log | `DaQa.TeamsNotificationLog` table |
