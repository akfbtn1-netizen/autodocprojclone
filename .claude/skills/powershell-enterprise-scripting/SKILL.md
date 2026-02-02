---
name: powershell-enterprise-scripting
description: >
  Enterprise-grade PowerShell scripting patterns for deployment automation, SQL Server
  integration, error handling, and common syntax pitfalls. Prevents syntax errors and
  produces production-ready scripts. Use when writing deployment scripts, creating SQL
  Server automation with Invoke-Sqlcmd, building PowerShell modules, or implementing
  parameter validation and error handling.
license: MIT
---

# PowerShell Enterprise Scripting Skill

Production-ready PowerShell scripting patterns for enterprise environments, with emphasis on correct syntax, robust error handling, SQL Server integration, and deployment automation.

## When to Use This Skill

Activate when:
- Writing deployment scripts for databases or applications
- Creating SQL Server automation with Invoke-Sqlcmd
- Building PowerShell modules or advanced functions
- Needing here-strings for JSON, SQL, or multi-line content
- Implementing parameter validation
- Handling errors in production scripts
- Working with file operations and backups

## Section 1: Critical Syntax Rules

### 1.1 Here-String Syntax (MOST COMMON ERROR SOURCE)

Here-strings are the #1 source of PowerShell script failures. Follow these rules exactly:

#### Rule 1: Opening marker must be LAST on its line
```powershell
# CORRECT - @" is last thing on the line
$json = @"
{
    "name": "value"
}
"@

# WRONG - content on same line as @"
$json = @"{
    "name": "value"
}"@
```

#### Rule 2: Closing marker must be FIRST on its line (no indentation!)
```powershell
# CORRECT - "@ at column 0
function Test {
    $sql = @"
SELECT * FROM table
"@
    return $sql
}

# WRONG - "@ is indented (THIS WILL FAIL!)
function Test {
    $sql = @"
SELECT * FROM table
    "@  # <-- FATAL ERROR: indented closing marker
    return $sql
}
```

#### Rule 3: No whitespace after opening or before closing markers
```powershell
# CORRECT
$text = @"
content here
"@

# WRONG - space after @"
$text = @" 
content here
"@
```

#### Rule 4: Single vs Double Quote Here-Strings
```powershell
# Double-quote @" "@ - Variables ARE expanded
$name = "World"
$greeting = @"
Hello $name
Today is $(Get-Date -Format 'yyyy-MM-dd')
"@
# Output: Hello World\nToday is 2026-01-03

# Single-quote @' '@ - Variables are NOT expanded (literal)
$literal = @'
Hello $name
Today is $(Get-Date)
'@
# Output: Hello $name\nToday is $(Get-Date)
```

### 1.2 Here-String Best Practices for Functions

When using here-strings inside functions, ALWAYS place closing marker at column 0:

```powershell
function Deploy-Database {
    param([string]$ServerInstance, [string]$Database)
    
    # Here-string closing marker MUST be at column 0
    $createTableSql = @"
CREATE TABLE IF NOT EXISTS [dbo].[Logs] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [Message] NVARCHAR(MAX),
    [CreatedAt] DATETIME2 DEFAULT GETUTCDATE()
)
"@
    
    Invoke-Sqlcmd -ServerInstance $ServerInstance -Database $Database -Query $createTableSql
}
```

### 1.3 Alternative: Avoid Here-Strings When Possible

For simple multi-line strings, consider alternatives:

```powershell
# Option 1: String concatenation with line continuation
$sql = "SELECT * FROM Users " +
       "WHERE Status = 'Active' " +
       "ORDER BY Name"

# Option 2: Array join
$sql = @(
    "SELECT * FROM Users",
    "WHERE Status = 'Active'",
    "ORDER BY Name"
) -join "`n"

# Option 3: String builder for complex scenarios
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("SELECT *")
[void]$sb.AppendLine("FROM Users")
[void]$sb.AppendLine("WHERE Status = 'Active'")
$sql = $sb.ToString()
```

## Section 2: Script Structure & Standards

### 2.1 Standard Script Template

```powershell
#Requires -Version 5.1
#Requires -Modules SqlServer

