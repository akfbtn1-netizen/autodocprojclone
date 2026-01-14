# ============================================================================
# Copy Batch Processing System Files to Local V2 Project (Windows Version)
# ============================================================================
# Run this script on your Windows PC to copy all batch processing files
# from the cloned Git repository to your local V2 project
# ============================================================================

$ErrorActionPreference = "Stop"

# ============================================================================
# CONFIGURATION - Update these paths for your environment
# ============================================================================

# Path to your cloned autodocprojclone repository
$sourceRoot = "C:\Path\To\autodocprojclone"

# Path to your V2 project
$destRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

# ============================================================================
# DO NOT EDIT BELOW THIS LINE
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Batch Processing System - File Copy Script (Windows)" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Source: $sourceRoot" -ForegroundColor Yellow
Write-Host "Destination: $destRoot" -ForegroundColor Yellow
Write-Host ""

# Verify source exists
if (-not (Test-Path $sourceRoot)) {
    Write-Host "ERROR: Source directory not found: $sourceRoot" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please update the `$sourceRoot variable at the top of this script." -ForegroundColor Red
    Write-Host "It should point to your cloned autodocprojclone repository." -ForegroundColor Red
    Write-Host ""
    Write-Host "Example: `$sourceRoot = `"C:\Git\autodocprojclone`"" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Verify destination exists
if (-not (Test-Path $destRoot)) {
    Write-Host "ERROR: Destination directory not found: $destRoot" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please update the `$destRoot variable at the top of this script." -ForegroundColor Red
    Write-Host "It should point to your V2 project directory." -ForegroundColor Red
    Write-Host ""
    Write-Host "Example: `$destRoot = `"C:\Projects\EnterpriseDocumentationPlatform.V2`"" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# File mapping: source -> destination (relative paths)
$filesToCopy = @(
    # Domain Entities
    @{
        RelPath = "src\Core\Domain\Entities\BatchJob.cs"
        Category = "Domain Entities"
        Description = "BatchJob entity with multi-source support"
    },
    @{
        RelPath = "src\Core\Domain\Entities\BatchJobItem.cs"
        Category = "Domain Entities"
        Description = "BatchJobItem entity with confidence tracking"
    },

    # Metadata Extraction Service
    @{
        RelPath = "src\Core\Application\Services\MetadataExtraction\IMetadataExtractionService.cs"
        Category = "Metadata Extraction"
        Description = "Metadata extraction service interface"
    },
    @{
        RelPath = "src\Core\Application\Services\MetadataExtraction\MetadataExtractionService.cs"
        Category = "Metadata Extraction"
        Description = "Metadata extraction implementation (830 lines)"
    },

    # Batch Processing Orchestrator
    @{
        RelPath = "src\Core\Application\Services\Batch\IBatchProcessingOrchestrator.cs"
        Category = "Batch Orchestrator"
        Description = "Batch processing orchestrator interface"
    },
    @{
        RelPath = "src\Core\Application\Services\Batch\BatchProcessingOrchestrator.cs"
        Category = "Batch Orchestrator"
        Description = "Batch orchestrator implementation (1,050 lines)"
    },

    # Vector Indexing Service
    @{
        RelPath = "src\Core\Application\Services\VectorIndexing\IVectorIndexingService.cs"
        Category = "Vector Indexing"
        Description = "Vector indexing service interface"
    },
    @{
        RelPath = "src\Core\Application\Services\VectorIndexing\VectorIndexingService.cs"
        Category = "Vector Indexing"
        Description = "Vector indexing with Pinecone/Weaviate (650 lines)"
    },

    # API Layer
    @{
        RelPath = "src\Api\Controllers\BatchProcessingController.cs"
        Category = "API Controllers"
        Description = "REST API controller with 10 endpoints"
    },
    @{
        RelPath = "src\Api\Configuration\HangfireConfiguration.cs"
        Category = "API Configuration"
        Description = "Hangfire background job configuration"
    },

    # Database
    @{
        RelPath = "sql\CREATE_BatchProcessing_Tables.sql"
        Category = "Database Schema"
        Description = "SQL migration with tables, views, procedures"
    },

    # Documentation
    @{
        RelPath = "docs\BATCH-PROCESSING-SETUP.md"
        Category = "Documentation"
        Description = "Complete setup guide with examples"
    },
    @{
        RelPath = "docs\BATCH-SYSTEM-SUMMARY.md"
        Category = "Documentation"
        Description = "System architecture and features summary"
    }
)

# Statistics
$totalFiles = $filesToCopy.Count
$copiedFiles = 0
$failedFiles = 0
$skippedFiles = 0
$totalBytes = 0

Write-Host "Files to copy: $totalFiles" -ForegroundColor Cyan
Write-Host ""

# Copy files
$fileNumber = 1
foreach ($file in $filesToCopy) {
    $relPath = $file.RelPath
    $sourcePath = Join-Path $sourceRoot $relPath
    $destPath = Join-Path $destRoot $relPath
    $category = $file.Category
    $description = $file.Description
    $fileName = Split-Path $destPath -Leaf

    Write-Host "[$fileNumber/$totalFiles] " -ForegroundColor Cyan -NoNewline
    Write-Host "[$category] " -ForegroundColor Magenta -NoNewline
    Write-Host "$fileName" -ForegroundColor White
    Write-Host "  $description" -ForegroundColor Gray

    # Check if source exists
    if (-not (Test-Path $sourcePath)) {
        Write-Host "  [WARNING] Source file not found: $sourcePath" -ForegroundColor Yellow
        $skippedFiles++
        $fileNumber++
        Write-Host ""
        continue
    }

    # Create destination directory if it doesn't exist
    $destDir = Split-Path $destPath -Parent
    if (-not (Test-Path $destDir)) {
        Write-Host "  Creating directory: $destDir" -ForegroundColor Gray
        try {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        catch {
            Write-Host "  [ERROR] Failed to create directory: $_" -ForegroundColor Red
            $failedFiles++
            $fileNumber++
            Write-Host ""
            continue
        }
    }

    try {
        # Copy file
        Copy-Item -Path $sourcePath -Destination $destPath -Force

        # Verify copy
        if (Test-Path $destPath) {
            $sourceSize = (Get-Item $sourcePath).Length
            $destSize = (Get-Item $destPath).Length

            if ($sourceSize -eq $destSize) {
                $sizeKB = [math]::Round($sourceSize / 1KB, 2)
                Write-Host "  [OK] Copied successfully ($sizeKB KB)" -ForegroundColor Green
                $copiedFiles++
                $totalBytes += $sourceSize
            } else {
                Write-Host "  [WARNING] Size mismatch (source: $sourceSize, dest: $destSize)" -ForegroundColor Yellow
                $copiedFiles++
                $totalBytes += $destSize
            }
        } else {
            Write-Host "  [ERROR] File not found after copy" -ForegroundColor Red
            $failedFiles++
        }
    }
    catch {
        Write-Host "  [ERROR] Copy failed: $_" -ForegroundColor Red
        $failedFiles++
    }

    $fileNumber++
    Write-Host ""
}

# Summary
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Copy Summary" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

$totalSizeKB = [math]::Round($totalBytes / 1KB, 2)
Write-Host "Total Files:    $totalFiles" -ForegroundColor White
Write-Host "Copied:         $copiedFiles" -ForegroundColor Green
Write-Host "Failed:         $failedFiles" -ForegroundColor $(if ($failedFiles -gt 0) { "Red" } else { "Gray" })
Write-Host "Skipped:        $skippedFiles" -ForegroundColor $(if ($skippedFiles -gt 0) { "Yellow" } else { "Gray" })
Write-Host "Total Size:     $totalSizeKB KB" -ForegroundColor White
Write-Host ""

if ($copiedFiles -eq $totalFiles) {
    Write-Host "SUCCESS: All files copied successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host "Next Steps" -ForegroundColor Cyan
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Open Visual Studio" -ForegroundColor Yellow
    Write-Host "   - Open the solution: $destRoot\EnterpriseDocumentationPlatform.V2.sln" -ForegroundColor White
    Write-Host "   - Right-click solution -> Reload" -ForegroundColor White
    Write-Host ""
    Write-Host "2. Run SQL Migration" -ForegroundColor Yellow
    Write-Host "   - Open SSMS (SQL Server Management Studio)" -ForegroundColor White
    Write-Host "   - Connect to: (localdb)\mssqllocaldb" -ForegroundColor White
    Write-Host "   - Open file: $destRoot\sql\CREATE_BatchProcessing_Tables.sql" -ForegroundColor White
    Write-Host "   - Execute against database: IRFS1" -ForegroundColor White
    Write-Host ""
    Write-Host "3. Update Program.cs" -ForegroundColor Yellow
    Write-Host "   - Open file: $destRoot\src\Api\Program.cs" -ForegroundColor White
    Write-Host "   - Follow instructions in: $destRoot\docs\BATCH-PROCESSING-SETUP.md" -ForegroundColor White
    Write-Host "   - Add service registrations (lines ~105-130)" -ForegroundColor White
    Write-Host ""
    Write-Host "4. Configure appsettings.json" -ForegroundColor Yellow
    Write-Host "   - Open file: $destRoot\src\Api\appsettings.json" -ForegroundColor White
    Write-Host "   - Add OpenAI API key" -ForegroundColor White
    Write-Host "   - Add Pinecone API key and endpoint" -ForegroundColor White
    Write-Host "   - Configure Hangfire settings" -ForegroundColor White
    Write-Host ""
    Write-Host "5. Install NuGet Packages (if not already installed)" -ForegroundColor Yellow
    Write-Host "   - Hangfire.AspNetCore" -ForegroundColor White
    Write-Host "   - Hangfire.SqlServer" -ForegroundColor White
    Write-Host "   - DocumentFormat.OpenXml" -ForegroundColor White
    Write-Host "   - Dapper" -ForegroundColor White
    Write-Host ""
    Write-Host "6. Test the System" -ForegroundColor Yellow
    Write-Host "   - Build and run the API project" -ForegroundColor White
    Write-Host "   - Navigate to: http://localhost:5195/swagger" -ForegroundColor White
    Write-Host "   - Test endpoint: POST /api/batchprocessing/schema" -ForegroundColor White
    Write-Host "   - Access Hangfire dashboard: http://localhost:5195/hangfire" -ForegroundColor White
    Write-Host ""
    Write-Host "============================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Documentation:" -ForegroundColor Cyan
    Write-Host "  Setup Guide:  $destRoot\docs\BATCH-PROCESSING-SETUP.md" -ForegroundColor White
    Write-Host "  System Summary: $destRoot\docs\BATCH-SYSTEM-SUMMARY.md" -ForegroundColor White
    Write-Host ""
} elseif ($failedFiles -gt 0) {
    Write-Host "WARNING: Some files failed to copy. Please review the errors above." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "- File is open in Visual Studio or another editor" -ForegroundColor White
    Write-Host "- Insufficient permissions" -ForegroundColor White
    Write-Host "- Destination path is too long" -ForegroundColor White
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
} else {
    Write-Host "PARTIAL SUCCESS: $copiedFiles/$totalFiles files copied." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# File breakdown by category
Write-Host "Files Copied by Category:" -ForegroundColor Cyan
$filesToCopy | Group-Object Category | ForEach-Object {
    $categoryCount = $_.Count
    $categoryName = $_.Name
    Write-Host "  $categoryName" -ForegroundColor Magenta -NoNewline
    Write-Host ": $categoryCount files" -ForegroundColor Gray
}
Write-Host ""

Write-Host "Source:      $sourceRoot" -ForegroundColor Gray
Write-Host "Destination: $destRoot" -ForegroundColor Gray
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Pause before closing
Read-Host "Press Enter to close this window"
