# ============================================================================
# Verify Batch Processing Setup
# ============================================================================
# Checks that all required changes were made to Program.cs and NuGet packages
# ============================================================================

$ErrorActionPreference = "Continue"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Batch Processing Setup Verification" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Path to V2 project (update if different)
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

if (-not (Test-Path $projectRoot)) {
    Write-Host "ERROR: Project directory not found: $projectRoot" -ForegroundColor Red
    Write-Host "Please update the `$projectRoot variable in this script." -ForegroundColor Red
    exit 1
}

$programCsPath = "$projectRoot\src\Api\Program.cs"
$apiCsprojPath = "$projectRoot\src\Api\Api.csproj"
$appSettingsPath = "$projectRoot\src\Api\appsettings.json"

Write-Host "Project Root: $projectRoot" -ForegroundColor Yellow
Write-Host ""

# ============================================================================
# 1. CHECK PROGRAM.CS
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "1. Checking Program.cs Service Registrations" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $programCsPath)) {
    Write-Host "[ERROR] Program.cs not found at: $programCsPath" -ForegroundColor Red
} else {
    $programContent = Get-Content $programCsPath -Raw

    # Required service registrations
    $checks = @(
        @{
            Name = "IMetadataExtractionService"
            Pattern = "IMetadataExtractionService.*MetadataExtractionService"
            Required = $true
        },
        @{
            Name = "IBatchProcessingOrchestrator"
            Pattern = "IBatchProcessingOrchestrator.*BatchProcessingOrchestrator"
            Required = $true
        },
        @{
            Name = "IVectorIndexingService"
            Pattern = "IVectorIndexingService.*VectorIndexingService"
            Required = $true
        },
        @{
            Name = "Hangfire Services"
            Pattern = "AddHangfireServices"
            Required = $true
        },
        @{
            Name = "Hangfire Configuration"
            Pattern = "UseHangfireConfiguration"
            Required = $true
        },
        @{
            Name = "Batch Services Namespace"
            Pattern = "using.*Services\.Batch"
            Required = $false
        },
        @{
            Name = "Hangfire Configuration Namespace"
            Pattern = "using.*Configuration.*Hangfire|Api\.Configuration"
            Required = $false
        }
    )

    $allPassed = $true
    foreach ($check in $checks) {
        if ($programContent -match $check.Pattern) {
            Write-Host "[OK] " -ForegroundColor Green -NoNewline
            Write-Host "$($check.Name) - Found" -ForegroundColor White
        } else {
            if ($check.Required) {
                Write-Host "[MISSING] " -ForegroundColor Red -NoNewline
                Write-Host "$($check.Name) - NOT FOUND" -ForegroundColor White
                $allPassed = $false
            } else {
                Write-Host "[WARNING] " -ForegroundColor Yellow -NoNewline
                Write-Host "$($check.Name) - Not found (may use different pattern)" -ForegroundColor White
            }
        }
    }

    Write-Host ""
    if ($allPassed) {
        Write-Host "Program.cs: " -NoNewline
        Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
    } else {
        Write-Host "Program.cs: " -NoNewline
        Write-Host "MISSING REQUIRED REGISTRATIONS" -ForegroundColor Red
    }
}

Write-Host ""

# ============================================================================
# 2. CHECK NUGET PACKAGES
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "2. Checking NuGet Packages" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $apiCsprojPath)) {
    Write-Host "[ERROR] Api.csproj not found at: $apiCsprojPath" -ForegroundColor Red
} else {
    $csprojContent = Get-Content $apiCsprojPath -Raw

    # Required NuGet packages
    $packages = @(
        @{ Name = "Hangfire.AspNetCore"; MinVersion = "1.8"; Required = $true },
        @{ Name = "Hangfire.SqlServer"; MinVersion = "1.8"; Required = $true },
        @{ Name = "DocumentFormat.OpenXml"; MinVersion = "2.0"; Required = $true },
        @{ Name = "Dapper"; MinVersion = "2.0"; Required = $false }
    )

    $allPackagesInstalled = $true
    foreach ($pkg in $packages) {
        if ($csprojContent -match "<PackageReference\s+Include=`"$($pkg.Name)`"") {
            # Extract version
            if ($csprojContent -match "<PackageReference\s+Include=`"$($pkg.Name)`"\s+Version=`"([^`"]+)`"") {
                $version = $matches[1]
                Write-Host "[OK] " -ForegroundColor Green -NoNewline
                Write-Host "$($pkg.Name) v$version" -ForegroundColor White
            } else {
                Write-Host "[OK] " -ForegroundColor Green -NoNewline
                Write-Host "$($pkg.Name) (version not specified)" -ForegroundColor White
            }
        } else {
            if ($pkg.Required) {
                Write-Host "[MISSING] " -ForegroundColor Red -NoNewline
                Write-Host "$($pkg.Name) - NOT INSTALLED" -ForegroundColor White
                $allPackagesInstalled = $false
            } else {
                Write-Host "[INFO] " -ForegroundColor Yellow -NoNewline
                Write-Host "$($pkg.Name) - Not found (may already be installed)" -ForegroundColor White
            }
        }
    }

    Write-Host ""
    if ($allPackagesInstalled) {
        Write-Host "NuGet Packages: " -NoNewline
        Write-Host "ALL REQUIRED PACKAGES INSTALLED" -ForegroundColor Green
    } else {
        Write-Host "NuGet Packages: " -NoNewline
        Write-Host "MISSING REQUIRED PACKAGES" -ForegroundColor Red
    }
}

