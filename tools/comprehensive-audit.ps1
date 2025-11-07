#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Comprehensive enterprise audit system with quality scoring
.DESCRIPTION
    Validates project structure, code quality, security, and documentation
    Produces detailed audit reports with actionable recommendations
#>

param(
    [string]$ProjectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2",
    [string]$OutputPath = ".\audit-reports",
    [switch]$FailOnWarnings,
    [int]$MinimumScore = 80
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ============================================================================
# CONFIGURATION
# ============================================================================

$AuditConfig = @{
    ProjectRoot = $ProjectRoot
    OutputPath = $OutputPath
    Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    
    # Quality thresholds
    Thresholds = @{
        Critical = 100  # Must be 100%
        High = 90       # Should be 90%+
        Medium = 80     # Should be 80%+
        Low = 70        # Should be 70%+
    }
    
    # Scoring weights (must sum to 100)
    Weights = @{
        ProjectStructure = 10
        CodeQuality = 25
        Security = 20
        Documentation = 15
        Testing = 15
        Performance = 10
        Maintainability = 5
    }
}

# ============================================================================
# AUDIT RESULTS TRACKING
# ============================================================================

$AuditResults = @{
    Timestamp = $AuditConfig.Timestamp
    ProjectRoot = $ProjectRoot
    Scores = @{}
    Issues = @{
        Critical = @()
        High = @()
        Medium = @()
        Low = @()
        Info = @()
    }
    Metrics = @{}
    Recommendations = @()
}

# ============================================================================
# UTILITY FUNCTIONS
# ============================================================================

function Write-AuditHeader {
    param([string]$Title)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-AuditSection {
    param([string]$Section)
    Write-Host "`n--- $Section ---" -ForegroundColor Yellow
}

function Add-Issue {
    param(
        [ValidateSet('Critical','High','Medium','Low','Info')]
        [string]$Severity,
        [string]$Category,
        [string]$Message,
        [string]$File = "",
        [string]$Recommendation = ""
    )
    
    $issue = @{
        Severity = $Severity
        Category = $Category
        Message = $Message
        File = $File
        Recommendation = $Recommendation
        Timestamp = Get-Date
    }
    
    $AuditResults.Issues[$Severity] += $issue
    
    $color = switch($Severity) {
        'Critical' { 'Red' }
        'High' { 'Magenta' }
        'Medium' { 'Yellow' }
        'Low' { 'Gray' }
        'Info' { 'Cyan' }
    }
    
    Write-Host "  [$Severity] $Message" -ForegroundColor $color
    if ($File) { Write-Host "    File: $File" -ForegroundColor Gray }
}

function Set-Score {
    param(
        [string]$Category,
        [double]$Score,
        [double]$MaxScore = 100
    )
    
    $percentage = ($Score / $MaxScore) * 100
    $AuditResults.Scores[$Category] = @{
        Score = $Score
        MaxScore = $MaxScore
        Percentage = $percentage
    }
    
    $color = if ($percentage -ge 90) { 'Green' } 
             elseif ($percentage -ge 80) { 'Yellow' } 
             else { 'Red' }
    
    Write-Host "  Score: $($percentage.ToString('F1'))% ($Score/$MaxScore)" -ForegroundColor $color
}

# ============================================================================
# AUDIT FUNCTIONS
# ============================================================================

function Test-ProjectStructure {
    Write-AuditHeader "PROJECT STRUCTURE AUDIT"
    
    $score = 0
    $maxScore = 100
    
    # Check required directories
    Write-AuditSection "Directory Structure"
    $requiredDirs = @(
        "src/Api",
        "src/Services",
        "src/WorkflowEngine",
        "src/WebApp",
        "tests",
        "docs",
        "tools",
        ".github/workflows"
    )
    
    foreach ($dir in $requiredDirs) {
        $fullPath = Join-Path $ProjectRoot $dir
        if (Test-Path $fullPath) {
            $score += (100 / $requiredDirs.Count)
            Write-Host "  [OK] $dir" -ForegroundColor Green
        } else {
            Add-Issue -Severity 'High' -Category 'Structure' `
                -Message "Missing required directory: $dir" `
                -Recommendation "Create directory: mkdir $fullPath"
        }
    }
    
    # Check critical files
    Write-AuditSection "Critical Files"
    $criticalFiles = @(
        "README.md",
        ".gitignore",
        "src/Api/Program.cs",
        "src/Api/appsettings.json"
    )
    
    foreach ($file in $criticalFiles) {
        $fullPath = Join-Path $ProjectRoot $file
        if (Test-Path $fullPath) {
            Write-Host "  [OK] $file" -ForegroundColor Green
        } else {
            Add-Issue -Severity 'Critical' -Category 'Structure' `
                -Message "Missing critical file: $file" `
                -File $fullPath `
                -Recommendation "Create required file"
            $score -= 5
        }
    }
    
    Set-Score -Category "ProjectStructure" -Score ([Math]::Max(0, $score)) -MaxScore $maxScore
}

function Test-CodeQuality {
    Write-AuditHeader "CODE QUALITY AUDIT"
    
    $score = 100
    $issues = 0
    
    # Find all C# files
    Write-AuditSection "C# Code Analysis"
    $csFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
    
    if ($csFiles.Count -eq 0) {
        Add-Issue -Severity 'High' -Category 'CodeQuality' `
            -Message "No C# files found in project"
        Set-Score -Category "CodeQuality" -Score 0 -MaxScore 100
        return
    }
    
    Write-Host "  Found $($csFiles.Count) C# files"
    
    foreach ($file in $csFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        
        # Check for common issues
        if ($content -match 'TODO:|HACK:|FIXME:') {
            Add-Issue -Severity 'Low' -Category 'CodeQuality' `
                -Message "Contains TODO/HACK/FIXME comments" `
                -File $file.FullName
            $issues++
        }
        
        if ($content -match 'Console\.WriteLine|System\.Diagnostics\.Debug') {
            Add-Issue -Severity 'Medium' -Category 'CodeQuality' `
                -Message "Contains debug statements" `
                -File $file.FullName `
                -Recommendation "Replace with proper logging (ILogger)"
            $issues++
            $score -= 2
        }
        
        if (-not ($content -match 'namespace\s+[\w\.]+')) {
            Add-Issue -Severity 'Medium' -Category 'CodeQuality' `
                -Message "Missing namespace declaration" `
                -File $file.FullName
            $issues++
            $score -= 2
        }
    }
    
    Write-Host "  Total issues found: $issues"
    Set-Score -Category "CodeQuality" -Score ([Math]::Max(0, $score)) -MaxScore 100
}

function Test-Security {
    Write-AuditHeader "SECURITY AUDIT"
    
    $score = 100
    $criticalIssues = 0
    
    Write-AuditSection "Secrets & Credentials Scan"
    
    $patterns = @{
        'API Key' = '(api[_-]?key|apikey)["\s:=]+[a-zA-Z0-9]{20,}'
        'Password' = '(password|passwd|pwd)["\s:=]+[^"\s]{8,}'
        'Connection String' = 'Server=.+;Database=.+;User Id=.+;Password=.+'
        'Private Key' = '-----BEGIN (RSA |)PRIVATE KEY-----'
        'AWS Secret' = '(aws[_-]?secret[_-]?access[_-]?key|AKIA[0-9A-Z]{16})'
    }
    
    $searchFiles = Get-ChildItem -Path $ProjectRoot -Include "*.cs","*.json","*.config","*.xml" -Recurse -ErrorAction SilentlyContinue
    
    foreach ($file in $searchFiles) {
        # Skip appsettings.json and similar config files that should have placeholders
        if ($file.Name -match 'appsettings|\.example\.|\.template\.') {
            continue
        }
        
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        
        foreach ($patternName in $patterns.Keys) {
            if ($content -match $patterns[$patternName]) {
                Add-Issue -Severity 'Critical' -Category 'Security' `
                    -Message "Potential hardcoded $patternName detected" `
                    -File $file.FullName `
                    -Recommendation "Move to Azure Key Vault or environment variables"
                $criticalIssues++
                $score -= 20
            }
        }
    }
    
    Write-AuditSection "Configuration Security"
    
    # Check appsettings.json
    $appsettings = Join-Path $ProjectRoot "src/Api/appsettings.json"
    if (Test-Path $appsettings) {
        $config = Get-Content $appsettings -Raw | ConvertFrom-Json
        
        if ($config.ConnectionStrings) {
            foreach ($connName in $config.ConnectionStrings.PSObject.Properties.Name) {
                $connString = $config.ConnectionStrings.$connName
                if ($connString -match 'Password=(?!@|{)[^;]{3,}') {
                    Add-Issue -Severity 'Critical' -Category 'Security' `
                        -Message "Connection string contains plain-text password: $connName" `
                        -File $appsettings `
                        -Recommendation "Use Azure Key Vault reference: @Microsoft.KeyVault(SecretUri=...)"
                    $criticalIssues++
                    $score -= 20
                }
            }
        }
    }
    
    if ($criticalIssues -eq 0) {
        Write-Host "  [OK] No critical security issues found" -ForegroundColor Green
    } else {
        Write-Host "  [CRITICAL] Found $criticalIssues critical security issues!" -ForegroundColor Red
    }
    
    Set-Score -Category "Security" -Score ([Math]::Max(0, $score)) -MaxScore 100
}

function Test-Documentation {
    Write-AuditHeader "DOCUMENTATION AUDIT"
    
    $score = 100
    
    Write-AuditSection "Required Documentation"
    
    $requiredDocs = @{
        "README.md" = "Project overview and setup instructions"
        "docs/ARCHITECTURE.md" = "System architecture documentation"
        "docs/API.md" = "API documentation"
        "docs/DEPLOYMENT.md" = "Deployment guide"
    }
    
    foreach ($doc in $requiredDocs.Keys) {
        $fullPath = Join-Path $ProjectRoot $doc
        if (Test-Path $fullPath) {
            $content = Get-Content $fullPath -Raw
            $wordCount = ($content -split '\s+').Count
            
            if ($wordCount -lt 100) {
                Add-Issue -Severity 'Medium' -Category 'Documentation' `
                    -Message "$doc exists but is minimal ($wordCount words)" `
                    -File $fullPath `
                    -Recommendation "Expand documentation to at least 100 words"
                $score -= 5
            } else {
                Write-Host "  [OK] $doc ($wordCount words)" -ForegroundColor Green
            }
        } else {
            Add-Issue -Severity 'High' -Category 'Documentation' `
                -Message "Missing required documentation: $doc" `
                -Recommendation $requiredDocs[$doc]
            $score -= 15
        }
    }
    
    Set-Score -Category "Documentation" -Score ([Math]::Max(0, $score)) -MaxScore 100
}

function Test-TestingInfrastructure {
    Write-AuditHeader "TESTING INFRASTRUCTURE AUDIT"
    
    $score = 0
    $maxScore = 100
    
    Write-AuditSection "Test Projects"
    
    # Check for test projects
    $testProjects = Get-ChildItem -Path (Join-Path $ProjectRoot "tests") -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue
    
    if ($testProjects.Count -eq 0) {
        Add-Issue -Severity 'High' -Category 'Testing' `
            -Message "No test projects found" `
            -Recommendation "Create test projects using: dotnet new xunit"
        Set-Score -Category "Testing" -Score 0 -MaxScore 100
        return
    }
    
    Write-Host "  Found $($testProjects.Count) test project(s)" -ForegroundColor Green
    $score += 30
    
    # Check for test files
    $testFiles = @(Get-ChildItem -Path (Join-Path $ProjectRoot "tests") -Filter "*Tests.cs" -Recurse -ErrorAction SilentlyContinue)
    
    if ($testFiles -and $testFiles.Count -gt 0) {
        Write-Host "  Found $($testFiles.Count) test file(s)" -ForegroundColor Green
        $score += 40
        
        # Calculate test metrics
        $totalTests = 0
        foreach ($file in $testFiles) {
            $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
            if ($content) {
                $testCount = ([regex]::Matches($content, '\[Fact\]|\[Theory\]')).Count
                $totalTests += $testCount
            }
        }
        
        if ($totalTests -gt 0) {
            Write-Host "  Total test methods: $totalTests" -ForegroundColor Green
            $score += 30
        } else {
            Add-Issue -Severity 'Medium' -Category 'Testing' `
                -Message "Test files exist but contain no test methods" `
                -Recommendation "Add [Fact] or [Theory] test methods"
        }
    } else {
        Add-Issue -Severity 'High' -Category 'Testing' `
            -Message "No test files found" `
            -Recommendation "Create test files with naming pattern *Tests.cs"
    }
    
    Set-Score -Category "Testing" -Score $score -MaxScore $maxScore
}

function Test-Performance {
    Write-AuditHeader "PERFORMANCE AUDIT"
    
    $score = 100
    
    Write-AuditSection "Performance Best Practices"
    
    # Check for async/await usage
    $csFiles = Get-ChildItem -Path (Join-Path $ProjectRoot "src") -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
    $asyncCount = 0
    $syncDbCalls = 0
    
    foreach ($file in $csFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }
        
        if ($content -match '\basync\s+Task') {
            $asyncCount++
        }
        
        # Check for synchronous DB calls
        if ($content -match '\.ToList\(\)|\.FirstOrDefault\(\)|\.Count\(\)') {
            if ($content -notmatch 'await.*\.ToListAsync\(\)|await.*\.FirstOrDefaultAsync\(\)|await.*\.CountAsync\(\)') {
                Add-Issue -Severity 'Low' -Category 'Performance' `
                    -Message "Potential synchronous database call" `
                    -File $file.FullName `
                    -Recommendation "Use async methods (ToListAsync, FirstOrDefaultAsync, etc.)"
                $syncDbCalls++
                $score -= 2
            }
        }
    }
    
    if ($asyncCount -gt 0) {
        Write-Host "  [OK] Found $asyncCount async methods" -ForegroundColor Green
    } else {
        Add-Issue -Severity 'Medium' -Category 'Performance' `
            -Message "No async methods found" `
            -Recommendation "Use async/await for I/O operations"
        $score -= 10
    }
    
    if ($syncDbCalls -gt 0) {
        Write-Host "  [WARNING] Found $syncDbCalls potential synchronous DB calls" -ForegroundColor Yellow
    }
    
    Set-Score -Category "Performance" -Score ([Math]::Max(0, $score)) -MaxScore 100
}

function Test-Maintainability {
    Write-AuditHeader "MAINTAINABILITY AUDIT"
    
    $score = 100
    
    Write-AuditSection "Code Organization"
    
    # Check for proper separation of concerns
    $srcPath = Join-Path $ProjectRoot "src"
    $hasControllers = Test-Path (Join-Path $srcPath "Api/Controllers")
    $hasServices = Test-Path (Join-Path $srcPath "Services")
    $hasModels = Test-Path (Join-Path $srcPath "Api/Models")
    
    if ($hasControllers) { 
        Write-Host "  [OK] Controllers directory exists" -ForegroundColor Green 
    } else {
        Add-Issue -Severity 'Medium' -Category 'Maintainability' `
            -Message "No Controllers directory found" `
            -Recommendation "Organize API endpoints in Controllers directory"
        $score -= 20
    }
    
    if ($hasServices) { 
        Write-Host "  [OK] Services directory exists" -ForegroundColor Green 
    } else {
        Add-Issue -Severity 'Medium' -Category 'Maintainability' `
            -Message "No Services directory found" `
            -Recommendation "Separate business logic into Services"
        $score -= 20
    }
    
    if ($hasModels) { 
        Write-Host "  [OK] Models directory exists" -ForegroundColor Green 
    } else {
        Add-Issue -Severity 'Low' -Category 'Maintainability' `
            -Message "No Models directory found" `
            -Recommendation "Organize data models in Models directory"
        $score -= 10
    }
    
    Set-Score -Category "Maintainability" -Score ([Math]::Max(0, $score)) -MaxScore 100
}

# ============================================================================
# REPORT GENERATION
# ============================================================================

function New-AuditReport {
    Write-AuditHeader "GENERATING AUDIT REPORT"
    
    # Calculate final score
    $finalScore = 0
    foreach ($category in $AuditConfig.Weights.Keys) {
        if ($AuditResults.Scores.ContainsKey($category)) {
            $categoryScore = $AuditResults.Scores[$category].Percentage
            $weight = $AuditConfig.Weights[$category] / 100
            $finalScore += $categoryScore * $weight
        }
    }
    
    # Display summary
    Write-Host "`n========================================"
    Write-Host "  AUDIT SUMMARY"
    Write-Host "========================================" -ForegroundColor Cyan
    
    Write-Host "`nSCORES BY CATEGORY:"
    foreach ($category in $AuditResults.Scores.Keys | Sort-Object) {
        $score = $AuditResults.Scores[$category]
        $weight = $AuditConfig.Weights[$category]
        $icon = if ($score.Percentage -ge 90) { "[OK]" } elseif ($score.Percentage -ge 80) { "[WARN]" } else { "[FAIL]" }
        $color = if ($score.Percentage -ge 90) { "Green" } elseif ($score.Percentage -ge 80) { "Yellow" } else { "Red" }
        
        Write-Host "  $icon ${category}: $($score.Percentage.ToString('F1'))/100 ($weight% weight)" -ForegroundColor $color
    }
    
    Write-Host "`nOVERALL QUALITY SCORE: $($finalScore.ToString('F1'))/100" -ForegroundColor $(
        if ($finalScore -ge 90) { 'Green' } elseif ($finalScore -ge 80) { 'Yellow' } else { 'Red' }
    )
    
    Write-Host "`nISSUES SUMMARY:"
    Write-Host "  Critical: $($AuditResults.Issues.Critical.Count)" -ForegroundColor Red
    Write-Host "  High: $($AuditResults.Issues.High.Count)" -ForegroundColor Magenta
    Write-Host "  Medium: $($AuditResults.Issues.Medium.Count)" -ForegroundColor Yellow
    Write-Host "  Low: $($AuditResults.Issues.Low.Count)" -ForegroundColor Gray
    
    # Save JSON report
    $reportPath = Join-Path $OutputPath "audit-$($AuditConfig.Timestamp).json"
    $null = New-Item -ItemType Directory -Path $OutputPath -Force
    
    $AuditResults | ConvertTo-Json -Depth 10 | Out-File $reportPath -Encoding UTF8
    Write-Host "`nDetailed report saved: $reportPath" -ForegroundColor Cyan
    
    # Create markdown report
    $mdReport = @"
# Enterprise Documentation Platform - Audit Report
**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Project:** $ProjectRoot

## Overall Score: $($finalScore.ToString('F1'))/100

## Scores by Category
$(foreach ($cat in $AuditResults.Scores.Keys | Sort-Object) {
    $s = $AuditResults.Scores[$cat]
    "- **${cat}**: $($s.Percentage.ToString('F1'))% ($($s.Score)/$($s.MaxScore))"
})

## Critical Issues ($($AuditResults.Issues.Critical.Count))
$(if ($AuditResults.Issues.Critical.Count -gt 0) {
    foreach ($issue in $AuditResults.Issues.Critical) {
        "- [$($issue.Category)] $($issue.Message)"
        if ($issue.File) { "  - File: ``$($issue.File)``" }
        if ($issue.Recommendation) { "  - **Recommendation:** $($issue.Recommendation)" }
    }
} else { "*No critical issues found*" })

## High Priority Issues ($($AuditResults.Issues.High.Count))
$(if ($AuditResults.Issues.High.Count -gt 0) {
    foreach ($issue in $AuditResults.Issues.High) {
        "- [$($issue.Category)] $($issue.Message)"
        if ($issue.Recommendation) { "  - **Recommendation:** $($issue.Recommendation)" }
    }
} else { "*No high priority issues found*" })

---
*Report generated by Enterprise Audit System*
"@
    
    $mdPath = Join-Path $OutputPath "audit-$($AuditConfig.Timestamp).md"
    $mdReport | Out-File $mdPath -Encoding UTF8
    Write-Host "Markdown report saved: $mdPath" -ForegroundColor Cyan
    
    return $finalScore
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

try {
    Write-Host "Enterprise Documentation Platform - Comprehensive Audit" -ForegroundColor Cyan
    Write-Host "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    Write-Host "Project: $ProjectRoot`n" -ForegroundColor Gray
    
    # Verify project exists
    if (-not (Test-Path $ProjectRoot)) {
        throw "Project root not found: $ProjectRoot"
    }
    
    # Run all audits
    Test-ProjectStructure
    Test-CodeQuality
    Test-Security
    Test-Documentation
    Test-TestingInfrastructure
    Test-Performance
    Test-Maintainability
    
    # Generate report
    $finalScore = New-AuditReport
    
    # Determine exit code
    $exitCode = 0
    $criticalCount = $AuditResults.Issues.Critical.Count
    
    if ($criticalCount -gt 0) {
        Write-Host "`n[FAILED] $criticalCount critical issues must be resolved!" -ForegroundColor Red
        $exitCode = 1
    } elseif ($finalScore -lt $MinimumScore) {
        Write-Host "`n[FAILED] Score $($finalScore.ToString('F1')) is below minimum threshold of $MinimumScore" -ForegroundColor Red
        $exitCode = 1
    } elseif ($FailOnWarnings -and $AuditResults.Issues.High.Count -gt 0) {
        Write-Host "`n[FAILED] High priority issues found with -FailOnWarnings enabled" -ForegroundColor Red
        $exitCode = 1
    } else {
        Write-Host "`n[PASSED] Audit completed successfully!" -ForegroundColor Green
    }
    
    Write-Host "`nCompleted: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    exit $exitCode
    
} catch {
    Write-Host "`n[ERROR] Audit failed: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 2
}
