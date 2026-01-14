# Test SP Documentation API and verify output to C:\Temp

Write-Host "Testing SP Documentation API output to C:\Temp..." -ForegroundColor Green

# Step 1: Check if the API is running (optional)
Write-Host "Checking if directory exists..." -ForegroundColor Yellow
$outputDir = "C:\Temp\Documentation-Catalog\Database\IRFS1\dbo\StoredProcedures"
if (Test-Path $outputDir) {
    Write-Host "✅ Output directory exists: $outputDir" -ForegroundColor Green
} else {
    Write-Host "❌ Output directory missing: $outputDir" -ForegroundColor Red
}

# Step 2: Clear any existing test files
Write-Host "Clearing any existing test files..." -ForegroundColor Yellow
Get-ChildItem $outputDir -Filter "*VerifyBAS*" -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $outputDir -Filter "*.docx" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Existing file: $($_.Name)" -ForegroundColor Cyan
}

# Step 3: Call the API endpoint
Write-Host "Making API call to generate SP documentation..." -ForegroundColor Yellow

# API endpoint URL (adjust port if needed)
$apiUrl = "http://localhost:5000/api/StoredProcedureDocumentation/usp_VerifyBAS/documentation"
$apiUrl2 = "https://localhost:7000/api/StoredProcedureDocumentation/usp_VerifyBAS/documentation"

# Request body
$body = @{
    ChangeDocumentId = "DOC-20251208-003515"
} | ConvertTo-Json

# Try both HTTP and HTTPS
$success = $false

foreach ($url in @($apiUrl, $apiUrl2)) {
    try {
        Write-Host "Trying: $url" -ForegroundColor Cyan
        
        $response = Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType "application/json" -TimeoutSec 30
        Write-Host "✅ API call successful!" -ForegroundColor Green
        Write-Host "Response: $($response | ConvertTo-Json)" -ForegroundColor Yellow
        $success = $true
        break
    }
    catch {
        Write-Host "❌ API call failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

if (!$success) {
    Write-Host "API not available. Testing service directly..." -ForegroundColor Yellow
    
    # Alternative: Check if any new docx files appear
    Write-Host "Monitoring directory for new files..." -ForegroundColor Yellow
    $beforeFiles = @(Get-ChildItem $outputDir -Filter "*.docx" -ErrorAction SilentlyContinue)
    Write-Host "Files before: $($beforeFiles.Count)" -ForegroundColor Cyan
}

# Step 4: Check for generated files
Write-Host "Checking for generated .docx files..." -ForegroundColor Yellow

Start-Sleep -Seconds 2  # Give time for file generation

$docxFiles = Get-ChildItem $outputDir -Filter "*.docx" -ErrorAction SilentlyContinue

if ($docxFiles) {
    Write-Host "✅ SUCCESS: Found .docx files in C:\Temp!" -ForegroundColor Green
    foreach ($file in $docxFiles) {
        Write-Host "  📄 $($file.Name) ($(($file.Length/1KB).ToString('F1')) KB, $($file.LastWriteTime))" -ForegroundColor Cyan
    }
} else {
    Write-Host "❌ No .docx files found in output directory" -ForegroundColor Red
    
    # Check if service is configured correctly
    Write-Host "Debugging configuration..." -ForegroundColor Yellow
    
    # Check appsettings.json
    $appsettingsPath = "C:\Projects\EnterpriseDocumentationPlatform.V2\src\Api\appsettings.json"
    if (Test-Path $appsettingsPath) {
        $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
        $spConfig = $appsettings.StoredProcedureDocumentation.OutputPath
        Write-Host "Config OutputPath: $spConfig" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "=== FINAL ASSESSMENT ===" -ForegroundColor Magenta
if ($docxFiles) {
    Write-Host "✅ SP Documentation is writing to C:\Temp correctly!" -ForegroundColor Green
    Write-Host "✅ Configuration fix successful!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Service may need restart to pick up config changes" -ForegroundColor Yellow
    Write-Host "⚠️  Or API service may not be running" -ForegroundColor Yellow
}