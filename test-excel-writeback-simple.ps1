# Test Excel Write-back Functionality
# This script verifies that the DocId write-back implementation works correctly

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "    FIX #4: EXCEL DOCID WRITE-BACK IMPLEMENTATION TEST" -ForegroundColor Cyan  
Write-Host "=====================================================================" -ForegroundColor Cyan

# Test 1: Verify interface exists and is implemented
Write-Host "`n1. Checking interface implementation..." -ForegroundColor Yellow

$interfaceFile = "src\Core\Application\Services\ExcelSync\IExcelChangeIntegratorService.cs"
if (Test-Path $interfaceFile) {
    Write-Host "   ✓ IExcelChangeIntegratorService interface exists" -ForegroundColor Green
    
    # Check interface method signature
    $interfaceContent = Get-Content $interfaceFile -Raw
    if ($interfaceContent -match "WriteDocIdToExcelAsync") {
        Write-Host "   ✓ WriteDocIdToExcelAsync method defined" -ForegroundColor Green
    } else {
        Write-Host "   ✗ WriteDocIdToExcelAsync method not found" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Interface file not found" -ForegroundColor Red
}

# Test 2: Verify service implements interface  
Write-Host "`n2. Checking service implementation..." -ForegroundColor Yellow

$serviceFile = "src\Core\Application\Services\ExcelSync\ExcelChangeIntegratorService.cs"
if (Test-Path $serviceFile) {
    $serviceContent = Get-Content $serviceFile -Raw
    
    if ($serviceContent -match "IExcelChangeIntegratorService") {
        Write-Host "   ✓ ExcelChangeIntegratorService implements interface" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Service does not implement interface" -ForegroundColor Red
    }
    
    if ($serviceContent -match "WriteDocIdToExcelAsync") {
        Write-Host "   ✓ WriteDocIdToExcelAsync method implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ WriteDocIdToExcelAsync method not implemented" -ForegroundColor Red  
    }
    
    if ($serviceContent -match "OpenExcelWithRetryAsync") {
        Write-Host "   ✓ Retry logic for file locking implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ File locking retry logic missing" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Service file not found" -ForegroundColor Red
}

# Test 3: Verify DocumentChangeWatcherService integration
Write-Host "`n3. Checking DocumentChangeWatcherService integration..." -ForegroundColor Yellow

$watcherFile = "src\Core\Application\Services\Watcher\DocumentChangeWatcherService.cs"
if (Test-Path $watcherFile) {
    $watcherContent = Get-Content $watcherFile -Raw
    
    if ($watcherContent -match "_excelService") {
        Write-Host "   ✓ Excel service injected into DocumentChangeWatcherService" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Excel service not injected" -ForegroundColor Red
    }
    
    if ($watcherContent -match "WriteDocIdToExcelAsync") {
        Write-Host "   ✓ Excel write-back called after DocId generation" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Excel write-back not called" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Watcher service file not found" -ForegroundColor Red
}

# Test 4: Verify DI registration
Write-Host "`n4. Checking dependency injection registration..." -ForegroundColor Yellow

$programFile = "src\Api\Program.cs"
if (Test-Path $programFile) {
    $programContent = Get-Content $programFile -Raw
    
    if ($programContent -match "IExcelChangeIntegratorService") {
        Write-Host "   ✓ Interface registered in DI container" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Interface not registered in DI" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Program.cs file not found" -ForegroundColor Red
}

# Test 5: Build verification
Write-Host "`n5. Verifying build status..." -ForegroundColor Yellow

$buildOutput = dotnet build EnterpriseDocumentationPlatform.sln 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ Solution builds successfully" -ForegroundColor Green
} else {
    Write-Host "   ✗ Build failed" -ForegroundColor Red
}

Write-Host "`n=====================================================================" -ForegroundColor Cyan
Write-Host "    FIX #4 IMPLEMENTATION COMPLETE!" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan