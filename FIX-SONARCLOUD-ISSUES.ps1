# ============================================================================
# AUTO-FIX SONARCLOUD ISSUES
# ============================================================================
# Fixes: JavaScript templates, C# unused parameters, commented code
# ============================================================================

param(
    [string]$ProjectPath = "C:\Projects\EnterpriseDocumentationPlatform.V2",
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  AUTO-FIX SONARCLOUD ISSUES" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "  [DRY RUN MODE - No changes will be made]" -ForegroundColor Yellow
    Write-Host ""
}

$fixCount = 0

# ============================================================================
# 1. FIX JAVASCRIPT TEMPLATES
# ============================================================================
Write-Host "1. FIXING JAVASCRIPT TEMPLATES" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray

$jsFiles = Get-ChildItem -Path "$ProjectPath\Templates" -Filter "*.js" -Recurse -ErrorAction SilentlyContinue

foreach ($file in $jsFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $originalContent = $content
    $fileFixed = $false

    # 1.1 Fix: Prefer node:fs over fs
    if ($content -match "require\('fs'\)") {
        $content = $content -replace "require\('fs'\)", "require('node:fs')"
        $fileFixed = $true
        Write-Host "  [$($file.Name)] Fixed: require('fs') -> require('node:fs')" -ForegroundColor Green
    }

    # 1.2 Fix: Prefer node:path over path
    if ($content -match "require\('path'\)") {
        $content = $content -replace "require\('path'\)", "require('node:path')"
        $fileFixed = $true
        Write-Host "  [$($file.Name)] Fixed: require('path') -> require('node:path')" -ForegroundColor Green
    }

    # 1.3 Fix: Optional chaining for common patterns
    # Pattern: obj && obj.property -> obj?.property
    $optionalChainPatterns = @(
        @{ Find = 'data && data\.'; Replace = 'data?.' },
        @{ Find = 'result && result\.'; Replace = 'result?.' },
        @{ Find = 'response && response\.'; Replace = 'response?.' },
        @{ Find = 'config && config\.'; Replace = 'config?.' },
        @{ Find = 'options && options\.'; Replace = 'options?.' },
        @{ Find = 'params && params\.'; Replace = 'params?.' },
        @{ Find = 'item && item\.'; Replace = 'item?.' },
        @{ Find = 'row && row\.'; Replace = 'row?.' },
        @{ Find = 'doc && doc\.'; Replace = 'doc?.' }
    )

    foreach ($pattern in $optionalChainPatterns) {
        if ($content -match $pattern.Find) {
            $content = $content -replace $pattern.Find, $pattern.Replace
            $fileFixed = $true
            Write-Host "  [$($file.Name)] Fixed: Optional chaining for $($pattern.Replace -replace '\?\.', '')" -ForegroundColor Green
        }
    }

    # 1.4 Fix: Convert .then().catch() to async/await at top level
    # This is complex, so we'll just flag it
    if ($content -match '^\s*\w+\([^)]*\)\s*\.then\(' -and $content -notmatch 'async\s+function|async\s*\(') {
        Write-Host "  [$($file.Name)] MANUAL: Convert promise chain to top-level await" -ForegroundColor Yellow
    }

    # Save changes
    if ($fileFixed -and -not $DryRun) {
        [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
        $fixCount++
    }
}

Write-Host ""

# ============================================================================
# 2. FIX C# UNUSED PARAMETERS
# ============================================================================
Write-Host "2. FIXING C# ISSUES" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray

$csFiles = Get-ChildItem -Path $ProjectPath -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "\\(bin|obj|node_modules)\\" }

# 2.1 Fix unused cancellationToken parameters with discard
$cancellationTokenFiles = @(
    "DocumentsController.cs",
    "AuthorizationBehavior.cs",
    "LoggingBehavior.cs",
    "ValidationBehavior.cs",
    "DocumentPublishedEventHandler.cs"
)

foreach ($file in $csFiles) {
    if ($file.Name -notin $cancellationTokenFiles) { continue }

    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $originalContent = $content
    $fileFixed = $false

    # Add pragma to suppress unused parameter warnings for cancellationToken
    # Better approach: use the token or add _ discard

    # For event handlers with unused notification parameter
    if ($content -match 'notification,\s*CancellationToken\s+cancellationToken\)' -and
        $content -notmatch 'notification.*cancellationToken[^)]*\).*\{[^}]*notification') {
        # The notification isn't used - this needs manual review
        Write-Host "  [$($file.Name)] MANUAL: Review unused 'notification' parameter" -ForegroundColor Yellow
    }
}

# 2.2 Fix: Remove commented out code in AuthController
$authController = $csFiles | Where-Object { $_.Name -eq "AuthController.cs" }
if ($authController) {
    $content = Get-Content $authController.FullName -Raw -ErrorAction SilentlyContinue

    # Remove blocks of commented code (3+ consecutive commented lines)
    $lines = $content -split "`n"
    $newLines = @()
    $commentBlock = @()

    foreach ($line in $lines) {
        if ($line -match '^\s*//(?!//)' -and $line -notmatch '^\s*///') {
            $commentBlock += $line
        } else {
            if ($commentBlock.Count -ge 3) {
                # Skip this comment block (it's commented-out code)
                Write-Host "  [AuthController.cs] Removed $($commentBlock.Count) lines of commented code" -ForegroundColor Green
                $fixCount++
            } else {
                $newLines += $commentBlock
            }
            $commentBlock = @()
            $newLines += $line
        }
    }

    if (-not $DryRun -and $newLines.Count -lt $lines.Count) {
        $newContent = $newLines -join "`n"
        [System.IO.File]::WriteAllText($authController.FullName, $newContent, [System.Text.UTF8Encoding]::new($false))
    }
}

