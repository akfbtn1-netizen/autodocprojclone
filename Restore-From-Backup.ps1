# ============================================================================
# Restore Files from Backup
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host "Restoring files from backup..." -ForegroundColor Cyan
Write-Host ""

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

$files = @(
    "$projectRoot\src\Core\Application\Services\TemplateSelector.cs",
    "$projectRoot\src\Core\Application\Services\DocGeneratorService.cs"
)

foreach ($file in $files) {
    $backup = "$file.backup"

    if (Test-Path $backup) {
        Copy-Item $backup $file -Force
        $fileName = Split-Path $file -Leaf
        Write-Host "[OK] Restored: $fileName" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Files restored from backup." -ForegroundColor Green
Write-Host ""

Read-Host "Press Enter to exit"
