#!/usr/bin/env pwsh
# Pre-commit hook for Enterprise Quality Validation
# Validates complexity ≤6, methods ≤20 lines, classes ≤200 lines

param(
    [string]$ProjectPath = ".",
    [switch]$FailOnViolations = $true
)

Write-Host "🔍 Enterprise Quality Gate - Pre-Commit Validation" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Gray

# Get all modified C# files
$changedFiles = @()
try {
    $gitDiff = git diff --cached --name-only --diff-filter=ACM | Where-Object { $_ -like "*.cs" }
    $changedFiles = $gitDiff | ForEach-Object { Join-Path $ProjectPath $_ }
} catch {
    Write-Host "⚠️ Not in a git repository, scanning all C# files..." -ForegroundColor Yellow
    $changedFiles = Get-ChildItem -Path $ProjectPath -Recurse -Filter "*.cs" | Where-Object { 
        $_.Name -ne "GlobalUsings.cs" -and $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" 
    } | Select-Object -ExpandProperty FullName
}

if ($changedFiles.Count -eq 0) {
    Write-Host "✅ No C# files to validate" -ForegroundColor Green
    exit 0
}

Write-Host "📁 Validating $($changedFiles.Count) files..." -ForegroundColor White

# Quality rules
$maxComplexity = 6
$maxMethodLines = 20
$maxClassLines = 200
$minQualityScore = 85

# Results tracking
$totalViolations = 0
$failedFiles = @()
$results = @()

foreach ($file in $changedFiles) {
    if (-not (Test-Path $file)) { continue }
    
    Write-Host "   📄 $($file.Split('\')[-1])" -ForegroundColor Gray
    
    $content = Get-Content $file -Raw
    $violations = @()
    $fileScore = 100
    
    # Parse with regex (simplified analysis)
    # Class length check
    $classMatches = [regex]::Matches($content, '(?s)class\s+\w+[^{]*\{(?:[^{}]*\{[^{}]*\})*[^{}]*\}')
    foreach ($match in $classMatches) {
        $classLines = ($match.Value -split "`n").Count
        if ($classLines -gt $maxClassLines) {
            $violations += "Class has $classLines lines (max: $maxClassLines)"
            $fileScore -= 15
        }
    }
    
    # Method length check
    $methodMatches = [regex]::Matches($content, '(?s)(public|private|protected|internal).*?\w+\s*\([^)]*\)\s*\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}')
    foreach ($match in $methodMatches) {
        $methodLines = ($match.Value -split "`n").Count
        if ($methodLines -gt $maxMethodLines) {
            $violations += "Method has $methodLines lines (max: $maxMethodLines)"
            $fileScore -= 5
        }
    }
    
    # Complexity check (simplified - count decision points)
    $complexityKeywords = @('if', 'while', 'for', 'foreach', 'switch', 'case', 'catch', '\?')
    $methodBodies = [regex]::Matches($content, '(?s)\w+\s*\([^)]*\)\s*\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}')
    foreach ($method in $methodBodies) {
        $complexity = 1 # Base complexity
        foreach ($keyword in $complexityKeywords) {
            $complexity += ([regex]::Matches($method.Value, $keyword, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
        }
        if ($complexity -gt $maxComplexity) {
            $violations += "Method has complexity $complexity (max: $maxComplexity)"
            $fileScore -= 10
        }
    }
    
    # Documentation check
    $xmlDocCount = ([regex]::Matches($content, '///\s*<summary>')).Count
    $publicMembersCount = ([regex]::Matches($content, '(public class|public interface|public enum|public struct)')).Count
    if ($publicMembersCount -gt 0 -and $xmlDocCount -eq 0) {
        $violations += "Missing XML documentation for public members"
        $fileScore -= 3
    }
    
    $fileScore = [Math]::Max(0, $fileScore)
    $results += @{
        File = $file
        Score = $fileScore
        Violations = $violations
        IsValid = $fileScore -ge $minQualityScore
    }
    
    if ($violations.Count -gt 0) {
        $totalViolations += $violations.Count
        $failedFiles += $file
        
        Write-Host "      ❌ Score: $fileScore/100" -ForegroundColor Red
        foreach ($violation in $violations) {
            Write-Host "         • $violation" -ForegroundColor Yellow
        }
    } else {
        Write-Host "      ✅ Score: $fileScore/100" -ForegroundColor Green
    }
}

# Summary
Write-Host ""
Write-Host "📊 QUALITY GATE RESULTS" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Gray

$averageScore = ($results | Measure-Object -Property Score -Average).Average
$validFiles = ($results | Where-Object { $_.IsValid }).Count
$invalidFiles = $results.Count - $validFiles

Write-Host "Files Analyzed: $($results.Count)" -ForegroundColor White
Write-Host "Valid Files: $validFiles" -ForegroundColor Green
Write-Host "Invalid Files: $invalidFiles" -ForegroundColor $(if ($invalidFiles -gt 0) { "Red" } else { "Green" })
Write-Host "Average Score: $($averageScore.ToString('F1'))/100" -ForegroundColor $(if ($averageScore -ge $minQualityScore) { "Green" } else { "Red" })
Write-Host "Total Violations: $totalViolations" -ForegroundColor $(if ($totalViolations -gt 0) { "Red" } else { "Green" })

if ($totalViolations -eq 0) {
    Write-Host ""
    Write-Host "🎉 ALL QUALITY GATES PASSED!" -ForegroundColor Green
    Write-Host "✅ Ready for commit" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "❌ QUALITY GATE VIOLATIONS DETECTED" -ForegroundColor Red
    Write-Host "Failed files:" -ForegroundColor Red
    foreach ($file in $failedFiles) {
        Write-Host "  • $($file.Split('\')[-1])" -ForegroundColor Yellow
    }
    
    if ($FailOnViolations) {
        Write-Host ""
        Write-Host "🚫 COMMIT BLOCKED - Fix violations before committing" -ForegroundColor Red
        exit 1
    } else {
        Write-Host ""
        Write-Host "COMMIT ALLOWED - But please fix violations" -ForegroundColor Yellow
        exit 0
    }
}