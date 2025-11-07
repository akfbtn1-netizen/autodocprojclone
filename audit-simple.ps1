# Enterprise Documentation Platform V2 - Quality Audit Script
# PowerShell audit for comprehensive system assessment

Write-Host "Enterprise Documentation Platform V2 - Quality Audit" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Gray
Write-Host ""

$rootPath = "c:\Projects\EnterpriseDocumentationPlatform.V2"
$srcPath = Join-Path $rootPath "src"

# Initialize scoring
$totalScore = 0.0
$maxScore = 0.0

# Helper functions
function Get-CSharpFiles($path) {
    if (Test-Path $path) {
        return Get-ChildItem -Path $path -Recurse -Filter "*.cs" | Where-Object { $_.Name -ne "GlobalUsings.cs" }
    }
    return @()
}

function Count-Lines($filePath) {
    if (Test-Path $filePath) {
        return (Get-Content $filePath).Count
    }
    return 0
}

# 1. PROJECT STRUCTURE ANALYSIS (Weight: 15%)
Write-Host "1. PROJECT STRUCTURE ANALYSIS" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

$structureScore = 0.0
$structureWeight = 15.0

# Check core directories
$expectedDirs = @(
    "src\Api", "src\Core\Domain", "src\Core\Application", "src\Core\Infrastructure",
    "src\Shared\Contracts", "tests\Unit", "tests\Integration"
)

$existingDirs = $expectedDirs | Where-Object { Test-Path (Join-Path $rootPath $_) }
$dirScore = ($existingDirs.Count / $expectedDirs.Count) * 100
$structureScore += $dirScore * 0.4

Write-Host "   Directory Structure: $($existingDirs.Count)/$($expectedDirs.Count) ($($dirScore.ToString('F1')))" -ForegroundColor Green

# Check key files
$keyFiles = @(
    "src\Api\Program.cs", "src\Api\Api.csproj",
    "src\Core\Domain\Entities\Document.cs", "src\Core\Domain\Entities\User.cs",
    "Directory.Packages.props", "EnterpriseDocumentationPlatform.sln"
)

$existingFiles = $keyFiles | Where-Object { Test-Path (Join-Path $rootPath $_) }
$fileScore = ($existingFiles.Count / $keyFiles.Count) * 100
$structureScore += $fileScore * 0.6

Write-Host "   Key Files Present: $($existingFiles.Count)/$($keyFiles.Count) ($($fileScore.ToString('F1')))" -ForegroundColor Green
Write-Host "   Structure Score: $($structureScore.ToString('F1'))/100" -ForegroundColor Cyan
Write-Host ""

$totalScore += $structureScore * ($structureWeight / 100)
$maxScore += $structureWeight

# 2. CODE QUALITY ANALYSIS (Weight: 25%)
Write-Host "2. CODE QUALITY ANALYSIS" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

$codeScore = 0.0
$codeWeight = 25.0

# Count total lines of code
$allCsFiles = Get-CSharpFiles $srcPath
$totalLines = ($allCsFiles | ForEach-Object { Count-Lines $_.FullName } | Measure-Object -Sum).Sum
$totalFiles = $allCsFiles.Count

Write-Host "   Total C# Files: $totalFiles" -ForegroundColor White
Write-Host "   Total Lines of Code: $($totalLines.ToString('N0'))" -ForegroundColor White

# Basic quality metrics
$qualityScore = 0.0
if ($totalFiles -gt 0) {
    $avgLinesPerFile = $totalLines / $totalFiles
    $sizeScore = if ($avgLinesPerFile -lt 500) { 100 } else { [Math]::Max(0, 100 - (($avgLinesPerFile - 500) / 10)) }
    $qualityScore += $sizeScore * 0.3
    Write-Host "   Average Lines/File: $($avgLinesPerFile.ToString('F1')) (Score: $($sizeScore.ToString('F1')))" -ForegroundColor White
    
    # Check for documentation
    $documentedFiles = 0
    $sampleFiles = $allCsFiles | Select-Object -First ([Math]::Min(20, $totalFiles))
    foreach ($file in $sampleFiles) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match "/// <summary>" -or $content -match "// <summary>") {
            $documentedFiles++
        }
    }
    
    $docScore = ($documentedFiles / [Math]::Min(20, $totalFiles)) * 100
    $qualityScore += $docScore * 0.4
    Write-Host "   Documentation Coverage: $($docScore.ToString('F1')) (sampled)" -ForegroundColor White
    
    # Check for error handling
    $errorHandlingFiles = 0
    foreach ($file in $sampleFiles) {
        $content = Get-Content $file.FullName -Raw
        if ($content -match "try" -and $content -match "catch" -or $content -match "throw") {
            $errorHandlingFiles++
        }
    }
    
    $errorScore = ($errorHandlingFiles / [Math]::Min(20, $totalFiles)) * 100
    $qualityScore += $errorScore * 0.3
    Write-Host "   Error Handling: $($errorScore.ToString('F1')) (sampled)" -ForegroundColor White
}

