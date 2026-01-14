# Enterprise Comprehensive Audit System
# Industry-standard code quality and architecture assessment

param(
    [string]$ProjectPath = ".",
    [switch]$Detailed = $false
)

Write-Host "ENTERPRISE COMPREHENSIVE AUDIT SYSTEM" -ForegroundColor Cyan
Write-Host "Industry-Standard Code Quality & Architecture Assessment" -ForegroundColor Gray
Write-Host "=======================================================" -ForegroundColor Gray
Write-Host ""

$rootPath = Resolve-Path $ProjectPath
$srcPath = Join-Path $rootPath "src"

# Industry-standard thresholds
$QualityThresholds = @{
    MaxCyclomaticComplexity = 15    # SonarQube standard
    MaxMethodLines = 50             # Microsoft standard  
    MaxClassLines = 500             # Industry standard
    MaxParameterCount = 7           # Clean Code standard
    MinDocumentationCoverage = 60   # Enterprise standard
    MaxDuplicationPercentage = 5    # Industry standard
    MinTestCoverage = 70           # Microsoft standard
}

function Get-CSharpFiles($path) {
    if (-not (Test-Path $path)) { return @() }
    
    $excludePatterns = @("*\bin\*", "*\obj\*", "*Migrations*", "*.Designer.cs", "*.g.cs")
    $files = Get-ChildItem -Path $path -Recurse -Filter "*.cs" | Where-Object {
        $file = $_.FullName
        $exclude = $false
        foreach ($pattern in $excludePatterns) {
            if ($file -like $pattern) { $exclude = $true; break }
        }
        return -not $exclude
    }
    return $files
}

