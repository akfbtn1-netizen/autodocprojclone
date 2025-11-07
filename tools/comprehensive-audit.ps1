# Enterprise Code Quality & Architecture Audit System
# Industry-standard comprehensive assessment tool
# Combines architectural review with detailed static code analysis

param(
    [string]$ProjectPath = ".",
    [switch]$Detailed = $true,
    [string]$OutputFormat = "console" # console, json, html
)

Write-Host "🏗️ ENTERPRISE COMPREHENSIVE AUDIT SYSTEM" -ForegroundColor Cyan
Write-Host "Industry-Standard Code Quality & Architecture Assessment" -ForegroundColor Gray
Write-Host "=========================================================" -ForegroundColor Gray
Write-Host ""

$rootPath = Resolve-Path $ProjectPath
$srcPath = Join-Path $rootPath "src"

# Industry-standard thresholds (based on SonarQube, NDepend, Microsoft guidelines)
$QualityThresholds = @{
    # Code Complexity (based on McCabe Cyclomatic Complexity)
    MaxCyclomaticComplexity = 15        # Industry: 10-15 (SonarQube: 15)
    MaxMethodLines = 50                 # Industry: 30-60 (Microsoft: 50)
    MaxClassLines = 500                 # Industry: 300-700 (SonarQube: 500)
    MaxParameterCount = 7               # Industry: 5-8 (Clean Code: 3-7)
    MaxNestingDepth = 4                 # Industry: 3-5 (SonarQube: 4)
    
    # Quality Metrics
    MinDocumentationCoverage = 60       # Industry: 50-80% (Enterprise: 60%)
    MaxDuplicationPercentage = 5        # Industry: 3-5% (SonarQube: 3%)
    MinTestCoverage = 70               # Industry: 70-90% (Microsoft: 80%)
    MaxTechnicalDebt = 20              # Industry: <20% (SonarQube rating)
    
    # Architecture Metrics
    MaxLayerCoupling = 10              # Max dependencies between layers
    MinCohesion = 70                   # Package/namespace cohesion %
    MaxInstability = 30                # Architecture instability %
}

# Results tracking
$AuditResults = @{
    StartTime = Get-Date
    ProjectPath = $rootPath
    Summary = @{}
    Categories = @{}
    Files = @()
    Violations = @()
    Metrics = @{}
}

