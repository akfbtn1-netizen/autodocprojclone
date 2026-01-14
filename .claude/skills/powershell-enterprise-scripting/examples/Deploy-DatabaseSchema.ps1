#Requires -Version 5.1
#Requires -Modules SqlServer

<#
.SYNOPSIS
    Deploys database schema changes with backup and rollback support.

.DESCRIPTION
    Production-ready deployment script demonstrating:
    - Proper here-string usage
    - Error handling with try/catch/finally
    - Parameter validation
    - SQL Server integration
    - Backup and rollback patterns

.PARAMETER ServerInstance
    The SQL Server instance name (e.g., "localhost" or "server\instance").

.PARAMETER Database
    The target database name.

.PARAMETER ScriptPath
    Optional path to SQL script file. If not provided, uses embedded SQL.

.PARAMETER BackupPath
    Directory for schema backups. Defaults to current directory.

.PARAMETER WhatIf
    Shows what would happen without making changes.

.EXAMPLE
    .\Deploy-DatabaseSchema.ps1 -ServerInstance "localhost" -Database "MyDB"

.EXAMPLE
    .\Deploy-DatabaseSchema.ps1 -ServerInstance "prod-server" -Database "MyDB" -WhatIf

.NOTES
    Author: Enterprise Documentation Platform
    Version: 1.0.0
    Date: 2026-01-03
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
    [ValidateScript({
        if ($_ -and -not (Test-Path $_)) {
            throw "Script file not found: $_"
        }
        $true
    })]
    [string]$ScriptPath,

    [Parameter(Mandatory = $false)]
    [ValidateScript({
        if (-not (Test-Path $_)) {
            New-Item -Path $_ -ItemType Directory -Force | Out-Null
        }
        $true
    })]
    [string]$BackupPath = ".",

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

#region Configuration
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:StartTime = Get-Date
$script:LogFile = Join-Path $BackupPath "deploy_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
#endregion

#region Functions
function Write-Log {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Message,

        [Parameter()]
        [ValidateSet('Info', 'Warning', 'Error', 'Success', 'Debug')]
        [string]$Level = 'Info'
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $logMessage = "[$timestamp] [$Level] $Message"

    # Console output with colors
    $color = switch ($Level) {
        'Info'    { 'White' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
        'Success' { 'Green' }
        'Debug'   { 'Cyan' }
    }

    Write-Host $logMessage -ForegroundColor $color

    # File output
    Add-Content -Path $script:LogFile -Value $logMessage -ErrorAction SilentlyContinue
}

function Test-DatabaseConnection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ServerInstance,

        [Parameter(Mandatory)]
        [string]$Database
    )

    Write-Log "Testing connection to $ServerInstance/$Database..." -Level Debug

    try {
        $result = Invoke-Sqlcmd -ServerInstance $ServerInstance `
                               -Database $Database `
                               -Query "SELECT 1 AS ConnectionTest" `
                               -QueryTimeout 10 `
                               -ErrorAction Stop

        if ($result.ConnectionTest -eq 1) {
            Write-Log "Connection successful" -Level Success
            return $true
        }
    }
    catch {
        Write-Log "Connection failed: $($_.Exception.Message)" -Level Error
        return $false
    }

    return $false
}

function Backup-CurrentSchema {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ServerInstance,

        [Parameter(Mandatory)]
        [string]$Database,

        [Parameter(Mandatory)]
        [string]$BackupPath
    )

    $backupFile = Join-Path $BackupPath "schema_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql"
    Write-Log "Creating schema backup: $backupFile" -Level Info

    # Query to get current schema (simplified example)
    # NOTE: Closing "@ MUST be at column 0!
    $schemaQuery = @"
