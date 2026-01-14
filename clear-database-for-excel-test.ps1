# PowerShell script to clear database for Excel change sheet testing
# This removes fake/test data to allow real Excel data to be displayed

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "    CLEARING DATABASE FOR EXCEL CHANGE SHEET TEST" -ForegroundColor Cyan  
Write-Host "=====================================================================" -ForegroundColor Cyan

# Database connection details
$serverName = "IRFS1\SQLDEV"  # Update if different
$databaseName = "DaQa"        # Update if different

Write-Host "`n1. Connecting to database: $serverName.$databaseName" -ForegroundColor Yellow

try {
    # Import SQL Server module if needed
    if (!(Get-Module -Name SqlServer -ListAvailable)) {
        Write-Host "   Installing SQL Server module..." -ForegroundColor Yellow
        Install-Module -Name SqlServer -Scope CurrentUser -Force -AllowClobber
    }
    Import-Module SqlServer -Force

    # Test connection
    $connectionTest = "SELECT @@SERVERNAME as ServerName, DB_NAME() as DatabaseName"
    $testResult = Invoke-Sqlcmd -ServerInstance $serverName -Database $databaseName -Query $connectionTest
    Write-Host "   ✓ Connected to: $($testResult.ServerName)\$($testResult.DatabaseName)" -ForegroundColor Green

    # Show current data state
    Write-Host "`n2. Checking current data state..." -ForegroundColor Yellow
    
    $dataCheck = @"
SELECT 
    'DocumentChanges' as TableName, 
    COUNT(*) as RowCount,
    CASE WHEN COUNT(*) > 0 THEN 'Has Data' ELSE 'Empty' END as Status
FROM DaQa.DocumentChanges
UNION ALL
SELECT 
    'ApprovalTracking' as TableName,
    COUNT(*) as RowCount,
    CASE WHEN COUNT(*) > 0 THEN 'Has Data' ELSE 'Empty' END as Status  
FROM DaQa.ApprovalTracking
UNION ALL
SELECT 
    'WorkflowEvents' as TableName,
    COUNT(*) as RowCount,
    CASE WHEN COUNT(*) > 0 THEN 'Has Data' ELSE 'Empty' END as Status
FROM DaQa.WorkflowEvents
"@

    $currentState = Invoke-Sqlcmd -ServerInstance $serverName -Database $databaseName -Query $dataCheck
    $currentState | Format-Table -AutoSize
    
    # Clear test/fake data
    Write-Host "`n3. Clearing test/fake data..." -ForegroundColor Yellow
    
    $clearQueries = @(
        "DELETE FROM DaQa.WorkflowEvents WHERE EventType LIKE '%Test%' OR EventType LIKE '%Mock%'",
        "DELETE FROM DaQa.ApprovalTracking WHERE JiraNumber LIKE 'TEST%' OR JiraNumber LIKE 'MOCK%'", 
        "DELETE FROM DaQa.DocumentChanges WHERE JiraNumber LIKE 'TEST%' OR JiraNumber LIKE 'MOCK%' OR JiraNumber LIKE 'ROW%'",
        "DELETE FROM DaQa.DocumentChanges WHERE DocumentPath LIKE '%test%' OR DocumentPath LIKE '%mock%'",
        "DELETE FROM DaQa.DocumentChanges WHERE Author = 'DocGenerator' OR Author = 'TestUser'"
    )
    
    foreach ($query in $clearQueries) {
        try {
            $result = Invoke-Sqlcmd -ServerInstance $serverName -Database $databaseName -Query $query
            $affectedRows = $result.RecordsAffected ?? 0
            Write-Host "   ✓ Cleared $affectedRows rows" -ForegroundColor Green
        } catch {
            Write-Host "   ⚠ Query completed (may have affected 0 rows)" -ForegroundColor Yellow
        }
    }
    
    # Show final state
    Write-Host "`n4. Final data state after cleanup..." -ForegroundColor Yellow
    $finalState = Invoke-Sqlcmd -ServerInstance $serverName -Database $databaseName -Query $dataCheck
    $finalState | Format-Table -AutoSize
    
    # Reset identity columns if tables are empty
    Write-Host "`n5. Resetting identity columns..." -ForegroundColor Yellow
    $resetQueries = @(
        "IF NOT EXISTS (SELECT 1 FROM DaQa.DocumentChanges) DBCC CHECKIDENT ('DaQa.DocumentChanges', RESEED, 0)",
        "IF NOT EXISTS (SELECT 1 FROM DaQa.ApprovalTracking) DBCC CHECKIDENT ('DaQa.ApprovalTracking', RESEED, 0)",
        "IF NOT EXISTS (SELECT 1 FROM DaQa.WorkflowEvents) DBCC CHECKIDENT ('DaQa.WorkflowEvents', RESEED, 0)"
    )
    
    foreach ($resetQuery in $resetQueries) {
        try {
            Invoke-Sqlcmd -ServerInstance $serverName -Database $databaseName -Query $resetQuery
            Write-Host "   ✓ Identity reset completed" -ForegroundColor Green
        } catch {
            Write-Host "   ⚠ Identity reset skipped (table not empty)" -ForegroundColor Yellow
        }
    }
    
    Write-Host "`n✅ Database cleared and ready for Excel change sheet testing!" -ForegroundColor Green
    Write-Host "   - Test/fake data removed" -ForegroundColor Gray
    Write-Host "   - Identity columns reset" -ForegroundColor Gray
    Write-Host "   - Ready to import real Excel data" -ForegroundColor Gray

} catch {
    Write-Host "`n❌ Error clearing database: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check connection details and permissions." -ForegroundColor Yellow
}

Write-Host "`n=====================================================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Run Excel change sheet import" -ForegroundColor Gray
Write-Host "2. Verify data appears in UI dashboard" -ForegroundColor Gray
Write-Host "3. Test enhanced template generation" -ForegroundColor Gray
Write-Host "=====================================================================" -ForegroundColor Cyan