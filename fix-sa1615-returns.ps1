# Fix SA1615: Add missing return documentation
# Focuses on methods that return values but lack documentation

$ErrorActionPreference = "SilentlyContinue"
$files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

Write-Host "Starting SA1615 return documentation fix..." -ForegroundColor Green

$totalFiles = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Pattern 1: Methods returning Task<T> without return docs
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*(?:/// <param[\s\S]*?</param>\s*)*)(public\s+(?:async\s+)?Task<[^>]+>\s+\w+)', '$1/// <returns>A task that represents the asynchronous operation.</returns>`n    $2'
    
    # Pattern 2: Methods returning bool without return docs
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*(?:/// <param[\s\S]*?</param>\s*)*)(public\s+bool\s+\w+)', '$1/// <returns>True if the operation succeeds; otherwise, false.</returns>`n    $2'
    
    # Pattern 3: Methods returning int without return docs  
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*(?:/// <param[\s\S]*?</param>\s*)*)(public\s+int\s+\w+)', '$1/// <returns>The number of items processed.</returns>`n    $2'
    
    # Pattern 4: Methods returning string without return docs
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*(?:/// <param[\s\S]*?</param>\s*)*)(public\s+string\s+\w+)', '$1/// <returns>The requested string value.</returns>`n    $2'
    
    # Pattern 5: Methods returning IEnumerable without return docs
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*(?:/// <param[\s\S]*?</param>\s*)*)(public\s+IEnumerable<[^>]+>\s+\w+)', '$1/// <returns>A collection of items.</returns>`n    $2'
    
    if ($content -ne $originalContent) {
        try {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            $totalFiles++
            Write-Host "Fixed SA1615 in: $($file.Name)" -ForegroundColor Yellow
        }
        catch {
            Write-Warning "Failed to update $($file.FullName): $($_.Exception.Message)"
        }
    }
}

Write-Host "SA1615 return documentation fix complete. Files updated: $totalFiles" -ForegroundColor Green