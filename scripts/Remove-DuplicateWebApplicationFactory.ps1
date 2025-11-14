# Remove-DuplicateWebApplicationFactory.ps1
# Script to identify and remove duplicate WebApplicationFactory definitions

$ErrorActionPreference = "Stop"

Write-Host "=== Duplicate WebApplicationFactory Cleanup Script ===" -ForegroundColor Cyan
Write-Host ""

$projectRoot = Split-Path -Parent $PSScriptRoot
$integrationTestPath = Join-Path $projectRoot "tests\Integration"

Write-Host "Searching for WebApplicationFactory files in: $integrationTestPath" -ForegroundColor Yellow
Write-Host ""

# Find all .cs files containing CustomWebApplicationFactory class
$files = Get-ChildItem -Path $integrationTestPath -Filter "*.cs" -Recurse |
    Where-Object {
        $content = Get-Content $_.FullName -Raw
        $content -match "class\s+CustomWebApplicationFactory"
    }

Write-Host "Found the following files containing CustomWebApplicationFactory:" -ForegroundColor Green
$files | ForEach-Object {
    Write-Host "  - $($_.FullName)" -ForegroundColor White

    # Show first few lines to help identify the file
    $lines = Get-Content $_.FullName -TotalCount 20
    Write-Host "    Preview (first 20 lines):" -ForegroundColor Gray
    $lines | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    Write-Host ""
}

Write-Host ""
Write-Host "Analysis:" -ForegroundColor Cyan

if ($files.Count -gt 1) {
    Write-Host "  ERROR: Found $($files.Count) files with CustomWebApplicationFactory class!" -ForegroundColor Red
    Write-Host "  This is causing the CS0101 error (duplicate type definition)" -ForegroundColor Red
    Write-Host ""

    # Identify which file should be kept
    $correctFile = $files | Where-Object { $_.Name -eq "CustomWebApplicationFactory.cs" }
    $duplicateFiles = $files | Where-Object { $_.Name -ne "CustomWebApplicationFactory.cs" }

    if ($correctFile) {
        Write-Host "  The CORRECT file to keep is:" -ForegroundColor Green
        Write-Host "    $($correctFile.FullName)" -ForegroundColor White
        Write-Host ""
    }

    if ($duplicateFiles) {
        Write-Host "  The following DUPLICATE file(s) should be removed:" -ForegroundColor Yellow
        $duplicateFiles | ForEach-Object {
            Write-Host "    $($_.FullName)" -ForegroundColor White
        }
        Write-Host ""

        # Ask for confirmation to remove
        $response = Read-Host "Do you want to remove the duplicate file(s)? (yes/no)"

        if ($response -eq "yes" -or $response -eq "y") {
            foreach ($dupFile in $duplicateFiles) {
                Write-Host "  Removing: $($dupFile.FullName)" -ForegroundColor Yellow
                Remove-Item $dupFile.FullName -Force
                Write-Host "  Removed successfully!" -ForegroundColor Green
            }
            Write-Host ""
            Write-Host "Cleanup complete! Please rebuild the solution." -ForegroundColor Green
        } else {
            Write-Host "  Operation cancelled. No files were removed." -ForegroundColor Yellow
        }
    }
} elseif ($files.Count -eq 1) {
    Write-Host "  OK: Found exactly 1 file with CustomWebApplicationFactory" -ForegroundColor Green
    Write-Host "  File: $($files[0].FullName)" -ForegroundColor White
    Write-Host ""
    Write-Host "  No duplicates found. The build error might be from cached files." -ForegroundColor Yellow
    Write-Host "  Try running: dotnet clean && dotnet build" -ForegroundColor Cyan
} else {
    Write-Host "  WARNING: No files found with CustomWebApplicationFactory class!" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Script Complete ===" -ForegroundColor Cyan
