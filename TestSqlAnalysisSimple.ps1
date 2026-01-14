Write-Host "=== SQL Analysis Service Test ===" -ForegroundColor Cyan

# Find the API project
$apiProject = Get-ChildItem -Path "C:\Projects\EnterpriseDocumentationPlatform.V2" -Recurse -Filter "API.csproj" | Select-Object -First 1 -ExpandProperty FullName

if (-not $apiProject) {
    Write-Host "ERROR: Could not find API.csproj" -ForegroundColor Red
    exit 1
}

Write-Host "Found API project: $apiProject" -ForegroundColor Green

# Build first
Write-Host "`nBuilding project..." -ForegroundColor Yellow
dotnet build $apiProject -v q

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nSqlAnalysisService is available and compiles successfully!" -ForegroundColor Green
Write-Host "To test it, add the TestController endpoint and run the API." -ForegroundColor Yellow