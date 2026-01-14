# Phase 2 Test Script - AI-Powered Metadata
# Tests the MetadataAIService and Phase 16 AI enrichment

Write-Host "🧪 Testing Phase 2: AI-Powered Metadata" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check API is running
Write-Host "Test 1: API Health Check" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "http://localhost:5195/weatherforecast" -Method GET -TimeoutSec 10
    Write-Host "✅ API is running on port 5195" -ForegroundColor Green
}
catch {
    Write-Host "❌ API not responding: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Insert test data to trigger AI processing
Write-Host ""
Write-Host "Test 2: Creating test document change to trigger AI processing" -ForegroundColor Yellow

$testData = @{
    UniqueKey = "TEST-AI-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    JiraNumber = "TEST-AI-001"
    Status = "Completed"
    ChangeType = "Enhancement"
    TableName = "irf_policy"
    ColumnName = "PolicyStatus"
    Description = "Enhanced policy status tracking for premium calculations and billing cycles"
    ReportedBy = "Phase2Test"
    AssignedTo = "developer@tfic.com"
}

# Insert to database
$connectionString = "Server=ibidb2003dv;Database=IRFS1;Integrated Security=true;TrustServerCertificate=true"
$insertSql = @"
INSERT INTO DaQa.DocumentChanges (
    UniqueKey, JiraNumber, Status, ChangeType, 
    TableName, ColumnName, Description, ReportedBy, AssignedTo,
    CreatedAt, UpdatedAt
)
VALUES (
    '$($testData.UniqueKey)',
    '$($testData.JiraNumber)',
    '$($testData.Status)',
    '$($testData.ChangeType)',
    '$($testData.TableName)',
    '$($testData.ColumnName)',
    '$($testData.Description)',
    '$($testData.ReportedBy)',
    '$($testData.AssignedTo)',
    GETUTCDATE(),
    GETUTCDATE()
)
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    $command = New-Object System.Data.SqlClient.SqlCommand($insertSql, $connection)
    $result = $command.ExecuteNonQuery()
    $connection.Close()
    Write-Host "✅ Test document change inserted successfully" -ForegroundColor Green
}
catch {
    Write-Host "❌ Failed to insert test data: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Wait for processing and check results
Write-Host ""
Write-Host "Test 3: Monitoring processing pipeline..." -ForegroundColor Yellow
Write-Host "⏳ Waiting for background services to process the test data..." -ForegroundColor Yellow

# Wait for processing (background services poll every minute)
Start-Sleep -Seconds 75

# Check if DocId was generated
$checkDocIdSql = @"
SELECT TOP 1 DocId, Status 
FROM DaQa.DocumentChanges 
WHERE UniqueKey = '$($testData.UniqueKey)'
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($checkDocIdSql, $connection)
    $dataTable = New-Object System.Data.DataTable
    $adapter.Fill($dataTable)
    $connection.Close()
    
    if ($dataTable.Rows.Count -gt 0) {
        $docId = $dataTable.Rows[0]["DocId"]
        if ($docId -and $docId -ne [System.DBNull]::Value) {
            Write-Host "✅ DocId generated: $docId" -ForegroundColor Green
            
            # Test 4: Check MasterIndex for AI enrichment
            Write-Host ""
            Write-Host "Test 4: Checking AI metadata enrichment" -ForegroundColor Yellow
            
            $checkAISql = @"
            SELECT 
                DocId,
                BusinessDomain,
                SemanticCategory,
                AIGeneratedTags,
                ComplianceTags,
                PIIIndicator,
                SensitivityLevel,
                CompletenessScore,
                CreatedDate
            FROM DaQa.MasterIndex 
            WHERE DocId = '$docId'
            "@
            
            $connection.Open()
            $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($checkAISql, $connection)
            $aiDataTable = New-Object System.Data.DataTable
            $adapter.Fill($aiDataTable)
            $connection.Close()
            
            if ($aiDataTable.Rows.Count -gt 0) {
                $row = $aiDataTable.Rows[0]
                Write-Host "✅ MasterIndex record found for DocId: $docId" -ForegroundColor Green
                Write-Host ""
                
                Write-Host "📊 AI Metadata Results:" -ForegroundColor Cyan
                Write-Host "├─ BusinessDomain: $($row['BusinessDomain'])" -ForegroundColor White
                Write-Host "├─ SemanticCategory: $($row['SemanticCategory'])" -ForegroundColor White
                Write-Host "├─ AIGeneratedTags: $($row['AIGeneratedTags'])" -ForegroundColor White
                Write-Host "├─ ComplianceTags: $($row['ComplianceTags'])" -ForegroundColor White
                Write-Host "├─ PIIIndicator: $($row['PIIIndicator'])" -ForegroundColor White
                Write-Host "├─ SensitivityLevel: $($row['SensitivityLevel'])" -ForegroundColor White
                Write-Host "└─ CompletenessScore: $($row['CompletenessScore'])%" -ForegroundColor White
                Write-Host ""
                
                # Validate Phase 2 features
                $phase2Success = 0
                $phase2Total = 3
                
                if ($row['SemanticCategory'] -and $row['SemanticCategory'] -ne [System.DBNull]::Value) {
                    Write-Host "✅ SemanticCategory populated (AI Classification working)" -ForegroundColor Green
                    $phase2Success++
                } else {
                    Write-Host "⚠️  SemanticCategory not populated" -ForegroundColor Yellow
                }
                
                if ($row['AIGeneratedTags'] -and $row['AIGeneratedTags'] -ne [System.DBNull]::Value) {
                    Write-Host "✅ AIGeneratedTags populated (AI Tag Generation working)" -ForegroundColor Green
                    $phase2Success++
                } else {
                    Write-Host "⚠️  AIGeneratedTags not populated" -ForegroundColor Yellow
                }
                
                if ($row['ComplianceTags'] -and $row['ComplianceTags'] -ne [System.DBNull]::Value) {
                    Write-Host "✅ ComplianceTags populated (AI Compliance Classification working)" -ForegroundColor Green
                    $phase2Success++
                } else {
                    Write-Host "⚠️  ComplianceTags not populated" -ForegroundColor Yellow
                }
                
                Write-Host ""
                Write-Host "📈 Phase 2 Test Results:" -ForegroundColor Cyan
                Write-Host "Phase 2 AI Features: $phase2Success/$phase2Total working" -ForegroundColor Yellow
                
                if ($phase2Success -eq $phase2Total) {
                    Write-Host "🎉 Phase 2 AI-Powered Metadata: FULLY FUNCTIONAL!" -ForegroundColor Green
                } elseif ($phase2Success -gt 0) {
                    Write-Host "⚠️  Phase 2 AI-Powered Metadata: PARTIALLY FUNCTIONAL" -ForegroundColor Yellow
                } else {
                    Write-Host "❌ Phase 2 AI-Powered Metadata: NOT FUNCTIONAL" -ForegroundColor Red
                }
                
            } else {
                Write-Host "❌ No MasterIndex record found for DocId: $docId" -ForegroundColor Red
            }
        } else {
            Write-Host "⏳ DocId not yet generated - processing may still be in progress" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ Test document change not found" -ForegroundColor Red
    }
}
catch {
    Write-Host "❌ Failed to check processing results: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "🏁 Phase 2 Test Complete" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 Note: If AI features are not working, check:" -ForegroundColor Yellow
Write-Host "   - OpenAI configuration in appsettings.json" -ForegroundColor White
Write-Host "   - API logs for AI service errors" -ForegroundColor White
Write-Host "   - Network connectivity to Azure OpenAI endpoint" -ForegroundColor White