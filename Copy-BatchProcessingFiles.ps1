# ============================================================================
# Copy Batch Processing System Files to Local V2 Project
# ============================================================================
# This script copies all batch processing files from autodocprojclone
# to your local EnterpriseDocumentationPlatform.V2 project
# ============================================================================

$ErrorActionPreference = "Stop"

# Define paths
$sourceRoot = "/home/user/autodocprojclone"
$destRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Batch Processing System - File Copy Script" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Source: $sourceRoot" -ForegroundColor Yellow
Write-Host "Destination: $destRoot" -ForegroundColor Yellow
Write-Host ""

# Verify destination exists
if (-not (Test-Path $destRoot)) {
    Write-Host "ERROR: Destination directory not found: $destRoot" -ForegroundColor Red
    Write-Host "Please update the `$destRoot variable in this script." -ForegroundColor Red
    exit 1
}

# File mapping: source -> destination
$filesToCopy = @(
    # Domain Entities
    @{
        Source = "$sourceRoot/src/Core/Domain/Entities/BatchJob.cs"
        Dest = "$destRoot/src/Core/Domain/Entities/BatchJob.cs"
        Category = "Domain Entities"
    },
    @{
        Source = "$sourceRoot/src/Core/Domain/Entities/BatchJobItem.cs"
        Dest = "$destRoot/src/Core/Domain/Entities/BatchJobItem.cs"
        Category = "Domain Entities"
    },

    # Metadata Extraction Service
    @{
        Source = "$sourceRoot/src/Core/Application/Services/MetadataExtraction/IMetadataExtractionService.cs"
        Dest = "$destRoot/src/Core/Application/Services/MetadataExtraction/IMetadataExtractionService.cs"
        Category = "Metadata Extraction"
    },
    @{
        Source = "$sourceRoot/src/Core/Application/Services/MetadataExtraction/MetadataExtractionService.cs"
        Dest = "$destRoot/src/Core/Application/Services/MetadataExtraction/MetadataExtractionService.cs"
        Category = "Metadata Extraction"
    },

    # Batch Processing Orchestrator
    @{
        Source = "$sourceRoot/src/Core/Application/Services/Batch/IBatchProcessingOrchestrator.cs"
        Dest = "$destRoot/src/Core/Application/Services/Batch/IBatchProcessingOrchestrator.cs"
        Category = "Batch Orchestrator"
    },
    @{
        Source = "$sourceRoot/src/Core/Application/Services/Batch/BatchProcessingOrchestrator.cs"
        Dest = "$destRoot/src/Core/Application/Services/Batch/BatchProcessingOrchestrator.cs"
        Category = "Batch Orchestrator"
    },

    # Vector Indexing Service
    @{
        Source = "$sourceRoot/src/Core/Application/Services/VectorIndexing/IVectorIndexingService.cs"
        Dest = "$destRoot/src/Core/Application/Services/VectorIndexing/IVectorIndexingService.cs"
        Category = "Vector Indexing"
    },
    @{
        Source = "$sourceRoot/src/Core/Application/Services/VectorIndexing/VectorIndexingService.cs"
        Dest = "$destRoot/src/Core/Application/Services/VectorIndexing/VectorIndexingService.cs"
        Category = "Vector Indexing"
    },

    # API Layer
    @{
        Source = "$sourceRoot/src/Api/Controllers/BatchProcessingController.cs"
        Dest = "$destRoot/src/Api/Controllers/BatchProcessingController.cs"
        Category = "API Controllers"
    },
    @{
        Source = "$sourceRoot/src/Api/Configuration/HangfireConfiguration.cs"
        Dest = "$destRoot/src/Api/Configuration/HangfireConfiguration.cs"
        Category = "API Configuration"
    },

    # Database
    @{
        Source = "$sourceRoot/sql/CREATE_BatchProcessing_Tables.sql"
        Dest = "$destRoot/sql/CREATE_BatchProcessing_Tables.sql"
        Category = "Database Schema"
    },

    # Documentation
    @{
        Source = "$sourceRoot/docs/BATCH-PROCESSING-SETUP.md"
        Dest = "$destRoot/docs/BATCH-PROCESSING-SETUP.md"
        Category = "Documentation"
    },
    @{
        Source = "$sourceRoot/docs/BATCH-SYSTEM-SUMMARY.md"
        Dest = "$destRoot/docs/BATCH-SYSTEM-SUMMARY.md"
        Category = "Documentation"
    }
)

