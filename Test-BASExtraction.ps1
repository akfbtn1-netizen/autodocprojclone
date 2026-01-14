# Test BAS marker extraction directly

# First check what's actually in the stored procedure
$sp = sqlcmd -S "ibidb2003dv" -d "IRFS1" -E -Q "SELECT OBJECT_DEFINITION(OBJECT_ID('DaQa.usp_VerifyBAS'))" -h -1 -W
Write-Host "Stored Procedure Content:" -ForegroundColor Green
Write-Host $sp

# Now let's manually test the BAS regex patterns
$beginPattern = "--\s*Begin\s+BAS-?\d{3,4}"
$endPattern = "--\s*End\s+BAS-?\d{3,4}"

Write-Host "`n`nTesting BAS patterns:" -ForegroundColor Green
Write-Host "Begin Pattern: $beginPattern"
Write-Host "End Pattern: $endPattern"

# Check if patterns match
if ($sp -match $beginPattern) {
    Write-Host "✓ BEGIN pattern matches!" -ForegroundColor Green
    $matches = [regex]::Matches($sp, $beginPattern)
    Write-Host "Found $($matches.Count) BEGIN matches"
} else {
    Write-Host "✗ BEGIN pattern does NOT match" -ForegroundColor Red
}

if ($sp -match $endPattern) {
    Write-Host "✓ END pattern matches!" -ForegroundColor Green
    $matches = [regex]::Matches($sp, $endPattern)
    Write-Host "Found $($matches.Count) END matches"
} else {
    Write-Host "✗ END pattern does NOT match" -ForegroundColor Red
}

# Extract the actual BAS code
Write-Host "`n`nExtracting BAS code manually:" -ForegroundColor Green
$lines = $sp -split "`n"
$extracting = $false
$extracted = @()

foreach ($line in $lines) {
    if ($line -match $beginPattern) {
        Write-Host "Found BEGIN marker: $line" -ForegroundColor Yellow
        $extracting = $true
        continue
    }
    
    if ($line -match $endPattern) {
        Write-Host "Found END marker: $line" -ForegroundColor Yellow
        $extracting = $false
        break
    }
    
    if ($extracting) {
        $extracted += $line
    }
}

Write-Host "`nExtracted BAS code:"
Write-Host "==================" -ForegroundColor Cyan
$extracted | ForEach-Object { Write-Host $_ }
Write-Host "==================" -ForegroundColor Cyan

if ($extracted.Count -gt 0) {
    Write-Host "`n✅ FIX #1 (BAS marker extraction) WORKS!" -ForegroundColor Green
} else {
    Write-Host "`n❌ FIX #1 (BAS marker extraction) FAILED!" -ForegroundColor Red
}