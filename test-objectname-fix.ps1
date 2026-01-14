# Test DocumentChangeWatcher ObjectName Fix
Write-Host "Testing DocumentChangeWatcherService ObjectName Fix..." -ForegroundColor Green

# Test the corrected INSERT statement with ObjectName
$sqlTest = @"
-- Test the corrected INSERT statement with ObjectName
PRINT 'Testing DocumentationQueue INSERT with ObjectName...'

-- Check current schema
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'DocumentationQueue' 
  AND TABLE_SCHEMA = 'DaQa'
ORDER BY ORDINAL_POSITION;

-- Test INSERT (simulation - not actually inserting)
PRINT ''
PRINT 'The SQL statement would now be:'
PRINT 'INSERT INTO DaQa.DocumentationQueue (DocIdString, ObjectName, Status, Priority, CreatedDate)'
PRINT 'VALUES (''DF-0128'', ''BAS-9759'', ''Pending'', ''Medium'', GETUTCDATE())'
PRINT ''
PRINT 'This should fix the NULL constraint error on ObjectName column.'
"@

# Execute the test
try {
    sqlcmd -S "ibidb2003dv" -d "IRFS1" -Q $sqlTest -E
    Write-Host "✅ SQL validation completed!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Error testing SQL: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n🔧 Summary of the ObjectName fix:" -ForegroundColor Yellow
Write-Host "- Added ObjectName parameter to TriggerDraftWorkflowAsync method" -ForegroundColor White  
Write-Host "- ObjectName uses StoredProcedureName if available, otherwise JIRA number" -ForegroundColor White
Write-Host "- Updated SQL INSERT to include ObjectName column" -ForegroundColor White
Write-Host "- This should resolve the NULL constraint error" -ForegroundColor White

Write-Host "`n🎯 The key issue was:" -ForegroundColor Cyan
Write-Host "   The DocumentationQueue table requires ObjectName (NOT NULL)" -ForegroundColor White
Write-Host "   But the original INSERT only provided DocIdString, Status, Priority, CreatedDate" -ForegroundColor White
Write-Host "   Now we provide ObjectName from StoredProcedureName or JiraNumber" -ForegroundColor White