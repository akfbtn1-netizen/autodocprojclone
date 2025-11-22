# Copy Batch Processing Files to Local PC

## Quick Start

### Step 1: Clone or Pull Latest Changes

```bash
cd C:\Git
git clone <repo-url> autodocprojclone
# OR if already cloned:
cd autodocprojclone
git pull origin claude/add-sync-service-logging-01P3rHxqMWFpWvVAk7WXSUUw
```

### Step 2: Update PowerShell Script

Edit `Copy-BatchProcessingFiles-Windows.ps1` and update these lines:

```powershell
# Path to your cloned repository
$sourceRoot = "C:\Git\autodocprojclone"  # <-- UPDATE THIS

# Path to your V2 project
$destRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"  # <-- UPDATE THIS
```

### Step 3: Run PowerShell Script

**Option A: Right-click and Run**
1. Right-click `Copy-BatchProcessingFiles-Windows.ps1`
2. Select "Run with PowerShell"

**Option B: Run from PowerShell**
```powershell
cd C:\Git\autodocprojclone
.\Copy-BatchProcessingFiles-Windows.ps1
```

**Option C: Run with Execution Policy Bypass** (if you get execution policy errors)
```powershell
PowerShell -ExecutionPolicy Bypass -File ".\Copy-BatchProcessingFiles-Windows.ps1"
```

---

## What Gets Copied

The script copies **13 files** to your local V2 project:

### Domain Entities (2 files)
- `src/Core/Domain/Entities/BatchJob.cs`
- `src/Core/Domain/Entities/BatchJobItem.cs`

### Application Services (6 files)
- `src/Core/Application/Services/MetadataExtraction/IMetadataExtractionService.cs`
- `src/Core/Application/Services/MetadataExtraction/MetadataExtractionService.cs`
- `src/Core/Application/Services/Batch/IBatchProcessingOrchestrator.cs`
- `src/Core/Application/Services/Batch/BatchProcessingOrchestrator.cs`
- `src/Core/Application/Services/VectorIndexing/IVectorIndexingService.cs`
- `src/Core/Application/Services/VectorIndexing/VectorIndexingService.cs`

### API Layer (2 files)
- `src/Api/Controllers/BatchProcessingController.cs`
- `src/Api/Configuration/HangfireConfiguration.cs`

### Database (1 file)
- `sql/CREATE_BatchProcessing_Tables.sql`

### Documentation (2 files)
- `docs/BATCH-PROCESSING-SETUP.md`
- `docs/BATCH-SYSTEM-SUMMARY.md`

**Total**: 5,614 lines of code

---

## After Copying

Once the files are copied, follow these steps:

### 1. Reload Visual Studio Solution
- Open `EnterpriseDocumentationPlatform.V2.sln`
- Right-click solution → Reload

### 2. Run SQL Migration
```sql
-- In SSMS, connect to (localdb)\mssqllocaldb
-- Open: sql/CREATE_BatchProcessing_Tables.sql
-- Execute against database: IRFS1
```

### 3. Install NuGet Packages
```powershell
Install-Package Hangfire.AspNetCore
Install-Package Hangfire.SqlServer
Install-Package DocumentFormat.OpenXml
Install-Package Dapper
```

### 4. Update Program.cs
Follow instructions in `docs/BATCH-PROCESSING-SETUP.md` to add service registrations.

### 5. Configure appsettings.json
Add configuration for OpenAI, Pinecone, and Hangfire.

### 6. Test
- Run the API project
- Navigate to: `http://localhost:5195/swagger`
- Access Hangfire dashboard: `http://localhost:5195/hangfire`

---

## Troubleshooting

### "Execution Policy" Error

If you see:
```
File cannot be loaded because running scripts is disabled on this system
```

**Solution**:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Or run with bypass:
```powershell
PowerShell -ExecutionPolicy Bypass -File ".\Copy-BatchProcessingFiles-Windows.ps1"
```

### "Source Directory Not Found"

Update the `$sourceRoot` variable in the PowerShell script to point to your cloned repository.

### "Destination Directory Not Found"

Update the `$destRoot` variable in the PowerShell script to point to your V2 project.

### Files Already Exist / File in Use

- Close Visual Studio
- Close any editors that have the files open
- Re-run the script

### Permission Denied

- Run PowerShell as Administrator
- Check file permissions

---

## Script Features

✅ **Automatic Directory Creation**: Creates missing directories
✅ **Progress Reporting**: Shows detailed progress for each file
✅ **Size Verification**: Verifies file sizes match after copy
✅ **Category Grouping**: Organizes files by component
✅ **Error Handling**: Reports any failures
✅ **Next Steps Guide**: Shows what to do after copying

---

## Manual Copy (Alternative)

If you prefer to copy manually:

1. **Domain Entities**: Copy 2 files from `src/Core/Domain/Entities/`
2. **Services**: Copy 6 files from `src/Core/Application/Services/`
3. **API**: Copy 2 files from `src/Api/`
4. **Database**: Copy 1 file from `sql/`
5. **Docs**: Copy 2 files from `docs/`

---

## Need Help?

- **Setup Guide**: `docs/BATCH-PROCESSING-SETUP.md`
- **System Summary**: `docs/BATCH-SYSTEM-SUMMARY.md`
- **Git Branch**: `claude/add-sync-service-logging-01P3rHxqMWFpWvVAk7WXSUUw`

---

**Last Updated**: November 21, 2025
**Total Files**: 13
**Total Size**: ~200 KB
**Total Lines**: 5,614
