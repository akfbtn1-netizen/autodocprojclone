#!/usr/bin/env pwsh
# Build validation for CI/CD pipeline

param(
    [switch]$SkipTests,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$startTime = Get-Date

Write-Host "Starting build validation..." -ForegroundColor Cyan

try {
    # 1. Restore dependencies
    Write-Host "`n[1/5] Restoring dependencies..." -ForegroundColor Yellow
    $restoreArgs = @("restore")
    if (-not $Verbose) { $restoreArgs += "--verbosity", "minimal" }
    
    & dotnet @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw "Dependency restore failed" }
    
    # 2. Build solution
    Write-Host "`n[2/5] Building solution..." -ForegroundColor Yellow
    $buildArgs = @("build", "--configuration", "Release", "--no-restore")
    if (-not $Verbose) { $buildArgs += "--verbosity", "minimal" }
    
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    
    # 3. Run tests
    if (-not $SkipTests) {
        Write-Host "`n[3/5] Running tests..." -ForegroundColor Yellow
        $testArgs = @(
            "test",
            "--configuration", "Release",
            "--no-build",
            "--logger", "trx",
            "--collect:XPlat Code Coverage"
        )
        if (-not $Verbose) { $testArgs += "--verbosity", "minimal" }
        
        & dotnet @testArgs
        if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    } else {
        Write-Host "`n[3/5] Skipping tests (--SkipTests flag)" -ForegroundColor Gray
    }
    
    # 4. Check code coverage
    Write-Host "`n[4/5] Checking code coverage..." -ForegroundColor Yellow
    $coverageFiles = Get-ChildItem -Path "TestResults" -Filter "coverage.cobertura.xml" -Recurse -ErrorAction SilentlyContinue
    
    if ($coverageFiles) {
        foreach ($file in $coverageFiles) {
            [xml]$coverage = Get-Content $file.FullName
            $lineRate = [double]$coverage.coverage.'line-rate' * 100
            
            Write-Host "  Line coverage: $($lineRate.ToString('F2'))%" -ForegroundColor $(
                if ($lineRate -ge 80) { 'Green' } elseif ($lineRate -ge 70) { 'Yellow' } else { 'Red' }
            )
            
            if ($lineRate -lt 80) {
                Write-Host "  [WARN] Coverage below 80% threshold" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "  [WARN] No coverage reports found" -ForegroundColor Yellow
    }
    
    # 5. Validate artifacts
    Write-Host "`n[5/5] Validating build artifacts..." -ForegroundColor Yellow
    $requiredArtifacts = @(
        "src/Api/bin/Release/net8.0/Api.dll",
        "src/Services/bin/Release/net8.0/Services.dll"
    )
    
    $allFound = $true
    foreach ($artifact in $requiredArtifacts) {
        if (Test-Path $artifact) {
            Write-Host "  [OK] $artifact" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] Missing: $artifact" -ForegroundColor Red
            $allFound = $false
        }
    }
    
    if (-not $allFound) {
        throw "Required build artifacts missing"
    }
    
    $duration = (Get-Date) - $startTime
    Write-Host "`n[SUCCESS] Build validation completed in $($duration.TotalSeconds.ToString('F1'))s" -ForegroundColor Green
    exit 0
    
} catch {
    $duration = (Get-Date) - $startTime
    Write-Host "`n[FAILED] Build validation failed after $($duration.TotalSeconds.ToString('F1'))s" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
