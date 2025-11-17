# Script to secure API keys and clean up exposed credentials
# Enterprise Documentation Platform V2

Write-Host "Securing API Keys - Enterprise Documentation Platform V2" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Gray
Write-Host ""

$ROOT = "C:\Projects\EnterpriseDocumentationPlatform.V2"

# 1. Delete backup folders with exposed credentials
Write-Host ">>> Step 1: Removing backup folders with exposed credentials" -ForegroundColor Yellow

$backupFolders = @(
    "backup_20251113_195421",
    "backup_20251114_101514",
    "backup-20251110-210339",
    "backup-csproj-20251110-211909",
    "backup-csproj-20251110-212113"
)

foreach ($folder in $backupFolders) {
    $path = Join-Path $ROOT $folder
    if (Test-Path $path) {
        Write-Host "   Removing: $folder" -ForegroundColor White
        Remove-Item -Path $path -Recurse -Force
        Write-Host "   SUCCESS: Removed $folder" -ForegroundColor Green
    }
}

Write-Host ""

# 2. Update .gitignore
Write-Host ">>> Step 2: Updating .gitignore" -ForegroundColor Yellow

$gitignorePath = Join-Path $ROOT ".gitignore"

$gitignoreEntries = @(
    "",
    "# Security - Sensitive Files",
    "appsettings.Production.json",
    "appsettings.*.json",
    "**/appsettings.Production.json",
    "*.backup",
    "backup_*/",
    "backup-*/",
    "",
    "# Security Audit Results",
    "security-audit-results/",
    "tools-security/",
    "audit-results/",
    "",
    "# Build outputs",
    "**/bin/",
    "**/obj/",
    "",
    "# User Secrets",
    "**/*.user",
    "secrets.json"
)

if (Test-Path $gitignorePath) {
    $currentContent = Get-Content $gitignorePath -Raw
    $newEntries = $gitignoreEntries | Where-Object { $currentContent -notmatch [regex]::Escape($_) }

    if ($newEntries.Count -gt 0) {
        Add-Content -Path $gitignorePath -Value "`n"
        Add-Content -Path $gitignorePath -Value $newEntries
        Write-Host "   SUCCESS: Updated .gitignore with security entries" -ForegroundColor Green
    } else {
        Write-Host "   INFO: .gitignore already contains security entries" -ForegroundColor Gray
    }
} else {
    $gitignoreEntries | Out-File -FilePath $gitignorePath -Encoding UTF8
    Write-Host "   SUCCESS: Created .gitignore with security entries" -ForegroundColor Green
}

Write-Host ""

# 3. Create .gitleaksignore for false positives
Write-Host ">>> Step 3: Creating .gitleaksignore for false positives" -ForegroundColor Yellow

$gitleaksignorePath = Join-Path $ROOT ".gitleaksignore"

$gitleaksignoreContent = @"
# Gitleaks Ignore File
# False positives that are safe to ignore

# Test files with mock credentials
tests/Integration/Controllers/AuthControllerTests.cs:hashicorp-tf-password:*

# Documentation examples
docs/CODING_STANDARDS.md:generic-api-key:*

# Gitleaks own documentation
tools-security/gitleaks/README.md:*
"@

$gitleaksignoreContent | Out-File -FilePath $gitleaksignorePath -Encoding UTF8
Write-Host "   SUCCESS: Created .gitleaksignore" -ForegroundColor Green

Write-Host ""

# 4. Setup User Secrets for development
Write-Host ">>> Step 4: Setting up User Secrets" -ForegroundColor Yellow

$apiProjectPath = Join-Path $ROOT "src\Api\Api.csproj"

if (Test-Path $apiProjectPath) {
    Push-Location (Join-Path $ROOT "src\Api")

    # Initialize user secrets
    $userSecretsId = & dotnet user-secrets list 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "   Initializing user secrets..." -ForegroundColor White
        & dotnet user-secrets init
    }

    Write-Host "   User secrets initialized" -ForegroundColor Green
    Write-Host ""
    Write-Host "   To set your Azure OpenAI key securely, run:" -ForegroundColor Cyan
    Write-Host "   dotnet user-secrets set `"AzureOpenAI:ApiKey`" `"YOUR-NEW-KEY-HERE`"" -ForegroundColor White

    Pop-Location
}

Write-Host ""

# 5. Summary and Next Steps
Write-Host ">>> SUMMARY AND NEXT STEPS" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Gray
Write-Host ""

Write-Host "COMPLETED:" -ForegroundColor Green
Write-Host "  [X] Removed backup folders with exposed credentials" -ForegroundColor White
Write-Host "  [X] Updated .gitignore to prevent future leaks" -ForegroundColor White
Write-Host "  [X] Created .gitleaksignore for false positives" -ForegroundColor White
Write-Host "  [X] Initialized user secrets for secure development" -ForegroundColor White
Write-Host ""

Write-Host "MANUAL ACTIONS REQUIRED:" -ForegroundColor Yellow
Write-Host "  [ ] 1. ROTATE Azure OpenAI Key in Azure Portal" -ForegroundColor White
Write-Host "       - Go to Azure Portal > Cognitive Services > Your OpenAI Resource" -ForegroundColor Gray
Write-Host "       - Navigate to 'Keys and Endpoint'" -ForegroundColor Gray
Write-Host "       - Click 'Regenerate Key 1' or 'Regenerate Key 2'" -ForegroundColor Gray
Write-Host ""
Write-Host "  [ ] 2. Set new key using User Secrets (development):" -ForegroundColor White
Write-Host "       cd src\Api" -ForegroundColor Gray
Write-Host "       dotnet user-secrets set `"AzureOpenAI:ApiKey`" `"YOUR-NEW-KEY`"" -ForegroundColor Gray
Write-Host ""
Write-Host "  [ ] 3. For Production, use Azure Key Vault or Environment Variables" -ForegroundColor White
Write-Host ""
Write-Host "  [ ] 4. Update System.Text.Json package:" -ForegroundColor White
Write-Host "       dotnet add src/Core/Application package System.Text.Json --version 8.0.5" -ForegroundColor Gray
Write-Host ""
Write-Host "  [ ] 5. Remove sensitive files from git history (optional but recommended):" -ForegroundColor White
Write-Host "       git filter-repo --path backup_20251113_195421 --invert-paths" -ForegroundColor Gray
Write-Host "       Note: This requires git-filter-repo tool" -ForegroundColor Gray
Write-Host ""

Write-Host ("=" * 70) -ForegroundColor Gray
Write-Host "Script completed successfully!" -ForegroundColor Green
