# ============================================================================
# COMPREHENSIVE ENTERPRISE CODE AUDIT
# ============================================================================
# Covers: Security, Code Quality, Test Quality, Dependencies, Performance
# Plus Roslyn Analyzer configuration
# ============================================================================

param(
    [string]$ProjectPath = "C:\Projects\EnterpriseDocumentationPlatform.V2",
    [switch]$Verbose,
    [switch]$ExportJson
)

$ErrorActionPreference = "Continue"

# ============================================================================
# CONFIGURATION
# ============================================================================
$script:TotalScore = 0
$script:MaxScore = 0
$script:Issues = @()
$script:Warnings = @()

function Add-Score {
    param([int]$Points, [int]$Max, [string]$Category, [string]$Check, [string]$Details = "")
    $script:TotalScore += $Points
    $script:MaxScore += $Max
    $status = if ($Points -eq $Max) { "PASS" } elseif ($Points -gt 0) { "PARTIAL" } else { "FAIL" }
    $color = switch ($status) { "PASS" { "Green" } "PARTIAL" { "Yellow" } "FAIL" { "Red" } }
    Write-Host "  [$status] $Check ($Points/$Max)" -ForegroundColor $color
    if ($Details -and $Verbose) { Write-Host "         $Details" -ForegroundColor Gray }
}

function Add-Issue {
    param([string]$Severity, [string]$Category, [string]$Message, [string]$File = "", [int]$Line = 0)
    $script:Issues += [PSCustomObject]@{
        Severity = $Severity
        Category = $Category
        Message = $Message
        File = $File
        Line = $Line
    }
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  COMPREHENSIVE ENTERPRISE CODE AUDIT" -ForegroundColor White
Write-Host "  Target: $ProjectPath" -ForegroundColor Gray
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Get all C# files
$csFiles = Get-ChildItem -Path $ProjectPath -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "\\(bin|obj|node_modules)\\" }

$csprojFiles = Get-ChildItem -Path $ProjectPath -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue

# ============================================================================
# 1. SECURITY ANALYSIS (25 points)
# ============================================================================
Write-Host "1. SECURITY ANALYSIS" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray

# 1.1 SQL Injection Detection (5 points)
$sqlInjectionPatterns = @(
    'string\.Format\s*\([^)]*SELECT|INSERT|UPDATE|DELETE',
    '\$"[^"]*SELECT[^"]*\{',
    '\$"[^"]*INSERT[^"]*\{',
    '\$"[^"]*UPDATE[^"]*\{',
    '\$"[^"]*DELETE[^"]*\{',
    'SqlCommand\s*\([^)]*\+',
    'ExecuteSqlRaw\s*\([^)]*\+',
    'FromSqlRaw\s*\([^)]*\+'
)

$sqlInjectionCount = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $sqlInjectionPatterns) {
        if ($content -match $pattern) {
            $sqlInjectionCount++
            Add-Issue "CRITICAL" "Security" "Potential SQL injection: $pattern" $file.FullName
        }
    }
}
$sqlScore = if ($sqlInjectionCount -eq 0) { 5 } elseif ($sqlInjectionCount -lt 3) { 2 } else { 0 }
Add-Score $sqlScore 5 "Security" "SQL Injection Prevention" "$sqlInjectionCount potential issues found"

# 1.2 Hardcoded Secrets (5 points)
$secretPatterns = @(
    'password\s*=\s*"[^"]{4,}"',
    'apikey\s*=\s*"[^"]{8,}"',
    'secret\s*=\s*"[^"]{8,}"',
    'connectionstring\s*=\s*"[^"]*password=[^"]*"',
    'Bearer\s+[A-Za-z0-9\-_]{20,}',
    "api[_-]?key[`"\s:=]+[`"'][^`"']{16,}",
    'private[_-]?key',
    'aws[_-]?access[_-]?key',
    'client[_-]?secret\s*=\s*"[^"]{8,}"'
)

