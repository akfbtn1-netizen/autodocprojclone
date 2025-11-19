# ============================================================================
# SETUP SCRIPT FOR DOCUMENTATION TEMPLATES
# ============================================================================
# Run from: C:\Projects\EnterpriseDocumentationPlatform.V2
# ============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$templatesDir = Join-Path $projectRoot "Templates"
$npmCmd = Join-Path $projectRoot "node-v24.11.0-win-x64\npm.cmd"
$nodeExe = Join-Path $projectRoot "node-v24.11.0-win-x64\node.exe"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  SETTING UP DOCUMENTATION TEMPLATES" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/7] Creating Templates directory..." -ForegroundColor Yellow
if (-not (Test-Path $templatesDir)) {
    New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null
}
Write-Host "  Done" -ForegroundColor Green

Write-Host "[2/7] Creating package.json..." -ForegroundColor Yellow
$packageJson = @'
{
  "name": "documentation-templates",
  "version": "1.0.0",
  "dependencies": {
    "docx": "^8.5.0"
  }
}
'@
$packageJson | Out-File -FilePath (Join-Path $templatesDir "package.json") -Encoding utf8
Write-Host "  Done" -ForegroundColor Green

Write-Host "[3/7] Creating .gitignore..." -ForegroundColor Yellow
$gitignore = @'
node_modules/
package-lock.json
test-*
*.log
'@
$gitignore | Out-File -FilePath (Join-Path $templatesDir ".gitignore") -Encoding utf8
Write-Host "  Done" -ForegroundColor Green

Write-Host "[4/7] Downloading TEMPLATE_Tier1_Comprehensive.js..." -ForegroundColor Yellow
$url1 = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier1_Comprehensive.js"
Invoke-WebRequest -Uri $url1 -OutFile (Join-Path $templatesDir "TEMPLATE_Tier1_Comprehensive.js") -UseBasicParsing
Write-Host "  Done (17 KB)" -ForegroundColor Green

Write-Host "[5/7] Downloading TEMPLATE_Tier2_Standard.js..." -ForegroundColor Yellow
$url2 = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier2_Standard.js"
Invoke-WebRequest -Uri $url2 -OutFile (Join-Path $templatesDir "TEMPLATE_Tier2_Standard.js") -UseBasicParsing
Write-Host "  Done (12 KB)" -ForegroundColor Green

Write-Host "[6/7] Downloading TEMPLATE_Tier3_Lightweight.js..." -ForegroundColor Yellow
$url3 = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier3_Lightweight.js"
Invoke-WebRequest -Uri $url3 -OutFile (Join-Path $templatesDir "TEMPLATE_Tier3_Lightweight.js") -UseBasicParsing
Write-Host "  Done (11 KB)" -ForegroundColor Green

Write-Host "[7/7] Installing npm dependencies..." -ForegroundColor Yellow
Push-Location $templatesDir
& $npmCmd install
Pop-Location
Write-Host "  Done" -ForegroundColor Green

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  SETUP COMPLETE!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Templates created in:" -ForegroundColor White
Write-Host "  $templatesDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Files created:" -ForegroundColor White
Write-Host "  - TEMPLATE_Tier1_Comprehensive.js" -ForegroundColor Green
Write-Host "  - TEMPLATE_Tier2_Standard.js" -ForegroundColor Green
Write-Host "  - TEMPLATE_Tier3_Lightweight.js" -ForegroundColor Green
Write-Host "  - package.json + node_modules/" -ForegroundColor Green
Write-Host ""
Write-Host "Next: Run your document generation service!" -ForegroundColor Yellow
Write-Host ""