<#
.SYNOPSIS
    Brief description of what the script does.

.DESCRIPTION
    Detailed description of the script's purpose and functionality.

.PARAMETER ServerInstance
    The SQL Server instance name.

.PARAMETER Database
    The target database name.

.EXAMPLE
    .\Deploy-Schema.ps1 -ServerInstance "localhost" -Database "MyDB"

.NOTES
    Author: Your Name
    Date: 2026-01-03
    Version: 1.0.0
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$ServerInstance,

    [Parameter(Mandatory = $true, Position = 1)]
    [ValidateNotNullOrEmpty()]
    [string]$Database,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

# Set strict mode for better error detection
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Functions
function Write-Log {
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        
        [ValidateSet('Info', 'Warning', 'Error', 'Success')]
        [string]$Level = 'Info'
    )
    
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $color = switch ($Level) {
        'Info'    { 'White' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
        'Success' { 'Green' }
    }
    
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}
#endregion

#region Main Script
try {
    Write-Log "Starting deployment to $ServerInstance/$Database" -Level Info
    
    # Your main logic here
    
    Write-Log "Deployment completed successfully" -Level Success
}
catch {
    Write-Log "Deployment failed: $($_.Exception.Message)" -Level Error
    Write-Log "Stack trace: $($_.ScriptStackTrace)" -Level Error
    throw
}
finally {
    # Cleanup code here
    Write-Log "Script execution finished" -Level Info
}
#endregion
```

### 2.2 Naming Conventions

```powershell
# Functions: Verb-Noun (PascalCase)
function Get-DatabaseSchema { }
function Set-Configuration { }
function New-BackupFile { }
function Remove-TempFiles { }

# Variables: PascalCase for public, camelCase for local
$ServerInstance = "localhost"      # Parameter/public
$connectionString = "..."          # Local variable
$Script:ModuleConfig = @{}         # Script-scoped

# Constants: ALL_CAPS with underscores
$MAX_RETRY_COUNT = 3
$DEFAULT_TIMEOUT_SECONDS = 30

# Boolean variables: Use Is/Has/Can prefix
$IsConnected = $false
$HasPermission = $true
$CanRetry = $true
```

## Section 3: Error Handling Patterns

### 3.1 Try-Catch-Finally Pattern

```powershell
function Invoke-DatabaseOperation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ServerInstance,
        
        [Parameter(Mandatory)]
        [string]$Query
    )
    
    $connection = $null
    
    try {
        Write-Verbose "Connecting to $ServerInstance"
        
        # Force terminating error for non-terminating cmdlets
        $result = Invoke-Sqlcmd -ServerInstance $ServerInstance `
                               -Query $Query `
                               -ErrorAction Stop
        
        return $result
    }
    catch [System.Data.SqlClient.SqlException] {
        # Handle SQL-specific errors
        $sqlError = $_.Exception
        Write-Error "SQL Error $($sqlError.Number): $($sqlError.Message)"
        throw
    }
    catch [System.Net.Sockets.SocketException] {
        # Handle network errors
        Write-Error "Network error connecting to $ServerInstance"
        throw
    }
    catch {
        # Handle all other errors
        Write-Error "Unexpected error: $($_.Exception.Message)"
        Write-Error "Error Type: $($_.Exception.GetType().FullName)"
        throw
    }
    finally {
        # Always runs - cleanup resources
        if ($connection) {
            $connection.Close()
            $connection.Dispose()
        }
        Write-Verbose "Operation completed"
    }
}
```

### 3.2 ErrorAction Parameter

```powershell
# ErrorAction values:
# - Stop: Convert non-terminating to terminating (RECOMMENDED in try blocks)
# - Continue: Default - show error, continue execution
# - SilentlyContinue: Suppress error, continue execution
# - Ignore: Suppress error, don't add to $Error
# - Inquire: Prompt user for action

# CRITICAL: Use -ErrorAction Stop in try blocks!
try {
    # Without -ErrorAction Stop, this WON'T trigger the catch block
    Get-ChildItem "C:\NonExistent" -ErrorAction Stop
}
catch {
    Write-Host "Caught the error!"
}

