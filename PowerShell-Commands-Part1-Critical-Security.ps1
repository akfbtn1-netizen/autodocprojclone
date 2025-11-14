# ================================================================
# Part 1: CRITICAL SECURITY FIXES
# ================================================================
# Copy and run these commands in PowerShell from your project directory:
# cd C:\Projects\EnterpriseDocumentationPlatform.V2
# ================================================================

# STEP 1: Delete dead code files
# ================================================================
Write-Host "[STEP 1] Removing dead code files..." -ForegroundColor Yellow

if (Test-Path "src\Core\Infrastructure\Class1.cs") {
    Remove-Item "src\Core\Infrastructure\Class1.cs" -Force
    Write-Host "  ✓ Deleted src\Core\Infrastructure\Class1.cs" -ForegroundColor Green
}

if (Test-Path "src\Shared\Contracts\Class1.cs") {
    Remove-Item "src\Shared\Contracts\Class1.cs" -Force
    Write-Host "  ✓ Deleted src\Shared\Contracts\Class1.cs" -ForegroundColor Green
}

# STEP 2: Update appsettings.json (REMOVE HARDCODED JWT SECRET)
# ================================================================
Write-Host ""
Write-Host "[STEP 2] Updating appsettings.json..." -ForegroundColor Yellow

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

[System.IO.File]::WriteAllText("$PWD\src\Api\appsettings.json", $appsettingsContent, [System.Text.Encoding]::UTF8)
Write-Host "  ✓ Updated appsettings.json (JWT SecretKey removed)" -ForegroundColor Green

# STEP 3: Add [Authorize] attributes to controllers
# ================================================================
Write-Host ""
Write-Host "[STEP 3] Adding [Authorize] attributes to controllers..." -ForegroundColor Yellow

# DocumentsController.cs
$documentsControllerPath = "src\Api\Controllers\DocumentsController.cs"
$content = Get-Content $documentsControllerPath -Raw
if ($content -notmatch '\[Authorize\][\r\n\s]+public class DocumentsController') {
    $content = $content -replace 'public class DocumentsController', '[Authorize]
public class DocumentsController'
    [System.IO.File]::WriteAllText("$PWD\$documentsControllerPath", $content, [System.Text.Encoding]::UTF8)
    Write-Host "  ✓ Added [Authorize] to DocumentsController" -ForegroundColor Green
} else {
    Write-Host "  ○ DocumentsController already has [Authorize]" -ForegroundColor Gray
}

# UsersController.cs
$usersControllerPath = "src\Api\Controllers\UsersController.cs"
$content = Get-Content $usersControllerPath -Raw
if ($content -notmatch '\[Authorize\][\r\n\s]+public class UsersController') {
    $content = $content -replace 'public class UsersController', '[Authorize]
public class UsersController'
    [System.IO.File]::WriteAllText("$PWD\$usersControllerPath", $content, [System.Text.Encoding]::UTF8)
    Write-Host "  ✓ Added [Authorize] to UsersController" -ForegroundColor Green
} else {
    Write-Host "  ○ UsersController already has [Authorize]" -ForegroundColor Gray
}

# TemplatesController.cs
$templatesControllerPath = "src\Api\Controllers\TemplatesController.cs"
$content = Get-Content $templatesControllerPath -Raw
if ($content -notmatch '\[Authorize\][\r\n\s]+public class TemplatesController') {
    $content = $content -replace 'public class TemplatesController', '[Authorize]
public class TemplatesController'
    [System.IO.File]::WriteAllText("$PWD\$templatesControllerPath", $content, [System.Text.Encoding]::UTF8)
    Write-Host "  ✓ Added [Authorize] to TemplatesController" -ForegroundColor Green
} else {
    Write-Host "  ○ TemplatesController already has [Authorize]" -ForegroundColor Gray
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host " Part 1 Complete: Basic Security Fixes Applied" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "What was fixed:" -ForegroundColor Cyan
Write-Host "  ✓ Removed hardcoded JWT secret from appsettings.json" -ForegroundColor Green
Write-Host "  ✓ Added CORS configuration" -ForegroundColor Green
Write-Host "  ✓ Added [Authorize] to all controllers" -ForegroundColor Green
Write-Host "  ✓ Deleted dead code files" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: You still need to apply Part 2 and Part 3 scripts!" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Run Part 2 script (Program.cs updates - security middleware)" -ForegroundColor White
Write-Host "  2. Run Part 3 script (Performance optimizations)" -ForegroundColor White
Write-Host "  3. Set JWT secret: dotnet user-secrets set 'JwtSettings:SecretKey' 'your-32-char-secret'" -ForegroundColor White
Write-Host "  4. Run migrations: dotnet ef migrations add AddCompositeIndexes" -ForegroundColor White
Write-Host ""
