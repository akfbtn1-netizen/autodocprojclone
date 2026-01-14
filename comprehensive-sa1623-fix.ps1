# Comprehensive SA1623 Property Documentation Fix
# Fixes property documentation format across entire codebase

$ErrorActionPreference = "SilentlyContinue"
$files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

Write-Host "Starting comprehensive SA1623 property documentation fix..." -ForegroundColor Green

$totalFiles = 0
$totalReplacements = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Pattern 1: Fix "Gets or sets" properties
    $content = $content -replace '/// <summary>Gets or sets\s+([^<]+)</summary>', '/// <summary>Gets or sets $1.</summary>'
    
    # Pattern 2: Fix "Gets" properties  
    $content = $content -replace '/// <summary>Gets\s+([^<]+)</summary>', '/// <summary>Gets $1.</summary>'
    
    # Pattern 3: Fix "Sets" properties
    $content = $content -replace '/// <summary>Sets\s+([^<]+)</summary>', '/// <summary>Sets $1.</summary>'
    
    # Pattern 4: Fix properties starting with lowercase
    $content = $content -replace '/// <summary>Gets or sets ([a-z][^<]*)</summary>', {
        param($match)
        $text = $match.Groups[1].Value
        $capitalized = $text.Substring(0,1).ToUpper() + $text.Substring(1)
        "/// <summary>Gets or sets $capitalized.</summary>"
    }
    
    # Pattern 5: Fix missing periods at end
    $content = $content -replace '/// <summary>(Gets or sets [^<]+[^.])</summary>', '/// <summary>$1.</summary>'
    
    # Pattern 6: Fix double periods
    $content = $content -replace '/// <summary>([^<]+)\.\.</summary>', '/// <summary>$1.</summary>'
    
    # Pattern 7: Fix corrupted "Gets or setsGets" patterns
    $content = $content -replace '/// <summary>Gets or setsGets\s+([^<]+)</summary>', '/// <summary>Gets or sets $1.</summary>'
    $content = $content -replace '/// <summary>GetsGets\s+([^<]+)</summary>', '/// <summary>Gets $1.</summary>'
    
    if ($content -ne $originalContent) {
        try {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            $totalFiles++
            Write-Host "Fixed: $($file.FullName)" -ForegroundColor Yellow
        }
        catch {
            Write-Warning "Failed to update $($file.FullName): $($_.Exception.Message)"
        }
    }
}

Write-Host "SA1623 comprehensive fix complete. Files updated: $totalFiles" -ForegroundColor Green