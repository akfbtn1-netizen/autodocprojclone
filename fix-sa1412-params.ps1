# SA1412 Store Parameters on Separate Lines Fix
# Ensures constructor parameters are properly formatted on separate lines

$ErrorActionPreference = "SilentlyContinue"
$files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

Write-Host "Starting SA1412 parameter formatting fix..." -ForegroundColor Green

$totalFiles = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Pattern 1: Fix constructor parameters on same line
    $content = $content -replace '(\w+\([^)]*,\s*)(\w+\s+\w+[^)]*)\)', {
        param($match)
        $fullMatch = $match.Value
        # Only fix if parameters are clearly on same line with comma
        if ($fullMatch -match '(\w+)\s*\(\s*([^,)]+),\s*([^,)]+)\s*\)') {
            $method = $matches[1]
            $param1 = $matches[2].Trim()
            $param2 = $matches[3].Trim()
            "$method($param1,`n        $param2)"
        } else {
            $fullMatch
        }
    }
    
    # Pattern 2: Fix method parameters that should be on separate lines
    $content = $content -replace '(public\s+\w+\s+\w+\s*\()([^)]+,\s*[^)]+)(\))', {
        param($match)
        $prefix = $match.Groups[1].Value
        $params = $match.Groups[2].Value
        $suffix = $match.Groups[3].Value
        
        # Split parameters and put each on new line
        $paramList = $params -split ',\s*'
        if ($paramList.Length -gt 1) {
            $formattedParams = $paramList | ForEach-Object { "        $($_.Trim())" }
            "$prefix`n$($formattedParams -join ",`n")$suffix"
        } else {
            $match.Value
        }
    }
    
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

Write-Host "SA1412 parameter formatting fix complete. Files updated: $totalFiles" -ForegroundColor Green