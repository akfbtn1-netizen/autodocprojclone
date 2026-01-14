# Simple API Test for Enterprise Documentation Platform V2
param(
    [string]$BaseUrl = "http://localhost:5195"
)

Write-Host "🚀 Testing Enterprise Documentation Platform V2 API" -ForegroundColor Green
Write-Host "Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check Approvals endpoint
Write-Host "Testing Approvals endpoint..." -ForegroundColor Yellow
try {
    $approvals = Invoke-RestMethod -Uri "$BaseUrl/api/Approvals/pending" -Method GET
    Write-Host "✅ Approvals endpoint working - Found $($approvals.Count) pending approvals" -ForegroundColor Green
}
catch {
    Write-Host "❌ Approvals endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Check MasterIndex endpoint  
Write-Host "Testing MasterIndex endpoint..." -ForegroundColor Yellow
try {
    $masterIndex = Invoke-RestMethod -Uri "$BaseUrl/api/MasterIndex" -Method GET
    Write-Host "✅ MasterIndex endpoint working - Found $($masterIndex.Count) records" -ForegroundColor Green
}
catch {
    Write-Host "❌ MasterIndex endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "✅ API Test Complete!" -ForegroundColor Green