# Statistics
$totalFiles = $filesToCopy.Count
$copiedFiles = 0
$failedFiles = 0
$skippedFiles = 0

Write-Host "Files to copy: $totalFiles" -ForegroundColor Cyan
Write-Host ""

# Copy files
foreach ($file in $filesToCopy) {
    $sourcePath = $file.Source
    $destPath = $file.Dest
    $category = $file.Category
    $fileName = Split-Path $destPath -Leaf

    Write-Host "[$category] " -ForegroundColor Magenta -NoNewline
    Write-Host "$fileName" -ForegroundColor White

    # Check if source exists
    if (-not (Test-Path $sourcePath)) {
        Write-Host "  [WARNING] Source file not found: $sourcePath" -ForegroundColor Yellow
        $skippedFiles++
        continue
    }

    # Create destination directory if it doesn't exist
    $destDir = Split-Path $destPath -Parent
    if (-not (Test-Path $destDir)) {
        Write-Host "  Creating directory: $destDir" -ForegroundColor Gray
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    try {
        # Copy file
        Copy-Item -Path $sourcePath -Destination $destPath -Force

        # Verify copy
        if (Test-Path $destPath) {
            $sourceSize = (Get-Item $sourcePath).Length
            $destSize = (Get-Item $destPath).Length

            if ($sourceSize -eq $destSize) {
                Write-Host "  [OK] Copied successfully ($sourceSize bytes)" -ForegroundColor Green
                $copiedFiles++
            } else {
                Write-Host "  [WARNING] Size mismatch (source: $sourceSize, dest: $destSize)" -ForegroundColor Yellow
                $copiedFiles++
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

    Write-Host ""
}

# Summary
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Copy Summary" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Files:    $totalFiles" -ForegroundColor White
Write-Host "Copied:         $copiedFiles" -ForegroundColor Green
Write-Host "Failed:         $failedFiles" -ForegroundColor $(if ($failedFiles -gt 0) { "Red" } else { "Gray" })
Write-Host "Skipped:        $skippedFiles" -ForegroundColor $(if ($skippedFiles -gt 0) { "Yellow" } else { "Gray" })
Write-Host ""

if ($copiedFiles -eq $totalFiles) {
    Write-Host "SUCCESS: All files copied successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Open Visual Studio and reload the solution" -ForegroundColor White
    Write-Host "2. Run the SQL migration: sql/CREATE_BatchProcessing_Tables.sql" -ForegroundColor White
    Write-Host "3. Follow setup instructions in: docs/BATCH-PROCESSING-SETUP.md" -ForegroundColor White
    Write-Host "4. Update Program.cs with service registrations" -ForegroundColor White
    Write-Host "5. Configure appsettings.json with OpenAI/Pinecone keys" -ForegroundColor White
    Write-Host ""
} elseif ($failedFiles -gt 0) {
    Write-Host "WARNING: Some files failed to copy. Please review the errors above." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "PARTIAL SUCCESS: $copiedFiles/$totalFiles files copied." -ForegroundColor Yellow
    exit 1
}

# File breakdown by category
Write-Host "Files Copied by Category:" -ForegroundColor Cyan
$filesToCopy | Group-Object Category | ForEach-Object {
    Write-Host "  $($_.Name): $($_.Count) files" -ForegroundColor Gray
}
Write-Host ""

Write-Host "Destination: $destRoot" -ForegroundColor Yellow
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