$codeScore = $qualityScore
Write-Host "   Code Quality Score: $($codeScore.ToString('F1'))/100" -ForegroundColor Cyan
Write-Host ""

$totalScore += $codeScore * ($codeWeight / 100)
$maxScore += $codeWeight

# 3. ARCHITECTURE COMPLIANCE (Weight: 20%)
Write-Host "3. ARCHITECTURE COMPLIANCE" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

$archScore = 0.0
$archWeight = 20.0

# Check Clean Architecture layers
$domainFiles = (Get-CSharpFiles (Join-Path $srcPath "Core\Domain")).Count
$applicationFiles = (Get-CSharpFiles (Join-Path $srcPath "Core\Application")).Count
$infrastructureFiles = (Get-CSharpFiles (Join-Path $srcPath "Core\Infrastructure")).Count
$apiFiles = (Get-CSharpFiles (Join-Path $srcPath "Api")).Count

$layerScore = 0.0
if ($domainFiles -gt 0) { $layerScore += 25 }
if ($applicationFiles -gt 0) { $layerScore += 25 }
if ($infrastructureFiles -gt 0) { $layerScore += 25 }
if ($apiFiles -gt 0) { $layerScore += 25 }

Write-Host "   Domain Layer: $domainFiles files" -ForegroundColor White
Write-Host "   Application Layer: $applicationFiles files" -ForegroundColor White
Write-Host "   Infrastructure Layer: $infrastructureFiles files" -ForegroundColor White
Write-Host "   API Layer: $apiFiles files" -ForegroundColor White
Write-Host "   Layer Score: $($layerScore.ToString('F1'))/100" -ForegroundColor White

# Check for CQRS pattern
$cqrsScore = 0.0
$commandsPath = Join-Path $srcPath "Core\Application\Commands"
$queriesPath = Join-Path $srcPath "Core\Application\Queries"

if (Test-Path $commandsPath) { $cqrsScore += 50 }
if (Test-Path $queriesPath) { $cqrsScore += 50 }

Write-Host "   CQRS Implementation: $($cqrsScore.ToString('F1'))/100" -ForegroundColor White

$archScore = ($layerScore * 0.7) + ($cqrsScore * 0.3)
Write-Host "   Architecture Score: $($archScore.ToString('F1'))/100" -ForegroundColor Cyan
Write-Host ""

$totalScore += $archScore * ($archWeight / 100)
$maxScore += $archWeight

# 4. DATABASE & PERSISTENCE (Weight: 15%)
Write-Host "4. DATABASE AND PERSISTENCE" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

$dbScore = 0.0
$dbWeight = 15.0

# Check for EF Core setup
$dbContextPath = Join-Path $srcPath "Core\Infrastructure\Persistence"
$hasDbContext = (Test-Path $dbContextPath) -and ((Get-CSharpFiles $dbContextPath | Where-Object { $_.Name -like "*DbContext*" }).Count -gt 0)
$hasMigrations = Test-Path (Join-Path $srcPath "Core\Infrastructure\Migrations")
$hasRepositories = Test-Path (Join-Path $srcPath "Core\Infrastructure\Persistence\Repositories")

if ($hasDbContext) { $dbScore += 40 }
if ($hasMigrations) { $dbScore += 30 }
if ($hasRepositories) { $dbScore += 30 }

Write-Host "   DbContext Present: $hasDbContext" -ForegroundColor White
Write-Host "   Migrations Present: $hasMigrations" -ForegroundColor White
Write-Host "   Repositories Present: $hasRepositories" -ForegroundColor White
Write-Host "   Database Score: $($dbScore.ToString('F1'))/100" -ForegroundColor Cyan
Write-Host ""

$totalScore += $dbScore * ($dbWeight / 100)
$maxScore += $dbWeight

# 5. API & DOCUMENTATION (Weight: 15%)
Write-Host "5. API AND DOCUMENTATION" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