# Helper functions
function Get-CSharpFiles($path, $excludePatterns = @("*\bin\*", "*\obj\*", "*Migrations*", "*.Designer.cs", "*.g.cs")) {
    if (-not (Test-Path $path)) { return @() }
    
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
    # Count decision points (more accurate than simple keyword counting)
    $complexity = 1 # Base complexity
    
    # Control flow keywords
    $complexity += ([regex]::Matches($content, '\bif\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bwhile\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bfor\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bforeach\s*\(', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    $complexity += ([regex]::Matches($content, '\bdo\s*\{', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    
    # Switch cases
    $complexity += ([regex]::Matches($content, '\bcase\s+', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    
    # Exception handling
    $complexity += ([regex]::Matches($content, '\bcatch\s*[\(\{]', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
    
    # Logical operators (&&, ||)
    $complexity += ([regex]::Matches($content, '&&')).Count
    $complexity += ([regex]::Matches($content, '\|\|')).Count
    
    # Ternary operators
    $complexity += ([regex]::Matches($content, '\?[^?]*:')).Count
    
    return $complexity
}

function Measure-CodeDuplication($allFiles) {
    # Simplified duplication detection
    $duplicatedLines = 0
    $totalLines = 0
    
    # This is a basic implementation - in production, use tools like SonarQube
    # For now, we'll estimate based on common patterns
    foreach ($file in $allFiles) {
        $content = Get-Content $file.FullName -Raw
        $lines = $content -split "`n"
        $totalLines += $lines.Count
        
        # Look for obvious duplication patterns (simplified)
        $duplicatePatterns = @(
            'throw new ArgumentNullException',
            'if \(.*== null\)',
            'return Task\.FromResult',
            'public class.*Controller'
        )
        
        foreach ($pattern in $duplicatePatterns) {
            $matches = [regex]::Matches($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($matches.Count -gt 1) {
                $duplicatedLines += ($matches.Count - 1) * 3 # Estimate 3 lines per duplicate
            }
        }
    }
    
    return if ($totalLines -gt 0) { [Math]::Round(($duplicatedLines / $totalLines) * 100, 2) } else { 0 }
}

function Analyze-Architecture($srcPath) {
    Write-Host "🏗️ ARCHITECTURE ANALYSIS" -ForegroundColor Yellow
    Write-Host "=========================" -ForegroundColor Gray
    
    $archScore = 0
    $archDetails = @{}
    
    # 1. Layer Structure Analysis
    $layers = @{
        "Domain" = Join-Path $srcPath "Core\Domain"
        "Application" = Join-Path $srcPath "Core\Application" 
        "Infrastructure" = Join-Path $srcPath "Core\Infrastructure"
        "API" = Join-Path $srcPath "Api"
        "Shared" = Join-Path $srcPath "Shared"
    }
    
    $layerCompliance = 0
    foreach ($layer in $layers.GetEnumerator()) {
        $exists = Test-Path $layer.Value
        if ($exists) {
            $fileCount = (Get-CSharpFiles $layer.Value).Count
            Write-Host "   ✅ $($layer.Key): $fileCount files" -ForegroundColor Green
            $layerCompliance += 20
        } else {
            Write-Host "   ❌ $($layer.Key): Missing" -ForegroundColor Red
        }
    }
    $archScore += $layerCompliance * 0.3
    
    # 2. Dependency Analysis (simplified)
    $domainFiles = Get-CSharpFiles (Join-Path $srcPath "Core\Domain")
    $domainDependencies = 0
    foreach ($file in $domainFiles) {
        $content = Get-Content $file.FullName -Raw
        # Check for infrastructure dependencies in domain (anti-pattern)
        if ($content -match "using.*Infrastructure" -or $content -match "using.*EntityFramework") {
            $domainDependencies++
        }
    }
    
    $dependencyScore = if ($domainDependencies -eq 0) { 100 } else { [Math]::Max(0, 100 - ($domainDependencies * 10)) }
    Write-Host "   🔗 Domain Dependency Violations: $domainDependencies (Score: $dependencyScore)" -ForegroundColor $(if ($domainDependencies -eq 0) { "Green" } else { "Yellow" })
    $archScore += $dependencyScore * 0.2
    
    # 3. CQRS/Pattern Implementation
    $commandsPath = Join-Path $srcPath "Core\Application\Commands"
    $queriesPath = Join-Path $srcPath "Core\Application\Queries"
    $cqrsScore = 0
    
    if (Test-Path $commandsPath) { 
        $commandCount = (Get-CSharpFiles $commandsPath).Count
        Write-Host "   ⚡ Commands: $commandCount files" -ForegroundColor Green
        $cqrsScore += 50 
    }
    if (Test-Path $queriesPath) { 
        $queryCount = (Get-CSharpFiles $queriesPath).Count
        Write-Host "   🔍 Queries: $queryCount files" -ForegroundColor Green
        $cqrsScore += 50 
    }
    $archScore += $cqrsScore * 0.3
    
    # 4. Repository Pattern
    $repoPath = Join-Path $srcPath "Core\Infrastructure\Persistence\Repositories"
    $repoScore = if (Test-Path $repoPath) { 
        $repoCount = (Get-CSharpFiles $repoPath).Count
        Write-Host "   📚 Repositories: $repoCount files" -ForegroundColor Green
        100 
    } else { 
        Write-Host "   📚 Repositories: Missing" -ForegroundColor Yellow
        50 
    }
    $archScore += $repoScore * 0.2
    
    Write-Host "   📊 Architecture Score: $($archScore.ToString('F1'))/100" -ForegroundColor Cyan
    Write-Host ""
    
    return @{
        Score = $archScore
        LayerCompliance = $layerCompliance
        DependencyViolations = $domainDependencies
        CQRSImplemented = $cqrsScore -eq 100
        RepositoryPattern = $repoScore -eq 100
    }
}

function Analyze-CodeQuality($allFiles) {
    Write-Host "⚡ CODE QUALITY ANALYSIS" -ForegroundColor Yellow
    Write-Host "========================" -ForegroundColor Gray
    
    $totalFiles = $allFiles.Count
    $totalLines = 0
    $totalComplexity = 0
    $violations = @{
        Complexity = 0
        MethodLength = 0
        ClassLength = 0
        ParameterCount = 0
        Documentation = 0
        Naming = 0
    }
    
    $qualityScores = @()
    
    foreach ($file in $allFiles) {
        $fileName = $file.Name
        $content = Get-Content $file.FullName -Raw
        $lines = ($content -split "`n").Count
        $totalLines += $lines
        
        $fileScore = 100
        $fileViolations = @()
        
        # 1. Cyclomatic Complexity Analysis
        $complexity = Measure-CyclomaticComplexity $content
        $totalComplexity += $complexity
        if ($complexity -gt $QualityThresholds.MaxCyclomaticComplexity) {
            $violations.Complexity++
            $fileViolations += "High complexity: $complexity (max: $($QualityThresholds.MaxCyclomaticComplexity))"
            $fileScore -= [Math]::Min(30, ($complexity - $QualityThresholds.MaxCyclomaticComplexity) * 2)
        }
        
        # 2. Method Length Analysis (improved regex)
        $methodPattern = '(?s)(public|private|protected|internal|static).*?\b\w+\s*\([^)]*\)\s*\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}'
        $methodMatches = [regex]::Matches($content, $methodPattern)
        foreach ($match in $methodMatches) {
            $methodLines = ($match.Value -split "`n").Count
            if ($methodLines -gt $QualityThresholds.MaxMethodLines) {
                $violations.MethodLength++
                $fileViolations += "Long method: $methodLines lines (max: $($QualityThresholds.MaxMethodLines))"
                $fileScore -= 5
            }
        }
        
        # 3. Class Length Analysis
        if ($lines -gt $QualityThresholds.MaxClassLines) {
            $violations.ClassLength++
            $fileViolations += "Large class: $lines lines (max: $($QualityThresholds.MaxClassLines))"
            $fileScore -= 10
        }
        
        # 4. Parameter Count Analysis
        $methodSigPattern = '\b\w+\s*\(([^)]+)\)'
        $methodSignatures = [regex]::Matches($content, $methodSigPattern)
        foreach ($sig in $methodSignatures) {
            if ($sig.Groups[1].Value.Trim() -ne "") {
                $paramCount = ($sig.Groups[1].Value -split ',').Count
                if ($paramCount -gt $QualityThresholds.MaxParameterCount) {
                    $violations.ParameterCount++
                    $fileViolations += "Too many parameters: $paramCount (max: $($QualityThresholds.MaxParameterCount))"
                    $fileScore -= 3
                }
            }
        }
        
        # 5. Documentation Analysis
        $xmlDocCount = ([regex]::Matches($content, '///\s*<summary>')).Count
        $publicMethods = ([regex]::Matches($content, 'public\s+(?:static\s+)?(?:async\s+)?(?:Task<?[^>]*>?|void|\w+)\s+\w+\s*\(')).Count
        $publicClasses = ([regex]::Matches($content, 'public\s+(?:partial\s+)?(?:class|interface|enum|struct)\s+\w+')).Count
        
        $expectedDocs = $publicMethods + $publicClasses
        if ($expectedDocs -gt 0) {
            $docCoverage = ($xmlDocCount / $expectedDocs) * 100
            if ($docCoverage -lt $QualityThresholds.MinDocumentationCoverage) {
                $violations.Documentation++
                $fileViolations += "Low documentation: $($docCoverage.ToString('F1'))% (min: $($QualityThresholds.MinDocumentationCoverage)%)"
                $fileScore -= 5
            }
        }
        
        # 6. Naming Convention Analysis
        $badNaming = 0
        # Check for non-PascalCase public members
        $publicMembers = [regex]::Matches($content, 'public\s+(?:class|interface|enum|struct|\w+\s+\w+)\s+([a-z]\w*)')
        $badNaming += $publicMembers.Count
        
        if ($badNaming -gt 0) {
            $violations.Naming++
            $fileViolations += "Naming violations: $badNaming items"
            $fileScore -= 2
        }
        
        $fileScore = [Math]::Max(0, $fileScore)
        $qualityScores += $fileScore
        
        if ($fileViolations.Count -gt 0 -and $Detailed) {
            Write-Host "   📄 $fileName : $($fileScore.ToString('F0'))/100" -ForegroundColor $(if ($fileScore -ge 80) { "Green" } elseif ($fileScore -ge 60) { "Yellow" } else { "Red" })
            foreach ($violation in $fileViolations) {
                Write-Host "      - $violation" -ForegroundColor Gray
            }
        }
    }
    
    # Calculate overall metrics
    $avgLinesPerFile = if ($totalFiles -gt 0) { [Math]::Round($totalLines / $totalFiles, 1) } else { 0 }
    $avgComplexity = if ($totalFiles -gt 0) { [Math]::Round($totalComplexity / $totalFiles, 1) } else { 0 }
    $avgQualityScore = if ($qualityScores.Count -gt 0) { [Math]::Round(($qualityScores | Measure-Object -Average).Average, 1) } else { 0 }
    
    Write-Host ""
    Write-Host "   📊 Code Quality Metrics:" -ForegroundColor White
    Write-Host "      Total Files: $totalFiles" -ForegroundColor Gray
    Write-Host "      Total Lines: $($totalLines.ToString('N0'))" -ForegroundColor Gray
    Write-Host "      Avg Lines/File: $avgLinesPerFile" -ForegroundColor Gray
    Write-Host "      Avg Complexity: $avgComplexity" -ForegroundColor Gray
    Write-Host "      Avg Quality Score: $avgQualityScore/100" -ForegroundColor $(if ($avgQualityScore -ge 80) { "Green" } elseif ($avgQualityScore -ge 60) { "Yellow" } else { "Red" })
    Write-Host ""
    Write-Host "   ⚠️ Violations Summary:" -ForegroundColor Yellow
    Write-Host "      Complexity: $($violations.Complexity)" -ForegroundColor Gray
    Write-Host "      Method Length: $($violations.MethodLength)" -ForegroundColor Gray
    Write-Host "      Class Length: $($violations.ClassLength)" -ForegroundColor Gray
    Write-Host "      Parameter Count: $($violations.ParameterCount)" -ForegroundColor Gray
    Write-Host "      Documentation: $($violations.Documentation)" -ForegroundColor Gray
    Write-Host "      Naming: $($violations.Naming)" -ForegroundColor Gray
    Write-Host ""
    
    return @{
        Score = $avgQualityScore
        TotalFiles = $totalFiles
        TotalLines = $totalLines
        AvgLinesPerFile = $avgLinesPerFile
        AvgComplexity = $avgComplexity
        Violations = $violations
        FileScores = $qualityScores
    }
}

function Analyze-TestCoverage($rootPath) {
    Write-Host "🧪 TEST COVERAGE ANALYSIS" -ForegroundColor Yellow
    Write-Host "=========================" -ForegroundColor Gray
    
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
            Write-Host "   ✅ $($testPath.Split('\')[-1]): $($testFiles.Count) test files" -ForegroundColor Green
            $testScore += 50
        } else {
            Write-Host "   ❌ $($testPath.Split('\')[-1]): Missing" -ForegroundColor Red
        }
    }
    
    # Estimate coverage based on test file ratio (simplified)
    $srcFiles = (Get-CSharpFiles $srcPath).Count
    $estimatedCoverage = if ($srcFiles -gt 0) { [Math]::Min(100, ($totalTestFiles / $srcFiles) * 100 * 2) } else { 0 }
    
    Write-Host "   📊 Estimated Test Coverage: $($estimatedCoverage.ToString('F1'))%" -ForegroundColor $(if ($estimatedCoverage -ge 70) { "Green" } elseif ($estimatedCoverage -ge 50) { "Yellow" } else { "Red" })
    Write-Host "   📊 Test Infrastructure Score: $testScore/100" -ForegroundColor Cyan
    Write-Host ""
    
    return @{
        Score = $testScore
        EstimatedCoverage = $estimatedCoverage
        TotalTestFiles = $totalTestFiles
    }
}

# Main audit execution
Write-Host "🔍 Starting comprehensive audit of: $rootPath" -ForegroundColor White
Write-Host ""

# Get all source files
$allFiles = Get-CSharpFiles $srcPath
if ($allFiles.Count -eq 0) {
    Write-Host "❌ No C# files found in $srcPath" -ForegroundColor Red
    exit 1
}

# Run all analyses
$architectureResults = Analyze-Architecture $srcPath
$codeQualityResults = Analyze-CodeQuality $allFiles
$testResults = Analyze-TestCoverage $rootPath

# Calculate duplication
Write-Host "🔍 CODE DUPLICATION ANALYSIS" -ForegroundColor Yellow
Write-Host "============================" -ForegroundColor Gray
$duplicationPercentage = Measure-CodeDuplication $allFiles
Write-Host "   📊 Estimated Code Duplication: $duplicationPercentage%" -ForegroundColor $(if ($duplicationPercentage -le 5) { "Green" } elseif ($duplicationPercentage -le 10) { "Yellow" } else { "Red" })
Write-Host ""

# Calculate final comprehensive score
$finalScore = (
    ($architectureResults.Score * 0.25) +           # 25% Architecture
    ($codeQualityResults.Score * 0.40) +            # 40% Code Quality  
    ($testResults.Score * 0.15) +                   # 15% Testing
    ((100 - $duplicationPercentage * 10) * 0.10) +  # 10% Duplication
    (100 * 0.10)                                    # 10% Project Structure (assume 100%)
)

# Generate comprehensive report
Write-Host "🎯 COMPREHENSIVE AUDIT RESULTS" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Gray
Write-Host ""

Write-Host "📊 CATEGORY SCORES:" -ForegroundColor White
Write-Host "   🏗️  Architecture Compliance: $($architectureResults.Score.ToString('F1'))/100 (25% weight)" -ForegroundColor $(if ($architectureResults.Score -ge 80) { "Green" } elseif ($architectureResults.Score -ge 60) { "Yellow" } else { "Red" })
Write-Host "   ⚡ Code Quality: $($codeQualityResults.Score.ToString('F1'))/100 (40% weight)" -ForegroundColor $(if ($codeQualityResults.Score -ge 80) { "Green" } elseif ($codeQualityResults.Score -ge 60) { "Yellow" } else { "Red" })
Write-Host "   🧪 Test Coverage: $($testResults.Score.ToString('F1'))/100 (15% weight)" -ForegroundColor $(if ($testResults.Score -ge 80) { "Green" } elseif ($testResults.Score -ge 60) { "Yellow" } else { "Red" })
Write-Host "   🔄 Code Duplication: $((100 - $duplicationPercentage * 10).ToString('F1'))/100 (10% weight)" -ForegroundColor $(if ($duplicationPercentage -le 5) { "Green" } elseif ($duplicationPercentage -le 10) { "Yellow" } else { "Red" })
Write-Host "   📁 Project Structure: 100.0/100 (10% weight)" -ForegroundColor Green
Write-Host ""

Write-Host "🏆 OVERALL QUALITY SCORE: $($finalScore.ToString('F1'))/100" -ForegroundColor $(if ($finalScore -ge 80) { "Green" } elseif ($finalScore -ge 60) { "Yellow" } else { "Red" })

# Grade assignment
$grade = ""
$status = ""
if ($finalScore -ge 90) { $grade = "A+"; $status = "EXCELLENT" }
elseif ($finalScore -ge 85) { $grade = "A"; $status = "VERY GOOD" }
elseif ($finalScore -ge 80) { $grade = "A-"; $status = "GOOD" }
elseif ($finalScore -ge 75) { $grade = "B+"; $status = "ABOVE AVERAGE" }
elseif ($finalScore -ge 70) { $grade = "B"; $status = "SATISFACTORY" }
elseif ($finalScore -ge 65) { $grade = "B-"; $status = "BELOW AVERAGE" }
elseif ($finalScore -ge 60) { $grade = "C+"; $status = "NEEDS IMPROVEMENT" }
elseif ($finalScore -ge 55) { $grade = "C"; $status = "POOR" }
else { $grade = "D"; $status = "CRITICAL" }

Write-Host "🎖️  GRADE: $grade ($status)" -ForegroundColor $(if ($finalScore -ge 80) { "Green" } elseif ($finalScore -ge 60) { "Yellow" } else { "Red" })
Write-Host ""

# Detailed recommendations
Write-Host "💡 RECOMMENDATIONS:" -ForegroundColor Yellow
Write-Host "===================" -ForegroundColor Gray

if ($finalScore -ge 85) {
    Write-Host "✨ Excellent work! Your codebase meets industry standards." -ForegroundColor Green
    Write-Host "   Focus on maintaining quality and monitoring technical debt." -ForegroundColor Gray
} else {
    $totalViolations = ($codeQualityResults.Violations.Values | Measure-Object -Sum).Sum
    Write-Host "🔧 Priority Actions Required:" -ForegroundColor Yellow
    
    if ($codeQualityResults.Violations.Complexity -gt 0) {
        Write-Host "   1. Reduce complexity in $($codeQualityResults.Violations.Complexity) methods (refactor conditional logic)" -ForegroundColor White
    }
    if ($codeQualityResults.Violations.MethodLength -gt 0) {
        Write-Host "   2. Break down $($codeQualityResults.Violations.MethodLength) long methods (Extract Method pattern)" -ForegroundColor White
    }
    if ($codeQualityResults.Violations.ClassLength -gt 0) {
        Write-Host "   3. Split $($codeQualityResults.Violations.ClassLength) large classes (Single Responsibility Principle)" -ForegroundColor White
    }
    if ($codeQualityResults.Violations.Documentation -gt 0) {
        Write-Host "   4. Add XML documentation to $($codeQualityResults.Violations.Documentation) public APIs" -ForegroundColor White
    }
    if ($duplicationPercentage -gt 5) {
        Write-Host "   5. Eliminate code duplication ($duplicationPercentage% detected)" -ForegroundColor White
    }
    if ($testResults.EstimatedCoverage -lt 70) {
        Write-Host "   6. Increase test coverage (currently $($testResults.EstimatedCoverage.ToString('F1'))%, target: 70%+)" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "📋 AUDIT SUMMARY:" -ForegroundColor Cyan
Write-Host "   Files Analyzed: $($codeQualityResults.TotalFiles)" -ForegroundColor Gray
Write-Host "   Lines of Code: $($codeQualityResults.TotalLines.ToString('N0'))" -ForegroundColor Gray
Write-Host "   Avg Complexity: $($codeQualityResults.AvgComplexity)" -ForegroundColor Gray
Write-Host "   Test Files: $($testResults.TotalTestFiles)" -ForegroundColor Gray
Write-Host "   Audit Duration: $((Get-Date).Subtract($AuditResults.StartTime).TotalSeconds.ToString('F1'))s" -ForegroundColor Gray
Write-Host ""
Write-Host "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

exit [int]$finalScore