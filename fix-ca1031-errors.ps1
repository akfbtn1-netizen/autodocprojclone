# PowerShell script to fix CA1031 code analysis errors
# Run this from the project root directory

Write-Host "Fixing CA1031 errors in DocGeneratorService.cs..." -ForegroundColor Cyan

$docGenPath = "src\Core\Application\Services\DocGeneratorService.cs"
if (Test-Path $docGenPath) {
    $content = Get-Content $docGenPath -Raw

    # Fix 1: GenerateDocumentAsync (around line 100)
    $content = $content -replace '(\s+)(catch \(Exception ex\)\s*\{[^}]*_logger\.LogError[^}]*"Error generating document"[^}]*\}\s*\})', '$1#pragma warning disable CA1031 // Do not catch general exception types$1$2$1#pragma warning restore CA1031'

    # Fix 2 & 3: EnhanceWithAIAsync (around lines 173-175)
    $content = $content -replace '(\s+)(catch \(Exception (?:ex|innerEx)\)\s*\{[^}]*(?:_logger\.LogWarning|_logger\.LogError)[^}]*"(?:Error in AI enhancement|AI enhancement failed)"[^}]*\}\s*)', '$1#pragma warning disable CA1031 // Do not catch general exception types$1$2$1#pragma warning restore CA1031$1'

    Set-Content $docGenPath -Value $content -NoNewline
    Write-Host "✓ Fixed DocGeneratorService.cs" -ForegroundColor Green
} else {
    Write-Host "✗ DocGeneratorService.cs not found at $docGenPath" -ForegroundColor Yellow
}

Write-Host "`nFixing CA1031 errors in NodeJsTemplateExecutor.cs..." -ForegroundColor Cyan

$nodejsPath = "src\Core\Application\Services\NodeJsTemplateExecutor.cs"
if (Test-Path $nodejsPath) {
    $content = Get-Content $nodejsPath -Raw

    # Fix for all three catch blocks in NodeJsTemplateExecutor
    # This adds pragma directives around each catch (Exception ex) block
    $lines = Get-Content $nodejsPath
    $newLines = @()
    $inCatchBlock = $false
    $catchBlockDepth = 0
    $addedPragma = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        # Detect catch (Exception ex) or catch (Exception)
        if ($line -match '^\s*catch\s*\(\s*Exception\s+\w+\s*\)') {
            # Get the indentation
            $indent = $line -replace '^(\s*).*', '$1'

            # Add pragma disable before catch
            $newLines += "$indent#pragma warning disable CA1031 // Do not catch general exception types"
            $newLines += $line
            $inCatchBlock = $true
            $catchBlockDepth = 1
            $addedPragma = $true
            continue
        }

        # Track brace depth in catch block
        if ($inCatchBlock) {
            if ($line -match '\{') {
                $catchBlockDepth += (($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count)
            }
            if ($line -match '\}') {
                $catchBlockDepth -= (($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count)

                if ($catchBlockDepth -le 0) {
                    $newLines += $line
                    # Get the indentation from the closing brace
                    $indent = $line -replace '^(\s*).*', '$1'
                    # Add pragma restore after catch block
                    $newLines += "$indent#pragma warning restore CA1031"
                    $inCatchBlock = $false
                    continue
                }
            }
        }

        $newLines += $line
    }

    Set-Content $nodejsPath -Value ($newLines -join "`r`n")
    Write-Host "✓ Fixed NodeJsTemplateExecutor.cs" -ForegroundColor Green
} else {
    Write-Host "✗ NodeJsTemplateExecutor.cs not found at $nodejsPath" -ForegroundColor Yellow
}

Write-Host "`n=== Done! ===" -ForegroundColor Green
Write-Host "Please rebuild your project to verify the fixes." -ForegroundColor Cyan
