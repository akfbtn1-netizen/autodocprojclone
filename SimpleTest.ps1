# Simple Test for StoredProcedure Integration
Write-Host "=== StoredProcedure Integration Test ===" -ForegroundColor Cyan

# Test 1: Check key files exist
$files = @(
    "src\Application\Services\DocumentGeneration\Templates\StoredProcedureTemplate.cs",
    "src\Core\Application\Services\Documentation\StoredProcedureDocumentationService.cs",
    "dashboard.html",
    "sql\Add_DocumentVersionHistory_Table.sql"
)

Write-Host "`nFile Verification:" -ForegroundColor Yellow
foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "✅ $file" -ForegroundColor Green
    } else {
        Write-Host "❌ $file" -ForegroundColor Red
    }
}

# Test 2: Check project builds
Write-Host "`nBuild Test:" -ForegroundColor Yellow
try {
    dotnet build --no-restore --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Project builds successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Build error: $_" -ForegroundColor Red
}

# Test 3: Check critical code patterns
Write-Host "`nCode Pattern Verification:" -ForegroundColor Yellow

$spTemplate = "src\Application\Services\DocumentGeneration\Templates\StoredProcedureTemplate.cs"
if (Test-Path $spTemplate) {
    $content = Get-Content $spTemplate -Raw
    if ($content -match "GenerateDocument") {
        Write-Host "✅ StoredProcedureTemplate has GenerateDocument method" -ForegroundColor Green
    } else {
        Write-Host "❌ StoredProcedureTemplate missing GenerateDocument method" -ForegroundColor Red
    }
}

$spService = "src\Core\Application\Services\Documentation\StoredProcedureDocumentationService.cs"
if (Test-Path $spService) {
    $content = Get-Content $spService -Raw
    if ($content -match "CreateOrUpdateSPDocumentationAsync") {
        Write-Host "✅ StoredProcedureDocumentationService has main method" -ForegroundColor Green
    } else {
        Write-Host "❌ StoredProcedureDocumentationService missing main method" -ForegroundColor Red
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan