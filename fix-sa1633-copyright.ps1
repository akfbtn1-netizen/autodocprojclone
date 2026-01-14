# Fix SA1633: Add missing copyright headers to all C# files
# Adds standard enterprise copyright header

$ErrorActionPreference = "SilentlyContinue"
$files = Get-ChildItem -Path "src" -Filter "*.cs" -Recurse

Write-Host "Starting SA1633 copyright header fix..." -ForegroundColor Green

$copyrightHeader = @"
// <copyright file="{0}" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// This software is proprietary and confidential.
// </copyright>

"@

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    if (-not $content) { continue }
    
    # Skip if already has copyright
    if ($content -match '<copyright') { continue }
    
    $fileName = $file.Name
    $header = $copyrightHeader -f $fileName
    
    # Add header at the beginning
    $newContent = $header + $content
    
    try {
        Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8 -NoNewline
        Write-Host "Added copyright to: $fileName" -ForegroundColor Yellow
    }
    catch {
        Write-Warning "Failed to update $($file.FullName): $($_.Exception.Message)"
    }
}

Write-Host "SA1633 copyright header fix completed." -ForegroundColor Green