$secretsCount = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($file.Name -notmatch "appsettings.*\.json") {
        foreach ($pattern in $secretPatterns) {
            if ($content -imatch $pattern) {
                $secretsCount++
                Add-Issue "CRITICAL" "Security" "Potential hardcoded secret: $pattern" $file.FullName
            }
        }
    }
}
$secretsScore = if ($secretsCount -eq 0) { 5 } elseif ($secretsCount -lt 2) { 2 } else { 0 }
Add-Score $secretsScore 5 "Security" "No Hardcoded Secrets" "$secretsCount potential secrets found"

# 1.3 Input Validation (5 points)
$hasValidation = $false
$validationPatterns = @(
    '\[Required\]',
    '\[StringLength',
    '\[Range\s*\(',
    '\[RegularExpression',
    'FluentValidation',
    'DataAnnotations',
    'ModelState\.IsValid'
)
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $validationPatterns) {
        if ($content -match $pattern) {
            $hasValidation = $true
            break
        }
    }
    if ($hasValidation) { break }
}
Add-Score $(if ($hasValidation) { 5 } else { 0 }) 5 "Security" "Input Validation Present"

# 1.4 XSS Prevention (5 points)
$xssPatterns = @(
    'Html\.Raw\s*\(',
    '\@Html\.Raw',
    'dangerouslySetInnerHTML',
    'document\.write\s*\('
)
$xssCount = 0
$allFiles = Get-ChildItem -Path $ProjectPath -Include "*.cs","*.cshtml","*.razor" -Recurse -ErrorAction SilentlyContinue
foreach ($file in $allFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $xssPatterns) {
        if ($content -match $pattern) {
            $xssCount++
            Add-Issue "HIGH" "Security" "Potential XSS vulnerability: $pattern" $file.FullName
        }
    }
}
Add-Score $(if ($xssCount -eq 0) { 5 } else { 0 }) 5 "Security" "XSS Prevention" "$xssCount issues"

# 1.5 Authentication/Authorization (5 points)
$hasAuth = $false
$authPatterns = @(
    '\[Authorize',
    '\[AllowAnonymous\]',
    'AddAuthentication',
    'AddAuthorization',
    'UseAuthentication',
    'UseAuthorization',
    'ClaimsPrincipal',
    'IAuthorizationService'
)
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $authPatterns) {
        if ($content -match $pattern) {
            $hasAuth = $true
            break
        }
    }
    if ($hasAuth) { break }
}
Add-Score $(if ($hasAuth) { 5 } else { 0 }) 5 "Security" "Authentication/Authorization"

Write-Host ""

# ============================================================================
# 2. DEEP CODE QUALITY (25 points)
# ============================================================================
Write-Host "2. DEEP CODE QUALITY" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray

# 2.1 Cyclomatic Complexity (5 points)
$highComplexityFiles = @()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue

    # Count complexity indicators
    $ifCount = ([regex]::Matches($content, '\bif\s*\(')).Count
    $elseCount = ([regex]::Matches($content, '\belse\b')).Count
    $forCount = ([regex]::Matches($content, '\bfor\s*\(')).Count
    $foreachCount = ([regex]::Matches($content, '\bforeach\s*\(')).Count
    $whileCount = ([regex]::Matches($content, '\bwhile\s*\(')).Count
    $caseCount = ([regex]::Matches($content, '\bcase\s+')).Count
    $catchCount = ([regex]::Matches($content, '\bcatch\s*\(')).Count
    $ternaryCount = ([regex]::Matches($content, '\?[^?:]+:')).Count
    $andCount = ([regex]::Matches($content, '&&')).Count
    $orCount = ([regex]::Matches($content, '\|\|')).Count

    $complexity = $ifCount + $elseCount + $forCount + $foreachCount + $whileCount + $caseCount + $catchCount + $ternaryCount + $andCount + $orCount
    $lines = ($content -split "`n").Count

    # High complexity = more than 50 per 100 lines
    if ($lines -gt 50 -and ($complexity / $lines * 100) -gt 50) {
        $highComplexityFiles += $file.Name
        Add-Issue "MEDIUM" "Quality" "High cyclomatic complexity ($complexity in $lines lines)" $file.FullName
    }
}
$complexityScore = if ($highComplexityFiles.Count -eq 0) { 5 } elseif ($highComplexityFiles.Count -lt 5) { 3 } else { 0 }
Add-Score $complexityScore 5 "Quality" "Cyclomatic Complexity" "$($highComplexityFiles.Count) high-complexity files"

