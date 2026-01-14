# Fix copyright headers for Shared.Contracts project
$copyright = @"
// <copyright file="FILENAME" company="Enterprise Documentation Platform">
// Copyright (c) Enterprise Documentation Platform. All rights reserved.
// </copyright>

"@

Get-ChildItem -Path "src\Shared\Contracts" -Filter "*.cs" -Recurse | ForEach-Object {
    $content = Get-Content -Path $_.FullName -Raw
    if ($content -notmatch "Copyright \(c\) Enterprise Documentation Platform") {
        $filename = $_.Name
        $header = $copyright -replace "FILENAME", $filename
        $newContent = $header + $content
        Set-Content -Path $_.FullName -Value $newContent -NoNewline
        Write-Host "Added copyright to: $($_.FullName)"
    }
}