# Global error preference (use sparingly)
$ErrorActionPreference = 'Stop'
```

### 3.3 Retry Pattern with Exponential Backoff

```powershell
function Invoke-WithRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [scriptblock]$ScriptBlock,
        
        [int]$MaxRetries = 3,
        
        [int]$InitialDelaySeconds = 1,
        
        [string[]]$RetryableExceptions = @(
            'System.Net.Sockets.SocketException',
            'System.Data.SqlClient.SqlException'
        )
    )
    
    $attempt = 0
    $lastError = $null
    
    while ($attempt -lt $MaxRetries) {
        $attempt++
        
        try {
            Write-Verbose "Attempt $attempt of $MaxRetries"
            return & $ScriptBlock
        }
        catch {
            $lastError = $_
            $exceptionType = $_.Exception.GetType().FullName
            
            if ($exceptionType -notin $RetryableExceptions) {
                Write-Warning "Non-retryable exception: $exceptionType"
                throw
            }
            
            if ($attempt -lt $MaxRetries) {
                $delay = $InitialDelaySeconds * [Math]::Pow(2, $attempt - 1)
                Write-Warning "Attempt $attempt failed. Retrying in $delay seconds..."
                Start-Sleep -Seconds $delay
            }
        }
    }
    
    Write-Error "All $MaxRetries attempts failed"
    throw $lastError
}

# Usage
$result = Invoke-WithRetry -MaxRetries 3 -ScriptBlock {
    Invoke-Sqlcmd -ServerInstance "server" -Query "SELECT 1" -ErrorAction Stop
}
```

## Section 4: Parameter Validation

### 4.1 Validation Attributes

```powershell
function Set-Configuration {
    [CmdletBinding()]
    param(
        # Mandatory with position
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Name,
        
        # Validate not null or empty
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Value,
        
        # Validate from a set of values (with tab completion!)
        [Parameter()]
        [ValidateSet('Development', 'Testing', 'Staging', 'Production')]
        [string]$Environment = 'Development',
        
        # Validate numeric range
        [Parameter()]
        [ValidateRange(1, 100)]
        [int]$RetryCount = 3,
        
        # Validate string length
        [Parameter()]
        [ValidateLength(1, 50)]
        [string]$Description,
        
        # Validate with regex pattern
        [Parameter()]
        [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*$')]
        [string]$Identifier,
        
        # Validate file/path exists
        [Parameter()]
        [ValidateScript({ Test-Path $_ -PathType Leaf })]
        [string]$ConfigFile,
        
        # Validate with custom script and error message
        [Parameter()]
        [ValidateScript({
            if ($_ -gt (Get-Date)) {
                $true
            } else {
                throw "Date must be in the future"
            }
        })]
        [DateTime]$ScheduledDate,
        
        # Validate array count
        [Parameter()]
        [ValidateCount(1, 10)]
        [string[]]$Tags
    )
    
    # Function body
}
```

### 4.2 Dynamic ValidateSet with ArgumentCompleter

```powershell
function Get-DatabaseTable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ServerInstance,
        
        [Parameter(Mandatory)]
        [string]$Database,
        
        [Parameter()]
        [ArgumentCompleter({
            param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)
            
            # Dynamic completion based on actual database tables
            $server = $fakeBoundParameters['ServerInstance']
            $db = $fakeBoundParameters['Database']
            
            if ($server -and $db) {
                $query = "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"
                $tables = Invoke-Sqlcmd -ServerInstance $server -Database $db -Query $query
                $tables | ForEach-Object { $_.Column1 } | 
                    Where-Object { $_ -like "$wordToComplete*" }
            }
        })]
        [string]$TableName
    )
    
    # Function body
}
```

## Section 5: SQL Server Integration

### 5.1 SqlServer Module Setup

```powershell
# Check and install SqlServer module
if (-not (Get-Module -ListAvailable -Name SqlServer)) {
    Write-Host "Installing SqlServer module..."
    Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser
}

Import-Module SqlServer -Force