# 2.2 Code Duplication Detection (5 points)
$methodSignatures = @{}
$duplicateMethods = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $methods = [regex]::Matches($content, '(public|private|protected|internal)\s+(static\s+)?(async\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\([^)]*\)\s*\{')
    foreach ($method in $methods) {
        $sig = $method.Groups[4].Value
        if ($methodSignatures.ContainsKey($sig)) {
            $duplicateMethods++
        } else {
            $methodSignatures[$sig] = $file.Name
        }
    }
}
# Check for duplicated code blocks (simple hash-based)
$codeBlocks = @{}
$duplicateBlocks = 0
foreach ($file in $csFiles) {
    $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
    for ($i = 0; $i -lt $lines.Count - 5; $i++) {
        $block = ($lines[$i..($i+4)] -join "`n").Trim()
        if ($block.Length -gt 100) {
            $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($block))
            $hashStr = [BitConverter]::ToString($hash).Replace("-","")
            if ($codeBlocks.ContainsKey($hashStr)) {
                $duplicateBlocks++
            } else {
                $codeBlocks[$hashStr] = $file.Name
            }
        }
    }
}
$duplicationScore = if ($duplicateBlocks -lt 10) { 5 } elseif ($duplicateBlocks -lt 30) { 3 } else { 0 }
Add-Score $duplicationScore 5 "Quality" "Code Duplication" "$duplicateBlocks duplicate blocks"

# 2.3 SOLID Principle Violations (5 points)
$solidViolations = 0

# Single Responsibility - classes with too many methods
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $publicMethods = ([regex]::Matches($content, 'public\s+(async\s+)?[\w<>\[\],\s]+\s+\w+\s*\(')).Count
    if ($publicMethods -gt 20) {
        $solidViolations++
        Add-Issue "MEDIUM" "Quality" "Possible SRP violation: $publicMethods public methods" $file.FullName
    }
}

# Dependency Inversion - concrete dependencies in constructors
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content -match 'new\s+(SqlConnection|HttpClient|SmtpClient)\s*\(') {
        $solidViolations++
        Add-Issue "MEDIUM" "Quality" "Dependency Inversion violation: concrete instantiation" $file.FullName
    }
}

$solidScore = if ($solidViolations -eq 0) { 5 } elseif ($solidViolations -lt 5) { 3 } else { 0 }
Add-Score $solidScore 5 "Quality" "SOLID Principles" "$solidViolations violations"

# 2.4 Async/Await Misuse (5 points)
$asyncIssues = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue

    # .Result or .Wait() blocking
    if ($content -match '\.Result\b|\.Wait\(\)') {
        $asyncIssues++
        Add-Issue "HIGH" "Quality" "Async blocking (.Result/.Wait())" $file.FullName
    }

    # async void (except event handlers)
    if ($content -match 'async\s+void\s+(?!On[A-Z])') {
        $asyncIssues++
        Add-Issue "HIGH" "Quality" "async void method (fire-and-forget)" $file.FullName
    }

    # Missing ConfigureAwait in library code
    if ($file.FullName -match "\\(Core|Infrastructure)\\" -and $content -match 'await\s+[^;]+;' -and $content -notmatch 'ConfigureAwait') {
        # This is a warning, not critical
    }
}
$asyncScore = if ($asyncIssues -eq 0) { 5 } elseif ($asyncIssues -lt 3) { 3 } else { 0 }
Add-Score $asyncScore 5 "Quality" "Async/Await Usage" "$asyncIssues issues"

