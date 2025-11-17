# ============================================================================
# PowerShell Script to Create Node.js Documentation Templates Locally
# ============================================================================
# This script creates all necessary template files in your local project
# Run this from: C:\Projects\EnterpriseDocumentationPlatform.V2
# ============================================================================

$ErrorActionPreference = "Stop"

# Configuration
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$templatesDir = Join-Path $projectRoot "Templates"
$npmCmd = Join-Path $projectRoot "node-v24.11.0-win-x64\npm.cmd"
$nodeExe = Join-Path $projectRoot "node-v24.11.0-win-x64\node.exe"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "  Creating Documentation Templates" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Create Templates directory
Write-Host "[1/7] Creating Templates directory..." -ForegroundColor Yellow
if (-not (Test-Path $templatesDir)) {
    New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null
    Write-Host "  ✓ Created: $templatesDir" -ForegroundColor Green
} else {
    Write-Host "  ✓ Directory already exists: $templatesDir" -ForegroundColor Green
}
Write-Host ""

# Step 2: Create .gitignore
Write-Host "[2/7] Creating .gitignore..." -ForegroundColor Yellow
$gitignorePath = Join-Path $templatesDir ".gitignore"
$gitignoreContent = @"
# Node.js dependencies
node_modules/
package-lock.json

# Test outputs
test-*output.docx
*.log
"@
Set-Content -Path $gitignorePath -Value $gitignoreContent -Encoding UTF8
Write-Host "  ✓ Created: .gitignore" -ForegroundColor Green
Write-Host ""

# Step 3: Create package.json
Write-Host "[3/7] Creating package.json..." -ForegroundColor Yellow
$packageJsonPath = Join-Path $templatesDir "package.json"
$packageJsonContent = @"
{
  "name": "documentation-templates",
  "version": "1.0.0",
  "description": "Node.js templates for generating Word documentation",
  "main": "index.js",
  "scripts": {
    "test": "echo \"Error: no test specified\" && exit 1"
  },
  "keywords": ["documentation", "docx", "templates"],
  "author": "",
  "license": "MIT",
  "dependencies": {
    "docx": "^8.5.0"
  }
}
"@
Set-Content -Path $packageJsonPath -Value $packageJsonContent -Encoding UTF8
Write-Host "  ✓ Created: package.json" -ForegroundColor Green
Write-Host ""

# Step 4: Create test-data.json
Write-Host "[4/7] Creating test-data.json..." -ForegroundColor Yellow
$testDataPath = Join-Path $templatesDir "test-data.json"
$testDataContent = @"
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
Set-Content -Path $testDataPath -Value $testDataContent -Encoding UTF8
Write-Host "  ✓ Created: test-data.json" -ForegroundColor Green
Write-Host ""

# Step 5: Create Tier 2 Template (Standard)
Write-Host "[5/7] Creating TEMPLATE_Tier2_Standard.js..." -ForegroundColor Yellow
& "$PSScriptRoot\create-tier2-template.ps1" -OutputPath (Join-Path $templatesDir "TEMPLATE_Tier2_Standard.js")
Write-Host "  ✓ Created: TEMPLATE_Tier2_Standard.js" -ForegroundColor Green
Write-Host ""

# Step 6: Create Tier 1 Template (Comprehensive)
Write-Host "[6/7] Creating TEMPLATE_Tier1_Comprehensive.js..." -ForegroundColor Yellow
& "$PSScriptRoot\create-tier1-template.ps1" -OutputPath (Join-Path $templatesDir "TEMPLATE_Tier1_Comprehensive.js")
Write-Host "  ✓ Created: TEMPLATE_Tier1_Comprehensive.js" -ForegroundColor Green
Write-Host ""

# Step 7: Create Tier 3 Template (Lightweight)
Write-Host "[7/7] Creating TEMPLATE_Tier3_Lightweight.js..." -ForegroundColor Yellow
& "$PSScriptRoot\create-tier3-template.ps1" -OutputPath (Join-Path $templatesDir "TEMPLATE_Tier3_Lightweight.js")
Write-Host "  ✓ Created: TEMPLATE_Tier3_Lightweight.js" -ForegroundColor Green
Write-Host ""

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "  All template files created successfully!" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Install npm dependencies
Write-Host "Installing npm dependencies..." -ForegroundColor Yellow
Write-Host "  Using npm: $npmCmd" -ForegroundColor Gray
Write-Host ""

Push-Location $templatesDir
try {
    & $npmCmd install
    Write-Host ""
    Write-Host "  ✓ npm dependencies installed successfully!" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "  ✗ Failed to install npm dependencies: $_" -ForegroundColor Red
    Write-Host "  Please run manually: cd $templatesDir && npm install" -ForegroundColor Yellow
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "  Testing Templates" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Test Tier 2 template
Write-Host "Testing TEMPLATE_Tier2_Standard.js..." -ForegroundColor Yellow
Push-Location $templatesDir
try {
    $output = & $nodeExe "TEMPLATE_Tier2_Standard.js" "test-data.json" "test-output.docx" 2>&1
    Write-Host "  $output" -ForegroundColor Gray

    if (Test-Path "test-output.docx") {
        $fileSize = (Get-Item "test-output.docx").Length
        Write-Host "  ✓ SUCCESS! Output file created: test-output.docx ($fileSize bytes)" -ForegroundColor Green
        Remove-Item "test-output.docx" -Force
    } else {
        Write-Host "  ✗ FAILED - No output file created" -ForegroundColor Red
    }
} catch {
    Write-Host "  ✗ ERROR: $_" -ForegroundColor Red
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "  SETUP COMPLETE!" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Templates location: $templatesDir" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Update your C# code to use the Templates folder" -ForegroundColor White
Write-Host "  2. Test document generation with your stored procedures" -ForegroundColor White
Write-Host "  3. Check the temp folder for generated .docx files" -ForegroundColor White
Write-Host ""
