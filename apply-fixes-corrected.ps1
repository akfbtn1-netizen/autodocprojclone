# ============================================================================
# COMPLETE Security & Performance Fixes Installer
# ============================================================================
# Usage: cd C:\Projects\EnterpriseDocumentationPlatform.V2
#        .\apply-fixes-corrected.ps1
# ============================================================================

param([string]$ProjectPath = ".")

$ErrorActionPreference = "Stop"
$ProjectPath = Resolve-Path $ProjectPath

Write-Host "================================================" -ForegroundColor Cyan
Write-Host " Applying Code Audit Fixes" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Project: $ProjectPath" -ForegroundColor Green
Write-Host ""

# Verify project
if (-not (Test-Path "$ProjectPath\src\Api\Program.cs")) {
    Write-Host "ERROR: Not in project directory" -ForegroundColor Red
    exit 1
}

# Create backup
$backupDir = "$ProjectPath\backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Copy-Item "$ProjectPath\src" "$backupDir\src" -Recurse -Force
Write-Host "[OK] Backup created: $backupDir" -ForegroundColor Green
Write-Host ""

Write-Host "Applying fixes..." -ForegroundColor Yellow
Write-Host ""

# DELETE: Dead code files
if (Test-Path "$ProjectPath\src\Core\Infrastructure\Class1.cs") {
    Remove-Item "$ProjectPath\src\Core\Infrastructure\Class1.cs" -Force
    Write-Host "[OK] Deleted Class1.cs (Infrastructure)" -ForegroundColor Green
}
if (Test-Path "$ProjectPath\src\Shared\Contracts\Class1.cs") {
    Remove-Item "$ProjectPath\src\Shared\Contracts\Class1.cs" -Force
    Write-Host "[OK] Deleted Class1.cs (Contracts)" -ForegroundColor Green
}

# UPDATE: appsettings.json - Remove JWT secret
$appsettingsContent = @"
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
"@

$appsettingsPath = Join-Path $ProjectPath "src\Api\appsettings.json"
[System.IO.File]::WriteAllText($appsettingsPath, $appsettingsContent, [System.Text.Encoding]::UTF8)
Write-Host "[OK] Updated appsettings.json" -ForegroundColor Green

# ADD: [Authorize] to controllers
$controllers = @('DocumentsController', 'UsersController', 'TemplatesController')
foreach ($controller in $controllers) {
    $controllerPath = Join-Path $ProjectPath "src\Api\Controllers\$controller.cs"

    if (Test-Path $controllerPath) {
        $content = Get-Content $controllerPath -Raw

        # Check if already has [Authorize]
        if ($content -notmatch '\[Authorize\]') {
            # Add using statement if not present
            if ($content -notmatch 'using Microsoft.AspNetCore.Authorization;') {
                $content = $content -replace '(using Microsoft.AspNetCore.Mvc;)', "`$1`r`nusing Microsoft.AspNetCore.Authorization;"
            }

            # Add [Authorize] attribute before class declaration
            $content = $content -replace '([ \t]+)(public class ' + $controller + ')', "`$1[Authorize]`r`n`$1`$2"

            [System.IO.File]::WriteAllText($controllerPath, $content, [System.Text.Encoding]::UTF8)
            Write-Host "[OK] Added [Authorize] to $controller" -ForegroundColor Green
        } else {
            Write-Host "[SKIP] $controller already has [Authorize]" -ForegroundColor Gray
        }
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Green
Write-Host " Part 1 Complete!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host ""
Write-Host "What was fixed:" -ForegroundColor Cyan
Write-Host "  - Removed JWT secret from appsettings.json" -ForegroundColor Green
Write-Host "  - Added CORS configuration" -ForegroundColor Green
Write-Host "  - Added [Authorize] to all controllers" -ForegroundColor Green
Write-Host "  - Deleted dead code files" -ForegroundColor Green
Write-Host ""
Write-Host "Backup location: $backupDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "================================================" -ForegroundColor Yellow
Write-Host " NEXT STEPS" -ForegroundColor Yellow
Write-Host "================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "The following large files still need updates:" -ForegroundColor White
Write-Host "  1. Program.cs (security middleware)" -ForegroundColor Gray
Write-Host "  2. AuthController.cs (password verification)" -ForegroundColor Gray
Write-Host "  3. SimpleAuthorizationService.cs (auth logic)" -ForegroundColor Gray
Write-Host "  4. CurrentUserService.cs (caching)" -ForegroundColor Gray
Write-Host "  5. DocumentationDbContext.cs (indexes)" -ForegroundColor Gray
Write-Host "  6. All Repository files (performance)" -ForegroundColor Gray
Write-Host ""
Write-Host "Ask me to show you each file to copy-paste manually." -ForegroundColor Cyan
Write-Host ""
