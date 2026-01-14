# Write Test456 to DocID column in row 153 (using row 3 as headers)

Write-Host "Writing Test456 to DocID column in row 153..." -ForegroundColor Green

$excelPath = "C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx"

if (!(Test-Path $excelPath)) {
    Write-Host "Excel file not found: $excelPath" -ForegroundColor Red
    exit
}

try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $workbook = $excel.Workbooks.Open($excelPath)
    $worksheet = $workbook.Worksheets.Item(1)
    
    Write-Host "Successfully opened Excel file" -ForegroundColor Green
    Write-Host "Looking at row 3 for headers..." -ForegroundColor Yellow
    
    # Find DocID column by checking row 3 headers
    $docIdColumn = -1
    for ($col = 1; $col -le 30; $col++) {  # Check first 30 columns
        $header = $worksheet.Cells.Item(3, $col).Value2
        Write-Host "Column $col (row 3): '$header'" -ForegroundColor Cyan
        
        if ($header -and ($header -like "*Doc*ID*" -or $header -like "*DocID*" -or $header -eq "Doc_ID" -or $header -like "Doc ID")) {
            $docIdColumn = $col
            Write-Host "Found DocID column at position $col" -ForegroundColor Green
            break
        }
    }
    
    if ($docIdColumn -ne -1) {
        # Check current value in row 153
        $currentValue = $worksheet.Cells.Item(153, $docIdColumn).Value2
        Write-Host "Current DocID value in row 153: '$currentValue'" -ForegroundColor Yellow
        
        # Write Test456 to DocID column in row 153
        Write-Host "Writing 'Test456' to row 153, column $docIdColumn..." -ForegroundColor Cyan
        $worksheet.Cells.Item(153, $docIdColumn).Value2 = "Test456"
        
        # Save the workbook
        $workbook.Save()
        Write-Host "Excel file saved!" -ForegroundColor Green
        
        # Verify the write
        $newValue = $worksheet.Cells.Item(153, $docIdColumn).Value2
        Write-Host "Verified new DocID value: '$newValue'" -ForegroundColor Green
        
        Write-Host "✅ SUCCESS: Test456 written to DocID column in row 153!" -ForegroundColor Green
        
    } else {
        Write-Host "DocID column not found in row 3 headers" -ForegroundColor Red
        Write-Host "Available headers in row 3:" -ForegroundColor Yellow
        for ($col = 1; $col -le 15; $col++) {
            $header = $worksheet.Cells.Item(3, $col).Value2
            if ($header) {
                Write-Host "  Column $col : '$header'" -ForegroundColor Cyan
            }
        }
    }
    
    $workbook.Close()
    $excel.Quit()
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
} finally {
    if ($excel) {
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }
}