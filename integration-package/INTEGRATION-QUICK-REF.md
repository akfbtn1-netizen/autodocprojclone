# V2 Integration Quick Reference

## Project Location
```
C:\Projects\EnterpriseDocumentationPlatform.V2
```

## Key Files to Modify

| Step | File | Path |
|------|------|------|
| 1.1 | ApprovalTrackingService.cs | src/Api/Services/ |
| 1.2-5 | ComprehensiveMasterIndexService.cs | src/Api/Services/ |

## Commands

```powershell
# Build
cd C:\Projects\EnterpriseDocumentationPlatform.V2
dotnet build src/Api/Api.csproj

# Run
dotnet run --project src/Api/Api.csproj

# Test API
Start-Process "http://localhost:5195/swagger"
```

## Database Connection
```
Server: ibidb2003dv
Database: IRFS1
Schema: DaQa
Auth: Windows Integrated
```

## Execution Order

```
1. Fix Teams method      → ApprovalTrackingService.cs
2. Fix model default     → ComprehensiveMasterIndexService.cs (line ~48)
3. Add BusinessDomain    → ComprehensiveMasterIndexService.cs (new method)
4. Add PII detection     → ComprehensiveMasterIndexService.cs (new method)
5. Add Completeness      → ComprehensiveMasterIndexService.cs (new method)
6. Add FileHash          → ComprehensiveMasterIndexService.cs (new method)
7. Build & Test          → dotnet build / dotnet run
8. Verify                → Query MasterIndex
```

## Validation Query

```sql
SELECT 
    DocId,
    BusinessDomain,
    PIIIndicator,
    CompletenessScore,
    CreatedDate
FROM DaQa.MasterIndex
ORDER BY CreatedDate DESC;
```

## Expected Improvements

| Metric | Before | After |
|--------|--------|-------|
| BusinessDomain | NULL | "Policy Management" etc. |
| PIIIndicator | NULL | true/false |
| CompletenessScore | ~30 | ~45-50 |
| FileSize | NULL | Populated |
| FileHash | NULL | SHA256 hash |

## Success Criteria

- [ ] Build succeeds
- [ ] API starts on 5195
- [ ] No Teams method mismatch error
- [ ] BusinessDomain populated on new records
- [ ] CompletenessScore > 40
