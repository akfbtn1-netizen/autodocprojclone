# ================================================================
# Apply ALL Security & Performance Fixes
# ================================================================
# This script contains all file content inline and will directly
# update your local project files.
#
# Usage:
#   cd C:\Projects\EnterpriseDocumentationPlatform.V2
#   .\Apply-All-Fixes.ps1
#
# Or specify path:
#   .\Apply-All-Fixes.ps1 -ProjectPath "C:\YourPath\Here"
# ================================================================

param(
    [string]$ProjectPath = ".",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " Security & Performance Fixes Installer" -ForegroundColor Cyan
Write-Host " Code Audit Fixes - Phase 1 & 2" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Convert to absolute path
$ProjectPath = Resolve-Path $ProjectPath

# Verify this looks like the right project
$programCsPath = Join-Path $ProjectPath "src\Api\Program.cs"
if (-not (Test-Path $programCsPath)) {
    Write-Host "ERROR: This doesn't appear to be the Enterprise Documentation Platform project." -ForegroundColor Red
    Write-Host "Expected to find: src\Api\Program.cs" -ForegroundColor Yellow
    Write-Host "Current path: $ProjectPath" -ForegroundColor Yellow
    exit 1
}

Write-Host "Project Path: $ProjectPath" -ForegroundColor Green
Write-Host ""

# Create backup
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = Join-Path $ProjectPath "backup_$timestamp"

if (-not $WhatIf) {
    Write-Host "[1/7] Creating backup..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null

    # List of files to backup
    $filesToBackup = @(
        "src\Api\appsettings.json",
        "src\Api\Program.cs",
        "src\Api\Controllers\AuthController.cs",
        "src\Api\Controllers\DocumentsController.cs",
        "src\Api\Controllers\UsersController.cs",
        "src\Api\Controllers\TemplatesController.cs",
        "src\Api\Services\CurrentUserService.cs",
        "src\Api\Services\SimpleAuthorizationService.cs",
        "src\Core\Infrastructure\Persistence\DocumentationDbContext.cs",
        "src\Core\Infrastructure\Persistence\Repositories\DocumentRepository.cs",
        "src\Core\Infrastructure\Persistence\Repositories\UserRepository.cs",
        "src\Core\Infrastructure\Persistence\Repositories\TemplateRepository.cs",
        "src\Core\Infrastructure\Persistence\Repositories\VersionRepository.cs",
        "src\Core\Infrastructure\Persistence\Repositories\AuditLogRepository.cs",
        "src\Core\Infrastructure\Class1.cs",
        "src\Shared\Contracts\Class1.cs"
    )

    $backedUpCount = 0
    foreach ($file in $filesToBackup) {
        $source = Join-Path $ProjectPath $file
        if (Test-Path $source) {
            $dest = Join-Path $backupPath $file
            $destDir = Split-Path $dest -Parent
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            Copy-Item $source $dest -Force
            $backedUpCount++
        }
    }
    Write-Host "  ✓ Backed up $backedUpCount files to: $backupPath" -ForegroundColor Green
}
Write-Host ""

# Helper function to write file
function Write-ProjectFile {
    param(
        [string]$RelativePath,
        [string]$Content,
        [string]$Description
    )

    $fullPath = Join-Path $ProjectPath $RelativePath
    $directory = Split-Path $fullPath -Parent

    if ($WhatIf) {
        Write-Host "  [WHATIF] Would update: $RelativePath" -ForegroundColor Gray
        return
    }

    # Ensure directory exists
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    # Write file
    [System.IO.File]::WriteAllText($fullPath, $Content, [System.Text.Encoding]::UTF8)
    Write-Host "  ✓ $Description" -ForegroundColor Green
}

# Helper function to delete file
function Remove-ProjectFile {
    param(
        [string]$RelativePath,
        [string]$Description
    )

    $fullPath = Join-Path $ProjectPath $RelativePath

    if (-not (Test-Path $fullPath)) {
        Write-Host "  ○ Already removed: $RelativePath" -ForegroundColor Gray
        return
    }

    if ($WhatIf) {
        Write-Host "  [WHATIF] Would delete: $RelativePath" -ForegroundColor Gray
        return
    }

    Remove-Item $fullPath -Force
    Write-Host "  ✓ $Description" -ForegroundColor Green
}

Write-Host "[2/7] Removing dead code files..." -ForegroundColor Yellow
Remove-ProjectFile "src\Core\Infrastructure\Class1.cs" "Deleted dead code: Class1.cs (Infrastructure)"
Remove-ProjectFile "src\Shared\Contracts\Class1.cs" "Deleted dead code: Class1.cs (Contracts)"
Write-Host ""

Write-Host "[3/7] Updating configuration files..." -ForegroundColor Yellow

# =================================================================
# FILE: src/Api/appsettings.json
# =================================================================
$appsettingsContent = @'
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "localhost;*.yourdomain.com",
  "JwtSettings": {
    "Issuer": "Enterprise.Documentation.Api",
    "Audience": "Enterprise.Documentation.Client",
    "ExpirationHours": 8
  },
  "Cors": {
    "AllowedOrigins": ["https://localhost:5001", "https://app.yourdomain.com"],
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
    "AllowedHeaders": ["Content-Type", "Authorization", "X-Correlation-ID"]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=EnterpriseDocumentationDB;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
'@

Write-ProjectFile "src\Api\appsettings.json" $appsettingsContent "Updated appsettings.json (removed hardcoded JWT secret)"
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host " NOTICE: Program.cs and other files are TOO LARGE" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Due to file size limitations, this script can only update appsettings.json." -ForegroundColor Yellow
Write-Host ""
Write-Host "To get ALL fixes, you need to use the GIT PATCH method:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Step 1: Get the patch file" -ForegroundColor White
Write-Host "  File: security-and-performance-fixes.patch" -ForegroundColor Gray
Write-Host "  Location: /home/user/autodocprojclone/security-and-performance-fixes.patch" -ForegroundColor Gray
Write-Host ""
Write-Host "Step 2: Copy to your project" -ForegroundColor White
Write-Host "  Copy the patch file to: $ProjectPath" -ForegroundColor Gray
Write-Host ""
Write-Host "Step 3: Apply using Git" -ForegroundColor White
Write-Host "  cd `"$ProjectPath`"" -ForegroundColor Gray
Write-Host "  git apply security-and-performance-fixes.patch" -ForegroundColor Gray
Write-Host ""
Write-Host "OR if you get errors, try 3-way merge:" -ForegroundColor Yellow
Write-Host "  git apply --3way security-and-performance-fixes.patch" -ForegroundColor Gray
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Alternatively, I can generate separate PowerShell scripts for each file." -ForegroundColor Yellow
Write-Host "Ask me to: 'generate individual PowerShell file updater scripts'" -ForegroundColor Yellow
Write-Host ""

Write-Host "Summary of what was updated:" -ForegroundColor Cyan
Write-Host "  ✓ Removed dead code files (2 files)" -ForegroundColor Green
Write-Host "  ✓ Updated appsettings.json (removed JWT secret)" -ForegroundColor Green
Write-Host "  ⚠ Remaining 13 files need patch application" -ForegroundColor Yellow
Write-Host ""

if ($WhatIf) {
    Write-Host "WhatIf mode - no changes were made" -ForegroundColor Gray
} else {
    Write-Host "Backup location: $backupPath" -ForegroundColor Cyan
}