# Verify module loaded
if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
    throw "SqlServer module not loaded correctly"
}
```

### 5.2 Invoke-Sqlcmd Patterns

```powershell
# Basic query
$results = Invoke-Sqlcmd -ServerInstance "localhost" `
                         -Database "MyDB" `
                         -Query "SELECT * FROM Users"

# With Windows Authentication (default)
$results = Invoke-Sqlcmd -ServerInstance "server\instance" `
                         -Database "MyDB" `
                         -Query "SELECT 1"

# With SQL Authentication (use SecureString!)
$securePassword = ConvertTo-SecureString "Password123" -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential("sa", $securePassword)

$results = Invoke-Sqlcmd -ServerInstance "server" `
                         -Database "MyDB" `
                         -Credential $credential `
                         -Query "SELECT 1"

# Execute SQL file
Invoke-Sqlcmd -ServerInstance "server" `
              -Database "MyDB" `
              -InputFile "C:\Scripts\schema.sql"

# With variables
$variables = @("SchemaName=dbo", "TableName=Users")
Invoke-Sqlcmd -ServerInstance "server" `
              -Database "MyDB" `
              -Query 'SELECT * FROM [$(SchemaName)].[$(TableName)]' `
              -Variable $variables

# With timeout
Invoke-Sqlcmd -ServerInstance "server" `
              -Database "MyDB" `
              -Query "EXEC LongRunningProc" `
              -QueryTimeout 600  # 10 minutes

# Output errors properly
$results = Invoke-Sqlcmd -ServerInstance "server" `
                         -Database "MyDB" `
                         -Query "EXEC ProcWithErrors" `
                         -OutputSqlErrors $true `
                         -ErrorAction Stop
```

### 5.3 Parameterized Queries (SQL Injection Prevention)

```powershell
function Get-UserByEmail {
    param(
        [Parameter(Mandatory)]
        [string]$ServerInstance,
        
        [Parameter(Mandatory)]
        [string]$Database,
        
        [Parameter(Mandatory)]
        [ValidatePattern('^[\w\.\-]+@[\w\.\-]+\.\w+$')]
        [string]$Email
    )
    
    # WRONG - SQL Injection vulnerable!
    # $query = "SELECT * FROM Users WHERE Email = '$Email'"
    
    # CORRECT - Use SqlClient for parameterized queries
    $connectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    
    try {
        $connection.Open()
        
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT * FROM Users WHERE Email = @Email"
        $command.Parameters.AddWithValue("@Email", $Email) | Out-Null
        
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $dataSet = New-Object System.Data.DataSet
        $adapter.Fill($dataSet) | Out-Null
        
        return $dataSet.Tables[0]
    }
    finally {
        $connection.Close()
        $connection.Dispose()
    }
}
```

## Section 6: File Operations & Backup Patterns

### 6.1 Safe File Operations

```powershell
function Backup-AndReplace {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path $_ })]
        [string]$SourcePath,
        
        [Parameter(Mandatory)]
        [string]$Content,
        
        [string]$BackupSuffix = ".backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    )
    
    $backupPath = "$SourcePath$BackupSuffix"
    
    try {
        # Create backup
        if ($PSCmdlet.ShouldProcess($SourcePath, "Backup to $backupPath")) {
            Copy-Item -Path $SourcePath -Destination $backupPath -Force
            Write-Verbose "Backup created: $backupPath"
        }
        
        # Write new content
        if ($PSCmdlet.ShouldProcess($SourcePath, "Replace content")) {
            Set-Content -Path $SourcePath -Value $Content -Encoding UTF8
            Write-Verbose "Content replaced in: $SourcePath"
        }
        
        return @{
            Success = $true
            BackupPath = $backupPath
            OriginalPath = $SourcePath
        }
    }
    catch {
        # Restore from backup on failure
        if (Test-Path $backupPath) {
            Copy-Item -Path $backupPath -Destination $SourcePath -Force
            Write-Warning "Restored from backup due to error"
        }
        throw
    }
}
```

### 6.2 Cleanup Old Backup Files