function Measure-CyclomaticComplexity($content) {
    $complexity = 1 # Base complexity
    
    # Decision points
    $complexity += ([regex]::Matches($content, '\bif\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bwhile\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bfor\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bforeach\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bcase\s+', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bcatch\s*[\(\{]', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '&&')).Count
    $complexity += ([regex]::Matches($content, '\|\|')).Count
    $complexity += ([regex]::Matches($content, '\?[^?]*:')).Count
    
    return $complexity
}

function Analyze-Architecture($srcPath) {
    Write-Host "ARCHITECTURE ANALYSIS" -ForegroundColor Yellow
    Write-Host "====================" -ForegroundColor Gray
    
    $archScore = 0
    
    # Layer structure analysis
    $layers = @{
        "Domain" = Join-Path $srcPath "Core\Domain"
        "Application" = Join-Path $srcPath "Core\Application" 
        "Infrastructure" = Join-Path $srcPath "Core\Infrastructure"
        "API" = Join-Path $srcPath "Api"
    }
    
    $layerCompliance = 0
    foreach ($layer in $layers.GetEnumerator()) {
        $exists = Test-Path $layer.Value
        if ($exists) {
            $fileCount = (Get-CSharpFiles $layer.Value).Count
            Write-Host "   Layer $($layer.Key): $fileCount files" -ForegroundColor Green
            $layerCompliance += 25
        } else {
            Write-Host "   Layer $($layer.Key): Missing" -ForegroundColor Red
        }
    }
    $archScore += $layerCompliance * 0.4
    
    # CQRS pattern check
    $commandsPath = Join-Path $srcPath "Core\Application\Commands"
    $queriesPath = Join-Path $srcPath "Core\Application\Queries"
    $cqrsScore = 0
    
    if (Test-Path $commandsPath) { 
        $commandCount = (Get-CSharpFiles $commandsPath).Count
        Write-Host "   Commands: $commandCount files" -ForegroundColor Green
        $cqrsScore += 50 
    }
    if (Test-Path $queriesPath) { 
        $queryCount = (Get-CSharpFiles $queriesPath).Count
        Write-Host "   Queries: $queryCount files" -ForegroundColor Green
        $cqrsScore += 50 
    }
    $archScore += $cqrsScore * 0.3
    
    # Repository pattern
    $repoPath = Join-Path $srcPath "Core\Infrastructure\Persistence\Repositories"
    $repoScore = if (Test-Path $repoPath) { 
        $repoCount = (Get-CSharpFiles $repoPath).Count
        Write-Host "   Repositories: $repoCount files" -ForegroundColor Green
        100 
    } else { 
        Write-Host "   Repositories: Missing" -ForegroundColor Yellow
        50 
    }
    $archScore += $repoScore * 0.3
    
    Write-Host "   Architecture Score: $($archScore.ToString('F1'))/100" -ForegroundColor Cyan
    Write-Host ""
    
    return $archScore
}

function Analyze-CodeQuality($allFiles) {
    Write-Host "CODE QUALITY ANALYSIS" -ForegroundColor Yellow
    Write-Host "====================" -ForegroundColor Gray
    
    $totalFiles = $allFiles.Count
    $totalLines = 0
    $totalComplexity = 0
    $violations = @{
        Complexity = 0
        MethodLength = 0
        ClassLength = 0
        Documentation = 0
    }
    
    $qualityScores = @()
    
    foreach ($file in $allFiles) {
        $fileName = $file.Name
        $content = Get-Content $file.FullName -Raw
        $lines = ($content -split "`n").Count
        $totalLines += $lines
        
        $fileScore = 100
        $fileViolations = @()
        
        # Complexity analysis
        $complexity = Measure-CyclomaticComplexity $content
        $totalComplexity += $complexity
        if ($complexity -gt $QualityThresholds.MaxCyclomaticComplexity) {
            $violations.Complexity++
            $fileViolations += "High complexity: $complexity (max: $($QualityThresholds.MaxCyclomaticComplexity))"
            $fileScore -= [Math]::Min(30, ($complexity - $QualityThresholds.MaxCyclomaticComplexity) * 2)
        }
        
        # Method length analysis
        $methodPattern = '(?s)(public|private|protected|internal).*?\b\w+\s*\([^)]*\)\s*\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}'
        $methodMatches = [regex]::Matches($content, $methodPattern)
        foreach ($match in $methodMatches) {
            $methodLines = ($match.Value -split "`n").Count
            if ($methodLines -gt $QualityThresholds.MaxMethodLines) {
                $violations.MethodLength++
                $fileViolations += "Long method: $methodLines lines (max: $($QualityThresholds.MaxMethodLines))"
                $fileScore -= 5
            }
        }
        
        # Class length analysis
        if ($lines -gt $QualityThresholds.MaxClassLines) {
            $violations.ClassLength++
            $fileViolations += "Large class: $lines lines (max: $($QualityThresholds.MaxClassLines))"
            $fileScore -= 10
        }
        
        # Documentation analysis
        $xmlDocCount = ([regex]::Matches($content, '///\s*<summary>')).Count
        $publicMethods = ([regex]::Matches($content, 'public\s+(?:static\s+)?(?:async\s+)?(?:Task<?[^>]*>?|void|\w+)\s+\w+\s*\(')).Count
        $publicClasses = ([regex]::Matches($content, 'public\s+(?:partial\s+)?(?:class|interface|enum|struct)\s+\w+')).Count
        
        $expectedDocs = $publicMethods + $publicClasses
        if ($expectedDocs -gt 0) {
            $docCoverage = ($xmlDocCount / $expectedDocs) * 100
            if ($docCoverage -lt $QualityThresholds.MinDocumentationCoverage) {
                $violations.Documentation++
                $fileViolations += "Low documentation: $($docCoverage.ToString('F1')) percent (min: $($QualityThresholds.MinDocumentationCoverage) percent)"
                $fileScore -= 5
            }
        }
        
        $fileScore = [Math]::Max(0, $fileScore)
        $qualityScores += $fileScore
        
        if ($fileViolations.Count -gt 0 -and $Detailed) {
            Write-Host "   File $fileName : $($fileScore.ToString('F0'))/100" -ForegroundColor $(if ($fileScore -ge 80) { "Green" } elseif ($fileScore -ge 60) { "Yellow" } else { "Red" })
            foreach ($violation in $fileViolations) {
                Write-Host "      - $violation" -ForegroundColor Gray
            }
        }
    }
    
    # Calculate metrics
    $avgLinesPerFile = if ($totalFiles -gt 0) { [Math]::Round($totalLines / $totalFiles, 1) } else { 0 }
    $avgComplexity = if ($totalFiles -gt 0) { [Math]::Round($totalComplexity / $totalFiles, 1) } else { 0 }
    $avgQualityScore = if ($qualityScores.Count -gt 0) { [Math]::Round(($qualityScores | Measure-Object -Average).Average, 1) } else { 0 }
    
    Write-Host ""
    Write-Host "   Code Quality Metrics:" -ForegroundColor White
    Write-Host "      Total Files: $totalFiles" -ForegroundColor Gray
    Write-Host "      Total Lines: $($totalLines.ToString('N0'))" -ForegroundColor Gray
    Write-Host "      Avg Lines/File: $avgLinesPerFile" -ForegroundColor Gray
    Write-Host "      Avg Complexity: $avgComplexity" -ForegroundColor Gray
    Write-Host "      Avg Quality Score: $avgQualityScore/100" -ForegroundColor $(if ($avgQualityScore -ge 80) { "Green" } elseif ($avgQualityScore -ge 60) { "Yellow" } else { "Red" })
    Write-Host ""
    Write-Host "   Violations Summary:" -ForegroundColor Yellow
    Write-Host "      Complexity: $($violations.Complexity)" -ForegroundColor Gray
    Write-Host "      Method Length: $($violations.MethodLength)" -ForegroundColor Gray
    Write-Host "      Class Length: $($violations.ClassLength)" -ForegroundColor Gray
    Write-Host "      Documentation: $($violations.Documentation)" -ForegroundColor Gray
    Write-Host ""
    
    return @{
        Score = $avgQualityScore
        TotalFiles = $totalFiles
        TotalLines = $totalLines
        AvgComplexity = $avgComplexity
        Violations = $violations
    }
}

function Analyze-TestCoverage($rootPath) {
    Write-Host "TEST COVERAGE ANALYSIS" -ForegroundColor Yellow
    Write-Host "=====================" -ForegroundColor Gray
    
    $testPaths = @(
        Join-Path $rootPath "tests\Unit"
        Join-Path $rootPath "tests\Integration"
    )
    
    $testScore = 0
    $totalTestFiles = 0
    
    foreach ($testPath in $testPaths) {
        if (Test-Path $testPath) {
            $testFiles = Get-CSharpFiles $testPath
            $totalTestFiles += $testFiles.Count
            Write-Host "   $($testPath.Split('\')[-1]): $($testFiles.Count) test files" -ForegroundColor Green
            $testScore += 50
        } else {
            Write-Host "   $($testPath.Split('\')[-1]): Missing" -ForegroundColor Red
        }
    }
    
    Write-Host "   Test Infrastructure Score: $testScore/100" -ForegroundColor Cyan
    Write-Host ""
    
    return $testScore
}

# Main execution
Write-Host "Starting comprehensive audit of: $rootPath" -ForegroundColor White
Write-Host ""

# Get all source files
$allFiles = Get-CSharpFiles $srcPath
if ($allFiles.Count -eq 0) {
    Write-Host "No C# files found in $srcPath" -ForegroundColor Red
    exit 1
}

# Run analyses
$architectureScore = Analyze-Architecture $srcPath
$codeQualityResults = Analyze-CodeQuality $allFiles  
$testScore = Analyze-TestCoverage $rootPath

# Calculate final score (weighted)
$finalScore = (
    ($architectureScore * 0.30) +           # 30% Architecture
    ($codeQualityResults.Score * 0.50) +    # 50% Code Quality
    ($testScore * 0.20)                     # 20% Testing
)

# Generate report
Write-Host "COMPREHENSIVE AUDIT RESULTS" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Gray
Write-Host ""

Write-Host "CATEGORY SCORES:" -ForegroundColor White
Write-Host "   Architecture: $($architectureScore.ToString('F1'))/100 (30 percent weight)" -ForegroundColor $(if ($architectureScore -ge 80) { "Green" } elseif ($architectureScore -ge 60) { "Yellow" } else { "Red" })
Write-Host "   Code Quality: $($codeQualityResults.Score.ToString('F1'))/100 (50 percent weight)" -ForegroundColor $(if ($codeQualityResults.Score -ge 80) { "Green" } elseif ($codeQualityResults.Score -ge 60) { "Yellow" } else { "Red" })
Write-Host "   Test Coverage: $($testScore.ToString('F1'))/100 (20 percent weight)" -ForegroundColor $(if ($testScore -ge 80) { "Green" } elseif ($testScore -ge 60) { "Yellow" } else { "Red" })
Write-Host ""

Write-Host "OVERALL QUALITY SCORE: $($finalScore.ToString('F1'))/100" -ForegroundColor $(if ($finalScore -ge 80) { "Green" } elseif ($finalScore -ge 60) { "Yellow" } else { "Red" })

# Grade assignment
$grade = ""
$status = ""
if ($finalScore -ge 90) { $grade = "A+"; $status = "EXCELLENT" }
elseif ($finalScore -ge 85) { $grade = "A"; $status = "VERY GOOD" }
elseif ($finalScore -ge 80) { $grade = "A-"; $status = "GOOD" }
elseif ($finalScore -ge 75) { $grade = "B+"; $status = "ABOVE AVERAGE" }
elseif ($finalScore -ge 70) { $grade = "B"; $status = "SATISFACTORY" }
elseif ($finalScore -ge 65) { $grade = "B-"; $status = "NEEDS IMPROVEMENT" }
elseif ($finalScore -ge 60) { $grade = "C+"; $status = "POOR" }
else { $grade = "D"; $status = "CRITICAL" }

Write-Host "GRADE: $grade ($status)" -ForegroundColor $(if ($finalScore -ge 80) { "Green" } elseif ($finalScore -ge 60) { "Yellow" } else { "Red" })
Write-Host ""

# Recommendations
Write-Host "RECOMMENDATIONS:" -ForegroundColor Yellow
Write-Host "===============" -ForegroundColor Gray

if ($finalScore -ge 80) {
    Write-Host "Good work! Your codebase meets industry standards." -ForegroundColor Green
} else {
    Write-Host "Priority Actions Required:" -ForegroundColor Yellow
    
    if ($codeQualityResults.Violations.Complexity -gt 0) {
        Write-Host "   1. Reduce complexity in $($codeQualityResults.Violations.Complexity) methods" -ForegroundColor White
    }
    if ($codeQualityResults.Violations.MethodLength -gt 0) {
        Write-Host "   2. Break down $($codeQualityResults.Violations.MethodLength) long methods" -ForegroundColor White
    }
    if ($codeQualityResults.Violations.ClassLength -gt 0) {
        Write-Host "   3. Split $($codeQualityResults.Violations.ClassLength) large classes" -ForegroundColor White
    }
    if ($codeQualityResults.Violations.Documentation -gt 0) {
        Write-Host "   4. Add documentation to $($codeQualityResults.Violations.Documentation) public APIs" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "AUDIT SUMMARY:" -ForegroundColor Cyan
Write-Host "   Files Analyzed: $($codeQualityResults.TotalFiles)" -ForegroundColor Gray
Write-Host "   Lines of Code: $($codeQualityResults.TotalLines.ToString('N0'))" -ForegroundColor Gray
Write-Host "   Avg Complexity: $($codeQualityResults.AvgComplexity)" -ForegroundColor Gray
Write-Host ""
Write-Host "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

exit [int]$finalScore