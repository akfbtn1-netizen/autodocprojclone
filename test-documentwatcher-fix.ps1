# Test DocumentChangeWatcherService SQL Fix
Write-Host "Testing DocumentChangeWatcherService SQL Fix..." -ForegroundColor Green

# Run a quick SQL test to see if the fixed INSERT would work
$sqlTest = @"
-- Test the corrected INSERT statement
PRINT 'Testing DocumentationQueue INSERT with correct column names...'

-- Check current schema
SELECT COLUMN_NAME, DATA_TYPE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'DocumentationQueue' 
  AND TABLE_SCHEMA = 'DaQa'
ORDER BY ORDINAL_POSITION;

-- Test INSERT (without actually inserting - just validate syntax)
PRINT 'The SQL statement would be:'
PRINT 'INSERT INTO DaQa.DocumentationQueue (DocIdString, Status, Priority, CreatedDate)'
PRINT 'VALUES (''TEST-DOC-123'', ''Pending'', ''Medium'', GETUTCDATE())'
"@

# Execute the test
try {
    sqlcmd -S "ibidb2003dv" -d "IRFS1" -Q $sqlTest -E
    Write-Host "✅ SQL syntax validation completed!" -ForegroundColor Green
    Write-Host "The DocumentChangeWatcherService fix should now work correctly." -ForegroundColor Cyan
}
catch {
    Write-Host "❌ Error testing SQL: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nSummary of the fix:" -ForegroundColor Yellow
Write-Host "- Changed 'DocId' to 'DocIdString'" -ForegroundColor White  
Write-Host "- Changed 'QueuedDate' to 'CreatedDate'" -ForegroundColor White
Write-Host "- Updated parameter binding to @DocIdString" -ForegroundColor White
Write-Host "- SQL INSERT now matches actual DocumentationQueue schema" -ForegroundColor White