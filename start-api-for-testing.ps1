# Simple API Test - Start just the basic API for testing
# This script starts a minimal API to test the E2E implementation

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "    STARTING MINIMAL API FOR E2E TESTING" -ForegroundColor Cyan  
Write-Host "=====================================================================" -ForegroundColor Cyan

$projectPath = "c:\Projects\EnterpriseDocumentationPlatform.V2"

Write-Host "`n[STEP 1] Checking for existing API processes..." -ForegroundColor Yellow
$existingProcess = Get-Process | Where-Object { $_.ProcessName -like "*dotnet*" -and $_.CommandLine -like "*Api*" } -ErrorAction SilentlyContinue

if ($existingProcess) {
    Write-Host "   Found existing API processes. Stopping them..." -ForegroundColor Yellow
    $existingProcess | ForEach-Object { 
        Write-Host "   Stopping process $($_.Id): $($_.ProcessName)" -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue 
    }
    Start-Sleep -Seconds 3
}

Write-Host "`n[STEP 2] Building solution..." -ForegroundColor Yellow
Push-Location $projectPath

try {
    $buildResult = & dotnet build --configuration Release --verbosity minimal 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✓ Solution built successfully" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ Build had warnings/errors:" -ForegroundColor Yellow
        $buildResult | Select-Object -Last 5 | ForEach-Object { Write-Host "     $_" -ForegroundColor Gray }
        Write-Host "   Continuing with existing API from previous build..." -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ⚠ Build failed, trying to use existing API..." -ForegroundColor Yellow
}

Write-Host "`n[STEP 3] Starting existing API..." -ForegroundColor Yellow

# Check if we can start the existing API project
$apiPath = Join-Path $projectPath "src\Api"
if (Test-Path $apiPath) {
    Push-Location $apiPath
    
    Write-Host "   Starting API from: $apiPath" -ForegroundColor Gray
    
    # Start the API in the background
    $process = Start-Process "dotnet" -ArgumentList "run --configuration Release --urls http://localhost:5195" -PassThru -WindowStyle Hidden
    
    if ($process) {
        Write-Host "   ✓ API process started with PID: $($process.Id)" -ForegroundColor Green
        Write-Host "   ✓ API should be available at: http://localhost:5195" -ForegroundColor Green
        
        # Wait a bit for startup
        Write-Host "   Waiting 15 seconds for API startup..." -ForegroundColor Yellow
        Start-Sleep -Seconds 15
        
        # Test API health
        try {
            $healthCheck = Invoke-RestMethod -Uri "http://localhost:5195/health" -Method GET -TimeoutSec 10 -ErrorAction Stop
            Write-Host "   ✓ API Health Check Successful!" -ForegroundColor Green
            Write-Host "     Status: $($healthCheck.status)" -ForegroundColor Gray
        } catch {
            Write-Host "   ⚠ API Health Check Failed: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "   API may still be starting up..." -ForegroundColor Gray
        }
        
        # Store process ID for later cleanup
        $global:ApiProcessId = $process.Id
        
    } else {
        Write-Host "   ✗ Failed to start API process" -ForegroundColor Red
    }
    
    Pop-Location
} else {
    Write-Host "   ✗ API directory not found: $apiPath" -ForegroundColor Red
}

Write-Host "`n[STEP 4] Running E2E Tests..." -ForegroundColor Yellow

# Now run our E2E test script
if (Test-Path "$projectPath\test-e2e-implementation.ps1") {
    Write-Host "   Running comprehensive E2E tests..." -ForegroundColor Gray
    & "$projectPath\test-e2e-implementation.ps1"
} else {
    Write-Host "   ✗ E2E test script not found" -ForegroundColor Red
}

Write-Host "`n[STEP 5] Cleanup (Optional)..." -ForegroundColor Yellow
Write-Host "   API is still running on PID: $global:ApiProcessId" -ForegroundColor Gray
Write-Host "   To stop the API, run: Stop-Process -Id $global:ApiProcessId" -ForegroundColor Gray
Write-Host "   Or use: Get-Process dotnet | Where-Object CommandLine -like '*Api*' | Stop-Process" -ForegroundColor Gray

Pop-Location

Write-Host "`n=====================================================================" -ForegroundColor Cyan
Write-Host "    API STARTUP COMPLETED" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "`n🌐 API Endpoints Available:" -ForegroundColor Green
Write-Host "   • Health Check: http://localhost:5195/health" -ForegroundColor Gray
Write-Host "   • Swagger UI: http://localhost:5195/swagger" -ForegroundColor Gray
Write-Host "   • API Base: http://localhost:5195/api" -ForegroundColor Gray

Write-Host "`n🧪 Quick Test Commands:" -ForegroundColor Yellow
Write-Host '   Invoke-RestMethod -Uri "http://localhost:5195/health"' -ForegroundColor White
Write-Host '   Invoke-RestMethod -Uri "http://localhost:5195/swagger/v1/swagger.json"' -ForegroundColor White
Write-Host '   Start-Process "http://localhost:5195/swagger"' -ForegroundColor White

Write-Host "`n=====================================================================" -ForegroundColor Cyan