#!/usr/bin/env pwsh
# Security Audit Script - No Admin Required
# Downloads portable security tools and runs comprehensive scan

$ErrorActionPreference = "Continue"

Write-Host "Security Audit - Enterprise Documentation Platform V2" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Gray
Write-Host ""

# Setup paths
$ROOT = $PSScriptRoot
if ([string]::IsNullOrEmpty($ROOT)) {
    $ROOT = Get-Location
}

$TOOLS = Join-Path $ROOT "tools-security"
$OUT = Join-Path $ROOT "security-audit-results"

# Create directories
New-Item -ItemType Directory -Force -Path $TOOLS | Out-Null
New-Item -ItemType Directory -Force -Path $OUT | Out-Null

Write-Host "Project Root: $ROOT" -ForegroundColor White
Write-Host "Tools Directory: $TOOLS" -ForegroundColor White
Write-Host "Output Directory: $OUT" -ForegroundColor White
Write-Host ""

# ============================================================================
# DOWNLOAD SECURITY TOOLS
# ============================================================================

Write-Host ">>> Downloading Security Tools (portable versions)" -ForegroundColor Yellow
Write-Host ""

# Download Gitleaks (Windows x64)
$gitleaksUrl = "https://github.com/gitleaks/gitleaks/releases/download/v8.18.0/gitleaks_8.18.0_windows_x64.zip"
$gitleaksZip = Join-Path $TOOLS "gitleaks.zip"
$gitleaksDir = Join-Path $TOOLS "gitleaks"