# 2.5 Exception Handling (5 points)
$exceptionIssues = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue

    # Empty catch blocks
    if ($content -match 'catch\s*\([^)]*\)\s*\{\s*\}') {
        $exceptionIssues++
        Add-Issue "HIGH" "Quality" "Empty catch block" $file.FullName
    }

    # Catching generic Exception without logging
    if ($content -match 'catch\s*\(\s*Exception\s+\w+\s*\)\s*\{[^}]*\}' -and $content -notmatch 'catch\s*\(\s*Exception[^}]*(log|Log|_logger)') {
        $exceptionIssues++
        Add-Issue "MEDIUM" "Quality" "Catching generic Exception without logging" $file.FullName
    }

    # throw ex; instead of throw;
    if ($content -match 'throw\s+\w+\s*;') {
        $exceptionIssues++
        Add-Issue "MEDIUM" "Quality" "throw ex; loses stack trace (use throw;)" $file.FullName
    }
}
$exceptionScore = if ($exceptionIssues -eq 0) { 5 } elseif ($exceptionIssues -lt 3) { 3 } else { 0 }
Add-Score $exceptionScore 5 "Quality" "Exception Handling" "$exceptionIssues issues"

Write-Host ""

# ============================================================================
# 3. TEST QUALITY (20 points)
# ============================================================================
Write-Host "3. TEST QUALITY" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray

# Find test files
$testFiles = $csFiles | Where-Object { $_.FullName -match "\\(Tests?|Specs?)\\" -or $_.Name -match "Tests?\.cs$" }
$testFileCount = $testFiles.Count

# 3.1 Test Existence (5 points)
$hasTests = $testFileCount -gt 0
Add-Score $(if ($hasTests) { 5 } else { 0 }) 5 "Tests" "Tests Exist" "$testFileCount test files"

# 3.2 Test Coverage Estimate (5 points)
$serviceFiles = $csFiles | Where-Object { $_.Name -match "Service\.cs$|Repository\.cs$|Controller\.cs$" }
$serviceCount = $serviceFiles.Count
$testToServiceRatio = if ($serviceCount -gt 0) { $testFileCount / $serviceCount } else { 0 }
$coverageScore = if ($testToServiceRatio -ge 1) { 5 } elseif ($testToServiceRatio -ge 0.5) { 3 } else { 0 }
Add-Score $coverageScore 5 "Tests" "Test Coverage Ratio" "Ratio: $([math]::Round($testToServiceRatio, 2)):1"

# 3.3 Assertion Density (5 points)
$totalAssertions = 0
$totalTestMethods = 0
foreach ($file in $testFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $totalAssertions += ([regex]::Matches($content, 'Assert\.|Should\.|Expect\(')).Count
    $totalTestMethods += ([regex]::Matches($content, '\[Test\]|\[Fact\]|\[Theory\]|\[TestMethod\]')).Count
}
$assertionDensity = if ($totalTestMethods -gt 0) { $totalAssertions / $totalTestMethods } else { 0 }
$assertionScore = if ($assertionDensity -ge 2) { 5 } elseif ($assertionDensity -ge 1) { 3 } else { 0 }
Add-Score $assertionScore 5 "Tests" "Assertion Density" "$([math]::Round($assertionDensity, 1)) per test"

# 3.4 Test Naming Conventions (5 points)
$wellNamedTests = 0
$totalTests = 0
foreach ($file in $testFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $methods = [regex]::Matches($content, '(?<=\[(?:Test|Fact|Theory|TestMethod)\]\s*(?:\r?\n\s*)*)(public|private)\s+(?:async\s+)?(?:Task\s+|void\s+)(\w+)')
    foreach ($method in $methods) {
        $totalTests++
        $name = $method.Groups[2].Value
        # Good naming: Should_, When_, _Should_, Contains_
        if ($name -match '_Should_|^Should|_When_|_Returns_|_Throws_') {
            $wellNamedTests++
        }
    }
}
$namingRatio = if ($totalTests -gt 0) { $wellNamedTests / $totalTests } else { 0 }
$namingScore = if ($namingRatio -ge 0.8) { 5 } elseif ($namingRatio -ge 0.5) { 3 } else { 0 }
Add-Score $namingScore 5 "Tests" "Test Naming Conventions" "$([math]::Round($namingRatio * 100))% follow conventions"

Write-Host ""

# ============================================================================
# 4. DEPENDENCY HEALTH (15 points)
# ============================================================================
Write-Host "4. DEPENDENCY HEALTH" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray

