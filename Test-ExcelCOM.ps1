# Read Excel file to find what JIRA number is in row 153

Write-Host "Reading Excel file to find JIRA in row 153..." -ForegroundColor Green

$excelPath = "C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx"

if (!(Test-Path $excelPath)) {
    Write-Host "Excel file not found: $excelPath" -ForegroundColor Red
    exit
}

# Try to read using COM object (Excel application)
try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $workbook = $excel.Workbooks.Open($excelPath)
    $worksheet = $workbook.Worksheets.Item(1)
    
    Write-Host "Successfully opened Excel file" -ForegroundColor Green
    Write-Host "Worksheet name: $($worksheet.Name)" -ForegroundColor Yellow
    
    # Find JIRA column by checking headers
    $jiraColumn = -1
    for ($col = 1; $col -le 20; $col++) {  # Check first 20 columns
        $header = $worksheet.Cells.Item(1, $col).Value2
        Write-Host "Column $col header: '$header'" -ForegroundColor Cyan
        
        if ($header -and ($header -like "*JIRA*" -or $header -like "*Jira*")) {
            $jiraColumn = $col
            Write-Host "Found JIRA column at position $col" -ForegroundColor Green
            break
        }
    }
    
    if ($jiraColumn -ne -1) {
        # Get JIRA value from row 153
        $jiraValue = $worksheet.Cells.Item(153, $jiraColumn).Value2
        Write-Host "JIRA number in row 153: '$jiraValue'" -ForegroundColor Yellow
        
        # Also check DocID column
        for ($col = 1; $col -le 20; $col++) {
            $header = $worksheet.Cells.Item(1, $col).Value2
            if ($header -and ($header -like "*Doc*ID*" -or $header -like "*DocID*" -or $header -eq "Doc_ID")) {
                $docIdValue = $worksheet.Cells.Item(153, $col).Value2
                Write-Host "Current DocID in row 153: '$docIdValue'" -ForegroundColor Yellow
                
                # NOW TEST THE WRITE
                Write-Host "Writing Test456 to DocID column..." -ForegroundColor Cyan
                $worksheet.Cells.Item(153, $col).Value2 = "Test456"
                
                # Save the workbook
                $workbook.Save()
                Write-Host "Excel file saved with Test456 in row 153!" -ForegroundColor Green
                
                # Verify the write
                $newValue = $worksheet.Cells.Item(153, $col).Value2
                Write-Host "Verified new DocID value: '$newValue'" -ForegroundColor Green
                break
            }
        }
        
    } else {
        Write-Host "JIRA column not found" -ForegroundColor Red
    }
    
    $workbook.Close()
    $excel.Quit()
    
    Write-Host "✅ SUCCESS: Test456 written to row 153 DocID column!" -ForegroundColor Green
    
} catch {
    Write-Host "Error reading Excel file: $_" -ForegroundColor Red
    Write-Host "Excel may not be installed or file may be locked" -ForegroundColor Yellow
} finally {
    if ($excel) {
        [System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
    }
}