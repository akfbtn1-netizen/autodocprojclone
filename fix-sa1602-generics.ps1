# SA1602 Generic Type Parameter Documentation Fix
# Adds missing documentation for generic type parameters

$ErrorActionPreference = "SilentlyContinue"
$files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

Write-Host "Starting SA1602 generic type parameter documentation fix..." -ForegroundColor Green

$totalFiles = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Pattern: Add missing typeparam documentation for common generic parameters
    # Handle interfaces and classes with generic parameters
    
    # Fix <T> parameters
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*)((?:public\s+)?(?:interface|class|struct)\s+\w+<T[^>]*>)', {
        param($match)
        $summary = $match.Groups[1].Value
        $declaration = $match.Groups[2].Value
        
        # Check if typeparam already exists
        if ($summary -notmatch 'typeparam') {
            "$summary/// <typeparam name=`"T`">The entity type.</typeparam>`n$declaration"
        } else {
            $match.Value
        }
    }
    
    # Fix <TEntity> parameters
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*)((?:public\s+)?(?:interface|class|struct)\s+\w+<TEntity[^>]*>)', {
        param($match)
        $summary = $match.Groups[1].Value
        $declaration = $match.Groups[2].Value
        
        if ($summary -notmatch 'typeparam.*TEntity') {
            "$summary/// <typeparam name=`"TEntity`">The entity type.</typeparam>`n$declaration"
        } else {
            $match.Value
        }
    }
    
    # Fix <TKey> parameters
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*)((?:public\s+)?(?:interface|class|struct)\s+\w+<[^>]*TKey[^>]*>)', {
        param($match)
        $summary = $match.Groups[1].Value
        $declaration = $match.Groups[2].Value
        
        if ($summary -notmatch 'typeparam.*TKey') {
            "$summary/// <typeparam name=`"TKey`">The key type.</typeparam>`n$declaration"
        } else {
            $match.Value
        }
    }
    
    # Fix <TRequest> and <TResponse> parameters
    $content = $content -replace '(/// <summary>[\s\S]*?</summary>\s*)((?:public\s+)?(?:interface|class|struct)\s+\w+<TRequest,\s*TResponse>)', {
        param($match)
        $summary = $match.Groups[1].Value
        $declaration = $match.Groups[2].Value
        
        $newSummary = $summary
        if ($summary -notmatch 'typeparam.*TRequest') {
            $newSummary += "/// <typeparam name=`"TRequest`">The request type.</typeparam>`n"
        }
        if ($summary -notmatch 'typeparam.*TResponse') {
            $newSummary += "/// <typeparam name=`"TResponse`">The response type.</typeparam>`n"
        }
        "$newSummary$declaration"
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

Write-Host "SA1602 generic type parameter fix complete. Files updated: $totalFiles" -ForegroundColor Green