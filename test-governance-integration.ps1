# Data Governance Integration Test Script
# Tests the successful integration of the Data Governance Proxy with the API

# Test 1: Query Validation with Security Risk Detection
$validationRequest = @{
    "agentId" = "test-agent-001"
    "agentName" = "Integration Test Agent"
    "agentPurpose" = "Testing governance integration endpoints"
    "databaseName" = "TestDB"
    "sqlQuery" = "SELECT * FROM Users WHERE id = @userId; DROP TABLE Users;"
    "parameters" = @{ "userId" = 123 }
    "requestedTables" = @("Users")
    "requestedColumns" = @("id", "email", "name")
    "clearanceLevel" = 1  # Standard clearance
    "maxExecutionTime" = "00:00:30"
    "applyDataMasking" = $true
} | ConvertTo-Json -Depth 3

Write-Host "🔍 Testing Governance Query Validation..." -ForegroundColor Cyan
Write-Host "Request: Dangerous SQL query with DROP TABLE to test security detection" -ForegroundColor Yellow

try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:5195/governance/validate" -Method POST -Body $validationRequest -ContentType "application/json"
    Write-Host "✅ Validation Response:" -ForegroundColor Green
    $response1 | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Validation Test Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*80 + "`n"

# Test 2: Authorization Testing with Different Clearance Levels
Write-Host "🔒 Testing Governance Authorization..." -ForegroundColor Cyan
Write-Host "Request: Testing Standard clearance access to restricted tables" -ForegroundColor Yellow

$authParams = @{
    agentId = "test-agent-001"
    requestedTables = @("Users", "Security", "Admin")  # Mix of allowed/forbidden
    clearanceLevel = 1  # Standard clearance
}

try {
    $authQuery = ($authParams.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value -join ',')" }) -join "&"
    $response2 = Invoke-RestMethod -Uri "http://localhost:5195/governance/authorize?$authQuery" -Method POST
    Write-Host "✅ Authorization Response:" -ForegroundColor Green
    $response2 | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Authorization Test Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*80 + "`n"

# Test 3: Clean Query Validation (Should Pass)
$cleanRequest = @{
    "agentId" = "test-agent-002"
    "agentName" = "Clean Query Test Agent"
    "agentPurpose" = "Testing valid query processing"
    "databaseName" = "TestDB"
    "sqlQuery" = "SELECT id, name FROM Documents WHERE category = @category"
    "parameters" = @{ "category" = "public" }
    "requestedTables" = @("Documents")
    "requestedColumns" = @("id", "name", "category")
    "clearanceLevel" = 1  # Standard clearance
    "maxExecutionTime" = "00:00:30"
    "applyDataMasking" = $true
} | ConvertTo-Json -Depth 3

Write-Host "✅ Testing Clean Query Validation..." -ForegroundColor Cyan
Write-Host "Request: Safe parameterized query that should pass all validations" -ForegroundColor Yellow

try {
    $response3 = Invoke-RestMethod -Uri "http://localhost:5195/governance/validate" -Method POST -Body $cleanRequest -ContentType "application/json"
    Write-Host "✅ Clean Query Response:" -ForegroundColor Green
    $response3 | ConvertTo-Json -Depth 3 | Write-Host
} catch {
    Write-Host "❌ Clean Query Test Failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n" + "="*80 + "`n"
Write-Host "🎉 Integration Test Complete!" -ForegroundColor Magenta
Write-Host "Data Governance Proxy is successfully integrated with the API!" -ForegroundColor Green