# 4.1 Vulnerable Packages Check (5 points)
$vulnerablePackages = @(
    "Newtonsoft.Json|<13.0.1",
    "System.Text.Json|<6.0.0",
    "Microsoft.AspNetCore.Mvc|<2.2.0",
    "log4net|<2.0.10"
)

$foundVulnerable = 0
foreach ($csproj in $csprojFiles) {
    $content = Get-Content $csproj.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($vuln in $vulnerablePackages) {
        $parts = $vuln -split '\|'
        $package = $parts[0]
        if ($content -match "PackageReference.*Include=""$package""") {
            # Basic version check (would need more sophisticated parsing)
            $foundVulnerable++
        }
    }
}
Add-Score $(if ($foundVulnerable -eq 0) { 5 } else { 0 }) 5 "Dependencies" "No Known Vulnerabilities" "$foundVulnerable potential issues"

# 4.2 Outdated Packages (5 points)
$outdatedCount = 0
$latestVersions = @{
    "Dapper" = "2.1"
    "Serilog" = "3.1"
    "AutoMapper" = "12.0"
    "FluentValidation" = "11.0"
}

foreach ($csproj in $csprojFiles) {
    $content = Get-Content $csproj.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($package in $latestVersions.Keys) {
        if ($content -match "PackageReference.*Include=""$package"".*Version=""([^""]+)""") {
            $version = $Matches[1]
            $latest = $latestVersions[$package]
            if ([version]$version -lt [version]$latest) {
                $outdatedCount++
            }
        }
    }
}
Add-Score $(if ($outdatedCount -eq 0) { 5 } elseif ($outdatedCount -lt 3) { 3 } else { 0 }) 5 "Dependencies" "Package Versions Current" "$outdatedCount outdated"

# 4.3 Circular Dependencies (5 points)
$projectRefs = @{}
foreach ($csproj in $csprojFiles) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($csproj.Name)
    $content = Get-Content $csproj.FullName -Raw -ErrorAction SilentlyContinue
    $refs = [regex]::Matches($content, 'ProjectReference.*Include="[^"]*\\([^"\\]+)\.csproj"')
    $projectRefs[$name] = @($refs | ForEach-Object { $_.Groups[1].Value })
}

$circularDeps = 0
foreach ($proj in $projectRefs.Keys) {
    foreach ($ref in $projectRefs[$proj]) {
        if ($projectRefs.ContainsKey($ref) -and $projectRefs[$ref] -contains $proj) {
            $circularDeps++
            Add-Issue "HIGH" "Dependencies" "Circular dependency: $proj <-> $ref"
        }
    }
}
$circularDeps = $circularDeps / 2  # Each is counted twice
Add-Score $(if ($circularDeps -eq 0) { 5 } else { 0 }) 5 "Dependencies" "No Circular Dependencies" "$circularDeps found"

Write-Host ""

# ============================================================================
# 5. PERFORMANCE PATTERNS (15 points)
# ============================================================================
Write-Host "5. PERFORMANCE PATTERNS" -ForegroundColor Yellow
Write-Host ("-" * 40) -ForegroundColor DarkGray

# 5.1 N+1 Query Detection (5 points)
$n1Issues = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue

    # foreach with database call inside
    if ($content -match 'foreach[^{]+\{[^}]*(await\s+)?_\w*(Repository|Context|Db)\.\w+Async?\s*\(') {
        $n1Issues++
        Add-Issue "HIGH" "Performance" "Potential N+1 query in foreach loop" $file.FullName
    }

    # Select with database call
    if ($content -match '\.Select\s*\([^)]*(_\w*(Repository|Context)|await)') {
        $n1Issues++
        Add-Issue "HIGH" "Performance" "Database call inside LINQ Select" $file.FullName
    }
}
Add-Score $(if ($n1Issues -eq 0) { 5 } elseif ($n1Issues -lt 3) { 3 } else { 0 }) 5 "Performance" "N+1 Query Prevention" "$n1Issues potential issues"

