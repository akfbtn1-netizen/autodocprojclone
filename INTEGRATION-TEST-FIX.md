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
