# PowerShell Enterprise Scripting - Implementation Guide

## Quick Start Checklist

```
□ Install PowerShell 5.1+ or 7.4+
□ Install SqlServer module: Install-Module SqlServer -Force
□ Install VSCode with PowerShell extension
□ Configure Set-StrictMode -Version Latest
□ Set $ErrorActionPreference = 'Stop' in scripts
```

---

## Step 1: Script Template Setup

Create every new script from this template:

```powershell
#Requires -Version 5.1
#Requires -Modules SqlServer

<#
.SYNOPSIS
    [One-line description]

.DESCRIPTION
    [Detailed description]

.PARAMETER ParameterName
    [Parameter description]

.EXAMPLE
    .\Script-Name.ps1 -Param1 "value"

.NOTES
    Author: [Your Name]
    Date: [Date]
    Version: 1.0.0
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RequiredParam,

    [Parameter(Mandatory = $false)]
    [switch]$OptionalSwitch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Functions
# Your functions here
#endregion

#region Main
try {
    # Main logic
}
catch {
    Write-Error "Failed: $($_.Exception.Message)"
    throw
}
finally {
    # Cleanup
}
#endregion
```

---

## Step 2: Here-String Rules (CRITICAL)

### ✅ CORRECT Pattern
```powershell
function Deploy-Something {
    param([string]$Name)
    
    # Opening @" at end of line
    $sql = @"
SELECT * FROM Table
WHERE Name = '$Name'
"@
    # ↑ Closing "@ at COLUMN 0 (no spaces before it!)
    
    return $sql
}
```

### ❌ WRONG Pattern (Will Fail)
```powershell
function Deploy-Something {
    param([string]$Name)
    
    $sql = @"
SELECT * FROM Table
WHERE Name = '$Name'
    "@   # ← FATAL: Indented closing marker!
    
    return $sql
}
```

