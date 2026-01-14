# Phase1-Verification-and-Test.ps1
# Automated verification and testing for Phase 1 completion
# Run this from project root: C:\Projects\EnterpriseDocumentationPlatform.V2

$ErrorActionPreference = 'Stop'

# Colors
function Write-Step { param($msg) Write-Host "`n$msg" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warning { param($msg) Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host "  [ERROR] $msg" -ForegroundColor Red }
function Write-Info { param($msg) Write-Host "  [INFO] $msg" -ForegroundColor White }

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "         PHASE 1: VERIFICATION & TESTING                        " -ForegroundColor Cyan
Write-Host "      Enterprise Documentation Platform v2                      " -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = Get-Location
$apiPath = Join-Path $projectRoot "src\Api"
$testPath = Join-Path $projectRoot "tests\Unit"

Write-Info "Project Root: $projectRoot"
Write-Info "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

#############################################################################
# STEP 1: ENVIRONMENT CHECK
#############################################################################

Write-Step "STEP 1: Environment Verification"
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

# Check project structure
Write-Info "Checking project structure..."
$requiredPaths = @(
    "src\Api",
    "src\Core\Application",
    "src\Core\Domain",
    "src\Core\Infrastructure",
    "tests\Unit"
)

$structureValid = $true
foreach ($path in $requiredPaths) {
    $fullPath = Join-Path $projectRoot $path
    if (Test-Path $fullPath) {
        Write-Success "$path exists"
    } else {
        Write-Error "$path NOT FOUND"
        $structureValid = $false
    }
}

if (-not $structureValid) {
    Write-Error "Project structure validation failed!"
    Write-Warning "Make sure you're in the project root directory."
    exit 1
}

# Check .NET SDK
Write-Info "Checking .NET SDK..."
try {
    $dotnetVersion = dotnet --version 2>&1
    Write-Success ".NET SDK installed: $dotnetVersion"
} catch {
    Write-Error ".NET SDK not found!"
    exit 1
}

# Check Node.js (required for templates)
Write-Info "Checking Node.js..."
try {
    $nodeVersion = node --version 2>$null
    Write-Success "Node.js installed: $nodeVersion"
} catch {
    Write-Warning "Node.js not found - required for template execution!"
}

# Check Templates
$templatesPath = Join-Path $projectRoot "Templates"
if (Test-Path $templatesPath) {
    $templateCount = (Get-ChildItem $templatesPath -Filter "TEMPLATE_*.js" -ErrorAction SilentlyContinue).Count
    if ($templateCount -gt 0) {
        Write-Success "Templates found: $templateCount templates"
    } else {
        Write-Warning "Templates directory exists but no TEMPLATE_*.js files found"
    }
} else {
    Write-Warning "Templates directory not found at $templatesPath"
}

#############################################################################
# STEP 2: BUILD VERIFICATION
#############################################################################

Write-Step "STEP 2: Build Verification"
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

Write-Info "Building project..."
Set-Location $apiPath

$buildOutput = dotnet build --no-incremental 2>&1
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -eq 0) {
    Write-Success "Build SUCCESSFUL!"
    
    # Check for warnings
    $warnings = $buildOutput | Select-String "warning" | Measure-Object
    if ($warnings.Count -gt 0) {
        Write-Warning "Build completed with $($warnings.Count) warning(s)"
    }
} else {
    Write-Error "Build FAILED!"
    Write-Host ""
    Write-Host "Build Output:" -ForegroundColor Yellow
    Write-Host $buildOutput -ForegroundColor Gray
    Set-Location $projectRoot
    exit 1
}

Set-Location $projectRoot

#############################################################################
# STEP 3: UNIT TESTS
#############################################################################

Write-Step "STEP 3: Unit Tests"
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

Write-Info "Running unit tests..."
Set-Location $testPath

$testOutput = dotnet test --no-build --verbosity minimal 2>&1
$testExitCode = $LASTEXITCODE

Set-Location $projectRoot

if ($testExitCode -eq 0) {
    # Parse test results
    if ($testOutput -match "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)") {
        $passed = $matches[2]
        $total = $matches[4]
        Write-Success "All tests PASSED! ($passed/$total)"
    } else {
        Write-Success "Tests completed successfully"
    }
} else {
    Write-Warning "Some tests failed or could not run"
    Write-Info "Test output:"
    Write-Host $testOutput -ForegroundColor Gray
}

#############################################################################
# STEP 4: CONFIGURATION CHECK
#############################################################################

Write-Step "STEP 4: Configuration Check"
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

