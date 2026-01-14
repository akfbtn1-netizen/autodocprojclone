# Systematic Quality Fixes Script
# Fixes the most common patterns across all contract files

$contractFiles = Get-ChildItem -Path "C:\Projects\EnterpriseDocumentationPlatform.V2\src\Shared\Contracts" -Recurse -Filter "*.cs"

foreach ($file in $contractFiles) {
    Write-Host "Fixing $($file.Name)..."
    
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Fix SA1629: Add periods to documentation
    $content = $content -replace '(\s*/// <param name="[^"]+">)([^<]+)(?<!\.)(</param>)', '$1$2.$3'
    $content = $content -replace '(\s*/// <returns>)([^<]+)(?<!\.)(</returns>)', '$1$2.$3'
    $content = $content -replace '(\s*/// <summary>)([^<]+)(?<!\.)(</summary>)', '$1$2.$3'
    $content = $content -replace '(\s*/// <typeparam name="[^"]+">)([^<]+)(?<!\.)(</typeparam>)', '$1$2.$3'
    
    # Fix SA1623: Property documentation format
    $content = $content -replace '(\s*/// <summary>)([^<]*?)(ID|Id|Name|Type|Status|Version|Count|Size|Key|Value|Data|Info|Code|Error|Result|Message)([^<]*?)(</summary>)', '$1Gets $2$3$4.$5'
    $content = $content -replace '(\s*/// <summary>)([^<]*?)(Whether|If)([^<]*?)(</summary>)', '$1Gets a value indicating whether$2$3$4.$5'
    $content = $content -replace '(\s*/// <summary>)(List|Collection|Array)([^<]*?)(</summary>)', '$1Gets $2$3.$4'
    
    # Fix SA1028: Remove trailing whitespace
    $lines = $content -split "`r?`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lines[$i] = $lines[$i].TrimEnd()
    }
    $content = $lines -join "`n"
    
    # Fix SA1518: Ensure single newline at end
    $content = $content.TrimEnd() + "`n"
    
    # Only write if content changed
    if ($content -ne $originalContent) {
        Set-Content $file.FullName -Value $content -NoNewline
        Write-Host "  - Fixed $($file.Name)"
    }
}

Write-Host "Batch fixes completed!"