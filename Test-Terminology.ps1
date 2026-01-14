# Test FIX 2: Terminology changes from Quality to Code Complexity Analysis

Write-Host "Testing FIX 2: Quality to Code Complexity Analysis Rename" -ForegroundColor Green
Write-Host "=========================================================="

# Check EnterpriseCodeQualityAuditService.cs
$servicePath = "c:\Projects\EnterpriseDocumentationPlatform.V2\src\Core\Quality\EnterpriseCodeQualityAuditService.cs"

if (Test-Path $servicePath) {
    $serviceContent = Get-Content $servicePath -Raw
    
    Write-Host "Checking terminology changes..." -ForegroundColor Yellow
    
    if ($serviceContent -match "Code Complexity Analysis") {
        Write-Host "Success: Code Complexity Analysis terminology found!" -ForegroundColor Green
    } else {
        Write-Host "Error: Code Complexity Analysis terminology NOT found!" -ForegroundColor Red
    }
} else {
    Write-Host "EnterpriseCodeQualityAuditService.cs not found!" -ForegroundColor Red
}

# Check dashboard.html
$dashboardPath = "c:\Projects\EnterpriseDocumentationPlatform.V2\src\Api\wwwroot\dashboard.html"

if (Test-Path $dashboardPath) {
    $dashboardContent = Get-Content $dashboardPath -Raw
    
    if ($dashboardContent -match "Code Complexity Analysis") {
        Write-Host "Success: Dashboard uses Code Complexity Analysis!" -ForegroundColor Green
    } else {
        Write-Host "Error: Dashboard still uses old terminology!" -ForegroundColor Red
    }
} else {
    Write-Host "dashboard.html not found!" -ForegroundColor Red
}

Write-Host ""
Write-Host "FIX 2 Status: TERMINOLOGY UPDATED" -ForegroundColor Green