$appSettingsPath = Join-Path $apiPath "appsettings.json"
if (Test-Path $appSettingsPath) {
    Write-Success "appsettings.json found"
    
    Write-Info "Checking configuration..."
    $config = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
    
    # Check critical settings
    $hasConnectionString = $config.ConnectionStrings -and $config.ConnectionStrings.DefaultConnection
    $hasOpenAI = $config.OpenAI -and $config.OpenAI.Endpoint
    $hasDocGenerator = $config.DocGenerator
    
    if ($hasConnectionString) {
        Write-Success "Database connection string configured"
    } else {
        Write-Warning "Database connection string missing or empty"
    }
    
    if ($hasOpenAI) {
        Write-Success "OpenAI configuration found"
        if ($config.OpenAI.ApiKey -and $config.OpenAI.ApiKey -ne "YOUR_KEY_HERE") {
            Write-Success "OpenAI API key configured"
        } else {
            Write-Warning "OpenAI API key needs to be set"
        }
    } else {
        Write-Warning "OpenAI configuration missing"
    }
    
    if ($hasDocGenerator) {
        Write-Success "DocGenerator configuration found"
    } else {
        Write-Warning "DocGenerator configuration missing"
    }
} else {
    Write-Warning "appsettings.json not found"
}

#############################################################################
# STEP 5: OUTPUT DIRECTORY CHECK
#############################################################################

Write-Step "STEP 5: Output Directory Check"
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

$outputPath = "C:\Temp\Documentation-Catalog"
Write-Info "Checking output directory: $outputPath"

if (Test-Path $outputPath) {
    Write-Success "Output directory exists"
    
    # Check subdirectories
    $tempPath = Join-Path $outputPath "temp"
    if (-not (Test-Path $tempPath)) {
        Write-Info "Creating temp subdirectory..."
        New-Item -ItemType Directory -Path $tempPath -Force | Out-Null
        Write-Success "Temp directory created"
    } else {
        Write-Success "Temp directory exists"
    }
} else {
    Write-Info "Creating output directory..."
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $outputPath "temp") -Force | Out-Null
    Write-Success "Output directory created"
}

#############################################################################
# STEP 6: API ENDPOINT CHECK (if API is running)
#############################################################################

Write-Step "STEP 6: API Endpoint Check"
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray

Write-Info "Checking if API is running on http://localhost:5195..."
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5195/swagger/index.html" -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Success "API is running!"
        Write-Info "Swagger UI available at: http://localhost:5195/swagger"
        
        # Try to hit workflow endpoint
        Write-Info "Testing workflow endpoint..."
        try {
            $workflowResponse = Invoke-RestMethod -Uri "http://localhost:5195/api/workflow/test-workflow" -Method Post -TimeoutSec 5 -ErrorAction Stop
            Write-Success "Workflow endpoint responded!"
            Write-Host "    Response: $($workflowResponse | ConvertTo-Json -Compress)" -ForegroundColor Gray
        } catch {
            Write-Warning "Workflow endpoint not available or returned error: $($_.Exception.Message)"
        }
    }
} catch {
    Write-Warning "API is not running"
    Write-Info "To start API: cd src\Api && dotnet run"
}

#############################################################################
# SUMMARY
#############################################################################

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "                  VERIFICATION COMPLETE                         " -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""

Write-Host "VERIFICATION SUMMARY" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# Build checklist
$checklist = @(
    @{ Item = "Project Structure"; Status = $structureValid },
    @{ Item = ".NET SDK"; Status = $true },
    @{ Item = "Build Success"; Status = ($buildExitCode -eq 0) },
    @{ Item = "Unit Tests"; Status = ($testExitCode -eq 0) },
    @{ Item = "Configuration Files"; Status = (Test-Path $appSettingsPath) },
    @{ Item = "Output Directory"; Status = (Test-Path $outputPath) }
)

foreach ($check in $checklist) {
    if ($check.Status) {
        Write-Host "  [OK] $($check.Item)" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] $($check.Item)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "NEXT STEPS" -ForegroundColor Cyan
Write-Host "----------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

if ($buildExitCode -eq 0) {
    Write-Host "Your project is building successfully!" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "To complete Phase 1, do the following:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Start the API:" -ForegroundColor White
    Write-Host "   cd src\Api" -ForegroundColor Gray
    Write-Host "   dotnet run" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "2. Test workflow endpoint (in new terminal):" -ForegroundColor White
    Write-Host "   Invoke-RestMethod -Uri 'http://localhost:5195/api/workflow/test-workflow' -Method Post" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "3. Check generated output:" -ForegroundColor White
    Write-Host "   explorer C:\Temp\Documentation-Catalog\temp" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "4. View Swagger UI:" -ForegroundColor White
    Write-Host "   http://localhost:5195/swagger" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "5. Optional: Apply remaining fixes from COMPLETE_FIX_PACKAGE.md" -ForegroundColor White
    Write-Host "   - Update DocIdGeneratorService.cs (table-based uniqueness)" -ForegroundColor Gray
    Write-Host "   - Update AutoDraftService.cs (filename generation)" -ForegroundColor Gray
    Write-Host "   - Update ExcelSyncService.cs (DocumentApprovals integration)" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host "Build failed. Please review the errors above." -ForegroundColor Red
    Write-Host ""
}

Write-Host "Documentation References:" -ForegroundColor Cyan
Write-Host "  - MASTER_EXECUTION_PLAN.md - Complete guide" -ForegroundColor White
Write-Host "  - CLAUDE_HANDOFF_DOCUMENT.md - Project status" -ForegroundColor White
Write-Host "  - COMPLETE_FIX_PACKAGE.md - Detailed fixes" -ForegroundColor White
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Phase 1 Verification Complete!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
