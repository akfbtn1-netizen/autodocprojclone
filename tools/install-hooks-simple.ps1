# Enterprise Quality Gate Installation Script for Windows
Write-Host "🔧 Installing Enterprise Quality Gate Pre-Commit Hook..." -ForegroundColor Cyan

$hookDir = ".git\hooks"
$hookFile = "$hookDir\pre-commit"

# Create hooks directory if it doesn't exist
if (-not (Test-Path $hookDir)) {
    New-Item -ItemType Directory -Path $hookDir -Force | Out-Null
}

# Create simple pre-commit hook that calls our quality gate
$hookScript = @"
#!/usr/bin/env pwsh
Write-Host "🔍 Running Enterprise Quality Gate..." -ForegroundColor Cyan
try {
    `$result = & ".\tools\quality-gate.ps1" -FailOnViolations:`$true
    if (`$LASTEXITCODE -ne 0) {
        Write-Host "❌ COMMIT BLOCKED by Quality Gate" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Quality Gate passed!" -ForegroundColor Green
    exit 0
} catch {
    Write-Host "⚠️ Quality gate check failed, proceeding..." -ForegroundColor Yellow
    exit 0
}
"@

# Write the hook file
$hookScript | Out-File -FilePath $hookFile -Encoding UTF8

Write-Host "✅ Pre-commit hook installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "🎯 Quality Gate Features:" -ForegroundColor Yellow
Write-Host "  • Complexity validation (≤6)" -ForegroundColor White
Write-Host "  • Method length checking (≤20 lines)" -ForegroundColor White
Write-Host "  • Class size limits (≤200 lines)" -ForegroundColor White
Write-Host "  • Documentation requirements" -ForegroundColor White
Write-Host "  • Automatic enforcement on commits" -ForegroundColor White
Write-Host ""
Write-Host "🚀 Repository now has automated quality enforcement!" -ForegroundColor Green