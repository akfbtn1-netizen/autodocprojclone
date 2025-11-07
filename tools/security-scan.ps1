#!/usr/bin/env pwsh
# Security scanning for CI/CD pipeline

param(
    [switch]$FailOnHigh,
    [switch]$GenerateReport
)

$ErrorActionPreference = "Stop"

Write-Host "Starting security scan..." -ForegroundColor Cyan

$issuesFound = @{
    Critical = 0
    High = 0
    Medium = 0
    Low = 0
}

# 1. Dependency vulnerability check
Write-Host "`n[1/3] Checking dependencies for vulnerabilities..." -ForegroundColor Yellow

try {
    # Install dotnet-outdated if not present
    $outdatedInstalled = dotnet tool list -g | Select-String "dotnet-outdated-tool"
    if (-not $outdatedInstalled) {
        Write-Host "  Installing dotnet-outdated tool..." -ForegroundColor Gray
        dotnet tool install -g dotnet-outdated-tool
    }
    
    # Check for outdated packages
    $outdatedResult = dotnet outdated --output json 2>&1 | ConvertFrom-Json -ErrorAction SilentlyContinue
    
    if ($outdatedResult) {
        foreach ($project in $outdatedResult.Projects) {
            foreach ($framework in $project.TargetFrameworks) {
                foreach ($dependency in $framework.Dependencies) {
                    if ($dependency.LatestVersion -ne $dependency.ResolvedVersion) {
                        $issuesFound.Medium++
                        Write-Host "  [MEDIUM] Outdated: $($dependency.Name) $($dependency.ResolvedVersion) -> $($dependency.LatestVersion)" -ForegroundColor Yellow
                    }
                }
            }
        }
    }
} catch {
    Write-Host "  [WARN] Could not check outdated packages: $_" -ForegroundColor Yellow
}

# 2. Secret scanning
Write-Host "`n[2/3] Scanning for secrets..." -ForegroundColor Yellow

$secretPatterns = @{
    'API Key' = @{
        pattern = '(api[_-]?key|apikey)["\s:=]+([a-zA-Z0-9]{20,})'
        severity = 'Critical'
    }
    'Password' = @{
        pattern = '(password|passwd)["\s:=]+([^"\s]{8,})'
        severity = 'Critical'
    }
    'Connection String' = @{
        pattern = 'Server=.+;.*Password=(?!@|{)[^;]+;'
        severity = 'Critical'
    }
    'Private Key' = @{
        pattern = '-----BEGIN (RSA |)PRIVATE KEY-----'
        severity = 'Critical'
    }
}

$scanFiles = Get-ChildItem -Path "src" -Include "*.cs","*.json","*.config" -Recurse -ErrorAction SilentlyContinue

foreach ($file in $scanFiles) {
    # Skip test files and templates
    if ($file.FullName -match 'Test|\.example\.|\.template\.') {
        continue
    }
    
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    foreach ($secretType in $secretPatterns.Keys) {
        $pattern = $secretPatterns[$secretType]
        if ($content -match $pattern.pattern) {
            $issuesFound[$pattern.severity]++
            Write-Host "  [$($pattern.severity.ToUpper())] Potential $secretType in: $($file.FullName)" -ForegroundColor Red
        }
    }
}

# 3. Code vulnerability patterns
Write-Host "`n[3/3] Checking for vulnerable code patterns..." -ForegroundColor Yellow

$vulnerablePatterns = @{
    'SQL Injection' = @{
        pattern = 'new SqlCommand\([^@]'
        severity = 'High'
        message = 'Potential SQL injection - use parameterized queries'
    }
    'XSS' = @{
        pattern = 'Html\.Raw\(|@Html\.Raw'
        severity = 'High'
        message = 'Potential XSS vulnerability - avoid Html.Raw with user input'
    }
    'Weak Crypto' = @{
        pattern = 'new MD5CryptoServiceProvider|new SHA1CryptoServiceProvider'
        severity = 'Medium'
        message = 'Weak cryptographic algorithm - use SHA256 or higher'
    }
}

$csFiles = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    foreach ($vulnType in $vulnerablePatterns.Keys) {
        $pattern = $vulnerablePatterns[$vulnType]
        if ($content -match $pattern.pattern) {
            $issuesFound[$pattern.severity]++
            Write-Host "  [$($pattern.severity.ToUpper())] $vulnType in: $($file.Name)" -ForegroundColor $(
                if ($pattern.severity -eq 'Critical') { 'Red' } else { 'Yellow' }
            )
            Write-Host "    $($pattern.message)" -ForegroundColor Gray
        }
    }
}

# Generate summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Security Scan Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Critical: $($issuesFound.Critical)" -ForegroundColor $(if ($issuesFound.Critical -gt 0) { 'Red' } else { 'Green' })
Write-Host "  High: $($issuesFound.High)" -ForegroundColor $(if ($issuesFound.High -gt 0) { 'Magenta' } else { 'Green' })
Write-Host "  Medium: $($issuesFound.Medium)" -ForegroundColor $(if ($issuesFound.Medium -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "  Low: $($issuesFound.Low)" -ForegroundColor Gray

# Determine exit code
$exitCode = 0

if ($issuesFound.Critical -gt 0) {
    Write-Host "`n[FAILED] Critical security issues must be resolved" -ForegroundColor Red
    $exitCode = 1
} elseif ($FailOnHigh -and $issuesFound.High -gt 0) {
    Write-Host "`n[FAILED] High severity issues found with -FailOnHigh enabled" -ForegroundColor Red
    $exitCode = 1
} elseif ($issuesFound.Critical + $issuesFound.High + $issuesFound.Medium -eq 0) {
    Write-Host "`n[PASSED] No significant security issues found" -ForegroundColor Green
} else {
    Write-Host "`n[WARNING] Security issues found but not blocking" -ForegroundColor Yellow
}

if ($GenerateReport) {
    $reportPath = "security-scan-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $issuesFound | ConvertTo-Json | Out-File $reportPath -Encoding UTF8
    Write-Host "`nReport saved: $reportPath" -ForegroundColor Cyan
}

exit $exitCode
