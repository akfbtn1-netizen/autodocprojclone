# Create a simple test Excel file for DocId writeback testing

Write-Host "Creating test Excel file..." -ForegroundColor Green

# Define the test Excel path (same as configured in the service)
$excelPath = "C:\Users\Alexander.Kirby\Desktop\Doctest\BI Analytics Change Spreadsheet.xlsx"

# Create directory if it doesn't exist
$directory = Split-Path $excelPath -Parent
if (!(Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force
    Write-Host "Created directory: $directory" -ForegroundColor Yellow
}

# Create a simple CSV that can be opened as Excel
$csvContent = @"
JIRA #,Status,Priority,Doc_ID,Description
TEST-123,Completed,High,,Test BAS marker extraction
TEST-456,Active,Medium,,Another test row
"@

# Save as CSV first (Excel can open this)
$csvPath = $excelPath -replace "\.xlsx$", ".csv"
$csvContent | Out-File -FilePath $csvPath -Encoding UTF8

Write-Host "Created test CSV file: $csvPath" -ForegroundColor Green
Write-Host "Content preview:" -ForegroundColor Yellow
Get-Content $csvPath

Write-Host ""
Write-Host "Now restart the service to test Excel writeback!" -ForegroundColor Cyan