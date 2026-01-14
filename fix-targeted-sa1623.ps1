# Targeted SA1623 Fix for Remaining Property Documentation Issues
# Handles specific patterns that previous scripts missed

$ErrorActionPreference = "SilentlyContinue"
$files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

Write-Host "Starting targeted SA1623 property documentation fix..." -ForegroundColor Green

$totalFiles = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Pattern 1: Properties that should start with "Gets or sets" but don't
    $content = $content -replace '/// <summary>([A-Z][^<]*?\.)\s*</summary>\s*(public\s+[^{]*\{\s*get[^}]*set)', '/// <summary>Gets or sets $1</summary>`n$2'
    
    # Pattern 2: Properties that should start with "Gets" (readonly) but don't
    $content = $content -replace '/// <summary>([A-Z][^<]*?\.)\s*</summary>\s*(public\s+[^{]*\{\s*get[^}]*\})', '/// <summary>Gets $1</summary>`n$2'
    
    # Pattern 3: Properties starting with lowercase
    $content = $content -replace '/// <summary>Gets or sets ([a-z][^<]*?)</summary>', {
        param($match)
        $text = $match.Groups[1].Value
        $capitalized = $text.Substring(0,1).ToUpper() + $text.Substring(1)
        "/// <summary>Gets or sets $capitalized</summary>"
    }
    
    # Pattern 4: Properties starting with lowercase (readonly)
    $content = $content -replace '/// <summary>Gets ([a-z][^<]*?)</summary>', {
        param($match)
        $text = $match.Groups[1].Value
        $capitalized = $text.Substring(0,1).ToUpper() + $text.Substring(1)
        "/// <summary>Gets $capitalized</summary>"
    }
    
    # Pattern 5: Fix descriptions without proper prefixes
    $content = $content -replace '/// <summary>(The |A |An )?([a-z][^<]*?\.)\s*</summary>\s*(public\s+[^{]*\{\s*get[^}]*set)', '/// <summary>Gets or sets the $2</summary>`n$3'
    $content = $content -replace '/// <summary>(The |A |An )?([a-z][^<]*?\.)\s*</summary>\s*(public\s+[^{]*\{\s*get[^}]*\})', '/// <summary>Gets the $2</summary>`n$3'
    
    if ($content -ne $originalContent) {
        try {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            $totalFiles++
            Write-Host "Fixed targeted SA1623 in: $($file.Name)" -ForegroundColor Yellow
        }
        catch {
            Write-Warning "Failed to update $($file.FullName): $($_.Exception.Message)"
        }
    }
}

Write-Host "Targeted SA1623 property documentation fix complete. Files updated: $totalFiles" -ForegroundColor Green