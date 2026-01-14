# Enterprise Quality Gate - Pre-Commit Validation
# Validates complexity, method/class length, and documentation

param(
    [string]$ProjectPath = ".",
    [switch]$FailOnViolations = $true
)

Write-Host "Enterprise Quality Gate - Pre-Commit Validation" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Gray

# Get all C# files (exclude generated/bin/obj)
$changedFiles = Get-ChildItem -Path $ProjectPath -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" -and 
    $_.Name -ne "GlobalUsings.cs" -and
    $_.Name -notlike "*Designer.cs" -and
    $_.Name -notlike "*.g.cs"
} | Select-Object -ExpandProperty FullName

if ($changedFiles.Count -eq 0) {
    Write-Host "No C# files to validate" -ForegroundColor Green
    exit 0
}

Write-Host "Validating $($changedFiles.Count) files..." -ForegroundColor White

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
    $fileName = Split-Path $file -Leaf
    Write-Host "  Checking $fileName" -ForegroundColor Gray
    
    $content = Get-Content $file -Raw
    $violations = @()
    $fileScore = 100
    
    # Simple class length check using regex
    $classPattern = '(?s)class\s+\w+[^{]*\{(?:[^{}]*\{[^{}]*\})*[^{}]*\}'
    $classMatches = [regex]::Matches($content, $classPattern)
    foreach ($match in $classMatches) {
        $classLines = ($match.Value -split "`n").Count
        if ($classLines -gt $maxClassLines) {
            $violations += "Class has $classLines lines (max: $maxClassLines)"
            $fileScore -= 15
        }
    }
    
    # Simple method length check
    $methodPattern = '(?s)(public|private|protected|internal).*?\w+\s*\([^)]*\)\s*\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}'
    $methodMatches = [regex]::Matches($content, $methodPattern)
    foreach ($match in $methodMatches) {
        $methodLines = ($match.Value -split "`n").Count
        if ($methodLines -gt $maxMethodLines) {
            $violations += "Method has $methodLines lines (max: $maxMethodLines)"
            $fileScore -= 5
        }
    }
    
    # Simple complexity check - count decision points
    $complexityPoints = 0
    $complexityPoints += ([regex]::Matches($content, '\bif\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexityPoints += ([regex]::Matches($content, '\bwhile\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexityPoints += ([regex]::Matches($content, '\bfor\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexityPoints += ([regex]::Matches($content, '\bforeach\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexityPoints += ([regex]::Matches($content, '\bswitch\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexityPoints += ([regex]::Matches($content, '\bcatch\b', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    
    if ($complexityPoints -gt $maxComplexity) {
        $violations += "High complexity: $complexityPoints decision points (max: $maxComplexity)"
        $fileScore -= 10
    }
    
    # Documentation check
    $xmlDocCount = ([regex]::Matches($content, '///\s*<summary>')).Count
    $publicMembersCount = ([regex]::Matches($content, '(public class|public interface|public enum)')).Count
    if ($publicMembersCount -gt 0 -and $xmlDocCount -eq 0) {
        $violations += "Missing XML documentation for public members"
        $fileScore -= 3
    }
    
    $fileScore = [Math]::Max(0, $fileScore)
    $results += @{
        File = $fileName
        Score = $fileScore
        Violations = $violations
        IsValid = $fileScore -ge $minQualityScore
    }
    
    if ($violations.Count -gt 0) {
        $totalViolations += $violations.Count
        $failedFiles += $fileName
        
        Write-Host "    Score: $fileScore/100 (FAILED)" -ForegroundColor Red
        foreach ($violation in $violations) {
            Write-Host "      - $violation" -ForegroundColor Yellow
        }
    } else {
        Write-Host "    Score: $fileScore/100 (PASSED)" -ForegroundColor Green
    }
}

# Summary
Write-Host ""
Write-Host "QUALITY GATE RESULTS" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Gray

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
    Write-Host "ALL QUALITY GATES PASSED!" -ForegroundColor Green
    Write-Host "Ready for commit" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "QUALITY GATE VIOLATIONS DETECTED" -ForegroundColor Red
    Write-Host "Failed files:" -ForegroundColor Red
    foreach ($file in $failedFiles) {
        Write-Host "  - $file" -ForegroundColor Yellow
    }
    
    if ($FailOnViolations) {
        Write-Host ""
        Write-Host "COMMIT BLOCKED - Fix violations before committing" -ForegroundColor Red
        exit 1
    } else {
        Write-Host ""
        Write-Host "COMMIT ALLOWED - But please fix violations" -ForegroundColor Yellow
        exit 0
    }
}