Write-Host ""

# ============================================================================
# 3. CHECK APPSETTINGS.JSON
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "3. Checking appsettings.json Configuration" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $appSettingsPath)) {
    Write-Host "[ERROR] appsettings.json not found at: $appSettingsPath" -ForegroundColor Red
} else {
    $appSettingsContent = Get-Content $appSettingsPath -Raw

    # Check for required configuration sections
    $configChecks = @(
        @{ Name = "OpenAI Section"; Pattern = '"OpenAI"\s*:'; Required = $false },
        @{ Name = "VectorDB Section"; Pattern = '"VectorDB"\s*:'; Required = $false },
        @{ Name = "Hangfire Section"; Pattern = '"Hangfire"\s*:'; Required = $false },
        @{ Name = "ConnectionStrings"; Pattern = '"ConnectionStrings"\s*:'; Required = $true }
    )

    foreach ($check in $configChecks) {
        if ($appSettingsContent -match $check.Pattern) {
            Write-Host "[OK] " -ForegroundColor Green -NoNewline
            Write-Host "$($check.Name) - Found" -ForegroundColor White
        } else {
            if ($check.Required) {
                Write-Host "[MISSING] " -ForegroundColor Red -NoNewline
                Write-Host "$($check.Name) - NOT FOUND" -ForegroundColor White
            } else {
                Write-Host "[INFO] " -ForegroundColor Yellow -NoNewline
                Write-Host "$($check.Name) - Not configured (optional)" -ForegroundColor White
            }
        }
    }
}

Write-Host ""

# ============================================================================
# 4. CHECK FILES EXIST
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "4. Checking Batch Processing Files" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

$filesToCheck = @(
    @{ Path = "$projectRoot\src\Core\Domain\Entities\BatchJob.cs"; Name = "BatchJob.cs" },
    @{ Path = "$projectRoot\src\Core\Domain\Entities\BatchJobItem.cs"; Name = "BatchJobItem.cs" },
    @{ Path = "$projectRoot\src\Core\Application\Services\MetadataExtraction\MetadataExtractionService.cs"; Name = "MetadataExtractionService.cs" },
    @{ Path = "$projectRoot\src\Core\Application\Services\Batch\BatchProcessingOrchestrator.cs"; Name = "BatchProcessingOrchestrator.cs" },
    @{ Path = "$projectRoot\src\Core\Application\Services\VectorIndexing\VectorIndexingService.cs"; Name = "VectorIndexingService.cs" },
    @{ Path = "$projectRoot\src\Api\Controllers\BatchProcessingController.cs"; Name = "BatchProcessingController.cs" },
    @{ Path = "$projectRoot\src\Api\Configuration\HangfireConfiguration.cs"; Name = "HangfireConfiguration.cs" },
    @{ Path = "$projectRoot\sql\CREATE_BatchProcessing_Tables.sql"; Name = "CREATE_BatchProcessing_Tables.sql" }
)

$allFilesExist = $true
foreach ($file in $filesToCheck) {
    if (Test-Path $file.Path) {
        Write-Host "[OK] " -ForegroundColor Green -NoNewline
        Write-Host "$($file.Name)" -ForegroundColor White
    } else {
        Write-Host "[MISSING] " -ForegroundColor Red -NoNewline
        Write-Host "$($file.Name)" -ForegroundColor White
        $allFilesExist = $false
    }
}

Write-Host ""
if ($allFilesExist) {
    Write-Host "Files: " -NoNewline
    Write-Host "ALL FILES PRESENT" -ForegroundColor Green
} else {
    Write-Host "Files: " -NoNewline
    Write-Host "MISSING FILES - Run Copy-BatchProcessingFiles-Windows.ps1" -ForegroundColor Red
}

Write-Host ""

# ============================================================================
# SUMMARY
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

if ($allPassed -and $allPackagesInstalled -and $allFilesExist) {
    Write-Host "STATUS: " -NoNewline
    Write-Host "READY TO BUILD" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Configure OpenAI and Pinecone keys in appsettings.json" -ForegroundColor White
    Write-Host "2. Run: dotnet build" -ForegroundColor White
    Write-Host "3. Run: dotnet run --project src\Api" -ForegroundColor White
    Write-Host "4. Navigate to: http://localhost:5195/swagger" -ForegroundColor White
    Write-Host "5. Access Hangfire: http://localhost:5195/hangfire" -ForegroundColor White
} else {
    Write-Host "STATUS: " -NoNewline
    Write-Host "CONFIGURATION INCOMPLETE" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Issues found:" -ForegroundColor Yellow
    if (-not $allFilesExist) {
        Write-Host "- Missing batch processing files (run Copy-BatchProcessingFiles-Windows.ps1)" -ForegroundColor White
    }
    if (-not $allPassed) {
        Write-Host "- Missing service registrations in Program.cs" -ForegroundColor White
    }
    if (-not $allPackagesInstalled) {
        Write-Host "- Missing required NuGet packages" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Pause
Read-Host "Press Enter to exit"