```powershell
function Remove-OldBackups {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        
        [Parameter()]
        [string]$Pattern = "*.backup_*",
        
        [Parameter()]
        [int]$RetentionDays = 7
    )
    
    $cutoffDate = (Get-Date).AddDays(-$RetentionDays)
    
    Get-ChildItem -Path $Path -Filter $Pattern -File |
        Where-Object { $_.LastWriteTime -lt $cutoffDate } |
        ForEach-Object {
            if ($PSCmdlet.ShouldProcess($_.FullName, "Delete old backup")) {
                Remove-Item $_.FullName -Force
                Write-Verbose "Deleted: $($_.FullName)"
            }
        }
}
```

## Section 7: Common Pitfalls & Solutions

### 7.1 Here-String Indentation (THE #1 ISSUE)

```powershell
# ❌ WRONG - Closing "@ is indented
function Test {
    $sql = @"
SELECT 1
    "@  # Parser sees this as content, not closing marker!
}

# ✅ CORRECT - Closing "@ at column 0
function Test {
    $sql = @"
SELECT 1
"@
}
```

### 7.2 Comparison Operators

```powershell
# PowerShell uses -eq, -ne, -gt, etc. (not ==, !=, >)

# ❌ WRONG
if ($value == 5) { }      # This doesn't work as expected!
if ($name != "test") { }

# ✅ CORRECT
if ($value -eq 5) { }
if ($name -ne "test") { }
if ($count -gt 0) { }
if ($count -lt 10) { }
if ($count -ge 1 -and $count -le 10) { }

# String comparison (case-insensitive by default)
"Hello" -eq "hello"      # True
"Hello" -ceq "hello"     # False (case-sensitive)
"Hello" -like "Hel*"     # True (wildcard)
"Hello" -match "^Hel"    # True (regex)
```

### 7.3 Array Handling

```powershell
# Single item from cmdlet becomes scalar, not array!
$files = Get-ChildItem -Filter "*.txt"  # Might be 0, 1, or many items

# ❌ WRONG - Fails if only 1 file
$files.Count  # Returns nothing for single item

# ✅ CORRECT - Force array
$files = @(Get-ChildItem -Filter "*.txt")
$files.Count  # Always works

# Or use array subexpression
$files = @(Get-ChildItem -Filter "*.txt")
foreach ($file in $files) { }
```

### 7.4 Null Handling

```powershell
# ❌ WRONG - Null on left side always true!
if ($null -eq $value) { }  # Not reliable

# ✅ CORRECT - Put $null on right side
if ($value -eq $null) { }

# Or use test methods
if ([string]::IsNullOrEmpty($value)) { }
if ([string]::IsNullOrWhiteSpace($value)) { }

# Null coalescing (PowerShell 7+)
$result = $value ?? "default"

# PowerShell 5.1 equivalent
$result = if ($null -ne $value) { $value } else { "default" }
```

### 7.5 Scope Issues

```powershell
# Variables in child scopes don't modify parent by default
$count = 0

1..5 | ForEach-Object {
    $count++  # This creates a NEW local variable!
}

Write-Host $count  # Still 0!

# ✅ CORRECT - Use script scope
$script:count = 0

1..5 | ForEach-Object {
    $script:count++
}

Write-Host $script:count  # 5

# Or use a reference type
$counter = [ref]0
1..5 | ForEach-Object {
    $counter.Value++
}
Write-Host $counter.Value  # 5
```

### 7.6 Pipeline vs ForEach-Object vs foreach

```powershell
# Pipeline (streaming, memory efficient)
Get-Content "large.txt" | ForEach-Object { Process-Line $_ }

# ForEach-Object (pipeline, slower per item)
1..100 | ForEach-Object { $_ * 2 }

# foreach statement (faster, loads all into memory)
foreach ($item in Get-Content "small.txt") {
    Process-Line $item
}

# For large files, use pipeline or .NET reader
$reader = [System.IO.File]::OpenText("huge.txt")
try {
    while ($null -ne ($line = $reader.ReadLine())) {
        Process-Line $line
    }
}
finally {
    $reader.Close()
}
```

## Section 8: Module Development

### 8.1 Module Structure

