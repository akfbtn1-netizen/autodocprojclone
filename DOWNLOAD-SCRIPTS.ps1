# ============================================================================
# DOWNLOAD APPROVAL WORKFLOW SCRIPTS
# ============================================================================
# This script downloads the 4 build scripts from the repository
# Run this first, then run each BUILD-APPROVAL-WORKFLOW-PART*.ps1 in order
# ============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

Write-Host "Downloading approval workflow build scripts..." -ForegroundColor Cyan

# You'll need to update this URL to your actual repository
$repoBaseUrl = "https://raw.githubusercontent.com/YOUR-USERNAME/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX"

$scripts = @(
    "BUILD-APPROVAL-WORKFLOW-PART1.ps1",
    "BUILD-APPROVAL-WORKFLOW-PART2.ps1",
    "BUILD-APPROVAL-WORKFLOW-PART3.ps1",
    "BUILD-APPROVAL-WORKFLOW-PART4.ps1"
)

foreach ($script in $scripts) {
    $url = "$repoBaseUrl/$script"
    $destination = Join-Path $projectRoot $script

    Write-Host "  Downloading $script..." -ForegroundColor Yellow
    try {
        Invoke-WebRequest -Uri $url -OutFile $destination
        Write-Host "    Saved to: $destination" -ForegroundColor Green
    }
    catch {
        Write-Host "    Failed: $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Done! Now run each script in order:" -ForegroundColor Green
Write-Host "  .\BUILD-APPROVAL-WORKFLOW-PART1.ps1" -ForegroundColor Cyan
Write-Host "  .\BUILD-APPROVAL-WORKFLOW-PART2.ps1" -ForegroundColor Cyan
Write-Host "  .\BUILD-APPROVAL-WORKFLOW-PART3.ps1" -ForegroundColor Cyan
Write-Host "  .\BUILD-APPROVAL-WORKFLOW-PART4.ps1" -ForegroundColor Cyan
