# ============================================================================
# DEBUG SCRIPT - Test Template Execution
# ============================================================================
# This script helps diagnose why template execution might be failing
# ============================================================================

$ErrorActionPreference = "Continue"  # Don't stop on errors - we want to see them all

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$templatesDir = Join-Path $projectRoot "Templates"
$nodeExe = Join-Path $projectRoot "node-v24.11.0-win-x64\node.exe"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  TEMPLATE DEBUG TEST" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Step 1: Verify Node.js
Write-Host "[1] Checking Node.js..." -ForegroundColor Yellow
if (Test-Path $nodeExe) {
    Write-Host "  Found: $nodeExe" -ForegroundColor Green
    $nodeVersion = & $nodeExe --version 2>&1
    Write-Host "  Version: $nodeVersion" -ForegroundColor Green
} else {
    Write-Host "  ERROR: Node.js not found at $nodeExe" -ForegroundColor Red
    exit 1
}

# Step 2: Check Templates directory
Write-Host ""
Write-Host "[2] Checking Templates directory..." -ForegroundColor Yellow
if (Test-Path $templatesDir) {
    Write-Host "  Found: $templatesDir" -ForegroundColor Green
    $files = Get-ChildItem $templatesDir -Filter "*.js"
    Write-Host "  Template files found:" -ForegroundColor Green
    foreach ($file in $files) {
        Write-Host "    - $($file.Name) ($($file.Length) bytes)" -ForegroundColor Gray
    }
} else {
    Write-Host "  ERROR: Templates directory not found" -ForegroundColor Red
    exit 1
}

# Step 3: Check node_modules
Write-Host ""
Write-Host "[3] Checking node_modules..." -ForegroundColor Yellow
$nodeModulesPath = Join-Path $templatesDir "node_modules"
if (Test-Path $nodeModulesPath) {
    $docxPath = Join-Path $nodeModulesPath "docx"
    if (Test-Path $docxPath) {
        Write-Host "  docx package installed" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: docx package NOT found" -ForegroundColor Red
        Write-Host "  Run: cd $templatesDir && npm install" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "  ERROR: node_modules not found" -ForegroundColor Red
    Write-Host "  Run: cd $templatesDir && npm install" -ForegroundColor Yellow
    exit 1
}

# Step 4: Create test data
Write-Host ""
Write-Host "[4] Creating test data..." -ForegroundColor Yellow
$testDataPath = Join-Path $templatesDir "debug-test.json"
$testData = @'
{
  "schema": "dbo",
  "procedureName": "DebugTestProcedure",
  "author": "DebugTest",
  "ticket": "DEBUG-001",
  "created": "11/17/2025",
  "type": "Debug",
  "purpose": "This is a debug test to verify templates work correctly.",
  "parameters": [
    {
      "name": "@TestParam",
      "type": "int",
      "description": "Test parameter"
    }
  ],
  "returnValue": "None",
  "output": "Debug output",
  "executionLogic": [
    "Step 1: Initialize",
    "Step 2: Process",
    "Step 3: Complete"
  ],
  "dependencies": {
    "sourceTables": ["dbo.TestSource"],
    "targetTables": ["dbo.TestTarget"],
    "procedures": ["dbo.uspTestHelper"]
  },
  "usageExamples": [
    {
      "title": "Example 1",
      "code": "EXEC dbo.DebugTestProcedure @TestParam = 1"
    }
  ],
  "changeHistory": [
    {
      "date": "11/17/2025",
      "author": "DebugTest",
      "ticket": "DEBUG-001",
      "description": "Debug test"
    }
  ]
}
'@
$testData | Out-File -FilePath $testDataPath -Encoding utf8 -Force
Write-Host "  Created: debug-test.json" -ForegroundColor Green

# Step 5: Test template execution
Write-Host ""
Write-Host "[5] Testing TEMPLATE_Tier2_Standard.js..." -ForegroundColor Yellow
Write-Host "  Command:" -ForegroundColor Gray
Write-Host "    node.exe TEMPLATE_Tier2_Standard.js debug-test.json debug-output.docx" -ForegroundColor Gray
Write-Host ""

Push-Location $templatesDir
try {
    # Capture both stdout and stderr
    $output = & $nodeExe "TEMPLATE_Tier2_Standard.js" "debug-test.json" "debug-output.docx" 2>&1

    Write-Host "  Node.js Output:" -ForegroundColor Cyan
    foreach ($line in $output) {
        Write-Host "    $line" -ForegroundColor White
    }

    Write-Host ""

    # Check if output file was created
    if (Test-Path "debug-output.docx") {
        $fileInfo = Get-Item "debug-output.docx"
        Write-Host "  SUCCESS!" -ForegroundColor Green
        Write-Host "  Created: debug-output.docx" -ForegroundColor Green
        Write-Host "  Size: $($fileInfo.Length) bytes" -ForegroundColor Green
        Write-Host "  Location: $($fileInfo.FullName)" -ForegroundColor Green
    } else {
        Write-Host "  FAILED - No output file created" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Possible reasons:" -ForegroundColor Yellow
        Write-Host "    1. Node.js error (check output above)" -ForegroundColor Yellow
        Write-Host "    2. Missing dependencies (docx package)" -ForegroundColor Yellow
        Write-Host "    3. File path issues" -ForegroundColor Yellow
        Write-Host "    4. Permissions problems" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ERROR: $_" -ForegroundColor Red
    Write-Host "  Exception: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Pop-Location
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  DEBUG TEST COMPLETE" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Step 6: Show file listing
Write-Host "Files in Templates directory:" -ForegroundColor Yellow
Get-ChildItem $templatesDir | Format-Table Name, Length, LastWriteTime -AutoSize

Write-Host ""
Write-Host "If the test FAILED, please share the output above for debugging." -ForegroundColor Yellow
Write-Host ""
