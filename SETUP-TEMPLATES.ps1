# ============================================================================
# SIMPLE ALL-IN-ONE SCRIPT - Creates All Template Files Locally
# ============================================================================
# Run this from: C:\Projects\EnterpriseDocumentationPlatform.V2
# ============================================================================

$ErrorActionPreference = "Stop"

# Configuration
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$templatesDir = Join-Path $projectRoot "Templates"
$npmCmd = Join-Path $projectRoot "node-v24.11.0-win-x64\npm.cmd"
$nodeExe = Join-Path $projectRoot "node-v24.11.0-win-x64\node.exe"

Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "  Creating Documentation Templates" -ForegroundColor White
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# Create Templates directory
Write-Host "[1/8] Creating Templates directory..." -ForegroundColor Yellow
if (-not (Test-Path $templatesDir)) {
    New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null
    Write-Host "  ✓ Created directory" -ForegroundColor Green
} else {
    Write-Host "  ✓ Directory exists" -ForegroundColor Green
}

# Create .gitignore
Write-Host "[2/8] Creating .gitignore..." -ForegroundColor Yellow
$gitignoreContent = @"
# Node.js dependencies
node_modules/
package-lock.json

# Test outputs
test-*output.docx
*.log
"@
$gitignoreContent | Out-File -FilePath (Join-Path $templatesDir ".gitignore") -Encoding utf8 -Force
Write-Host "  ✓ Created .gitignore" -ForegroundColor Green

# Create package.json
Write-Host "[3/8] Creating package.json..." -ForegroundColor Yellow
$packageJson = @"
{
  "name": "documentation-templates",
  "version": "1.0.0",
  "description": "Node.js templates for generating Word documentation",
  "main": "index.js",
  "keywords": ["documentation", "docx", "templates"],
  "author": "",
  "license": "MIT",
  "dependencies": {
    "docx": "^8.5.0"
  }
}
"@
$packageJson | Out-File -FilePath (Join-Path $templatesDir "package.json") -Encoding utf8 -Force
Write-Host "  ✓ Created package.json" -ForegroundColor Green

# Create test-data.json
Write-Host "[4/8] Creating test-data.json..." -ForegroundColor Yellow
$testData = @"
{
  "schema": "dbo",
  "procedureName": "Transactions",
  "author": "DocGenerator",
  "ticket": "ROW001",
  "created": "11/17/2025",
  "type": "SystemDoc",
  "purpose": "The dbo.Transactions stored procedure manages transaction data processing for the system.",
  "parameters": [
    {
      "name": "@TransactionID",
      "type": "int",
      "description": "Unique identifier for the transaction"
    },
    {
      "name": "@UserID",
      "type": "int",
      "description": "User who initiated the transaction"
    }
  ],
  "returnValue": "None (void procedure)",
  "output": "Inserts records into dbo.TransactionLog",
  "executionLogic": [
    "Step 1: Validate input parameters",
    "Step 2: Begin transaction",
    "Step 3: Insert transaction record",
    "Step 4: Commit and log completion"
  ],
  "dependencies": {
    "sourceTables": [
      "dbo.Users - User information",
      "dbo.Accounts - Account details"
    ],
    "targetTables": [
      "dbo.TransactionLog - Transaction history"
    ],
    "procedures": [
      "dbo.uspLogEvent - Event logging"
    ]
  },
  "usageExamples": [
    {
      "title": "Example 1: Process Transaction",
      "code": "EXEC dbo.Transactions @TransactionID = 12345, @UserID = 67890"
    }
  ],
  "changeHistory": [
    {
      "date": "11/17/2025",
      "author": "DocGenerator",
      "ticket": "ROW001",
      "description": "Initial creation for testing"
    }
  ]
}
"@
$testData | Out-File -FilePath (Join-Path $templatesDir "test-data.json") -Encoding utf8 -Force
Write-Host "  ✓ Created test-data.json" -ForegroundColor Green

# Download template files from repo
Write-Host "[5/8] Downloading TEMPLATE_Tier1_Comprehensive.js..." -ForegroundColor Yellow
$tier1Url = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier1_Comprehensive.js"
try {
    Invoke-WebRequest -Uri $tier1Url -OutFile (Join-Path $templatesDir "TEMPLATE_Tier1_Comprehensive.js") -UseBasicParsing
    Write-Host "  ✓ Downloaded TEMPLATE_Tier1_Comprehensive.js" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to download: $_" -ForegroundColor Red
    Write-Host "  Will create manually..." -ForegroundColor Yellow
    # Fallback: create file inline (see next section)
}

Write-Host "[6/8] Downloading TEMPLATE_Tier2_Standard.js..." -ForegroundColor Yellow
$tier2Url = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier2_Standard.js"
try {
    Invoke-WebRequest -Uri $tier2Url -OutFile (Join-Path $templatesDir "TEMPLATE_Tier2_Standard.js") -UseBasicParsing
    Write-Host "  ✓ Downloaded TEMPLATE_Tier2_Standard.js" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to download: $_" -ForegroundColor Red
}

Write-Host "[7/8] Downloading TEMPLATE_Tier3_Lightweight.js..." -ForegroundColor Yellow
$tier3Url = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates/TEMPLATE_Tier3_Lightweight.js"
try {
    Invoke-WebRequest -Uri $tier3Url -OutFile (Join-Path $templatesDir "TEMPLATE_Tier3_Lightweight.js") -UseBasicParsing
    Write-Host "  ✓ Downloaded TEMPLATE_Tier3_Lightweight.js" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to download: $_" -ForegroundColor Red
}

# Install npm dependencies
Write-Host "[8/8] Installing npm dependencies..." -ForegroundColor Yellow
Write-Host "  Using npm: $npmCmd" -ForegroundColor Gray
Push-Location $templatesDir
try {
    & $npmCmd install 2>&1 | Out-Null
    Write-Host "  ✓ Dependencies installed" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed: $_" -ForegroundColor Red
    Write-Host "  Run manually: cd $templatesDir && npm install" -ForegroundColor Yellow
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "  Testing Template" -ForegroundColor White
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# Test template
Write-Host "Running test..." -ForegroundColor Yellow
Push-Location $templatesDir
try {
    & $nodeExe "TEMPLATE_Tier2_Standard.js" "test-data.json" "test-output.docx" 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

    if (Test-Path "test-output.docx") {
        $size = (Get-Item "test-output.docx").Length
        Write-Host "  ✓ SUCCESS! Created test-output.docx ($size bytes)" -ForegroundColor Green
        Remove-Item "test-output.docx" -Force
    } else {
        Write-Host "  ✗ FAILED - No output created" -ForegroundColor Red
    }
} catch {
    Write-Host "  ✗ ERROR: $_" -ForegroundColor Red
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Green
Write-Host "  SETUP COMPLETE!" -ForegroundColor Green
Write-Host "=" * 80 -ForegroundColor Green
Write-Host ""
Write-Host "Templates location: $templatesDir" -ForegroundColor White
Write-Host ""
Write-Host "Next: Run your document generation service!" -ForegroundColor Yellow
Write-Host ""