SELECT 
    'IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = ''' + t.name + ''')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    -- Table: ' + t.name + CHAR(13) + CHAR(10) +
    'END' AS SchemaScript
FROM sys.tables t
WHERE t.is_ms_shipped = 0
ORDER BY t.name
"@

    try {
        $schemaScripts = Invoke-Sqlcmd -ServerInstance $ServerInstance `
                                       -Database $Database `
                                       -Query $schemaQuery `
                                       -ErrorAction Stop

        if ($schemaScripts) {
            $schemaScripts | ForEach-Object { $_.SchemaScript } | 
                Set-Content -Path $backupFile -Encoding UTF8
            Write-Log "Schema backup created successfully" -Level Success
        }
        else {
            Write-Log "No schema objects found to backup" -Level Warning
        }

        return $backupFile
    }
    catch {
        Write-Log "Schema backup failed: $($_.Exception.Message)" -Level Error
        throw
    }
}

function Get-DeploymentSql {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$ScriptPath
    )

    if ($ScriptPath -and (Test-Path $ScriptPath)) {
        Write-Log "Loading SQL from file: $ScriptPath" -Level Info
        return Get-Content -Path $ScriptPath -Raw
    }

    Write-Log "Using embedded deployment SQL" -Level Info

    # Example embedded SQL - closing "@ at column 0!
    $sql = @"
-- =============================================
-- Deployment Script
-- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
-- =============================================

PRINT 'Starting deployment...';

-- Create DocumentChanges table if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentChanges')
BEGIN
    CREATE TABLE [dbo].[DocumentChanges] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [ChangeType] NVARCHAR(50) NOT NULL,
        [ObjectName] NVARCHAR(256) NOT NULL,
        [ChangeDescription] NVARCHAR(MAX),
        [ChangedBy] NVARCHAR(100),
        [ChangedAt] DATETIME2 DEFAULT GETUTCDATE(),
        [IsProcessed] BIT DEFAULT 0
    );
    PRINT 'Created table: DocumentChanges';
END
ELSE
BEGIN
    PRINT 'Table already exists: DocumentChanges';
END

-- Create MasterIndex table if not exists  
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MasterIndex')
BEGIN
    CREATE TABLE [dbo].[MasterIndex] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [SchemaName] NVARCHAR(128) NOT NULL,
        [TableName] NVARCHAR(128) NOT NULL,
        [ColumnName] NVARCHAR(128),
        [DataType] NVARCHAR(128),
        [Description] NVARCHAR(MAX),
        [BusinessDefinition] NVARCHAR(MAX),
        [IsPII] BIT DEFAULT 0,
        [LastUpdated] DATETIME2 DEFAULT GETUTCDATE(),
        CONSTRAINT [UQ_MasterIndex_Schema_Table_Column] 
            UNIQUE ([SchemaName], [TableName], [ColumnName])
    );
    PRINT 'Created table: MasterIndex';
END
ELSE
BEGIN
    PRINT 'Table already exists: MasterIndex';
END

PRINT 'Deployment completed successfully';
"@

    return $sql
}

function Invoke-Deployment {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [string]$ServerInstance,

        [Parameter(Mandatory)]
        [string]$Database,

        [Parameter(Mandatory)]
        [string]$Sql
    )

    if ($PSCmdlet.ShouldProcess("$ServerInstance/$Database", "Execute deployment SQL")) {
        Write-Log "Executing deployment SQL..." -Level Info

        try {
            # Split by GO statements for batch execution
            $batches = $Sql -split '\r?\nGO\r?\n'

            foreach ($batch in $batches) {
                $trimmed = $batch.Trim()
                if ($trimmed) {
                    Invoke-Sqlcmd -ServerInstance $ServerInstance `
                                  -Database $Database `
                                  -Query $trimmed `
                                  -QueryTimeout 300 `
                                  -ErrorAction Stop `
                                  -Verbose:$false
                }
            }

            Write-Log "Deployment SQL executed successfully" -Level Success
            return $true
        }
        catch {
            Write-Log "Deployment failed: $($_.Exception.Message)" -Level Error
            throw
        }
    }
    else {
        Write-Log "WhatIf: Would execute deployment SQL" -Level Info
        return $true
    }
}
#endregion

#region Main Script
Write-Log "========================================" -Level Info
Write-Log "Database Schema Deployment" -Level Info
Write-Log "========================================" -Level Info
Write-Log "Server: $ServerInstance" -Level Info
Write-Log "Database: $Database" -Level Info
Write-Log "Backup Path: $BackupPath" -Level Info
Write-Log "----------------------------------------" -Level Info

$backupFile = $null

try {
    # Step 1: Test connection
    if (-not (Test-DatabaseConnection -ServerInstance $ServerInstance -Database $Database)) {
        throw "Cannot connect to database. Aborting deployment."
    }

    # Step 2: Create backup
    if (-not $WhatIfPreference) {
        $backupFile = Backup-CurrentSchema -ServerInstance $ServerInstance `
                                           -Database $Database `
                                           -BackupPath $BackupPath
    }

    # Step 3: Get deployment SQL
    $deploymentSql = Get-DeploymentSql -ScriptPath $ScriptPath

    # Step 4: Execute deployment
    $success = Invoke-Deployment -ServerInstance $ServerInstance `
                                 -Database $Database `
                                 -Sql $deploymentSql

    if ($success) {
        Write-Log "========================================" -Level Success
        Write-Log "Deployment completed successfully!" -Level Success
        Write-Log "========================================" -Level Success
    }
}
catch {
    Write-Log "========================================" -Level Error
    Write-Log "DEPLOYMENT FAILED" -Level Error
    Write-Log "========================================" -Level Error
    Write-Log "Error: $($_.Exception.Message)" -Level Error
    Write-Log "Stack Trace: $($_.ScriptStackTrace)" -Level Error

    if ($backupFile -and (Test-Path $backupFile)) {
        Write-Log "Backup available for rollback: $backupFile" -Level Warning
    }

    exit 1
}
finally {
    $duration = (Get-Date) - $script:StartTime
    Write-Log "----------------------------------------" -Level Info
    Write-Log "Total execution time: $($duration.ToString('hh\:mm\:ss'))" -Level Info
    Write-Log "Log file: $script:LogFile" -Level Info
}
#endregion
