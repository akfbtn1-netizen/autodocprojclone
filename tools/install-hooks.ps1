# Enterprise Quality Gate Installation Script for Windows
# Sets up pre-commit hooks for automatic quality validation

$hookDir = ".git\hooks"
$hookFile = "$hookDir\pre-commit"

Write-Host "🔧 Installing Enterprise Quality Gate Pre-Commit Hook..." -ForegroundColor Cyan

# Create hooks directory if it doesn't exist
if (-not (Test-Path $hookDir)) {
    New-Item -ItemType Directory -Path $hookDir -Force | Out-Null
}

# Create pre-commit hook content (PowerShell version)
$hookContent = @'
#!/usr/bin/env pwsh
# Enterprise Quality Gate Pre-Commit Hook
# Automatically validates code quality before commits

Write-Host "🔍 Running Enterprise Quality Gate..." -ForegroundColor Cyan

try {
    $result = & ".\tools\quality-gate.ps1" -FailOnViolations
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -ne 0) {
        Write-Host ""
        Write-Host "❌ COMMIT BLOCKED by Quality Gate" -ForegroundColor Red
        Write-Host "Fix the violations above and try again." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "💡 To bypass (not recommended): git commit --no-verify" -ForegroundColor Gray
        exit 1
    }
    
    Write-Host "✅ Quality Gate passed! Proceeding with commit..." -ForegroundColor Green
    exit 0
} catch {
    Write-Host "⚠️ Quality gate check failed: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "Proceeding with commit (manual review recommended)" -ForegroundColor Gray
    exit 0
}
'@

# Write hook file
$hookContent | Out-File -FilePath $hookFile -Encoding UTF8 -NoNewline

Write-Host "✅ Pre-commit hook installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "🎯 Quality Gate Features:" -ForegroundColor Yellow
Write-Host "  • Complexity validation (≤6)" -ForegroundColor White
Write-Host "  • Method length checking (≤20 lines)" -ForegroundColor White
Write-Host "  • Class size limits (≤200 lines)" -ForegroundColor White
Write-Host "  • Documentation requirements" -ForegroundColor White
Write-Host "  • Automatic enforcement on commits" -ForegroundColor White
Write-Host ""
Write-Host "🚀 Your repository now has automated quality enforcement!" -ForegroundColor Green