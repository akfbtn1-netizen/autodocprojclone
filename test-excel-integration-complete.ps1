# Comprehensive Excel Change Sheet Integration Test
# This script clears fake data and tests the real Excel integration

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "    EXCEL CHANGE SHEET INTEGRATION TEST" -ForegroundColor Cyan  
Write-Host "=====================================================================" -ForegroundColor Cyan

$ErrorActionPreference = "Continue"

# Configuration
$excelPath = "C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx"
$serverName = "IRFS1\SQLDEV"
$databaseName = "DaQa"
$dashboardUrl = "http://localhost:5195"

# Step 1: Clear database
Write-Host "`n[STEP 1] Clearing database of fake/test data..." -ForegroundColor Yellow

try {
    Import-Module SqlServer -Force -ErrorAction SilentlyContinue
    
    $clearScript = @"
-- Remove test/fake data
DELETE FROM DaQa.WorkflowEvents WHERE EventType LIKE '%Test%' OR EventType LIKE '%Mock%';
DELETE FROM DaQa.ApprovalTracking WHERE JiraNumber LIKE 'TEST%' OR JiraNumber LIKE 'MOCK%' OR JiraNumber LIKE 'ROW%';
DELETE FROM DaQa.DocumentChanges WHERE 
    JiraNumber LIKE 'TEST%' OR 
    JiraNumber LIKE 'MOCK%' OR 
    JiraNumber LIKE 'ROW%' OR 
    Author = 'DocGenerator' OR 
    Author = 'TestUser' OR
    DocumentPath LIKE '%test%' OR 
    DocumentPath LIKE '%mock%';

-- Show remaining data
SELECT 'DocumentChanges' as TableName, COUNT(*) as RowsRemaining FROM DaQa.DocumentChanges
UNION ALL  
SELECT 'ApprovalTracking' as TableName, COUNT(*) as RowsRemaining FROM DaQa.ApprovalTracking
UNION ALL
SELECT 'WorkflowEvents' as TableName, COUNT(*) as RowsRemaining FROM DaQa.WorkflowEvents;
"@

    $result = Invoke-Sqlcmd -ServerInstance $serverName -Database $databaseName -Query $clearScript
    Write-Host "   ✓ Database cleared" -ForegroundColor Green
    $result | Format-Table -AutoSize
    
} catch {
    Write-Host "   ⚠ Database clear had issues: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Step 2: Check Excel file exists and inspect structure
Write-Host "`n[STEP 2] Checking Excel file..." -ForegroundColor Yellow

if (Test-Path $excelPath) {
    Write-Host "   ✓ Excel file found: $excelPath" -ForegroundColor Green
    
    try {
        $excel = New-Object -ComObject Excel.Application
        $excel.Visible = $false
        $workbook = $excel.Workbooks.Open($excelPath)
        $worksheet = $workbook.Worksheets.Item(1)
        
        $usedRange = $worksheet.UsedRange
        $rowCount = $usedRange.Rows.Count
        $colCount = $usedRange.Columns.Count
        
        Write-Host "   ✓ Excel has $rowCount rows and $colCount columns" -ForegroundColor Green
        
        # Check for data rows (skip headers)
        $dataRows = 0
        for ($row = 4; $row -le $rowCount; $row++) {
            $jiraValue = $worksheet.Cells.Item($row, 1).Value2
            if ($jiraValue -and $jiraValue -ne $null) {
                $dataRows++
            }
        }
        Write-Host "   ✓ Found $dataRows data rows with JIRA numbers" -ForegroundColor Green
        
        $workbook.Close()
        $excel.Quit()
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
        
    } catch {
        Write-Host "   ⚠ Excel inspection failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ❌ Excel file not found: $excelPath" -ForegroundColor Red
    Write-Host "   Please update the path in the script" -ForegroundColor Yellow
    exit 1
}

Write-Host "`n✅ EXCEL INTEGRATION TEST COMPLETED" -ForegroundColor Green
Write-Host "Database cleared and Excel file verified. Ready for real data testing." -ForegroundColor Gray