# ============================================================================
# COMPLETE SETUP - All 5 Documentation Templates
# ============================================================================
# This script downloads and sets up ALL template files:
#   - TEMPLATE_Tier1_Comprehensive.js (Stored Procedures - Complex)
#   - TEMPLATE_Tier2_Standard.js (Stored Procedures - Standard)
#   - TEMPLATE_Tier3_Lightweight.js (Stored Procedures - Simple)
#   - TEMPLATE_DefectFix.js (Defect/Bug Fixes)
#   - TEMPLATE_BusinessRequest.js (New Business Requests)
# ============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$templatesDir = Join-Path $projectRoot "Templates"
$npmCmd = Join-Path $projectRoot "node-v24.11.0-win-x64\npm.cmd"
$nodeExe = Join-Path $projectRoot "node-v24.11.0-win-x64\node.exe"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  COMPLETE TEMPLATE SETUP - ALL 5 TEMPLATES" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Step 1: Create directory
Write-Host "[1/10] Creating Templates directory..." -ForegroundColor Yellow
if (-not (Test-Path $templatesDir)) {
    New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null
}
Write-Host "  Done" -ForegroundColor Green

# Step 2: package.json
Write-Host "[2/10] Creating package.json..." -ForegroundColor Yellow
$packageJson = @'
{
  "name": "documentation-templates",
  "version": "1.0.0",
  "description": "Documentation templates for stored procedures, defects, and business requests",
  "dependencies": {
    "docx": "^8.5.0"
  }
}
'@
$packageJson | Out-File -FilePath (Join-Path $templatesDir "package.json") -Encoding utf8
Write-Host "  Done" -ForegroundColor Green

# Step 3: .gitignore
Write-Host "[3/10] Creating .gitignore..." -ForegroundColor Yellow
$gitignore = @'
node_modules/
package-lock.json
test-*
debug-*
*.log
'@
$gitignore | Out-File -FilePath (Join-Path $templatesDir ".gitignore") -Encoding utf8
Write-Host "  Done" -ForegroundColor Green

# Base URL for downloads
$baseUrl = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates"

# Step 4: Download Tier1
Write-Host "[4/10] Downloading TEMPLATE_Tier1_Comprehensive.js..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_Tier1_Comprehensive.js" -OutFile (Join-Path $templatesDir "TEMPLATE_Tier1_Comprehensive.js") -UseBasicParsing
    Write-Host "  Done (17 KB)" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $_" -ForegroundColor Red
}

# Step 5: Download Tier2
Write-Host "[5/10] Downloading TEMPLATE_Tier2_Standard.js..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_Tier2_Standard.js" -OutFile (Join-Path $templatesDir "TEMPLATE_Tier2_Standard.js") -UseBasicParsing
    Write-Host "  Done (12 KB)" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $_" -ForegroundColor Red
}

# Step 6: Download Tier3
Write-Host "[6/10] Downloading TEMPLATE_Tier3_Lightweight.js..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_Tier3_Lightweight.js" -OutFile (Join-Path $templatesDir "TEMPLATE_Tier3_Lightweight.js") -UseBasicParsing
    Write-Host "  Done (11 KB)" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $_" -ForegroundColor Red
}

# Step 7: Download DefectFix
Write-Host "[7/10] Downloading TEMPLATE_DefectFix.js..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_DefectFix.js" -OutFile (Join-Path $templatesDir "TEMPLATE_DefectFix.js") -UseBasicParsing
    Write-Host "  Done (12 KB)" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $_" -ForegroundColor Red
}

# Step 8: Download BusinessRequest
Write-Host "[8/10] Downloading TEMPLATE_BusinessRequest.js..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_BusinessRequest.js" -OutFile (Join-Path $templatesDir "TEMPLATE_BusinessRequest.js") -UseBasicParsing
    Write-Host "  Done (11 KB)" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $_" -ForegroundColor Red
}

# Step 9: Install dependencies
Write-Host "[9/10] Installing npm dependencies..." -ForegroundColor Yellow
Push-Location $templatesDir
try {
    & $npmCmd install 2>&1 | Out-Null
    Write-Host "  Done" -ForegroundColor Green
} catch {
    Write-Host "  Failed: $_" -ForegroundColor Red
} finally {
    Pop-Location
}

# Step 10: Verify installation
Write-Host "[10/10] Verifying installation..." -ForegroundColor Yellow
$templates = @(
    "TEMPLATE_Tier1_Comprehensive.js",
    "TEMPLATE_Tier2_Standard.js",
    "TEMPLATE_Tier3_Lightweight.js",
    "TEMPLATE_DefectFix.js",
    "TEMPLATE_BusinessRequest.js"
)

$allFound = $true
foreach ($template in $templates) {
    $path = Join-Path $templatesDir $template
    if (Test-Path $path) {
        $size = (Get-Item $path).Length
        Write-Host "  Found: $template ($size bytes)" -ForegroundColor Green
    } else {
        Write-Host "  Missing: $template" -ForegroundColor Red
        $allFound = $false
    }
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  SETUP COMPLETE!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Templates installed in:" -ForegroundColor White
Write-Host "  $templatesDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Templates available:" -ForegroundColor White
Write-Host "  1. TEMPLATE_Tier1_Comprehensive.js - Complex stored procedures" -ForegroundColor Gray
Write-Host "  2. TEMPLATE_Tier2_Standard.js - Standard stored procedures" -ForegroundColor Gray
Write-Host "  3. TEMPLATE_Tier3_Lightweight.js - Simple stored procedures" -ForegroundColor Gray
Write-Host "  4. TEMPLATE_DefectFix.js - Defect/bug fix documentation" -ForegroundColor Gray
Write-Host "  5. TEMPLATE_BusinessRequest.js - Business request documentation" -ForegroundColor Gray
Write-Host ""

if ($allFound) {
    Write-Host "All templates installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next step: Run DEBUG-TEMPLATE-TEST.ps1 to verify templates work" -ForegroundColor Yellow
} else {
    Write-Host "WARNING: Some templates are missing!" -ForegroundColor Red
    Write-Host "Please check your internet connection and try again." -ForegroundColor Yellow
}

Write-Host ""
