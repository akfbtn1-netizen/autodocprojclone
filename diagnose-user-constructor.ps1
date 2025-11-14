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