# 2.3 Fix: Use ArgumentNullException.ThrowIfNull
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $originalContent = $content

    # Pattern: if (x == null) throw new ArgumentNullException(nameof(x));
    # Replace with: ArgumentNullException.ThrowIfNull(x);
    $pattern = 'if\s*\(\s*(\w+)\s*==\s*null\s*\)\s*\{\s*throw\s+new\s+ArgumentNullException\s*\(\s*nameof\s*\(\s*\1\s*\)\s*\)\s*;\s*\}'
    $replacement = 'ArgumentNullException.ThrowIfNull($1);'

    if ($content -match $pattern) {
        $content = $content -replace $pattern, $replacement
        Write-Host "  [$($file.Name)] Fixed: Use ArgumentNullException.ThrowIfNull" -ForegroundColor Green

        if (-not $DryRun) {
            [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
            $fixCount++
        }
    }
}

# 2.4 Fix: Use Count > 0 instead of Any()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue

    if ($content -match '\.Any\(\)\s*\)') {
        # This needs context - sometimes Any() is correct
        Write-Host "  [$($file.Name)] REVIEW: Consider using .Count > 0 instead of .Any()" -ForegroundColor Yellow
    }
}

Write-Host ""

# ============================================================================
# 3. SECURITY FIXES (MANUAL)
# ============================================================================
Write-Host "3. SECURITY FIXES (MANUAL REQUIRED)" -ForegroundColor Red
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

Write-Host "  [CRITICAL] AuthController.cs:199 - JWT Secret Key Exposure" -ForegroundColor Red
Write-Host ""
Write-Host "  The JWT secret key is hardcoded. Fix by:" -ForegroundColor Yellow
Write-Host ""
Write-Host @"
  1. Add to appsettings.json (for structure only):
     "JwtSettings": {
       "SecretKey": "",
       "Issuer": "your-app",
       "Audience": "your-app",
       "ExpirationMinutes": 60
     }

  2. Set actual secret via User Secrets (dev):
     dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-key-min-32-chars"

  3. Or Environment Variable (prod):
     JWT_SECRET_KEY=your-secret-key

  4. Update AuthController.cs:
     var secretKey = _configuration["JwtSettings:SecretKey"]
         ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
         ?? throw new InvalidOperationException("JWT secret not configured");

"@ -ForegroundColor Gray

Write-Host ""

# ============================================================================
# 4. REMOVE UNUSED PRIVATE FIELDS
# ============================================================================
Write-Host "4. UNUSED PRIVATE FIELDS (MANUAL)" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

$unusedFields = @(
    @{ File = "TemplatesController.cs"; Line = 46; Field = "_unitOfWork" },
    @{ File = "DocumentApprovalStatusChangedEventHandler.cs"; Line = 18; Field = "_userRepository" },
    @{ File = "GetDocumentsByUserQuery.cs"; Line = 61; Field = "_authorizationService" }
)

foreach ($field in $unusedFields) {
    Write-Host "  $($field.File):$($field.Line) - Remove or use '$($field.Field)'" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  Either:" -ForegroundColor Gray
Write-Host "    - Remove the field and constructor parameter" -ForegroundColor Gray
Write-Host "    - Or use the field (implement the intended functionality)" -ForegroundColor Gray
Write-Host ""

# ============================================================================
# 5. FIX BLOCKER ISSUES (MANUAL)
# ============================================================================
Write-Host "5. BLOCKER ISSUES (MANUAL)" -ForegroundColor Red
Write-Host ("-" * 40) -ForegroundColor DarkGray
Write-Host ""

Write-Host "  [Agent.cs:432] Method signature overlap" -ForegroundColor Red
Write-Host "    Two method overloads with default parameters conflict." -ForegroundColor Gray
Write-Host "    Solution: Remove default value or rename one method." -ForegroundColor Gray
Write-Host ""

Write-Host "  [BaseEntity.cs:157] Remove operator == overload" -ForegroundColor Red
Write-Host "    Custom == operator on entity can cause reference issues." -ForegroundColor Gray
Write-Host "    Solution: Remove the operator overload, use .Equals() instead." -ForegroundColor Gray
Write-Host ""

# ============================================================================
# SUMMARY
# ============================================================================
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  FIX SUMMARY" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
Write-Host "  Automatic fixes applied: $fixCount" -ForegroundColor $(if ($fixCount -gt 0) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "  Manual fixes required:" -ForegroundColor Yellow
Write-Host "    - 1 Security vulnerability (JWT secret)" -ForegroundColor Red
Write-Host "    - 3 Unused private fields" -ForegroundColor Yellow
Write-Host "    - 2 Blocker issues (method overlap, operator ==)" -ForegroundColor Red
Write-Host "    - Several promise chains to convert to async/await" -ForegroundColor Yellow
Write-Host ""

if ($DryRun) {
    Write-Host "  [DRY RUN] No files were modified. Run without -DryRun to apply fixes." -ForegroundColor Yellow
} else {
    Write-Host "  Run SonarCloud analysis again to verify fixes." -ForegroundColor Cyan
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
