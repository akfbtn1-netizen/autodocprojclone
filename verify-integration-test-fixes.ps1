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
