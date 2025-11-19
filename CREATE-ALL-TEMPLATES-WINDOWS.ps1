# ============================================================================
# COMPLETE TEMPLATE SETUP SCRIPT FOR WINDOWS
# ============================================================================
# Copy this entire file to: C:\Projects\EnterpriseDocumentationPlatform.V2
# Then run: .\CREATE-ALL-TEMPLATES-WINDOWS.ps1
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$templatesDir = Join-Path $projectRoot "Templates"
$npmCmd = Join-Path $projectRoot "node-v24.11.0-win-x64\npm.cmd"
$nodeExe = Join-Path $projectRoot "node-v24.11.0-win-x64\node.exe"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  DOCUMENTATION TEMPLATES SETUP" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Step 1: Create directory
Write-Host "[1/8] Creating Templates directory..." -ForegroundColor Yellow
if (-not (Test-Path $templatesDir)) {
    New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null
}
Write-Host "  Done" -ForegroundColor Green

# Step 2: .gitignore
Write-Host "[2/8] Creating .gitignore..." -ForegroundColor Yellow
@"
node_modules/
package-lock.json
test-*output.docx
*.log
"@ | Out-File -FilePath (Join-Path $templatesDir ".gitignore") -Encoding utf8
Write-Host "  Done" -ForegroundColor Green

# Step 3: package.json  
Write-Host "[3/8] Creating package.json..." -ForegroundColor Yellow
@"
{
  "name": "documentation-templates",
  "version": "1.0.0",
  "description": "Node.js templates for generating Word documentation",
  "dependencies": {
    "docx": "^8.5.0"
  }
}
"@ | Out-File -FilePath (Join-Path $templatesDir "package.json") -Encoding utf8
Write-Host "  Done" -ForegroundColor Green

# Step 4: test-data.json
Write-Host "[4/8] Creating test-data.json..." -ForegroundColor Yellow
@"
{
  "schema": "dbo",
  "procedureName": "Transactions",
  "author": "DocGenerator",
  "ticket": "ROW001",
  "created": "11/17/2025",
  "type": "SystemDoc",
  "purpose": "The dbo.Transactions stored procedure manages transaction data processing.",
  "parameters": [{"name": "@TransactionID", "type": "int", "description": "Transaction ID"}, {"name": "@UserID", "type": "int", "description": "User ID"}],
  "returnValue": "None",
  "output": "Inserts into dbo.TransactionLog",
  "executionLogic": ["Step 1: Validate parameters", "Step 2: Begin transaction", "Step 3: Insert record", "Step 4: Commit and log"],
  "dependencies": {"sourceTables": ["dbo.Users"], "targetTables": ["dbo.TransactionLog"], "procedures": ["dbo.uspLogEvent"]},
  "usageExamples": [{"title": "Example 1", "code": "EXEC dbo.Transactions @TransactionID = 123, @UserID = 456"}],
  "changeHistory": [{"date": "11/17/2025", "author": "DocGenerator", "ticket": "ROW001", "description": "Initial"}]
}
"@ | Out-File -FilePath (Join-Path $templatesDir "test-data.json") -Encoding utf8
Write-Host "  Done" -ForegroundColor Green

Write-Host "[5/8] Creating TEMPLATE_Tier1_Comprehensive.js (17KB)..." -ForegroundColor Yellow
$tier1Path = Join-Path $templatesDir "TEMPLATE_Tier1_Comprehensive.js"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier1_Comprehensive.js" -OutFile $tier1Path -UseBasicParsing
Write-Host "  Done" -ForegroundColor Green

Write-Host "[6/8] Creating TEMPLATE_Tier2_Standard.js (12KB)..." -ForegroundColor Yellow
$tier2Path = Join-Path $templatesDir "TEMPLATE_Tier2_Standard.js"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier2_Standard.js" -OutFile $tier2Path -UseBasicParsing
Write-Host "  Done" -ForegroundColor Green

Write-Host "[7/8] Creating TEMPLATE_Tier3_Lightweight.js (11KB)..." -ForegroundColor Yellow
$tier3Path = Join-Path $templatesDir "TEMPLATE_Tier3_Lightweight.js"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier3_Lightweight.js" -OutFile $tier3Path -UseBasicParsing
Write-Host "  Done" -ForegroundColor Green

# Step 8: Install dependencies
Write-Host "[8/8] Installing npm dependencies..." -ForegroundColor Yellow
Push-Location $templatesDir
& $npmCmd install 2>&1 | Out-Null
Pop-Location
Write-Host "  Done" -ForegroundColor Green

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  RUNNING TEST" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

Push-Location $templatesDir
& $nodeExe "TEMPLATE_Tier2_Standard.js" "test-data.json" "test-output.docx" 2>&1 | ForEach-Object { Write-Host "  $_" }
if (Test-Path "test-output.docx") {
    $size = (Get-Item "test-output.docx").Length
    Write-Host ""
    Write-Host "  SUCCESS! Created test-output.docx ($size bytes)" -ForegroundColor Green
    Remove-Item "test-output.docx"
} else {
    Write-Host "  FAILED" -ForegroundColor Red
}
Pop-Location

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  COMPLETE!" -ForegroundColor Green  
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Templates created in: $templatesDir" -ForegroundColor White
Write-Host "Now run your document generation service!" -ForegroundColor Yellow
Write-Host ""
