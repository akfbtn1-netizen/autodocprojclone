# ============================================================================
# Search for ITemplateSelector Definition
# ============================================================================

$ErrorActionPreference = "Continue"

Write-Host "Searching for ITemplateSelector..." -ForegroundColor Cyan
Write-Host ""

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

# Search all .cs files for the interface definition
Write-Host "Searching all .cs files..." -ForegroundColor Yellow

$results = Get-ChildItem -Path "$projectRoot\src" -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue |
    Select-String -Pattern "interface\s+ITemplateSelector" -Context 2,5

if ($results) {
    Write-Host ""
    Write-Host "Found ITemplateSelector definition:" -ForegroundColor Green
    Write-Host ""

    foreach ($result in $results) {
        $fileName = $result.Filename
        $filePath = $result.Path
        $lineNumber = $result.LineNumber

        Write-Host "File: $filePath" -ForegroundColor Cyan
        Write-Host "Line: $lineNumber" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Context:" -ForegroundColor Yellow
        Write-Host $result.Context.DisplayPreContext -ForegroundColor Gray
        Write-Host $result.Line -ForegroundColor White
        Write-Host $result.Context.DisplayPostContext -ForegroundColor Gray
        Write-Host ""
        Write-Host "============================================================================" -ForegroundColor Cyan
    }
} else {
    Write-Host "ITemplateSelector interface not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "Also searching for 'SelectTemplate' method signature..." -ForegroundColor Yellow
Write-Host ""

$methodResults = Get-ChildItem -Path "$projectRoot\src" -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue |
    Select-String -Pattern "SelectTemplate.*MasterIndex" -Context 1,2

if ($methodResults) {
    Write-Host "Found SelectTemplate with MasterIndex:" -ForegroundColor Green
    Write-Host ""

    foreach ($result in $methodResults) {
        $filePath = $result.Path
        $lineNumber = $result.LineNumber

        Write-Host "File: $filePath" -ForegroundColor Cyan
        Write-Host "Line: $lineNumber" -ForegroundColor Gray
        Write-Host $result.Line -ForegroundColor White
        Write-Host ""
    }
}

Write-Host ""
Read-Host "Press Enter to exit"
