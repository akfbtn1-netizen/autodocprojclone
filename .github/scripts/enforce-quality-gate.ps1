#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Enforces strict quality gate standards using SonarCloud API

.DESCRIPTION
    This script fetches metrics from SonarCloud and enforces custom quality standards
    that are stricter than the default "Sonar way" quality gate.
    Works with free SonarCloud tier.

.PARAMETER ProjectKey
    The SonarCloud project key

.PARAMETER SonarToken
    SonarCloud authentication token

.PARAMETER Branch
    Branch name to analyze (optional, uses main branch metrics if not specified)

.PARAMETER PullRequestKey
    Pull request number for PR analysis (optional)

.EXAMPLE
    ./enforce-quality-gate.ps1 -ProjectKey "my-project" -SonarToken "token123"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectKey,

    [Parameter(Mandatory=$true)]
    [string]$SonarToken,

    [Parameter(Mandatory=$false)]
    [string]$Branch,

    [Parameter(Mandatory=$false)]
    [string]$PullRequestKey
)

$ErrorActionPreference = "Stop"

# SonarCloud API base URL
$apiBase = "https://sonarcloud.io/api"

# Quality gate thresholds - CUSTOMIZE THESE
$thresholds = @{
    # Overall Code Thresholds
    "coverage" = @{
        "overall_min" = 80.0
        "new_code_min" = 85.0
    }
    "duplicated_lines_density" = @{
        "overall_max" = 3.0
        "new_code_max" = 2.0
    }
    "blocker_violations" = @{
        "new_max" = 0
    }
    "critical_violations" = @{
        "new_max" = 0
    }
    "major_violations" = @{
        "new_max" = 5
    }
    "code_smells" = @{
        "new_max" = 10
    }
    "sqale_rating" = @{  # Maintainability Rating
        "max" = 1.0  # A rating
    }
    "reliability_rating" = @{
        "max" = 1.0  # A rating
    }
    "security_rating" = @{
        "max" = 1.0  # A rating
    }
}

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")

    $colorMap = @{
        "Red" = "91"
        "Green" = "92"
        "Yellow" = "93"
        "Blue" = "94"
        "Magenta" = "95"
        "Cyan" = "96"
        "White" = "97"
    }

    $code = $colorMap[$Color]
    Write-Host "`e[${code}m${Message}`e[0m"
}

function Get-SonarMetric {
    param(
        [string]$MetricKey,
        [string]$Component,
        [string]$AuthToken
    )

    try {
        $headers = @{
            "Authorization" = "Bearer $AuthToken"
        }

        $url = "$apiBase/measures/component?component=$Component&metricKeys=$MetricKey"

        if ($Branch) {
            $url += "&branch=$Branch"
        }

        if ($PullRequestKey) {
            $url += "&pullRequest=$PullRequestKey"
        }

        $response = Invoke-RestMethod -Uri $url -Headers $headers -Method Get

        $measure = $response.component.measures | Where-Object { $_.metric -eq $MetricKey } | Select-Object -First 1

        if ($measure) {
            if ($measure.value) {
                return [double]$measure.value
            }
            elseif ($measure.period.value) {
                return [double]$measure.period.value
            }
        }

        return $null
    }
    catch {
        Write-ColorOutput "Warning: Could not fetch metric '$MetricKey': $_" "Yellow"
        return $null
    }
}

function Test-QualityGate {
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "  ENTERPRISE QUALITY GATE ENFORCEMENT" "Cyan"
    Write-ColorOutput "========================================`n" "Cyan"

    Write-ColorOutput "Project: $ProjectKey" "Blue"
    if ($Branch) {
        Write-ColorOutput "Branch: $Branch" "Blue"
    }
    if ($PullRequestKey) {
        Write-ColorOutput "Pull Request: #$PullRequestKey" "Blue"
    }
    Write-ColorOutput ""

    $failures = @()
    $warnings = @()
    $passed = @()

    # Check Coverage (Overall)
    Write-ColorOutput "Checking Code Coverage..." "Blue"
    $coverage = Get-SonarMetric -MetricKey "coverage" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $coverage) {
        Write-Host "  Overall Coverage: $coverage%"
        if ($coverage -lt $thresholds.coverage.overall_min) {
            $failures += "Coverage is $coverage% (minimum: $($thresholds.coverage.overall_min)%)"
            Write-ColorOutput "  ❌ FAILED: Below minimum threshold" "Red"
        } else {
            $passed += "Coverage: $coverage%"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check New Code Coverage (for PRs)
    $newCoverage = Get-SonarMetric -MetricKey "new_coverage" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $newCoverage) {
        Write-Host "  New Code Coverage: $newCoverage%"
        if ($newCoverage -lt $thresholds.coverage.new_code_min) {
            $failures += "New code coverage is $newCoverage% (minimum: $($thresholds.coverage.new_code_min)%)"
            Write-ColorOutput "  ❌ FAILED: New code below minimum threshold" "Red"
        } else {
            $passed += "New Code Coverage: $newCoverage%"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Duplications
    Write-ColorOutput "`nChecking Code Duplications..." "Blue"
    $duplications = Get-SonarMetric -MetricKey "duplicated_lines_density" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $duplications) {
        Write-Host "  Duplicated Lines: $duplications%"
        if ($duplications -gt $thresholds.duplicated_lines_density.overall_max) {
            $failures += "Duplicated lines density is $duplications% (maximum: $($thresholds.duplicated_lines_density.overall_max)%)"
            Write-ColorOutput "  ❌ FAILED: Too many duplications" "Red"
        } else {
            $passed += "Duplications: $duplications%"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    $newDuplications = Get-SonarMetric -MetricKey "new_duplicated_lines_density" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $newDuplications) {
        Write-Host "  New Code Duplications: $newDuplications%"
        if ($newDuplications -gt $thresholds.duplicated_lines_density.new_code_max) {
            $failures += "New code duplications is $newDuplications% (maximum: $($thresholds.duplicated_lines_density.new_code_max)%)"
            Write-ColorOutput "  ❌ FAILED: New code has too many duplications" "Red"
        } else {
            $passed += "New Code Duplications: $newDuplications%"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Blocker Issues
    Write-ColorOutput "`nChecking Blocker Violations..." "Blue"
    $newBlockers = Get-SonarMetric -MetricKey "new_blocker_violations" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $newBlockers) {
        Write-Host "  New Blocker Issues: $newBlockers"
        if ($newBlockers -gt $thresholds.blocker_violations.new_max) {
            $failures += "Found $newBlockers new blocker issues (maximum: $($thresholds.blocker_violations.new_max))"
            Write-ColorOutput "  ❌ FAILED: Blocker issues found" "Red"
        } else {
            $passed += "No new blocker issues"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Critical Issues
    Write-ColorOutput "`nChecking Critical Violations..." "Blue"
    $newCritical = Get-SonarMetric -MetricKey "new_critical_violations" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $newCritical) {
        Write-Host "  New Critical Issues: $newCritical"
        if ($newCritical -gt $thresholds.critical_violations.new_max) {
            $failures += "Found $newCritical new critical issues (maximum: $($thresholds.critical_violations.new_max))"
            Write-ColorOutput "  ❌ FAILED: Critical issues found" "Red"
        } else {
            $passed += "No new critical issues"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Major Issues
    Write-ColorOutput "`nChecking Major Violations..." "Blue"
    $newMajor = Get-SonarMetric -MetricKey "new_major_violations" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $newMajor) {
        Write-Host "  New Major Issues: $newMajor"
        if ($newMajor -gt $thresholds.major_violations.new_max) {
            $failures += "Found $newMajor new major issues (maximum: $($thresholds.major_violations.new_max))"
            Write-ColorOutput "  ❌ FAILED: Too many major issues" "Red"
        } else {
            $passed += "Major issues: $newMajor (within limit)"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Code Smells
    Write-ColorOutput "`nChecking Code Smells..." "Blue"
    $newCodeSmells = Get-SonarMetric -MetricKey "new_code_smells" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $newCodeSmells) {
        Write-Host "  New Code Smells: $newCodeSmells"
        if ($newCodeSmells -gt $thresholds.code_smells.new_max) {
            $warnings += "Found $newCodeSmells new code smells (recommended maximum: $($thresholds.code_smells.new_max))"
            Write-ColorOutput "  ⚠️  WARNING: Many code smells found" "Yellow"
        } else {
            $passed += "Code smells: $newCodeSmells (within limit)"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Maintainability Rating
    Write-ColorOutput "`nChecking Maintainability Rating..." "Blue"
    $maintainability = Get-SonarMetric -MetricKey "sqale_rating" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $maintainability) {
        $rating = switch ($maintainability) {
            1 { "A" }
            2 { "B" }
            3 { "C" }
            4 { "D" }
            5 { "E" }
            default { "Unknown" }
        }
        Write-Host "  Maintainability Rating: $rating"
        if ($maintainability -gt $thresholds.sqale_rating.max) {
            $failures += "Maintainability rating is $rating (required: A)"
            Write-ColorOutput "  ❌ FAILED: Rating below A" "Red"
        } else {
            $passed += "Maintainability: $rating"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Reliability Rating
    Write-ColorOutput "`nChecking Reliability Rating..." "Blue"
    $reliability = Get-SonarMetric -MetricKey "reliability_rating" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $reliability) {
        $rating = switch ($reliability) {
            1 { "A" }
            2 { "B" }
            3 { "C" }
            4 { "D" }
            5 { "E" }
            default { "Unknown" }
        }
        Write-Host "  Reliability Rating: $rating"
        if ($reliability -gt $thresholds.reliability_rating.max) {
            $failures += "Reliability rating is $rating (required: A)"
            Write-ColorOutput "  ❌ FAILED: Rating below A" "Red"
        } else {
            $passed += "Reliability: $rating"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Check Security Rating
    Write-ColorOutput "`nChecking Security Rating..." "Blue"
    $security = Get-SonarMetric -MetricKey "security_rating" -Component $ProjectKey -AuthToken $SonarToken
    if ($null -ne $security) {
        $rating = switch ($security) {
            1 { "A" }
            2 { "B" }
            3 { "C" }
            4 { "D" }
            5 { "E" }
            default { "Unknown" }
        }
        Write-Host "  Security Rating: $rating"
        if ($security -gt $thresholds.security_rating.max) {
            $failures += "Security rating is $rating (required: A)"
            Write-ColorOutput "  ❌ FAILED: Rating below A" "Red"
        } else {
            $passed += "Security: $rating"
            Write-ColorOutput "  ✅ PASSED" "Green"
        }
    }

    # Print Summary
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "  QUALITY GATE SUMMARY" "Cyan"
    Write-ColorOutput "========================================`n" "Cyan"

    Write-ColorOutput "✅ Passed Checks: $($passed.Count)" "Green"
    foreach ($item in $passed) {
        Write-ColorOutput "  • $item" "Green"
    }

    if ($warnings.Count -gt 0) {
        Write-ColorOutput "`n⚠️  Warnings: $($warnings.Count)" "Yellow"
        foreach ($warning in $warnings) {
            Write-ColorOutput "  • $warning" "Yellow"
        }
    }

    if ($failures.Count -gt 0) {
        Write-ColorOutput "`n❌ Failed Checks: $($failures.Count)" "Red"
        foreach ($failure in $failures) {
            Write-ColorOutput "  • $failure" "Red"
        }

        Write-ColorOutput "`n========================================" "Red"
        Write-ColorOutput "  QUALITY GATE: FAILED" "Red"
        Write-ColorOutput "========================================" "Red"
        Write-ColorOutput "`nPlease fix the issues above before merging." "Red"
        Write-ColorOutput "See SonarCloud dashboard for details: https://sonarcloud.io/project/overview?id=$ProjectKey`n" "Blue"

        exit 1
    } else {
        Write-ColorOutput "`n========================================" "Green"
        Write-ColorOutput "  QUALITY GATE: PASSED ✅" "Green"
        Write-ColorOutput "========================================" "Green"
        if ($warnings.Count -gt 0) {
            Write-ColorOutput "`nNote: There are $($warnings.Count) warning(s) to review." "Yellow"
        }
        Write-ColorOutput ""

        exit 0
    }
}

# Wait for SonarCloud to process the analysis
Write-ColorOutput "Waiting for SonarCloud to complete analysis..." "Blue"
Write-ColorOutput "This can take 30-60 seconds..." "Yellow"

# Wait longer and retry if metrics aren't available
$maxRetries = 3
$retryCount = 0
$waitTime = 20

while ($retryCount -lt $maxRetries) {
    Start-Sleep -Seconds $waitTime

    # Try to fetch a simple metric to see if analysis is complete
    try {
        $testMetric = Get-SonarMetric -MetricKey "ncloc" -Component $ProjectKey -AuthToken $SonarToken
        if ($null -ne $testMetric) {
            Write-ColorOutput "Analysis complete! Running quality gate checks..." "Green"
            break
        }
    }
    catch {
        # Ignore errors during retry
    }

    $retryCount++
    if ($retryCount -lt $maxRetries) {
        Write-ColorOutput "Analysis not ready yet, waiting another $waitTime seconds... (Attempt $($retryCount + 1)/$maxRetries)" "Yellow"
    }
}

# Run quality gate check
Test-QualityGate
