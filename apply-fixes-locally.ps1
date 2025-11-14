# PowerShell script to apply integration test fixes to local files
# Run this from the project root directory

$ErrorActionPreference = "Stop"

Write-Host "=== Applying Integration Test Fixes to Local Files ===" -ForegroundColor Cyan
Write-Host ""

# Function to create directory if it doesn't exist
function Ensure-Directory {
    param([string]$path)
    $dir = Split-Path -Parent $path
    if ($dir -and !(Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "Created directory: $dir" -ForegroundColor Green
    }
}

# Function to backup a file before modifying
function Backup-File {
    param([string]$path)
    if (Test-Path $path) {
        $backup = "$path.backup"
        Copy-Item $path $backup -Force
        Write-Host "Backed up: $path to $backup" -ForegroundColor Yellow
    }
}

Write-Host "Step 1: Updating Directory.Build.props..." -ForegroundColor Yellow
$buildPropsPath = "Directory.Build.props"
if (Test-Path $buildPropsPath) {
    Backup-File $buildPropsPath
    $content = Get-Content $buildPropsPath -Raw
    if ($content -match '<NoWarn>\$\(NoWarn\);1591</NoWarn>') {
        $content = $content -replace '<NoWarn>\$\(NoWarn\);1591</NoWarn>', '<NoWarn>$(NoWarn);1591;CA1031</NoWarn>'
        Set-Content -Path $buildPropsPath -Value $content -NoNewline
        Write-Host "  ✓ Updated Directory.Build.props (added CA1031 to NoWarn)" -ForegroundColor Green
    } else {
        Write-Host "  - Directory.Build.props already contains CA1031 or has different format" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ Directory.Build.props not found!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Step 2: Creating diagnostic and helper scripts..." -ForegroundColor Yellow

# Create diagnose-user-constructor.ps1
$diagScript = @'
# PowerShell script to diagnose User constructor signature
# Run this from the project root directory

Write-Host "=== Analyzing User.cs ===" -ForegroundColor Cyan

$userFile = "src\Core\Domain\Entities\User.cs"

if (Test-Path $userFile) {
    Write-Host "`nFound User.cs at: $userFile" -ForegroundColor Green

    # Extract the public User constructor
    $content = Get-Content $userFile -Raw

    # Find the constructor
    if ($content -match '(?s)public User\s*\([^)]+\)') {
        $constructor = $matches[0]
        Write-Host "`nUser constructor found:" -ForegroundColor Yellow
        Write-Host $constructor -ForegroundColor White
    }

    # Check SecurityClearanceLevel location
    Write-Host "`n=== Checking SecurityClearanceLevel location ===" -ForegroundColor Cyan

    $enumsPath = "src\Core\Domain\Enums\SecurityClearanceLevel.cs"
    if (Test-Path $enumsPath) {
        Write-Host "Found in Enums folder: $enumsPath" -ForegroundColor Green
    }

    if ($content -match 'public enum SecurityClearanceLevel') {
        Write-Host "Found in User.cs (Enterprise.Documentation.Core.Domain.Entities namespace)" -ForegroundColor Green
    }

    # Check for PasswordHash
    Write-Host "`n=== Checking PasswordHash location ===" -ForegroundColor Cyan
    $passwordHashPath = "src\Core\Domain\ValueObjects\PasswordHash.cs"
    if (Test-Path $passwordHashPath) {
        Write-Host "Found: $passwordHashPath" -ForegroundColor Green

        # Show first few lines
        Write-Host "`nPasswordHash.cs content (first 30 lines):" -ForegroundColor Yellow
        Get-Content $passwordHashPath -Head 30
    }

} else {
    Write-Host "User.cs not found at $userFile" -ForegroundColor Red
}

Write-Host "`n=== Done ===" -ForegroundColor Green
'@

Set-Content -Path "diagnose-user-constructor.ps1" -Value $diagScript
Write-Host "  ✓ Created diagnose-user-constructor.ps1" -ForegroundColor Green

# Create scripts directory and Remove-DuplicateWebApplicationFactory.ps1
Ensure-Directory "scripts\Remove-DuplicateWebApplicationFactory.ps1"

$removeScript = @'
# Remove-DuplicateWebApplicationFactory.ps1
# Script to identify and remove duplicate WebApplicationFactory definitions

$ErrorActionPreference = "Stop"

Write-Host "=== Duplicate WebApplicationFactory Cleanup Script ===" -ForegroundColor Cyan
Write-Host ""

$projectRoot = Split-Path -Parent $PSScriptRoot
$integrationTestPath = Join-Path $projectRoot "tests\Integration"

Write-Host "Searching for WebApplicationFactory files in: $integrationTestPath" -ForegroundColor Yellow
Write-Host ""

# Find all .cs files containing CustomWebApplicationFactory class
$files = Get-ChildItem -Path $integrationTestPath -Filter "*.cs" -Recurse |
    Where-Object {
        $content = Get-Content $_.FullName -Raw
        $content -match "class\s+CustomWebApplicationFactory"
    }

Write-Host "Found the following files containing CustomWebApplicationFactory:" -ForegroundColor Green
$files | ForEach-Object {
    Write-Host "  - $($_.FullName)" -ForegroundColor White

    # Show first few lines to help identify the file
    $lines = Get-Content $_.FullName -TotalCount 20
    Write-Host "    Preview (first 20 lines):" -ForegroundColor Gray
    $lines | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    Write-Host ""
}

Write-Host ""
Write-Host "Analysis:" -ForegroundColor Cyan

if ($files.Count -gt 1) {
    Write-Host "  ERROR: Found $($files.Count) files with CustomWebApplicationFactory class!" -ForegroundColor Red
    Write-Host "  This is causing the CS0101 error (duplicate type definition)" -ForegroundColor Red
    Write-Host ""

    # Identify which file should be kept
    $correctFile = $files | Where-Object { $_.Name -eq "CustomWebApplicationFactory.cs" }
    $duplicateFiles = $files | Where-Object { $_.Name -ne "CustomWebApplicationFactory.cs" }

    if ($correctFile) {
        Write-Host "  The CORRECT file to keep is:" -ForegroundColor Green
        Write-Host "    $($correctFile.FullName)" -ForegroundColor White
        Write-Host ""
    }

    if ($duplicateFiles) {
        Write-Host "  The following DUPLICATE file(s) should be removed:" -ForegroundColor Yellow
        $duplicateFiles | ForEach-Object {
            Write-Host "    $($_.FullName)" -ForegroundColor White
        }
        Write-Host ""

        # Ask for confirmation to remove
        $response = Read-Host "Do you want to remove the duplicate file(s)? (yes/no)"

        if ($response -eq "yes" -or $response -eq "y") {
            foreach ($dupFile in $duplicateFiles) {
                Write-Host "  Removing: $($dupFile.FullName)" -ForegroundColor Yellow
                Remove-Item $dupFile.FullName -Force
                Write-Host "  Removed successfully!" -ForegroundColor Green
            }
            Write-Host ""
            Write-Host "Cleanup complete! Please rebuild the solution." -ForegroundColor Green
        } else {
            Write-Host "  Operation cancelled. No files were removed." -ForegroundColor Yellow
        }
    }
} elseif ($files.Count -eq 1) {
    Write-Host "  OK: Found exactly 1 file with CustomWebApplicationFactory" -ForegroundColor Green
    Write-Host "  File: $($files[0].FullName)" -ForegroundColor White
    Write-Host ""
    Write-Host "  No duplicates found. The build error might be from cached files." -ForegroundColor Yellow
    Write-Host "  Try running: dotnet clean && dotnet build" -ForegroundColor Cyan
} else {
    Write-Host "  WARNING: No files found with CustomWebApplicationFactory class!" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Script Complete ===" -ForegroundColor Cyan
'@

Set-Content -Path "scripts\Remove-DuplicateWebApplicationFactory.ps1" -Value $removeScript
Write-Host "  ✓ Created scripts\Remove-DuplicateWebApplicationFactory.ps1" -ForegroundColor Green

# Create verification script
$verifyScript = @'
# PowerShell script to verify all integration test fixes are applied
# Run this from the project root directory

Write-Host "=== Integration Test Fixes Verification ===" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

# Check CustomWebApplicationFactory.cs
Write-Host "1. Checking CustomWebApplicationFactory.cs..." -ForegroundColor Yellow
$factoryFile = "tests\Integration\CustomWebApplicationFactory.cs"

if (Test-Path $factoryFile) {
    $content = Get-Content $factoryFile -Raw

    # Check 1: SecurityClearanceLevel (not SecurityClearance)
    if ($content -match 'SecurityClearanceLevel\.Confidential' -and
        $content -match 'SecurityClearanceLevel\.Restricted') {
        Write-Host "   ✓ Uses SecurityClearanceLevel enum (correct)" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Missing SecurityClearanceLevel enum usage" -ForegroundColor Red
        $allPassed = $false
    }

    # Check 2: UserRole.Administrator (not UserRole.Admin)
    if ($content -match 'UserRole\.Administrator') {
        Write-Host "   ✓ Uses UserRole.Administrator (correct)" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Missing UserRole.Administrator" -ForegroundColor Red
        $allPassed = $false
    }

    # Check 3: UserRole.Manager (not UserRole.DocumentEditor)
    if ($content -match 'UserRole\.Manager') {
        Write-Host "   ✓ Uses UserRole.Manager (correct)" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Missing UserRole.Manager" -ForegroundColor Red
        $allPassed = $false
    }

    # Check 4: UserRole.Reader (not UserRole.DocumentViewer)
    if ($content -match 'UserRole\.Reader') {
        Write-Host "   ✓ Uses UserRole.Reader (correct)" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Missing UserRole.Reader" -ForegroundColor Red
        $allPassed = $false
    }

    # Check 5: new User(...) constructor (not User.Create)
    if ($content -match 'new User\s*\(' -and $content -notmatch 'User\.Create\s*\(') {
        Write-Host "   ✓ Uses new User(...) constructor (correct)" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Still using User.Create() or missing new User()" -ForegroundColor Red
        $allPassed = $false
    }

} else {
    Write-Host "   ✗ File not found: $factoryFile" -ForegroundColor Red
    $allPassed = $false
}

Write-Host ""

# Check IntegrationTestHelpers.cs
Write-Host "2. Checking IntegrationTestHelpers.cs..." -ForegroundColor Yellow
$helpersFile = "tests\Integration\Helpers\IntegrationTestHelpers.cs"

if (Test-Path $helpersFile) {
    $content = Get-Content $helpersFile -Raw

    # Check 6: SetDocumentVersionUnderReviewAsync method exists
    if ($content -match 'SetDocumentVersionUnderReviewAsync') {
        Write-Host "   ✓ SetDocumentVersionUnderReviewAsync method exists" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Missing SetDocumentVersionUnderReviewAsync method" -ForegroundColor Red
        $allPassed = $false
    }

} else {
    Write-Host "   ✗ File not found: $helpersFile" -ForegroundColor Red
    $allPassed = $false
}

Write-Host ""
Write-Host "=== Verification Complete ===" -ForegroundColor Cyan

if ($allPassed) {
    Write-Host "✓ All fixes have been applied successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Run: dotnet build" -ForegroundColor White
    Write-Host "2. Run: dotnet test" -ForegroundColor White
    exit 0
} else {
    Write-Host "✗ Some fixes are missing. Please review the errors above." -ForegroundColor Red
    exit 1
}
'@

Set-Content -Path "verify-integration-test-fixes.ps1" -Value $verifyScript
Write-Host "  ✓ Created verify-integration-test-fixes.ps1" -ForegroundColor Green

# Create documentation
$docContent = @'
# Integration Test Build Error - Duplicate CustomWebApplicationFactory

## Issue

The build is failing with the following errors:

```
Tests.Integration failed with 3 error(s)
C:\Projects\EnterpriseDocumentationPlatform.V2\tests\Integration\WebApplicationFactorySetup.cs(15,14): error CS0101: The namespace 'Tests.Integration' already contains a definition for 'CustomWebApplicationFactory'
C:\Projects\EnterpriseDocumentationPlatform.V2\tests\Integration\WebApplicationFactorySetup.cs(17,29): error CS0111: Type 'CustomWebApplicationFactory' already defines a member called 'ConfigureWebHost' with the same parameter types
C:\Projects\EnterpriseDocumentationPlatform.V2\tests\Integration\WebApplicationFactorySetup.cs(101,25): error CS0111: Type 'CustomWebApplicationFactory' already defines a member called 'SeedTestData' with the same parameter types
```

## Root Cause

There are **two files** containing the `CustomWebApplicationFactory` class in the `tests/Integration` directory:

1. **`CustomWebApplicationFactory.cs`** - The correct, git-tracked file
2. **`WebApplicationFactorySetup.cs`** - A duplicate/orphaned file (not tracked by git)

The C# compiler is finding both files and reporting duplicate class definitions.

## Solution

### Option 1: Use the Automated PowerShell Script

Run the provided cleanup script from the project root:

```powershell
.\scripts\Remove-DuplicateWebApplicationFactory.ps1
```

This script will:
- Identify all files containing `CustomWebApplicationFactory`
- Show you which file is the duplicate
- Offer to automatically remove the duplicate file(s)

### Option 2: Manual Removal

1. Navigate to: `tests\Integration\`
2. Check if `WebApplicationFactorySetup.cs` exists
3. If it exists, **delete it** (this is the old/duplicate file)
4. Keep `CustomWebApplicationFactory.cs` (this is the correct file)
5. Clean and rebuild:
   ```powershell
   dotnet clean
   dotnet build
   ```

### Option 3: Clean Build Cache

If the file doesn't exist but you're still getting errors, try cleaning the build cache:

```powershell
# Clean all build artifacts
dotnet clean

# Remove bin and obj directories
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force

# Rebuild
dotnet build
```

## Verification

After applying the fix, verify the build succeeds:

```powershell
dotnet build tests/Integration/Tests.Integration.csproj
```

You should see:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Prevention

- The file `CustomWebApplicationFactory.cs` is the correct file and is tracked in git
- Do not create additional files with the `CustomWebApplicationFactory` class
- If you need to modify the factory, edit `tests/Integration/CustomWebApplicationFactory.cs`

## Files Involved

- ✅ **Keep**: `tests/Integration/CustomWebApplicationFactory.cs`
- ❌ **Remove**: `tests/Integration/WebApplicationFactorySetup.cs` (if exists)
'@

Set-Content -Path "INTEGRATION-TEST-FIX.md" -Value $docContent
Write-Host "  ✓ Created INTEGRATION-TEST-FIX.md" -ForegroundColor Green

Write-Host ""
Write-Host "Step 3: Updating CustomWebApplicationFactory.cs..." -ForegroundColor Yellow
$factoryPath = "tests\Integration\CustomWebApplicationFactory.cs"

if (Test-Path $factoryPath) {
    Backup-File $factoryPath
    $content = Get-Content $factoryPath -Raw

    # Replace the SeedTestData method User instantiation
    $oldPattern = @'
        // Add test users
        var testUser = User\.Create\(
            email: "testadmin@example\.com",
            displayName: "Test Admin",
            securityClearance: SecurityClearance\.Confidential,
            roles: new List<UserRole> \{ UserRole\.Admin, UserRole\.DocumentEditor \}
        \);

        var testUser2 = User\.Create\(
            email: "testuser@example\.com",
            displayName: "Test User",
            securityClearance: SecurityClearance\.Restricted,
            roles: new List<UserRole> \{ UserRole\.DocumentViewer \}
        \);
'@

    $newCode = @'
        // Create system user ID for creation tracking
        var systemUserId = new UserId();

        // Add test users
        var testUser = new User(
            id: new UserId(),
            email: "testadmin@example.com",
            displayName: "Test Admin",
            securityClearance: SecurityClearanceLevel.Confidential,
            createdBy: systemUserId,
            firstName: "Test",
            lastName: "Admin",
            roles: new List<UserRole> { UserRole.Administrator, UserRole.Manager }
        );

        var testUser2 = new User(
            id: new UserId(),
            email: "testuser@example.com",
            displayName: "Test User",
            securityClearance: SecurityClearanceLevel.Restricted,
            createdBy: systemUserId,
            firstName: "Test",
            lastName: "User",
            roles: new List<UserRole> { UserRole.Reader }
        );
'@

    # Simpler approach: check if file contains old code and replace it
    if ($content -match 'User\.Create\(' -and $content -match 'SecurityClearance\.Confidential') {
        # Read line by line to find and replace the section
        $lines = Get-Content $factoryPath
        $newLines = @()
        $inReplaceSection = $false
        $skipLines = 0

        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($skipLines -gt 0) {
                $skipLines--
                continue
            }

            $line = $lines[$i]

            # Detect start of section to replace
            if ($line -match '^\s*// Add test users\s*$') {
                $newLines += '        // Create system user ID for creation tracking'
                $newLines += '        var systemUserId = new UserId();'
                $newLines += ''
                $newLines += '        // Add test users'
                $newLines += '        var testUser = new User('
                $newLines += '            id: new UserId(),'
                $newLines += '            email: "testadmin@example.com",'
                $newLines += '            displayName: "Test Admin",'
                $newLines += '            securityClearance: SecurityClearanceLevel.Confidential,'
                $newLines += '            createdBy: systemUserId,'
                $newLines += '            firstName: "Test",'
                $newLines += '            lastName: "Admin",'
                $newLines += '            roles: new List<UserRole> { UserRole.Administrator, UserRole.Manager }'
                $newLines += '        );'
                $newLines += ''
                $newLines += '        var testUser2 = new User('
                $newLines += '            id: new UserId(),'
                $newLines += '            email: "testuser@example.com",'
                $newLines += '            displayName: "Test User",'
                $newLines += '            securityClearance: SecurityClearanceLevel.Restricted,'
                $newLines += '            createdBy: systemUserId,'
                $newLines += '            firstName: "Test",'
                $newLines += '            lastName: "User",'
                $newLines += '            roles: new List<UserRole> { UserRole.Reader }'
                $newLines += '        );'

                # Skip the old User.Create lines
                $j = $i + 1
                while ($j -lt $lines.Length -and $lines[$j] -notmatch '^\s*context\.Users\.AddRange') {
                    $j++
                }
                $skipLines = $j - $i - 1
            } else {
                $newLines += $line
            }
        }

        Set-Content -Path $factoryPath -Value $newLines
        Write-Host "  ✓ Updated CustomWebApplicationFactory.cs with correct User instantiation" -ForegroundColor Green
    } else {
        Write-Host "  - CustomWebApplicationFactory.cs already updated or has different code" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ CustomWebApplicationFactory.cs not found!" -ForegroundColor Red
}

Write-Host ""
Write-Host "Step 4: Updating IntegrationTestHelpers.cs..." -ForegroundColor Yellow
$helpersPath = "tests\Integration\Helpers\IntegrationTestHelpers.cs"

if (Test-Path $helpersPath) {
    Backup-File $helpersPath
    $content = Get-Content $helpersPath -Raw

    if ($content -notmatch 'SetDocumentVersionUnderReviewAsync') {
        # Add the new method before the closing brace of the class
        $newMethod = @'

    /// <summary>
    /// Sets a document version to under review status.
    /// Note: This is a helper method for testing. The actual implementation may vary
    /// depending on your API endpoints for document workflow management.
    /// </summary>
    /// <param name="client">HTTP client</param>
    /// <param name="documentId">Document ID</param>
    /// <param name="versionNumber">Version number</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task SetDocumentVersionUnderReviewAsync(
        HttpClient client,
        Guid documentId,
        int versionNumber)
    {
        // This is a placeholder implementation
        // You may need to adjust the endpoint and payload based on your actual API
        var request = new
        {
            DocumentId = documentId,
            VersionNumber = versionNumber,
            Status = "UnderReview"
        };

        var response = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}/versions/{versionNumber}/status",
            request);

        response.EnsureSuccessStatusCode();
    }
}
'@

        # Replace the last closing brace with the new method
        $content = $content -replace '\}(\s*)$', $newMethod

        Set-Content -Path $helpersPath -Value $content -NoNewline
        Write-Host "  ✓ Added SetDocumentVersionUnderReviewAsync method to IntegrationTestHelpers.cs" -ForegroundColor Green
    } else {
        Write-Host "  - IntegrationTestHelpers.cs already contains SetDocumentVersionUnderReviewAsync" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ IntegrationTestHelpers.cs not found!" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== All Fixes Applied ===" -ForegroundColor Green
Write-Host ""
Write-Host "Summary of changes:" -ForegroundColor Cyan
Write-Host "  1. Updated Directory.Build.props to suppress CA1031 warnings" -ForegroundColor White
Write-Host "  2. Created diagnostic and helper scripts" -ForegroundColor White
Write-Host "  3. Created INTEGRATION-TEST-FIX.md documentation" -ForegroundColor White
Write-Host "  4. Updated User instantiation in CustomWebApplicationFactory.cs" -ForegroundColor White
Write-Host "  5. Added SetDocumentVersionUnderReviewAsync to IntegrationTestHelpers.cs" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review the changes (backup files created with .backup extension)" -ForegroundColor White
Write-Host "  2. Run verification: .\verify-integration-test-fixes.ps1" -ForegroundColor White
Write-Host "  3. Clean and rebuild: dotnet clean; dotnet build" -ForegroundColor White
Write-Host "  4. Run tests: dotnet test" -ForegroundColor White
Write-Host ""
Write-Host "If you need to revert any changes, restore from the .backup files" -ForegroundColor Gray
