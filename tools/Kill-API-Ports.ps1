#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Instantly kills processes blocking API ports and starts the API
.DESCRIPTION
    Finds and kills any process using ports 5195, 5000, 5001, then starts the API
#>

param(
    [int[]]$Ports = @(5195, 5000, 5001, 7000),
    [switch]$StartApi
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  API Port Killer & Starter" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Function to get process using port
function Get-ProcessOnPort {
    param([int]$Port)
    
    $netstat = netstat -ano | Select-String ":$Port\s" | Select-Object -First 1
    if ($netstat) {
        $parts = $netstat.ToString() -split '\s+' | Where-Object { $_ }
        $pid = $parts[-1]
        
        if ($pid -match '^\d+$') {
            try {
                $process = Get-Process -Id $pid -ErrorAction Stop
                return @{
                    PID = $pid
                    Name = $process.ProcessName
                    Port = $Port
                }
            } catch {
                return $null
            }
        }
    }
    return $null
}

# Check each port
$blockedPorts = @()
foreach ($port in $Ports) {
    $proc = Get-ProcessOnPort -Port $port
    if ($proc) {
        $blockedPorts += $proc
        Write-Host "[BLOCKED] Port $port is used by:" -ForegroundColor Red
        Write-Host "  Process: $($proc.Name) (PID: $($proc.PID))" -ForegroundColor Yellow
    } else {
        Write-Host "[OK] Port $port is available" -ForegroundColor Green
    }
}

if ($blockedPorts.Count -eq 0) {
    Write-Host "`n[SUCCESS] All ports are available!" -ForegroundColor Green
    
    if ($StartApi) {
        Write-Host "`nStarting API..." -ForegroundColor Cyan
        Set-Location "C:\Projects\EnterpriseDocumentationPlatform.V2"
        dotnet run --project src\Api\Api.csproj
    }
    exit 0
}

# Ask for confirmation
Write-Host "`n[WARNING] Found $($blockedPorts.Count) blocking process(es)" -ForegroundColor Yellow
Write-Host "Do you want to kill these processes? (Y/N): " -NoNewline -ForegroundColor Yellow
$confirmation = Read-Host

if ($confirmation -eq 'Y' -or $confirmation -eq 'y') {
    foreach ($proc in $blockedPorts) {
        try {
            Write-Host "Killing $($proc.Name) (PID: $($proc.PID))..." -ForegroundColor Yellow
            Stop-Process -Id $proc.PID -Force
            Write-Host "  [OK] Killed successfully" -ForegroundColor Green
        } catch {
            Write-Host "  [FAIL] Could not kill process: $_" -ForegroundColor Red
        }
    }
    
    # Wait a moment for ports to be released
    Start-Sleep -Seconds 2
    
    # Verify ports are free
    Write-Host "`nVerifying ports are now free..." -ForegroundColor Cyan
    $stillBlocked = @()
    foreach ($port in $Ports) {
        $proc = Get-ProcessOnPort -Port $port
        if ($proc) {
            $stillBlocked += $port
            Write-Host "  [FAIL] Port $port still blocked" -ForegroundColor Red
        } else {
            Write-Host "  [OK] Port $port is now free" -ForegroundColor Green
        }
    }
    
    if ($stillBlocked.Count -eq 0) {
        Write-Host "`n[SUCCESS] All ports are now available!" -ForegroundColor Green
        
        if ($StartApi) {
            Write-Host "`nStarting API..." -ForegroundColor Cyan
            Set-Location "C:\Projects\EnterpriseDocumentationPlatform.V2"
            dotnet run --project src\Api\Api.csproj
        } else {
            Write-Host "`nYou can now start the API with:" -ForegroundColor Cyan
            Write-Host "  cd C:\Projects\EnterpriseDocumentationPlatform.V2" -ForegroundColor Gray
            Write-Host "  dotnet run --project src\Api\Api.csproj" -ForegroundColor Gray
        }
    } else {
        Write-Host "`n[FAIL] Some ports are still blocked. Try running as Administrator." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "`nOperation cancelled. Ports remain blocked." -ForegroundColor Yellow
    Write-Host "`nAlternative: Run API on different port:" -ForegroundColor Cyan
    Write-Host '  $env:ASPNETCORE_URLS="http://localhost:5100"' -ForegroundColor Gray
    Write-Host "  dotnet run --project src\Api\Api.csproj" -ForegroundColor Gray
    exit 1
}
