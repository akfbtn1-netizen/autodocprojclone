# Enhanced SA1623 Fix for Corrupted Documentation Patterns
# Handles complex corrupted documentation that the previous script missed

$ErrorActionPreference = "SilentlyContinue"

# Get specific files that have SA1623 violations
$violations = dotnet build --no-restore 2>&1 | Select-String "SA1623" | ForEach-Object {
    if ($_ -match '^([^(]+)\([^)]+\):') {
        $matches[1]
    }
} | Sort-Object -Unique

Write-Host "Found SA1623 violations in $($violations.Count) files" -ForegroundColor Green

foreach ($filePath in $violations) {
    if (-not (Test-Path $filePath)) { continue }
    
    $content = Get-Content $filePath -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Fix corrupted patterns that weren't caught by previous script
    
    # Pattern 1: Fix "Gets /// " patterns (corrupted Gets)
    $content = $content -replace '/// <summary>Gets ///([^<]*)</summary>', '/// <summary>Gets or sets$1</summary>'
    
    # Pattern 2: Fix "Gets a value indicating whetherGets" patterns
    $content = $content -replace '/// <summary>Gets a value indicating whetherGets([^<]*)</summary>', '/// <summary>Gets or sets$1</summary>'
    
    # Pattern 3: Fix properties that don't start with Gets/Gets or sets
    $content = $content -replace '/// <summary>\s*([A-Z][^<]*?)</summary>\s*(public\s+[^{]*\{\s*get)', {
        param($match)
        $description = $match.Groups[1].Value.Trim()
        $property = $match.Groups[2].Value
        
        # Check if it has a setter
        if ($property -match '\{\s*get[^}]*set') {
            "/// <summary>Gets or sets $($description.ToLower()).</summary>`n$property"
        } else {
            "/// <summary>Gets $($description.ToLower()).</summary>`n$property"
        }
    }
    
    # Pattern 4: Fix missing periods and double periods
    $content = $content -replace '/// <summary>(Gets or sets [^<]+[^.])</summary>', '/// <summary>$1.</summary>'
    $content = $content -replace '/// <summary>(Gets [^<]+[^.])</summary>', '/// <summary>$1.</summary>'
    $content = $content -replace '/// <summary>([^<]+)\.\.</summary>', '/// <summary>$1.</summary>'
    
    # Pattern 5: Fix multi-line summaries that got corrupted
    $content = $content -replace '/// <summary>([^<]*?)\n\s*///\s*([^<]*?)\n\s*///\s*([^<]*?)</summary>', {
        param($match)
        $line1 = $match.Groups[1].Value.Trim()
        $line2 = $match.Groups[2].Value.Trim()
        $line3 = $match.Groups[3].Value.Trim()
        
        # Combine and clean up
        $combined = "$line1 $line2 $line3".Trim()
        $combined = $combined -replace '\s+', ' '
        $combined = $combined -replace '\.+$', '.'
        
        if ($combined -notmatch '^Gets') {
            $combined = "Gets or sets $($combined.ToLower())"
        }
        
        "/// <summary>$combined</summary>"
    }
    
    if ($content -ne $originalContent) {
        try {
            Set-Content -Path $filePath -Value $content -Encoding UTF8 -NoNewline
            Write-Host "Fixed SA1623 violations in: $filePath" -ForegroundColor Yellow
        }
        catch {
            Write-Warning "Failed to update $filePath`: $($_.Exception.Message)"
        }
    }
}

Write-Host "Enhanced SA1623 fix complete." -ForegroundColor Green