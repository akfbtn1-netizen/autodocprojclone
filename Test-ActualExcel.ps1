# Test writing DocID Test456 to row 153 of the actual Excel file

Write-Host "Testing Excel DocID write to actual file..." -ForegroundColor Green

$excelPath = "C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx"

# Check if file exists
if (!(Test-Path $excelPath)) {
    Write-Host "Excel file not found at: $excelPath" -ForegroundColor Red
    exit
}

Write-Host "Found Excel file: $excelPath" -ForegroundColor Yellow

# Try to load EPPlus
try {
    Add-Type -Path "C:\Users\Alexander.Kirby\.nuget\packages\epplus\7.0.0\lib\net6.0\EPPlus.dll" -ErrorAction Stop
    Write-Host "EPPlus loaded successfully" -ForegroundColor Green
} catch {
    try {
        # Try alternative path
        $packages = Get-ChildItem "C:\Users\Alexander.Kirby\.nuget\packages\epplus\*\lib\*\EPPlus.dll" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($packages) {
            Add-Type -Path $packages.FullName
            Write-Host "EPPlus loaded from: $($packages.FullName)" -ForegroundColor Green
        } else {
            throw "EPPlus not found"
        }
    } catch {
        Write-Host "EPPlus not available. Cannot directly test Excel manipulation." -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
        
        # Let's at least check if the file can be opened
        Write-Host "Attempting to check file accessibility..." -ForegroundColor Yellow
        
        try {
            $fileStream = [System.IO.File]::Open($excelPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            $fileStream.Close()
            Write-Host "File is accessible (not locked)" -ForegroundColor Green
        } catch {
            Write-Host "File may be locked or inaccessible: $_" -ForegroundColor Red
        }
        
        exit
    }
}

# Set EPPlus license context
[OfficeOpenXml.ExcelPackage]::LicenseContext = [OfficeOpenXml.LicenseContext]::NonCommercial

try {
    # Open the Excel file
    $fileInfo = New-Object System.IO.FileInfo($excelPath)
    $package = New-Object OfficeOpenXml.ExcelPackage($fileInfo)
    $worksheet = $package.Workbook.Worksheets[0]
    
    Write-Host "Opened worksheet: $($worksheet.Name)" -ForegroundColor Green
    Write-Host "Worksheet dimensions: $($worksheet.Dimension.Rows) rows x $($worksheet.Dimension.Columns) columns" -ForegroundColor Yellow
    
    # Find DocID column
    $docIdColumn = -1
    for ($col = 1; $col -le $worksheet.Dimension.Columns; $col++) {
        $header = $worksheet.Cells[1, $col].Value
        Write-Host "Column $col header: '$header'" -ForegroundColor Cyan
        
        if ($header -match "Doc.*ID|DocID" -or $header -eq "Doc_ID") {
            $docIdColumn = $col
            Write-Host "Found DocID column at position $col" -ForegroundColor Green
            break
        }
    }
    
    if ($docIdColumn -eq -1) {
        Write-Host "DocID column not found!" -ForegroundColor Red
        $package.Dispose()
        exit
    }
    
    # Check current value at row 153
    if ($worksheet.Dimension.Rows -ge 153) {
        $currentValue = $worksheet.Cells[153, $docIdColumn].Value
        Write-Host "Current value at row 153, DocID column: '$currentValue'" -ForegroundColor Yellow
        
        # Write Test456
        $worksheet.Cells[153, $docIdColumn].Value = "Test456"
        Write-Host "Set DocID to 'Test456' at row 153" -ForegroundColor Green
        
        # Save the file
        $package.Save()
        Write-Host "Excel file saved successfully!" -ForegroundColor Green
        
        # Verify the write
        $newValue = $worksheet.Cells[153, $docIdColumn].Value
        Write-Host "Verified new value: '$newValue'" -ForegroundColor Cyan
        
    } else {
        Write-Host "Row 153 does not exist (only $($worksheet.Dimension.Rows) rows)" -ForegroundColor Red
    }
    
    $package.Dispose()
    
} catch {
    Write-Host "Error manipulating Excel file: $_" -ForegroundColor Red
    Write-Host "Full error:" -ForegroundColor Red
    Write-Host $_.Exception.ToString() -ForegroundColor Red
}