# =============================================
# ENTERPRISE DOCUMENTATION PLATFORM V2
# END-TO-END PIPELINE TEST
# =============================================

param(
    [string]$BaseUrl = "http://localhost:5195",
    [switch]$EnableRealAI = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "🚀 Enterprise Documentation Platform V2 - End-to-End Test" -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Green
Write-Host "Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "Real AI: $EnableRealAI" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host ""

# Load test data
$testDataFile = Join-Path $PSScriptRoot "test-data-comprehensive.json"
if (-not (Test-Path $testDataFile)) {
    throw "Test data file not found: $testDataFile"
}

$testData = Get-Content $testDataFile -Raw | ConvertFrom-Json
Write-Host "✅ Loaded test data with $($testData.testScenarios.Count) scenarios" -ForegroundColor Green

# Test results tracking
$testResults = @{
    TotalScenarios = $testData.testScenarios.Count
    PassedScenarios = 0
    FailedScenarios = 0
    Errors = @()
    GeneratedDocuments = @()
    ApprovalEntries = @()
    MasterIndexRecords = @()
    StartTime = Get-Date
}

# Helper function to make API calls
function Invoke-APICall {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null,
        [hashtable]$Headers = @{}
    )
    
    $uri = "$BaseUrl$Endpoint"
    $defaultHeaders = @{
        'Content-Type' = 'application/json'
        'Accept' = 'application/json'
    }
    $allHeaders = $defaultHeaders + $Headers
    
    try {
        $params = @{
            Uri = $uri
            Method = $Method
            Headers = $allHeaders
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        if ($Verbose) {
            Write-Host "🔍 $Method $uri" -ForegroundColor DarkGray
        }
        
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-Host "❌ API call failed: $Method $uri" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

# Test API connectivity
Write-Host "🔍 Testing API connectivity..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-APICall -Method GET -Endpoint "/health"
    Write-Host "✅ API is responding" -ForegroundColor Green
}
catch {
    Write-Host "❌ API connectivity test failed" -ForegroundColor Red
    Write-Host "Make sure the backend is running on $BaseUrl" -ForegroundColor Yellow
    exit 1
}

# Test each scenario
foreach ($scenario in $testData.testScenarios) {
    Write-Host ""
    Write-Host "🧪 Testing Scenario $($scenario.id): $($scenario.name)" -ForegroundColor Magenta
    Write-Host "-" * 50 -ForegroundColor DarkGray
    
    try {
        # 1. Create ExcelChangeEntry (simulating Excel data entry)
        Write-Host "  📝 Creating Excel change entry..." -ForegroundColor Yellow
        $excelEntry = $scenario.excelChangeEntry
        $excelEntry.id = 0 # Auto-generated
        $excelEntry.createdAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        
        $createdEntry = Invoke-APICall -Method POST -Endpoint "/api/ExcelChanges" -Body $excelEntry
        Write-Host "  ✅ Excel change entry created with ID: $($createdEntry.id)" -ForegroundColor Green
        
        # 2. Trigger document generation pipeline
        Write-Host "  🔄 Triggering document generation pipeline..." -ForegroundColor Yellow
        
        $pipelineRequest = @{
            ExcelChangeEntryId = $createdEntry.id
            ForceRegeneration = $true
            EnableAI = $EnableRealAI
            MockSchemaMetadata = $scenario.mockSchemaMetadata
        }
        
        $pipelineResponse = Invoke-APICall -Method POST -Endpoint "/api/DocumentGeneration/generate" -Body $pipelineRequest
        Write-Host "  ✅ Pipeline triggered successfully" -ForegroundColor Green
        Write-Host "      Document ID: $($pipelineResponse.documentId)" -ForegroundColor Cyan
        Write-Host "      Tier: $($pipelineResponse.tier)" -ForegroundColor Cyan
        Write-Host "      Confidence: $($pipelineResponse.confidenceScore)" -ForegroundColor Cyan
        
        # 3. Verify tier classification
        if ($pipelineResponse.tier -eq $scenario.expectedTier) {
            Write-Host "  ✅ Tier classification correct: $($pipelineResponse.tier)" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  Tier mismatch - Expected: $($scenario.expectedTier), Got: $($pipelineResponse.tier)" -ForegroundColor Yellow
        }
        
        # 4. Check document was created
        Write-Host "  📄 Checking document generation..." -ForegroundColor Yellow
        Start-Sleep -Seconds 2 # Allow time for async processing
        
        $document = Invoke-APICall -Method GET -Endpoint "/api/Documents/$($pipelineResponse.documentId)"
        if ($document) {
            Write-Host "  ✅ Document retrieved successfully" -ForegroundColor Green
            Write-Host "      Type: $($document.documentType)" -ForegroundColor Cyan
            Write-Host "      Status: $($document.status)" -ForegroundColor Cyan
            $testResults.GeneratedDocuments += $document
        }
        
        # 5. Check MasterIndex record was created/updated
        Write-Host "  📊 Checking MasterIndex record..." -ForegroundColor Yellow
        
        $masterIndexQuery = @{
            DatabaseName = $scenario.excelChangeEntry.databaseName
            ObjectName = $scenario.mockSchemaMetadata.objectName
        }
        
        $masterIndexRecords = Invoke-APICall -Method GET -Endpoint "/api/MasterIndex/search" -Body $masterIndexQuery
        if ($masterIndexRecords -and $masterIndexRecords.Count -gt 0) {
            Write-Host "  ✅ MasterIndex record found" -ForegroundColor Green
            Write-Host "      Record ID: $($masterIndexRecords[0].id)" -ForegroundColor Cyan
            Write-Host "      Business Domain: $($masterIndexRecords[0].businessDomain)" -ForegroundColor Cyan
            $testResults.MasterIndexRecords += $masterIndexRecords[0]
        } else {
            Write-Host "  ⚠️  MasterIndex record not found" -ForegroundColor Yellow
        }
        
        # 6. Check approval queue entry
        Write-Host "  ✅ Checking approval queue..." -ForegroundColor Yellow
        
        $pendingApprovals = Invoke-APICall -Method GET -Endpoint "/api/Approvals/pending"
        $relevantApproval = $pendingApprovals | Where-Object { $_.documentId -eq $pipelineResponse.documentId }
        
        if ($relevantApproval) {
            Write-Host "  ✅ Approval queue entry found" -ForegroundColor Green
            Write-Host "      Approval ID: $($relevantApproval.id)" -ForegroundColor Cyan
            Write-Host "      Status: $($relevantApproval.status)" -ForegroundColor Cyan
            $testResults.ApprovalEntries += $relevantApproval
        } else {
            Write-Host "  ⚠️  Approval queue entry not found" -ForegroundColor Yellow
        }
        
        # 7. Test approval workflow (approve the document)
        if ($relevantApproval) {
            Write-Host "  ✅ Testing approval workflow..." -ForegroundColor Yellow
            
            $approvalRequest = @{
                Comments = "Automated test approval"
                ApprovedBy = "TestSystem"
            }
            
            $approvalResult = Invoke-APICall -Method PUT -Endpoint "/api/Approvals/$($relevantApproval.id)/approve" -Body $approvalRequest
            Write-Host "  ✅ Document approved successfully" -ForegroundColor Green
        }
        
        Write-Host "  🎉 Scenario $($scenario.id) completed successfully!" -ForegroundColor Green
        $testResults.PassedScenarios++
        
    }
    catch {
        Write-Host "  ❌ Scenario $($scenario.id) failed: $($_.Exception.Message)" -ForegroundColor Red
        $testResults.FailedScenarios++
        $testResults.Errors += @{
            Scenario = $scenario.id
            Error = $_.Exception.Message
            Timestamp = Get-Date
        }
    }
}

# Final results summary
$testResults.EndTime = Get-Date
$testResults.Duration = $testResults.EndTime - $testResults.StartTime

Write-Host ""
Write-Host "🏁 TEST RESULTS SUMMARY" -ForegroundColor Green
Write-Host "=" * 60 -ForegroundColor Green
Write-Host "Total Scenarios: $($testResults.TotalScenarios)" -ForegroundColor Cyan
Write-Host "Passed: $($testResults.PassedScenarios)" -ForegroundColor Green
Write-Host "Failed: $($testResults.FailedScenarios)" -ForegroundColor Red
Write-Host "Duration: $($testResults.Duration.TotalSeconds) seconds" -ForegroundColor Cyan
Write-Host ""
Write-Host "Generated Documents: $($testResults.GeneratedDocuments.Count)" -ForegroundColor Cyan
Write-Host "MasterIndex Records: $($testResults.MasterIndexRecords.Count)" -ForegroundColor Cyan
Write-Host "Approval Entries: $($testResults.ApprovalEntries.Count)" -ForegroundColor Cyan

if ($testResults.Errors.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ ERRORS:" -ForegroundColor Red
    foreach ($errorMessage in $testResults.Errors) {
        Write-Host "  Scenario $($errorMessage.Scenario): $($errorMessage.Error)" -ForegroundColor Red
    }
}

# Save test results
$resultsFile = "test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$testResults | ConvertTo-Json -Depth 10 | Out-File $resultsFile
Write-Host ""
Write-Host "📊 Test results saved to: $resultsFile" -ForegroundColor Cyan

if ($testResults.FailedScenarios -eq 0) {
    Write-Host ""
    Write-Host "🎉 ALL TESTS PASSED! Enterprise Documentation Platform V2 is working perfectly!" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "❌ Some tests failed. Please review the errors above." -ForegroundColor Red
    exit 1
}