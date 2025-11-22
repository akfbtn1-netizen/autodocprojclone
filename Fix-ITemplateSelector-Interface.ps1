# ============================================================================
# Fix ITemplateSelector Interface
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host "Fixing ITemplateSelector interface..." -ForegroundColor Cyan
Write-Host ""

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

# Find the interface file
$interfacePath = "$projectRoot\src\Core\Application\Services\ITemplateSelector.cs"

if (-not (Test-Path $interfacePath)) {
    Write-Host "ERROR: ITemplateSelector.cs not found at: $interfacePath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Searching for the file..." -ForegroundColor Yellow

    # Search for it
    $found = Get-ChildItem -Path "$projectRoot\src" -Recurse -Filter "ITemplateSelector.cs" -ErrorAction SilentlyContinue

    if ($found) {
        $interfacePath = $found.FullName
        Write-Host "Found at: $interfacePath" -ForegroundColor Green
    } else {
        Write-Host "Could not find ITemplateSelector.cs" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
}

Write-Host "Processing: $interfacePath" -ForegroundColor Yellow
Write-Host ""

# Backup
Copy-Item $interfacePath "$interfacePath.bak" -Force
Write-Host "Created backup: ITemplateSelector.cs.bak" -ForegroundColor Gray

# Read content
$content = Get-Content $interfacePath -Raw

# Add using alias if not present
$usingAlias = "using MasterIndexEntity = Enterprise.Documentation.Core.Domain.Entities.MasterIndex;"

if ($content -notmatch "using MasterIndexEntity") {
    $content = $content -replace "(using [^;]+;)(\r?\n\r?\nnamespace)", "`$1`r`n$usingAlias`$2"
    Write-Host "Added using alias" -ForegroundColor Green
}

# Replace type references in interface
$content = $content -replace 'SelectTemplate\(MasterIndex\s+', 'SelectTemplate(MasterIndexEntity '
$content = $content -replace 'Task<MasterIndex>', 'Task<MasterIndexEntity>'
$content = $content -replace '<MasterIndex>', '<MasterIndexEntity>'
$content = $content -replace 'MasterIndex\?', 'MasterIndexEntity?'

# Save
Set-Content $interfacePath $content -NoNewline

Write-Host "Fixed interface method signature" -ForegroundColor Green
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Fix Complete!" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Now run: dotnet build" -ForegroundColor Cyan
Write-Host ""

Read-Host "Press Enter to exit"