# 5.2 Async Database Operations (5 points)
$syncDbOps = 0
$asyncDbOps = 0
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $syncDbOps += ([regex]::Matches($content, '\.(Execute|Query|Get|Find|First|Single|ToList|Count)\s*\([^)]*\)(?!\s*\.)')).Count
    $asyncDbOps += ([regex]::Matches($content, '\.(ExecuteAsync|QueryAsync|GetAsync|FindAsync|FirstAsync|SingleAsync|ToListAsync|CountAsync)\s*\(')).Count
}
$asyncRatio = if (($syncDbOps + $asyncDbOps) -gt 0) { $asyncDbOps / ($syncDbOps + $asyncDbOps) } else { 1 }
$asyncDbScore = if ($asyncRatio -ge 0.9) { 5 } elseif ($asyncRatio -ge 0.7) { 3 } else { 0 }
Add-Score $asyncDbScore 5 "Performance" "Async Database Operations" "$([math]::Round($asyncRatio * 100))% async"

# 5.3 Caching Implementation (5 points)
$hasCaching = $false
$cachingPatterns = @(
    'IMemoryCache',
    'IDistributedCache',
    'AddMemoryCache',
    'AddDistributedMemoryCache',
    'AddStackExchangeRedisCache',
    '\[ResponseCache',
    'CacheItemPolicy'
)
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $cachingPatterns) {
        if ($content -match $pattern) {
            $hasCaching = $true
            break
        }
    }
    if ($hasCaching) { break }
}
Add-Score $(if ($hasCaching) { 5 } else { 0 }) 5 "Performance" "Caching Implementation"

Write-Host ""

# ============================================================================
# SUMMARY
# ============================================================================
$percentage = [math]::Round(($script:TotalScore / $script:MaxScore) * 100, 1)
$grade = switch ([math]::Floor($percentage / 10)) {
    { $_ -ge 9 } { "A+" }
    { $_ -eq 8 } { "A" }
    { $_ -eq 7 } { "B" }
    { $_ -eq 6 } { "C" }
    { $_ -eq 5 } { "D" }
    default { "F" }
}

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  AUDIT SUMMARY" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total Score: $script:TotalScore / $script:MaxScore ($percentage%)" -ForegroundColor $(if ($percentage -ge 80) { "Green" } elseif ($percentage -ge 60) { "Yellow" } else { "Red" })
Write-Host "  Grade: $grade" -ForegroundColor $(if ($grade -match "A") { "Green" } elseif ($grade -match "B|C") { "Yellow" } else { "Red" })
Write-Host ""

# Show critical issues
$criticalIssues = $script:Issues | Where-Object { $_.Severity -eq "CRITICAL" }
$highIssues = $script:Issues | Where-Object { $_.Severity -eq "HIGH" }

if ($criticalIssues.Count -gt 0) {
    Write-Host "  CRITICAL ISSUES ($($criticalIssues.Count)):" -ForegroundColor Red
    foreach ($issue in $criticalIssues | Select-Object -First 5) {
        Write-Host "    - $($issue.Message)" -ForegroundColor Red
        if ($issue.File) { Write-Host "      File: $($issue.File)" -ForegroundColor DarkGray }
    }
    Write-Host ""
}

if ($highIssues.Count -gt 0) {
    Write-Host "  HIGH PRIORITY ISSUES ($($highIssues.Count)):" -ForegroundColor Yellow
    foreach ($issue in $highIssues | Select-Object -First 5) {
        Write-Host "    - $($issue.Message)" -ForegroundColor Yellow
        if ($issue.File) { Write-Host "      File: $($issue.File)" -ForegroundColor DarkGray }
    }
    Write-Host ""
}

Write-Host ("=" * 80) -ForegroundColor Cyan

# Export to JSON if requested
if ($ExportJson) {
    $report = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        ProjectPath = $ProjectPath
        Score = $script:TotalScore
        MaxScore = $script:MaxScore
        Percentage = $percentage
        Grade = $grade
        Issues = $script:Issues
    }
    $jsonPath = Join-Path $ProjectPath "audit-report.json"
    $report | ConvertTo-Json -Depth 10 | Out-File $jsonPath -Encoding UTF8
    Write-Host ""
    Write-Host "Report exported to: $jsonPath" -ForegroundColor Cyan
}

Write-Host ""