### Safe Alternative (Avoid Here-Strings)
```powershell
# Use array join for simple multi-line strings
$sql = @(
    "SELECT *",
    "FROM Users",
    "WHERE Status = 'Active'"
) -join "`n"
```

---

## Step 3: Error Handling Pattern

### Standard Try-Catch-Finally
```powershell
function Invoke-DatabaseOperation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Query
    )
    
    try {
        # CRITICAL: Use -ErrorAction Stop!
        $result = Invoke-Sqlcmd -ServerInstance "server" `
                               -Database "db" `
                               -Query $Query `
                               -ErrorAction Stop
        return $result
    }
    catch [System.Data.SqlClient.SqlException] {
        # Handle SQL-specific errors
        Write-Error "SQL Error: $($_.Exception.Message)"
        throw
    }
    catch {
        # Handle all other errors
        Write-Error "Error: $($_.Exception.Message)"
        throw
    }
    finally {
        # Always runs - cleanup here
        Write-Verbose "Operation complete"
    }
}
```

### Retry Pattern
```powershell
function Invoke-WithRetry {
    param(
        [scriptblock]$ScriptBlock,
        [int]$MaxRetries = 3
    )
    
    $attempt = 0
    while ($attempt -lt $MaxRetries) {
        $attempt++
        try {
            return & $ScriptBlock
        }
        catch {
            if ($attempt -eq $MaxRetries) { throw }
            $delay = [Math]::Pow(2, $attempt)
            Write-Warning "Attempt $attempt failed. Retrying in $delay seconds..."
            Start-Sleep -Seconds $delay
        }
    }
}

# Usage
$result = Invoke-WithRetry -ScriptBlock {
    Invoke-Sqlcmd -ServerInstance "server" -Query "SELECT 1" -ErrorAction Stop
}
```

---

## Step 4: Parameter Validation

### Common Validation Attributes
```powershell
param(
    # Required, not empty
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Name,

    # Must be one of these values (with tab completion!)
    [ValidateSet('Dev', 'Test', 'Prod')]
    [string]$Environment = 'Dev',

    # Numeric range
    [ValidateRange(1, 100)]
    [int]$RetryCount = 3,

    # Regex pattern
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*$')]
    [string]$Identifier,

    # File must exist
    [ValidateScript({ Test-Path $_ })]
    [string]$ConfigFile,

    # Custom validation with message
    [ValidateScript({
        if ($_ -gt (Get-Date)) { $true }
        else { throw "Date must be in the future" }
    })]
    [DateTime]$ScheduledDate
)
```

---

## Step 5: SQL Server Integration

### Setup
```powershell
# Install module (one-time)
Install-Module SqlServer -Force -AllowClobber -Scope CurrentUser

# Import in script
Import-Module SqlServer -Force
```

### Basic Query Execution
```powershell
# Simple query
$results = Invoke-Sqlcmd -ServerInstance "localhost" `
                         -Database "MyDB" `
                         -Query "SELECT * FROM Users" `
                         -ErrorAction Stop

# Execute SQL file
Invoke-Sqlcmd -ServerInstance "localhost" `
              -Database "MyDB" `
              -InputFile "C:\Scripts\deploy.sql" `
              -ErrorAction Stop

# With timeout (for long operations)
Invoke-Sqlcmd -ServerInstance "localhost" `
              -Database "MyDB" `
              -Query "EXEC LongRunningProc" `
              -QueryTimeout 600 `
              -ErrorAction Stop
```

### SQL Authentication (When Required)
```powershell
$securePassword = ConvertTo-SecureString "Password" -AsPlainText -Force
$credential = New-Object PSCredential("username", $securePassword)

Invoke-Sqlcmd -ServerInstance "server" `
              -Database "db" `
              -Credential $credential `
              -Query "SELECT 1"
```

---

## Step 6: File Operations

### Safe Backup and Replace
```powershell
function Update-ConfigFile {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        
        [Parameter(Mandatory)]
        [string]$NewContent
    )
    
    $backupPath = "$FilePath.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    
    try {
        # Backup
        if (Test-Path $FilePath) {
            Copy-Item $FilePath $backupPath -Force
            Write-Verbose "Backup created: $backupPath"
        }
        
        # Update
        if ($PSCmdlet.ShouldProcess($FilePath, "Update content")) {
            Set-Content -Path $FilePath -Value $NewContent -Encoding UTF8
        }
    }
    catch {
        # Restore on failure
        if (Test-Path $backupPath) {
            Copy-Item $backupPath $FilePath -Force
            Write-Warning "Restored from backup"
        }
        throw
    }
}
```

---

## Step 7: Logging Function

```powershell
function Write-Log {
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        
        [ValidateSet('Info', 'Warning', 'Error', 'Success')]
        [string]$Level = 'Info'
    )
    
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $color = @{
        'Info' = 'White'
        'Warning' = 'Yellow'
        'Error' = 'Red'
        'Success' = 'Green'
    }[$Level]
    
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# Usage
Write-Log "Starting deployment" -Level Info
Write-Log "Completed successfully" -Level Success
Write-Log "Something went wrong" -Level Error
```

---

## Common Mistakes & Fixes

| Mistake | Fix |
|---------|-----|
| `if ($a == $b)` | `if ($a -eq $b)` |
| `if ($null -eq $var)` | `if ($var -eq $null)` |
| Indented `"@` closing | Put `"@` at column 0 |
| Missing `-ErrorAction Stop` | Add to cmdlets in try blocks |
| `$files.Count` on single item | `@(Get-ChildItem).Count` |
| Variable scope in ForEach-Object | Use `$script:varName` |

---

## Deployment Script Checklist

Before running any deployment script:

```
□ Script passes PSScriptAnalyzer
□ All here-string closing markers at column 0
□ -ErrorAction Stop on all database cmdlets
□ Backup logic implemented
□ Rollback logic tested
□ -WhatIf support added
□ Logging implemented
□ Tested in non-production environment
```

---

## Quick Reference Card

```powershell
# Force array from cmdlet
$items = @(Get-ChildItem)

# Null check
if ([string]::IsNullOrWhiteSpace($value)) { }

# Comparison operators
-eq, -ne, -gt, -lt, -ge, -le
-like "pattern*"
-match "regex"

# String here-string (literal)
@'
No $variable expansion
'@

# Expandable here-string
@"
With $variable expansion
"@

# Error handling
$ErrorActionPreference = 'Stop'
try { } catch { } finally { }

# Parameter validation
[ValidateSet('A','B')]
[ValidateRange(1,100)]
[ValidateNotNullOrEmpty()]
[ValidateScript({ Test-Path $_ })]
```

---

## Version History

- 1.0.0 (2026-01-03): Initial implementation guide