```
MyModule/
├── MyModule.psd1          # Module manifest
├── MyModule.psm1          # Module script
├── Public/                # Exported functions
│   ├── Get-Something.ps1
│   └── Set-Something.ps1
├── Private/               # Internal functions
│   └── Helper-Functions.ps1
└── Tests/
    └── MyModule.Tests.ps1
```

### 8.2 Module Manifest (PSD1)

```powershell
# Create manifest
New-ModuleManifest -Path "MyModule.psd1" `
    -RootModule "MyModule.psm1" `
    -ModuleVersion "1.0.0" `
    -Author "Your Name" `
    -Description "Module description" `
    -PowerShellVersion "5.1" `
    -FunctionsToExport @('Get-Something', 'Set-Something')
```

### 8.3 Module Script (PSM1)

```powershell
# MyModule.psm1

# Get public and private function files
$Public = @(Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 -ErrorAction SilentlyContinue)
$Private = @(Get-ChildItem -Path $PSScriptRoot\Private\*.ps1 -ErrorAction SilentlyContinue)

# Dot source the files
foreach ($import in @($Public + $Private)) {
    try {
        . $import.FullName
    }
    catch {
        Write-Error "Failed to import function $($import.FullName): $_"
    }
}

# Export public functions
Export-ModuleMember -Function $Public.BaseName
```

## Section 9: Quick Reference - Safe Patterns

### 9.1 SQL Script Execution Template

```powershell
#Requires -Version 5.1
#Requires -Modules SqlServer

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ServerInstance,
    
    [Parameter(Mandatory)]
    [string]$Database
)

$ErrorActionPreference = 'Stop'

# SQL without here-string (safer)
$createTableSql = @(
    "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MyTable')",
    "BEGIN",
    "    CREATE TABLE [dbo].[MyTable] (",
    "        [Id] INT IDENTITY(1,1) PRIMARY KEY,",
    "        [Name] NVARCHAR(100) NOT NULL",
    "    )",
    "END"
) -join "`n"

try {
    Write-Host "Executing SQL on $ServerInstance/$Database..."
    Invoke-Sqlcmd -ServerInstance $ServerInstance -Database $Database -Query $createTableSql
    Write-Host "Success!" -ForegroundColor Green
}
catch {
    Write-Host "Failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
```

### 9.2 File Deployment Template

```powershell
#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ })]
    [string]$SourceFile,
    
    [Parameter(Mandatory)]
    [string]$DestinationPath
)

$ErrorActionPreference = 'Stop'

try {
    # Ensure destination directory exists
    $destDir = Split-Path $DestinationPath -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -Path $destDir -ItemType Directory -Force | Out-Null
    }
    
    # Backup if exists
    if (Test-Path $DestinationPath) {
        $backup = "$DestinationPath.bak_$(Get-Date -Format 'yyyyMMddHHmmss')"
        Copy-Item -Path $DestinationPath -Destination $backup -Force
        Write-Host "Backup created: $backup"
    }
    
    # Deploy
    Copy-Item -Path $SourceFile -Destination $DestinationPath -Force
    Write-Host "Deployed: $DestinationPath" -ForegroundColor Green
}
catch {
    Write-Host "Deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
```

## Section 10: Troubleshooting Checklist

### When Script Fails to Parse

1. **Check here-string closing markers** - Must be at column 0
2. **Check quote matching** - Every `"` and `'` must be paired
3. **Check brace matching** - Every `{` needs `}`
4. **Check parentheses** - Every `(` needs `)`
5. **Run `Test-ScriptFileInfo`** or use VSCode PowerShell extension

### When Cmdlet Errors Are Not Caught

1. Add `-ErrorAction Stop` to the cmdlet
2. Check `$ErrorActionPreference` is set to `'Stop'`
3. Ensure try/catch syntax is correct

### When Variables Are Empty/Wrong

1. Check variable scope (`$script:`, `$global:`)
2. Check if variable is being reassigned in child scope
3. Use `Set-StrictMode -Version Latest` to catch undefined variables

---

## Version History

- **1.0.0** (2026-01-03): Initial release with comprehensive patterns
