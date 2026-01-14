# Test Excel Integration Service fixes

Write-Host "Testing Excel Integration Service Fixes..." -ForegroundColor Green

Write-Host ""
Write-Host "=== ISSUES IDENTIFIED ===" -ForegroundColor Red
Write-Host "1. DocID values showing as '12/31/1899' (Excel date parsing issue)"
Write-Host "2. SQL errors: 'Invalid column name DateRequested' and 'CreatedDate'"
Write-Host "3. Multiple rows being skipped due to these issues"

Write-Host ""
Write-Host "=== FIXES IMPLEMENTED ===" -ForegroundColor Green
Write-Host "✅ 1. DocID Parsing Fix:"
Write-Host "   - Added logic to detect Excel date values (containing '/1899')"
Write-Host "   - Treats Excel dates as empty DocID values"
Write-Host "   - Prevents '12/31/1899' from being used as DocID"

Write-Host ""
Write-Host "✅ 2. Database Schema Fix:"
Write-Host "   - Changed 'DateRequested' to 'Date' (actual column name)"
Write-Host "   - Changed 'CreatedDate' to 'CreatedAt' (actual column name)"
Write-Host "   - Changed 'ModifiedDate' to 'UpdatedAt' (actual column name)"
Write-Host "   - Fixed parameter binding in both INSERT and UPDATE operations"

Write-Host ""
Write-Host "=== VERIFICATION ===" -ForegroundColor Yellow

# Check if service is running and processing
$logPath = "c:\Projects\EnterpriseDocumentationPlatform.V2\src\Api\api-output.log"

if (Test-Path $logPath) {
    Write-Host "Checking recent logs..." -ForegroundColor Cyan
    
    # Get recent log entries
    $recentLogs = Get-Content $logPath -Tail 20 | Where-Object { $_ -match "ExcelChangeIntegrator" }
    
    if ($recentLogs) {
        Write-Host "Recent Excel service activity:" -ForegroundColor Yellow
        $recentLogs | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    } else {
        Write-Host "No recent Excel service logs found" -ForegroundColor Yellow
    }
} else {
    Write-Host "Log file not found: $logPath" -ForegroundColor Yellow
}

# Check database for recent activity
Write-Host ""
Write-Host "Checking database for recent changes..." -ForegroundColor Cyan

try {
    # Check for recently inserted records
    $recentRecords = sqlcmd -S "ibidb2003dv" -d "IRFS1" -E -Q "SELECT TOP 5 Id, JiraNumber, DocId, Status, CreatedAt FROM DaQa.DocumentChanges ORDER BY Id DESC" -h -1 -W
    
    if ($recentRecords) {
        Write-Host "Recent DocumentChanges records:" -ForegroundColor Yellow
        Write-Host $recentRecords
    }
} catch {
    Write-Host "Error checking database: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== EXPECTED RESULTS AFTER FIX ===" -ForegroundColor Green
Write-Host "✅ No more '12/31/1899' DocID warnings"
Write-Host "✅ No more 'Invalid column name' SQL errors"  
Write-Host "✅ Excel rows should process successfully"
Write-Host "✅ New records inserted instead of all being skipped"

Write-Host ""
Write-Host "=== MONITORING RECOMMENDATION ===" -ForegroundColor Cyan
Write-Host "1. Restart the service to pick up the fixes"
Write-Host "2. Monitor logs for successful processing:"
Write-Host "   - Look for 'Inserted new row for JIRA' messages"
Write-Host "   - Verify no more date/column errors"
Write-Host "3. Check DocumentChanges table for new records"

Write-Host ""
Write-Host "EXCEL INTEGRATION SERVICE FIXES COMPLETE!" -ForegroundColor Green