$apiScore = 0.0
$apiWeight = 15.0

# Check API setup
$controllersPath = Join-Path $srcPath "Api\Controllers"
$hasControllers = (Test-Path $controllersPath) -and ((Get-CSharpFiles $controllersPath).Count -gt 0)
$programPath = Join-Path $srcPath "Api\Program.cs"
$hasSwagger = (Test-Path $programPath) -and ((Get-Content $programPath -Raw) -match "AddSwaggerGen")
$hasAuth = (Test-Path $programPath) -and ((Get-Content $programPath -Raw) -match "AddAuthentication")

if ($hasControllers) { $apiScore += 40 }
if ($hasSwagger) { $apiScore += 30 }
if ($hasAuth) { $apiScore += 30 }

Write-Host "   Controllers Present: $hasControllers" -ForegroundColor White
Write-Host "   Swagger Documentation: $hasSwagger" -ForegroundColor White
Write-Host "   Authentication Setup: $hasAuth" -ForegroundColor White
Write-Host "   API Score: $($apiScore.ToString('F1'))/100" -ForegroundColor Cyan
Write-Host ""

$totalScore += $apiScore * ($apiWeight / 100)
$maxScore += $apiWeight

# 6. TESTING & QUALITY ASSURANCE (Weight: 10%)
Write-Host "6. TESTING AND QUALITY ASSURANCE" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

$testScore = 0.0
$testWeight = 10.0

$unitTestPath = Join-Path $rootPath "tests\Unit"
$integrationTestPath = Join-Path $rootPath "tests\Integration"
$hasUnitTests = (Test-Path $unitTestPath) -and ((Get-CSharpFiles $unitTestPath).Count -gt 0)
$hasIntegrationTests = (Test-Path $integrationTestPath) -and ((Get-CSharpFiles $integrationTestPath).Count -gt 0)

if ($hasUnitTests) { $testScore += 50 }
if ($hasIntegrationTests) { $testScore += 50 }

Write-Host "   Unit Tests: $hasUnitTests" -ForegroundColor White
Write-Host "   Integration Tests: $hasIntegrationTests" -ForegroundColor White
Write-Host "   Testing Score: $($testScore.ToString('F1'))/100" -ForegroundColor Cyan
Write-Host ""

$totalScore += $testScore * ($testWeight / 100)
$maxScore += $testWeight

# FINAL AUDIT RESULTS
Write-Host "AUDIT SUMMARY" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Gray

$finalScore = ($totalScore / $maxScore) * 100

Write-Host "Overall Quality Score: $($finalScore.ToString('F1'))/100" -ForegroundColor White
Write-Host ""

# Grade calculation
$grade = ""
$emoji = ""
if ($finalScore -ge 90) { $grade = "A+"; $emoji = "EXCELLENT" }
elseif ($finalScore -ge 80) { $grade = "A"; $emoji = "VERY GOOD" }
elseif ($finalScore -ge 70) { $grade = "B+"; $emoji = "GOOD" }
elseif ($finalScore -ge 60) { $grade = "B"; $emoji = "SATISFACTORY" }
elseif ($finalScore -ge 50) { $grade = "C"; $emoji = "NEEDS IMPROVEMENT" }
else { $grade = "D"; $emoji = "POOR" }

Write-Host "$emoji - GRADE: $grade" -ForegroundColor Green
Write-Host ""

# Recommendations
Write-Host "RECOMMENDATIONS" -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

$recommendations = @()

if ($finalScore -lt 90) {
    if ($testScore -lt 50) { $recommendations += "- Implement comprehensive unit and integration tests" }
    if ($codeScore -lt 80) { $recommendations += "- Improve code documentation and error handling" }
    if ($archScore -lt 85) { $recommendations += "- Enhance architectural patterns and CQRS implementation" }
    if ($dbScore -lt 90) { $recommendations += "- Complete database migration and repository patterns" }
    if ($apiScore -lt 85) { $recommendations += "- Enhance API documentation and authentication" }
}

if ($recommendations.Count -eq 0) {
    Write-Host "Excellent! The system meets enterprise quality standards." -ForegroundColor Green
} else {
    foreach ($rec in $recommendations) {
        Write-Host $rec -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Audit completed for $totalFiles files ($($totalLines.ToString('N0')) lines of code)" -ForegroundColor Gray
Write-Host "Generated on: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

# Return the score as exit code (rounded)
exit [int]$finalScore