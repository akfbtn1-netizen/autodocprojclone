# PowerShell script to check Excel data
$excelPath = "C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx"

Write-Host "Investigating Excel File Data Structure" -ForegroundColor Yellow

try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $workbook = $excel.Workbooks.Open($excelPath)
    $worksheet = $workbook.Worksheets.Item(1)
    
    # Get used range
    $usedRange = $worksheet.UsedRange
    $rowCount = $usedRange.Rows.Count
    $colCount = $usedRange.Columns.Count
    
    Write-Host "Excel Statistics:" -ForegroundColor Cyan
    Write-Host "  Row count: $rowCount"
    Write-Host "  Column count: $colCount"
    Write-Host ""
    
    # Read headers from row 3
    Write-Host "Headers (Row 3):" -ForegroundColor Cyan
    for ($col = 1; $col -le $colCount; $col++) {
        $value = $worksheet.Cells.Item(3, $col).Value2
        if ($value) {
            Write-Host "  Column $col`: $value"
        }
    }
    Write-Host ""
    
    # Find Status column
    $statusColumn = $null
    for ($col = 1; $col -le $colCount; $col++) {
        $headerValue = $worksheet.Cells.Item(3, $col).Value2
        if ($headerValue -like "*Status*") {
            $statusColumn = $col
            break
        }
    }
    
    if ($statusColumn) {
        Write-Host "Status Column Data (Column $statusColumn):" -ForegroundColor Green
        for ($row = 4; $row -le [Math]::min($rowCount, 8); $row++) {
            $statusValue = $worksheet.Cells.Item($row, $statusColumn).Value2
            $docIdValue = $worksheet.Cells.Item($row, 1).Value2
            Write-Host "  Row $row`: DocID='$docIdValue', Status='$statusValue'"
        }
    }
    else {
        Write-Host "No Status column found!" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Raw Data Sample (Rows 4-8):" -ForegroundColor Cyan
    for ($row = 4; $row -le [Math]::min($rowCount, 8); $row++) {
        Write-Host "Row $row`:"
        for ($col = 1; $col -le [Math]::min($colCount, 10); $col++) {
            $value = $worksheet.Cells.Item($row, $col).Value2
            $headerValue = $worksheet.Cells.Item(3, $col).Value2
            Write-Host "  $headerValue = '$value'"
        }
        Write-Host ""
    }
}
catch {
    Write-Host "Error reading Excel: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    if ($workbook) { 
        $workbook.Close($false) 
    }
    if ($excel) { 
        $excel.Quit() 
    }
    if ($excel) {
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }
}

Write-Host "Excel inspection complete" -ForegroundColor Green