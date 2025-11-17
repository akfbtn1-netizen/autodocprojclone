# Documentation Templates Setup Instructions

## Quick Setup for Windows (Recommended)

### Option 1: Download and Run the Setup Script

1. **Download the setup script** from:
   ```
   https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/CREATE-ALL-TEMPLATES-WINDOWS.ps1
   ```

2. **Save it to your project root:**
   ```
   C:\Projects\EnterpriseDocumentationPlatform.V2\CREATE-ALL-TEMPLATES-WINDOWS.ps1
   ```

3. **Run PowerShell as Administrator** and execute:
   ```powershell
   cd C:\Projects\EnterpriseDocumentationPlatform.V2
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   .\CREATE-ALL-TEMPLATES-WINDOWS.ps1
   ```

4. **Done!** The script will:
   - ✅ Create `C:\Projects\EnterpriseDocumentationPlatform.V2\Templates\` directory
   - ✅ Download all 3 template files (Tier1, Tier2, Tier3)
   - ✅ Create package.json and install dependencies
   - ✅ Run a test to verify everything works
   - ✅ Show you SUCCESS message if .docx file is generated

---

## Option 2: Copy & Paste Method

If you can't download files, copy and paste this script:

### Step 1: Create the Setup Script

1. Open Notepad
2. Copy the entire script below
3. Save as: `C:\Projects\EnterpriseDocumentationPlatform.V2\SETUP.ps1`

```powershell
$ErrorActionPreference = "Stop"
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$templatesDir = Join-Path $projectRoot "Templates"
$npmCmd = Join-Path $projectRoot "node-v24.11.0-win-x64\npm.cmd"
$nodeExe = Join-Path $projectRoot "node-v24.11.0-win-x64\node.exe"

Write-Host "Creating Templates directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $templatesDir -Force | Out-Null

Write-Host "Creating package.json..." -ForegroundColor Yellow
@"
{
  "name": "documentation-templates",
  "version": "1.0.0",
  "dependencies": { "docx": "^8.5.0" }
}
"@ | Out-File -FilePath (Join-Path $templatesDir "package.json") -Encoding utf8

Write-Host "Downloading templates..." -ForegroundColor Yellow
$baseUrl = "https://raw.githubusercontent.com/akfbtn1-netizen/autodocprojclone/claude/debug-docgen-templates-01SHgnsugxx2XY7sXuC81ZyX/Templates"
Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_Tier1_Comprehensive.js" -OutFile (Join-Path $templatesDir "TEMPLATE_Tier1_Comprehensive.js") -UseBasicParsing
Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_Tier2_Standard.js" -OutFile (Join-Path $templatesDir "TEMPLATE_Tier2_Standard.js") -UseBasicParsing
Invoke-WebRequest -Uri "$baseUrl/TEMPLATE_Tier3_Lightweight.js" -OutFile (Join-Path $templatesDir "TEMPLATE_Tier3_Lightweight.js") -UseBasicParsing

Write-Host "Installing dependencies..." -ForegroundColor Yellow
Push-Location $templatesDir
& $npmCmd install
Pop-Location

Write-Host "DONE!" -ForegroundColor Green
```

### Step 2: Run the Script

```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\SETUP.ps1
```

---

## Verification

After running either script, verify the setup:

```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2\Templates
dir
```

You should see:
- ✅ `TEMPLATE_Tier1_Comprehensive.js` (~17 KB)
- ✅ `TEMPLATE_Tier2_Standard.js` (~12 KB)
- ✅ `TEMPLATE_Tier3_Lightweight.js` (~11 KB)
- ✅ `package.json`
- ✅ `node_modules\` folder (with docx package)

---

## Manual Testing

Test that templates work:

```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2\Templates

# Create test data
@"
{
  "schema": "dbo",
  "procedureName": "TestProcedure",
  "author": "Test",
  "ticket": "TEST-001",
  "created": "11/17/2025",
  "type": "Test",
  "purpose": "Testing the template",
  "parameters": [],
  "executionLogic": ["Step 1: Test"],
  "dependencies": {"sourceTables": [], "targetTables": [], "procedures": []},
  "usageExamples": [],
  "changeHistory": []
}
"@ | Out-File -FilePath "test.json" -Encoding utf8

# Run the template
..\node-v24.11.0-win-x64\node.exe TEMPLATE_Tier2_Standard.js test.json output.docx

# Check output
dir output.docx
```

If you see `output.docx` created successfully, **YOU'RE DONE!**

---

## Troubleshooting

### Error: "Invoke-WebRequest: Unable to connect"

**Solution:** Use the embedded template method (see below) or check your internet connection.

### Error: "Cannot find module 'docx'"

**Solution:** Install dependencies manually:
```powershell
cd C:\Projects\EnterpriseDocumentationPlatform.V2\Templates
..\node-v24.11.0-win-x64\npm.cmd install
```

### Error: "Script execution is disabled"

**Solution:** Run PowerShell as Administrator and execute:
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

---

## What These Templates Fix

### The Problem
Your C# code was creating JSON data files and modified.js templates, but NO .docx outputs were being generated.

### The Root Cause
The original templates had:
- ❌ Hardcoded data (not reading from JSON files)
- ❌ Hardcoded output filenames
- ❌ No command-line argument support

### The Solution
The new templates:
- ✅ Accept command-line arguments: `node template.js input.json output.docx`
- ✅ Read JSON data from the input file
- ✅ Generate .docx to the specified output path
- ✅ Include error handling and helpful error messages
- ✅ Support flexible JSON data structures

---

## Next Steps

After setup is complete:

1. **Update your C# code** to point to the Templates folder:
   ```csharp
   private const string TemplatesDirectory = @"C:\Projects\EnterpriseDocumentationPlatform.V2\Templates";
   ```

2. **Run your document generation service**

3. **Check the temp folder** - you should now see:
   - ✅ JSON data files created
   - ✅ Modified JS files created
   - ✅ **OUTPUT .docx files generated!**

---

## Support

If you have issues, check:
1. Node.js is at: `C:\Projects\EnterpriseDocumentationPlatform.V2\node-v24.11.0-win-x64\`
2. npm.cmd exists at that location
3. You have internet access (for downloading templates)
4. PowerShell execution policy allows scripts

For more help, see the repository issues.
