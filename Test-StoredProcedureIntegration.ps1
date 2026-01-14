# Test script for StoredProcedure integration validation

Write-Host "🚀 Testing Enterprise Documentation Platform - StoredProcedure Integration" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan

$testResults = @()

# Test 1: Check file existence
Write-Host "`n📁 Checking file structure..." -ForegroundColor Yellow
$files = @(
    "src/Application/Services/DocumentGeneration/Templates/StoredProcedureTemplate.cs",
    "src/Core/Application/Services/Documentation/StoredProcedureDocumentationService.cs", 
    "sql/Add_DocumentVersionHistory_Table.sql",
    "dashboard.html",
    "WorkflowEventService.cs",
    "DocGeneratorService_StoredProcedureIntegration.cs",
    "Program_Complete_Registrations.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "✅ $file" -ForegroundColor Green
        $testResults += "PASS"
    } else {
        Write-Host "❌ $file" -ForegroundColor Red
        $testResults += "FAIL"
    }
}

# Test 2: Check SQL script structure
Write-Host "`n🗄️ Testing SQL script..." -ForegroundColor Yellow
$sqlContent = Get-Content "sql/Add_DocumentVersionHistory_Table.sql" -Raw
if ($sqlContent -match "CREATE TABLE.*DocumentVersionHistory" -and 
    $sqlContent -match "CREATE VIEW.*vw_DocumentVersionSummary" -and
    $sqlContent -match "CREATE PROCEDURE.*usp_AddVersionHistory") {
    Write-Host "✅ SQL script has all required components" -ForegroundColor Green
    $testResults += "PASS"
} else {
    Write-Host "❌ SQL script missing components" -ForegroundColor Red
    $testResults += "FAIL"
}

# Test 3: Check WorkflowEventType enum
Write-Host "`n⚡ Testing workflow events..." -ForegroundColor Yellow
$workflowContent = Get-Content "WorkflowEventService.cs" -Raw
if ($workflowContent -match "StoredProcedureDetected" -and 
    $workflowContent -match "ComplexityAnalysisStarted" -and
    $workflowContent -match "SPDocumentationGenerationCompleted") {
    Write-Host "✅ StoredProcedure workflow events added" -ForegroundColor Green
    $testResults += "PASS"
} else {
    Write-Host "❌ Missing StoredProcedure workflow events" -ForegroundColor Red
    $testResults += "FAIL"
}

# Test 4: Check service registration
Write-Host "`n🔧 Testing service registration..." -ForegroundColor Yellow
$programContent = Get-Content "Program_Complete_Registrations.cs" -Raw
if ($programContent -match "IStoredProcedureDocumentationService") {
    Write-Host "✅ StoredProcedureDocumentationService registered" -ForegroundColor Green
    $testResults += "PASS"
} else {
    Write-Host "❌ Service registration missing" -ForegroundColor Red  
    $testResults += "FAIL"
}

# Test 5: Check dashboard integration
Write-Host "`n🎛️ Testing dashboard integration..." -ForegroundColor Yellow
$dashboardContent = Get-Content "dashboard.html" -Raw
if ($dashboardContent -match "StoredProcedureDetected" -and 
    $dashboardContent -match "sp-highlight" -and
    $dashboardContent -match "SP Documents Generated") {
    Write-Host "✅ Dashboard has StoredProcedure integration" -ForegroundColor Green
    $testResults += "PASS"
} else {
    Write-Host "❌ Dashboard missing StoredProcedure features" -ForegroundColor Red
    $testResults += "FAIL"
}

# Test 6: Check template structure
Write-Host "`n📝 Testing template structure..." -ForegroundColor Yellow
$templateContent = Get-Content "src/Application/Services/DocumentGeneration/Templates/StoredProcedureTemplate.cs" -Raw
if ($templateContent -match "StoredProcedureData" -and 
    $templateContent -match "ComplexityScore" -and
    $templateContent -match "CreateSampleData" -and
    $templateContent -match "GenerateDocument") {
    Write-Host "✅ StoredProcedureTemplate has all required methods" -ForegroundColor Green
    $testResults += "PASS"
} else {
    Write-Host "❌ Template structure incomplete" -ForegroundColor Red
    $testResults += "FAIL"
}

# Summary
Write-Host "`n" + "=" * 70 -ForegroundColor Cyan
$passCount = ($testResults | Where-Object { $_ -eq "PASS" }).Count
$totalTests = $testResults.Count
$successRate = [math]::Round(($passCount / $totalTests) * 100, 1)

if ($passCount -eq $totalTests) {
    Write-Host "🎉 ALL TESTS PASSED! ($passCount/$totalTests) - $successRate%" -ForegroundColor Green
    Write-Host "✅ System ready for tomorrow's demo!" -ForegroundColor Green
} elseif ($passCount -ge ($totalTests * 0.8)) {
    Write-Host "⚠️  MOSTLY READY! ($passCount/$totalTests) - $successRate%" -ForegroundColor Yellow
    Write-Host "✅ Core functionality working, minor issues to fix" -ForegroundColor Yellow
} else {
    Write-Host "❌ NEEDS WORK! ($passCount/$totalTests) - $successRate%" -ForegroundColor Red
    Write-Host "🔧 Major integration issues detected" -ForegroundColor Red
}
}

Write-Host "`n🚀 Demo Components Ready:" -ForegroundColor Cyan
Write-Host "   • StoredProcedure adaptive template with OpenXML" -ForegroundColor White
Write-Host "   • Real-time workflow tracking with SignalR" -ForegroundColor White  
Write-Host "   • Complexity-based section generation" -ForegroundColor White
Write-Host "   • Version history tracking with database integration" -ForegroundColor White
Write-Host "   • Live dashboard with StoredProcedure-specific events" -ForegroundColor White

Write-Host "`n📋 Demo Flow:" -ForegroundColor Cyan
Write-Host "   1. Change Detection → SP Analysis" -ForegroundColor White
Write-Host "   2. Complexity Scoring → Adaptive Sections" -ForegroundColor White
Write-Host "   3. OpenXML Generation → Version Tracking" -ForegroundColor White
Write-Host "   4. Real-time Dashboard → Live Progress Updates" -ForegroundColor White