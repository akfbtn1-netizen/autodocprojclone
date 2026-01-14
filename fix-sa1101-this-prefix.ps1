# Fix SA1101: Prefix local calls with this
# Adds 'this.' prefix to local property/field access

$ErrorActionPreference = "SilentlyContinue"
$contractFiles = Get-ChildItem -Path "src\Shared\Contracts" -Filter "*.cs" -Recurse

Write-Host "Starting SA1101 'this' prefix fix..." -ForegroundColor Green

foreach ($file in $contractFiles) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Common patterns for SA1101 fixes (be conservative)
    # Fix property assignments in constructors/methods
    $content = $content -replace '\s([A-Z]\w+)\s*=\s*([a-z]\w+)', ' this.$1 = $2'
    $content = $content -replace 'return\s+([A-Z]\w+);', 'return this.$1;'
    
    # Fix method calls that should be prefixed
    $content = $content -replace '(\s+)([A-Z]\w+)\s*\?\?\s*=', '$1this.$2 ??= '
    
    if ($content -ne $originalContent) {
        try {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            Write-Host "Fixed SA1101 in: $($file.Name)" -ForegroundColor Yellow
        }
        catch {
            Write-Warning "Failed to update $($file.FullName): $($_.Exception.Message)"
        }
    }
}

Write-Host "SA1101 'this' prefix fix completed." -ForegroundColor Green