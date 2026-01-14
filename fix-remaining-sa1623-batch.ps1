# Enterprise Quality Enforcement - SA1623 Property Documentation Batch Fix
# Focus on remaining main projects (excluding Shared.Contracts which is deferred)

Write-Host "Starting SA1623 property documentation fixes for main projects..." -ForegroundColor Green

# Build first to get accurate violation count
Write-Host "Building to identify violations..." -ForegroundColor Yellow
$buildResult = dotnet build EnterpriseDocumentationPlatform.sln -v quiet 2>&1

# Extract SA1623 violations for main projects (exclude Shared.Contracts)
$sa1623Violations = $buildResult | Select-String "error SA1623" | Where-Object { 
    $_.Line -notmatch "Shared.Contracts" 
}

Write-Host "Found $($sa1623Violations.Count) SA1623 violations in main projects" -ForegroundColor Yellow

# Pattern to fix different property documentation types
$propertyDocFixes = @{
    # Basic Gets or sets
    "(?<=summary>)[^<]*(?=</summary>)" = {
        param($match, $line)
        
        if ($line -match "Gets or sets a value indicating whether") {
            return $match.Value
        }
        if ($line -match "property.*bool.*\{.*get.*set" -or $line -match "bool\s+\w+\s*\{") {
            return "Gets or sets a value indicating whether"
        }
        if ($line -match "Gets" -and $line -notmatch "Gets or sets") {
            return "Gets"
        }
        return "Gets or sets"
    }
}

# Process each violation and apply targeted fix
foreach ($violation in $sa1623Violations) {
    if ($violation.Line -match "^(.+):(\d+):(\d+): error SA1623: (.+)$") {
        $filePath = $matches[1]
        $lineNumber = [int]$matches[2]
        
        Write-Host "Fixing SA1623 in: $filePath at line $lineNumber" -ForegroundColor Cyan
        
        try {
            $lines = Get-Content $filePath -Encoding UTF8
            
            if ($lineNumber -gt 0 -and $lineNumber -le $lines.Count) {
                $targetLine = $lines[$lineNumber - 1]
                
                # Find the property documentation comment above
                for ($i = $lineNumber - 2; $i -ge 0; $i--) {
                    if ($lines[$i] -match "/// <summary>(.+)</summary>") {
                        $currentDoc = $matches[1].Trim()
                        $propertyLine = $targetLine
                        
                        # Determine correct documentation based on property type and context
                        $newDoc = if ($propertyLine -match "bool\s+\w+.*\{.*get.*set" -or 
                                     ($propertyLine -match "bool" -and $currentDoc -notmatch "Gets or sets a value indicating whether")) {
                            "Gets or sets a value indicating whether"
                        }
                        elseif ($propertyLine -match "\{.*get;.*\}" -and $propertyLine -notmatch "set") {
                            "Gets"
                        }
                        else {
                            "Gets or sets"
                        }
                        
                        # Apply the fix if different
                        if ($currentDoc -ne $newDoc) {
                            $lines[$i] = $lines[$i] -replace "/// <summary>.+</summary>", "/// <summary>$newDoc</summary>"
                            Write-Host "  Updated: '$currentDoc' -> '$newDoc'" -ForegroundColor Green
                        }
                        break
                    }
                }
                
                # Write back to file
                $lines | Set-Content $filePath -Encoding UTF8
            }
        }
        catch {
            Write-Warning "Failed to process $filePath : $($_.Exception.Message)"
        }
    }
}

# Verify the fix
Write-Host "`nVerifying fixes..." -ForegroundColor Yellow
$postBuildResult = dotnet build EnterpriseDocumentationPlatform.sln -v quiet 2>&1
$remainingSA1623 = ($postBuildResult | Select-String "error SA1623" | Where-Object { $_.Line -notmatch "Shared.Contracts" }).Count
$totalRemaining = ($postBuildResult | Select-String "error" | Measure-Object).Count

Write-Host "SA1623 violations remaining in main projects: $remainingSA1623" -ForegroundColor $(if ($remainingSA1623 -eq 0) { "Green" } else { "Yellow" })
Write-Host "Total violations remaining: $totalRemaining" -ForegroundColor Yellow
Write-Host "Enterprise Quality Fix completed!" -ForegroundColor Green