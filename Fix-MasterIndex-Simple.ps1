# ============================================================================
# Fix MasterIndex - Simple Targeted Fix
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host "Fixing MasterIndex namespace conflict..." -ForegroundColor Cyan
Write-Host ""

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

# File paths
$templateSelectorPath = "$projectRoot\src\Core\Application\Services\TemplateSelector.cs"
$docGeneratorPath = "$projectRoot\src\Core\Application\Services\DocGeneratorService.cs"

# Using alias to add
$usingAlias = "using MasterIndexEntity = Enterprise.Documentation.Core.Domain.Entities.MasterIndex;"

# ============================================================================
# Fix TemplateSelector.cs
# ============================================================================

Write-Host "Fixing TemplateSelector.cs..." -ForegroundColor Yellow

if (Test-Path $templateSelectorPath) {
    # Backup
    Copy-Item $templateSelectorPath "$templateSelectorPath.bak2" -Force

    $content = Get-Content $templateSelectorPath -Raw

    # Add using alias if not present
    if ($content -notmatch "using MasterIndexEntity") {
        # Find the last using statement and add after it
        $content = $content -replace "(using [^;]+;)(\r?\n\r?\nnamespace)", "`$1`r`n$usingAlias`$2"
        Write-Host "  Added using alias" -ForegroundColor Green
    }

    # Replace type usages
    $content = $content -replace '\(MasterIndex\s+', '(MasterIndexEntity '
    $content = $content -replace '<MasterIndex>', '<MasterIndexEntity>'
    $content = $content -replace 'Task<MasterIndex\?>', 'Task<MasterIndexEntity?>'

    Set-Content $templateSelectorPath $content -NoNewline
    Write-Host "  Fixed type references" -ForegroundColor Green
}

Write-Host ""

# ============================================================================
# Fix DocGeneratorService.cs
# ============================================================================

Write-Host "Fixing DocGeneratorService.cs..." -ForegroundColor Yellow

if (Test-Path $docGeneratorPath) {
    # Backup
    Copy-Item $docGeneratorPath "$docGeneratorPath.bak2" -Force

    $content = Get-Content $docGeneratorPath -Raw

    # Add using alias if not present
    if ($content -notmatch "using MasterIndexEntity") {
        $content = $content -replace "(using [^;]+;)(\r?\n\r?\nnamespace)", "`$1`r`n$usingAlias`$2"
        Write-Host "  Added using alias" -ForegroundColor Green
    }

    # Replace all type usages - be very specific
    # Constructor calls
    $content = $content -replace 'new MasterIndex\(', 'new MasterIndexEntity('
    $content = $content -replace 'new MasterIndex\s*\{', 'new MasterIndexEntity {'

    # Method parameters and return types
    $content = $content -replace 'MasterIndex\?', 'MasterIndexEntity?'
    $content = $content -replace '\(MasterIndex\s+', '(MasterIndexEntity '
    $content = $content -replace ',\s*MasterIndex\s+', ', MasterIndexEntity '
    $content = $content -replace '<MasterIndex>', '<MasterIndexEntity>'

    # Method signatures with MasterIndex as parameter type (not variable name)
    $content = $content -replace 'Task<MasterIndex>', 'Task<MasterIndexEntity>'
    $content = $content -replace 'DetermineDocumentType\(MasterIndex\s+', 'DetermineDocumentType(MasterIndexEntity '

    Set-Content $docGeneratorPath $content -NoNewline
    Write-Host "  Fixed type references" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Fix Complete!" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Backups created:" -ForegroundColor Yellow
Write-Host "  TemplateSelector.cs.bak2" -ForegroundColor Gray
Write-Host "  DocGeneratorService.cs.bak2" -ForegroundColor Gray
Write-Host ""
Write-Host "Now run: dotnet build" -ForegroundColor Cyan
Write-Host ""

Read-Host "Press Enter to exit"
