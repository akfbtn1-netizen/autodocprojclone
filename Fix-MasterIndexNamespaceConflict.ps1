# ============================================================================
# Fix MasterIndex Namespace Conflict
# ============================================================================
# Automatically fixes the namespace/type conflict in TemplateSelector.cs
# and DocGeneratorService.cs by adding a using alias
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "MasterIndex Namespace Conflict - Automated Fix" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Path to V2 project
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

$files = @(
    "$projectRoot\src\Core\Application\Services\TemplateSelector.cs",
    "$projectRoot\src\Core\Application\Services\DocGeneratorService.cs"
)

# Verify project exists
if (-not (Test-Path $projectRoot)) {
    Write-Host "ERROR: Project directory not found: $projectRoot" -ForegroundColor Red
    Write-Host "Please update the `$projectRoot variable in this script." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Project Root: $projectRoot" -ForegroundColor Yellow
Write-Host ""

# Process each file
foreach ($filePath in $files) {
    $fileName = Split-Path $filePath -Leaf

    Write-Host "Processing: $fileName" -ForegroundColor Cyan

    if (-not (Test-Path $filePath)) {
        Write-Host "  [WARNING] File not found: $filePath" -ForegroundColor Yellow
        Write-Host ""
        continue
    }

    # Create backup
    $backupPath = "$filePath.backup"
    Write-Host "  Creating backup: $fileName.backup" -ForegroundColor Gray
    Copy-Item $filePath $backupPath -Force

    # Read file content
    $content = Get-Content $filePath -Raw

    # Check if already fixed
    if ($content -match "using MasterIndexEntity\s*=") {
        Write-Host "  [SKIP] Already fixed (using alias found)" -ForegroundColor Yellow
        Write-Host ""
        continue
    }

    # Add using alias after the last using statement
    $usingAlias = "using MasterIndexEntity = Enterprise.Documentation.Core.Domain.Entities.MasterIndex;"

    # Find the last using statement
    if ($content -match "(?s)(.*)(using [^;]+;)(\s*namespace)") {
        $beforeLastUsing = $matches[1]
        $lastUsing = $matches[2]
        $afterLastUsing = $matches[3]

        # Insert the alias after the last using
        $newContent = $beforeLastUsing + $lastUsing + "`r`n" + $usingAlias + $afterLastUsing

        Write-Host "  [OK] Added using alias after last using statement" -ForegroundColor Green
    } else {
        Write-Host "  [WARNING] Could not find using statements pattern" -ForegroundColor Yellow
        Write-Host "  Attempting alternative insertion method..." -ForegroundColor Gray

        # Alternative: Insert before namespace declaration
        if ($content -match "(?s)(.*)(namespace\s+\S+)") {
            $beforeNamespace = $matches[1]
            $namespaceDecl = $matches[2]

            $newContent = $beforeNamespace + $usingAlias + "`r`n`r`n" + $namespaceDecl
            Write-Host "  [OK] Added using alias before namespace declaration" -ForegroundColor Green
        } else {
            Write-Host "  [ERROR] Could not find insertion point for using alias" -ForegroundColor Red
            Write-Host ""
            continue
        }
    }

    # Replace type references of MasterIndex with MasterIndexEntity
    # Be careful to only replace when used as a type, not as a namespace

    $replacements = @(
        # Method parameters: (MasterIndex metadata)
        @{ Pattern = '\(\s*MasterIndex\s+'; Replacement = '(MasterIndexEntity ' },

        # Return types: Task<MasterIndex>
        @{ Pattern = '<\s*MasterIndex\s*>'; Replacement = '<MasterIndexEntity>' },

        # Variable declarations: MasterIndex metadata =
        @{ Pattern = '([^\.]\s+)MasterIndex\s+(\w+)\s*='; Replacement = '$1MasterIndexEntity $2 =' },

        # Property types: public MasterIndex
        @{ Pattern = '(public|private|protected|internal)\s+MasterIndex\s+'; Replacement = '$1 MasterIndexEntity ' },

        # Cast: (MasterIndex)
        @{ Pattern = '\(\s*MasterIndex\s*\)'; Replacement = '(MasterIndexEntity)' },

        # Generic constraints: where T : MasterIndex
        @{ Pattern = ':\s*MasterIndex\s*($|,|\))'; Replacement = ': MasterIndexEntity$1' },

        # Array: MasterIndex[]
        @{ Pattern = 'MasterIndex\s*\[\s*\]'; Replacement = 'MasterIndexEntity[]' },

        # List/Collection: List<MasterIndex>
        @{ Pattern = '(List|IEnumerable|ICollection|IList)<\s*MasterIndex\s*>'; Replacement = '$1<MasterIndexEntity>' }
    )

    $changeCount = 0
    foreach ($replacement in $replacements) {
        $before = $newContent
        $newContent = $newContent -replace $replacement.Pattern, $replacement.Replacement
        if ($before -ne $newContent) {
            $changeCount++
        }
    }

    Write-Host "  [OK] Applied $changeCount type reference replacements" -ForegroundColor Green

    # Write the modified content back
    Set-Content -Path $filePath -Value $newContent -NoNewline

    Write-Host "  [OK] File updated successfully" -ForegroundColor Green
    Write-Host ""
}

# Summary
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Fix Complete" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Changes made:" -ForegroundColor Yellow
Write-Host "1. Created .backup files for both files" -ForegroundColor White
Write-Host "2. Added using alias: using MasterIndexEntity = ..." -ForegroundColor White
Write-Host "3. Replaced type references of MasterIndex with MasterIndexEntity" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Run: dotnet build" -ForegroundColor White
Write-Host "2. Verify no MasterIndex errors remain" -ForegroundColor White
Write-Host "3. If successful, you can delete the .backup files" -ForegroundColor White
Write-Host ""
Write-Host "If something went wrong:" -ForegroundColor Yellow
Write-Host "- Backup files are at: *.backup" -ForegroundColor White
Write-Host "- To restore: Copy-Item *.backup <original-file> -Force" -ForegroundColor White
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

Read-Host "Press Enter to exit"