if (-not (Test-Path (Join-Path $gitleaksDir "gitleaks.exe"))) {
    Write-Host "   Downloading Gitleaks..." -ForegroundColor White
    try {
        Invoke-WebRequest -Uri $gitleaksUrl -OutFile $gitleaksZip -UseBasicParsing
        Expand-Archive -Path $gitleaksZip -DestinationPath $gitleaksDir -Force
        Remove-Item $gitleaksZip
        Write-Host "   SUCCESS Gitleaks downloaded" -ForegroundColor Green
    } catch {
        Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "   Gitleaks already present" -ForegroundColor Gray
}

Write-Host ""

# ============================================================================
# RUN SECURITY SCANS
# ============================================================================

Write-Host ">>> Running Security Scans" -ForegroundColor Yellow
Write-Host ""

# 1. GITLEAKS - Secret Scanning
Write-Host "1. Gitleaks Secret Scan" -ForegroundColor Cyan
Write-Host ("-" * 40) -ForegroundColor Gray

$gitleaksExe = Join-Path $gitleaksDir "gitleaks.exe"
if (Test-Path $gitleaksExe) {
    try {
        $gitleaksReport = Join-Path $OUT "gitleaks-report.json"
        $gitleaksLog = Join-Path $OUT "gitleaks.log"

        # Use array of arguments for proper execution
        $gitleaksArgs = @(
            "detect",
            "--source", $ROOT,
            "--report-format", "json",
            "--report-path", $gitleaksReport,
            "--no-git",
            "--verbose"
        )

        Write-Host "   Running gitleaks scan..." -ForegroundColor White

        # Execute with proper argument handling
        $process = Start-Process -FilePath $gitleaksExe -ArgumentList $gitleaksArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput $gitleaksLog -RedirectStandardError (Join-Path $OUT "gitleaks-error.log")

        if ($process.ExitCode -eq 0) {
            Write-Host "   SUCCESS: No secrets detected" -ForegroundColor Green
        } elseif ($process.ExitCode -eq 1) {
            Write-Host "   WARNING: Potential secrets found - check $gitleaksReport" -ForegroundColor Yellow
        } else {
            Write-Host "   COMPLETED with exit code: $($process.ExitCode)" -ForegroundColor White
        }

        if (Test-Path $gitleaksReport) {
            $findings = Get-Content $gitleaksReport -Raw | ConvertFrom-Json
            if ($findings -and $findings.Count -gt 0) {
                Write-Host "   Found: $($findings.Count) potential secret(s)" -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "   SKIPPED: Gitleaks not available" -ForegroundColor Gray
}

Write-Host ""

# 2. DOTNET VULNERABILITY CHECK
Write-Host "2. .NET Vulnerability Scan" -ForegroundColor Cyan
Write-Host ("-" * 40) -ForegroundColor Gray

$dotnetAvailable = Get-Command dotnet -ErrorAction SilentlyContinue

if ($dotnetAvailable) {
    try {
        Write-Host "   Checking NuGet packages for vulnerabilities..." -ForegroundColor White

        $vulnReport = Join-Path $OUT "dotnet-vulnerabilities.txt"

        # Find all .csproj files
        $csprojFiles = Get-ChildItem -Path $ROOT -Filter "*.csproj" -Recurse

        $vulnerabilitiesFound = $false

        foreach ($csproj in $csprojFiles) {
            Write-Host "   Scanning: $($csproj.Name)" -ForegroundColor White

            $result = dotnet list $csproj.FullName package --vulnerable 2>&1

            if ($result -match "has the following vulnerable packages") {
                $vulnerabilitiesFound = $true
                Add-Content -Path $vulnReport -Value "=== $($csproj.Name) ==="
                Add-Content -Path $vulnReport -Value $result
                Add-Content -Path $vulnReport -Value ""
            }
        }

        if ($vulnerabilitiesFound) {
            Write-Host "   WARNING: Vulnerable packages found - see $vulnReport" -ForegroundColor Yellow
        } else {
            Write-Host "   SUCCESS: No vulnerable packages detected" -ForegroundColor Green
        }
    } catch {
        Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "   SKIPPED: dotnet CLI not available" -ForegroundColor Gray
}

Write-Host ""

# 3. CUSTOM PATTERN SCAN
Write-Host "3. Custom Security Pattern Scan" -ForegroundColor Cyan
Write-Host ("-" * 40) -ForegroundColor Gray

try {
    Write-Host "   Scanning for sensitive patterns..." -ForegroundColor White

    $patternReport = Join-Path $OUT "pattern-scan.txt"
    $findings = @()

    # Define patterns to search for
    $patterns = @(
        @{Name="Hardcoded Passwords"; Pattern='password\s*=\s*".{3,}"'; Severity="HIGH"},
        @{Name="API Keys"; Pattern='api[_-]?key\s*=\s*".{10,}"'; Severity="HIGH"},
        @{Name="Connection Strings with Passwords"; Pattern='Password\s*=\s*[^;]{3,}'; Severity="MEDIUM"},
        @{Name="Private Keys"; Pattern='-----BEGIN (RSA |)PRIVATE KEY-----'; Severity="CRITICAL"},
        @{Name="AWS Access Keys"; Pattern='AKIA[0-9A-Z]{16}'; Severity="CRITICAL"},
        @{Name="Generic Secrets"; Pattern='secret\s*=\s*".{5,}"'; Severity="MEDIUM"}
    )

    # Scan .cs, .json, .config files
    $filesToScan = Get-ChildItem -Path $ROOT -Include "*.cs","*.json","*.config","*.xml","*.txt" -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git|tools-security)\\' }

    $totalFindings = 0

    foreach ($pattern in $patterns) {
        Write-Host "   Checking: $($pattern.Name)..." -ForegroundColor White

        $matches = $filesToScan | Select-String -Pattern $pattern.Pattern -AllMatches

        if ($matches) {
            $count = $matches.Count
            $totalFindings += $count

            $findings += "=== $($pattern.Name) [$($pattern.Severity)] ==="
            $findings += "Found $count occurrence(s):"

            foreach ($match in $matches | Select-Object -First 10) {
                $relativePath = $match.Path.Replace($ROOT, "").TrimStart('\').TrimStart('/')
                $findings += "  - $relativePath : Line $($match.LineNumber)"
            }

            if ($matches.Count -gt 10) {
                $findings += "  ... and $($matches.Count - 10) more"
            }

            $findings += ""
        }
    }

    if ($totalFindings -gt 0) {
        $findings | Out-File -FilePath $patternReport -Encoding UTF8
        Write-Host "   WARNING: Found $totalFindings potential issue(s) - see $patternReport" -ForegroundColor Yellow
    } else {
        Write-Host "   SUCCESS: No sensitive patterns detected" -ForegroundColor Green
    }

} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# ============================================================================
# GENERATE SUMMARY REPORT
# ============================================================================

Write-Host ">>> Generating Summary Report" -ForegroundColor Yellow
Write-Host ""

$summaryReport = Join-Path $OUT "SECURITY-AUDIT-SUMMARY.md"

$summary = @"
# Security Audit Summary

**Date:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Project:** Enterprise Documentation Platform V2

---

## Audit Results

### 1. Secret Scanning (Gitleaks)

"@

if (Test-Path (Join-Path $OUT "gitleaks-report.json")) {
    $gitleaksData = Get-Content (Join-Path $OUT "gitleaks-report.json") -Raw | ConvertFrom-Json
    if ($gitleaksData -and $gitleaksData.Count -gt 0) {
        $summary += "- **Status:** WARNING`n"
        $summary += "- **Findings:** $($gitleaksData.Count) potential secret(s) detected`n"
        $summary += "- **Report:** ``gitleaks-report.json```n`n"
    } else {
        $summary += "- **Status:** PASS`n"
        $summary += "- **Findings:** No secrets detected`n`n"
    }
} else {
    $summary += "- **Status:** COMPLETED`n"
    $summary += "- **Note:** Check gitleaks.log for details`n`n"
}

$summary += @"

### 2. NuGet Vulnerability Scan

"@

if (Test-Path (Join-Path $OUT "dotnet-vulnerabilities.txt")) {
    $summary += "- **Status:** WARNING`n"
    $summary += "- **Findings:** Vulnerable packages detected`n"
    $summary += "- **Report:** ``dotnet-vulnerabilities.txt```n`n"
} else {
    $summary += "- **Status:** PASS`n"
    $summary += "- **Findings:** No vulnerable packages`n`n"
}

$summary += @"

### 3. Custom Pattern Scan

"@

if (Test-Path (Join-Path $OUT "pattern-scan.txt")) {
    $patternContent = Get-Content (Join-Path $OUT "pattern-scan.txt") -Raw
    $summary += "- **Status:** WARNING`n"
    $summary += "- **Findings:** Sensitive patterns detected`n"
    $summary += "- **Report:** ``pattern-scan.txt```n`n"
} else {
    $summary += "- **Status:** PASS`n"
    $summary += "- **Findings:** No sensitive patterns detected`n`n"
}

$summary += @"

---

## Recommendations

1. **Review all findings** in the individual report files
2. **Remove hardcoded secrets** from source code
3. **Use environment variables** or Azure Key Vault for sensitive configuration
4. **Update vulnerable packages** to latest secure versions
5. **Add .gitignore entries** for sensitive files

---

## Report Files

All detailed reports are located in: ``$OUT``

- ``gitleaks-report.json`` - Secret scanning results
- ``dotnet-vulnerabilities.txt`` - NuGet vulnerability report
- ``pattern-scan.txt`` - Custom pattern findings

---

*Generated by Enterprise Security Audit Tool v1.0*
"@

$summary | Out-File -FilePath $summaryReport -Encoding UTF8

Write-Host "Security audit completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Summary Report: $summaryReport" -ForegroundColor Cyan
Write-Host "All Results: $OUT" -ForegroundColor Cyan
Write-Host ""
Write-Host ("=" * 70) -ForegroundColor Gray
