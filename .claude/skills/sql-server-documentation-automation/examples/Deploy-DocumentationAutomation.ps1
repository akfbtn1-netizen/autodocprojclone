# Deploy-DocumentationAutomation.ps1
# Comprehensive deployment script for SQL Server Documentation Automation
# Version: 2.0
# Requires: PowerShell 5.1+, SQL Server 2019+, Python 3.9+

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$SqlServer,
    
    [Parameter(Mandatory=$true)]
    [string]$Database,
    
    [Parameter(Mandatory=$false)]
    [string]$Schema = "dbo",
    
    [Parameter(Mandatory=$false)]
    [switch]$UseIntegratedSecurity,
    
    [Parameter(Mandatory=$false)]
    [PSCredential]$SqlCredential,
    
    [Parameter(Mandatory=$false)]
    [string]$AzureOpenAIEndpoint,
    
    [Parameter(Mandatory=$false)]
    [string]$AzureOpenAIKey,
    
    [Parameter(Mandatory=$false)]
    [switch]$InstallMCPServer,
    
    [Parameter(Mandatory=$false)]
    [switch]$SetupDDLTriggers,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateInitialDocs
)

$ErrorActionPreference = "Stop"
$Script:LogFile = Join-Path $PSScriptRoot "deployment_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage -ForegroundColor $(switch($Level) { "ERROR" { "Red" } "WARN" { "Yellow" } "SUCCESS" { "Green" } default { "White" } })
    Add-Content -Path $Script:LogFile -Value $logMessage
}

function Get-SqlConnection {
    if ($UseIntegratedSecurity) {
        return "Server=$SqlServer;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
    } else {
        $user = $SqlCredential.UserName
        $pass = $SqlCredential.GetNetworkCredential().Password
        return "Server=$SqlServer;Database=$Database;User Id=$user;Password=$pass;TrustServerCertificate=True"
    }
}

function Invoke-SqlScript {
    param(
        [string]$Query,
        [string]$Description
    )
    
    Write-Log "Executing: $Description"
    
    try {
        $connectionString = Get-SqlConnection
        $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $connection.Open()
        
        $command = New-Object System.Data.SqlClient.SqlCommand($Query, $connection)
        $command.CommandTimeout = 300
        $result = $command.ExecuteNonQuery()
        
        $connection.Close()
        Write-Log "Success: $Description" -Level "SUCCESS"
        return $result
    }
    catch {
        Write-Log "Failed: $Description - $($_.Exception.Message)" -Level "ERROR"
        throw
    }
}

function Install-DatabaseObjects {
    Write-Log "Installing database objects..."
    
    # Create schema change log table
    $createSchemaChangeLog = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SchemaChangeLog' AND schema_id = SCHEMA_ID('$Schema'))
BEGIN
    CREATE TABLE [$Schema].[SchemaChangeLog] (
        ChangeId INT IDENTITY(1,1) PRIMARY KEY,
        EventType NVARCHAR(100) NOT NULL,
        ObjectName NVARCHAR(256) NOT NULL,
        ObjectType NVARCHAR(50),
        SchemaName NVARCHAR(128),
        TSQLCommand NVARCHAR(MAX),
        LoginName NVARCHAR(128),
        HostName NVARCHAR(128),
        EventDate DATETIME2 DEFAULT GETDATE(),
        EventXML XML,
        DocumentationStatus NVARCHAR(50) DEFAULT 'Pending',
        ProcessedDate DATETIME2 NULL,
        INDEX IX_SchemaChangeLog_Date (EventDate DESC),
        INDEX IX_SchemaChangeLog_Status (DocumentationStatus)
    );
    PRINT 'Created SchemaChangeLog table';
END
ELSE
    PRINT 'SchemaChangeLog table already exists';
"@
    Invoke-SqlScript -Query $createSchemaChangeLog -Description "Create SchemaChangeLog table"
    
    # Create documentation audit log
    $createDocAuditLog = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentationAuditLog' AND schema_id = SCHEMA_ID('$Schema'))
BEGIN
    CREATE TABLE [$Schema].[DocumentationAuditLog] (
        AuditId INT IDENTITY(1,1) PRIMARY KEY,
        SchemaName NVARCHAR(128) NOT NULL,
        ObjectName NVARCHAR(128) NOT NULL,
        ObjectType NVARCHAR(50),
        ColumnName NVARCHAR(128),
        PreviousDescription NVARCHAR(MAX),
        NewDescription NVARCHAR(MAX),
        UpdatedBy NVARCHAR(128) DEFAULT SYSTEM_USER,
        UpdatedAt DATETIME2 DEFAULT GETDATE(),
        UpdateSource NVARCHAR(50) DEFAULT 'Manual', -- 'Manual', 'AI', 'Import'
        INDEX IX_DocAuditLog_Object (SchemaName, ObjectName),
        INDEX IX_DocAuditLog_Date (UpdatedAt DESC)
    );
    PRINT 'Created DocumentationAuditLog table';
END
"@
    Invoke-SqlScript -Query $createDocAuditLog -Description "Create DocumentationAuditLog table"
    
    # Create documentation update procedure
    $createUpdateProc = @"
CREATE OR ALTER PROCEDURE [$Schema].[usp_UpdateDocumentation]
    @SchemaName NVARCHAR(128),
    @ObjectName NVARCHAR(128),
    @ObjectType NVARCHAR(50),
    @ColumnName NVARCHAR(128) = NULL,
    @Description NVARCHAR(MAX),
    @UpdateSource NVARCHAR(50) = 'Manual'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @PreviousDesc NVARCHAR(MAX);
    DECLARE @ObjectId INT;
    DECLARE @ColumnId INT = 0;
    
    -- Get object ID
    SET @ObjectId = OBJECT_ID(@SchemaName + '.' + @ObjectName);
    
    IF @ObjectId IS NULL
    BEGIN
        RAISERROR('Object not found: %s.%s', 16, 1, @SchemaName, @ObjectName);
        RETURN;
    END
    
    -- Get column ID if specified
    IF @ColumnName IS NOT NULL
    BEGIN
        SELECT @ColumnId = column_id 
        FROM sys.columns 
        WHERE object_id = @ObjectId AND name = @ColumnName;
        
        IF @ColumnId IS NULL
        BEGIN
            RAISERROR('Column not found: %s', 16, 1, @ColumnName);
            RETURN;
        END
    END
    
    -- Get previous description
    SELECT @PreviousDesc = CAST(value AS NVARCHAR(MAX))
    FROM sys.extended_properties
    WHERE major_id = @ObjectId 
      AND minor_id = @ColumnId
      AND name = 'MS_Description';
    
    -- Update or add extended property
    IF @PreviousDesc IS NOT NULL
    BEGIN
        IF @ColumnName IS NULL
            EXEC sp_updateextendedproperty 
                @name = N'MS_Description', @value = @Description,
                @level0type = N'SCHEMA', @level0name = @SchemaName,
                @level1type = @ObjectType, @level1name = @ObjectName;
        ELSE
            EXEC sp_updateextendedproperty 
                @name = N'MS_Description', @value = @Description,
                @level0type = N'SCHEMA', @level0name = @SchemaName,
                @level1type = N'TABLE', @level1name = @ObjectName,
                @level2type = N'COLUMN', @level2name = @ColumnName;
    END
    ELSE
    BEGIN
        IF @ColumnName IS NULL
            EXEC sp_addextendedproperty 
                @name = N'MS_Description', @value = @Description,
                @level0type = N'SCHEMA', @level0name = @SchemaName,
                @level1type = @ObjectType, @level1name = @ObjectName;
        ELSE
            EXEC sp_addextendedproperty 
                @name = N'MS_Description', @value = @Description,
                @level0type = N'SCHEMA', @level0name = @SchemaName,
                @level1type = N'TABLE', @level1name = @ObjectName,
                @level2type = N'COLUMN', @level2name = @ColumnName;
    END
    
    -- Log the update
    INSERT INTO [$Schema].[DocumentationAuditLog] 
        (SchemaName, ObjectName, ObjectType, ColumnName, PreviousDescription, NewDescription, UpdateSource)
    VALUES 
        (@SchemaName, @ObjectName, @ObjectType, @ColumnName, @PreviousDesc, @Description, @UpdateSource);
    
    SELECT 'Documentation updated successfully' AS Result;
END;
"@
    Invoke-SqlScript -Query $createUpdateProc -Description "Create usp_UpdateDocumentation procedure"
    
    # Create documentation coverage view
    $createCoverageView = @"
CREATE OR ALTER VIEW [$Schema].[vw_DocumentationCoverage] AS
WITH ObjectDocs AS (
    SELECT 
        s.name AS SchemaName,
        o.name AS ObjectName,
        o.type_desc AS ObjectType,
        CASE WHEN ep.value IS NOT NULL THEN 1 ELSE 0 END AS IsDocumented,
        o.create_date AS CreatedDate,
        o.modify_date AS ModifiedDate
    FROM sys.objects o
    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = o.object_id 
        AND ep.minor_id = 0 
        AND ep.name = 'MS_Description'
    WHERE o.is_ms_shipped = 0
      AND o.type IN ('U', 'V', 'P', 'FN', 'TF', 'IF')
),
ColumnDocs AS (
    SELECT 
        s.name AS SchemaName,
        t.name AS TableName,
        COUNT(*) AS TotalColumns,
        SUM(CASE WHEN ep.value IS NOT NULL THEN 1 ELSE 0 END) AS DocumentedColumns
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
    LEFT JOIN sys.extended_properties ep 
        ON ep.major_id = c.object_id 
        AND ep.minor_id = c.column_id 
        AND ep.name = 'MS_Description'
    GROUP BY s.name, t.name
)
SELECT 
    od.SchemaName,
    od.ObjectType,
    COUNT(*) AS TotalObjects,
    SUM(od.IsDocumented) AS DocumentedObjects,
    CAST(100.0 * SUM(od.IsDocumented) / COUNT(*) AS DECIMAL(5,2)) AS ObjectCoveragePercent,
    COALESCE(SUM(cd.TotalColumns), 0) AS TotalColumns,
    COALESCE(SUM(cd.DocumentedColumns), 0) AS DocumentedColumns,
    CASE 
        WHEN COALESCE(SUM(cd.TotalColumns), 0) = 0 THEN 0
        ELSE CAST(100.0 * COALESCE(SUM(cd.DocumentedColumns), 0) / SUM(cd.TotalColumns) AS DECIMAL(5,2))
    END AS ColumnCoveragePercent
FROM ObjectDocs od
LEFT JOIN ColumnDocs cd ON od.SchemaName = cd.SchemaName AND od.ObjectName = cd.TableName
GROUP BY od.SchemaName, od.ObjectType;
"@
    Invoke-SqlScript -Query $createCoverageView -Description "Create vw_DocumentationCoverage view"
}

function Install-DDLTriggers {
    Write-Log "Installing DDL triggers for schema change detection..."
    
    $createDDLTrigger = @"
CREATE OR ALTER TRIGGER [TR_DDL_SchemaChangeCapture]
ON DATABASE
FOR CREATE_TABLE, ALTER_TABLE, DROP_TABLE,
    CREATE_VIEW, ALTER_VIEW, DROP_VIEW,
    CREATE_PROCEDURE, ALTER_PROCEDURE, DROP_PROCEDURE,
    CREATE_FUNCTION, ALTER_FUNCTION, DROP_FUNCTION
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @EventData XML = EVENTDATA();
    DECLARE @EventType NVARCHAR(100) = @EventData.value('(/EVENT_INSTANCE/EventType)[1]', 'NVARCHAR(100)');
    DECLARE @ObjectName NVARCHAR(256) = @EventData.value('(/EVENT_INSTANCE/ObjectName)[1]', 'NVARCHAR(256)');
    DECLARE @ObjectType NVARCHAR(50) = @EventData.value('(/EVENT_INSTANCE/ObjectType)[1]', 'NVARCHAR(50)');
    DECLARE @SchemaName NVARCHAR(128) = @EventData.value('(/EVENT_INSTANCE/SchemaName)[1]', 'NVARCHAR(128)');
    DECLARE @TSQLCommand NVARCHAR(MAX) = @EventData.value('(/EVENT_INSTANCE/TSQLCommand/CommandText)[1]', 'NVARCHAR(MAX)');
    DECLARE @LoginName NVARCHAR(128) = @EventData.value('(/EVENT_INSTANCE/LoginName)[1]', 'NVARCHAR(128)');
    
    INSERT INTO [$Schema].[SchemaChangeLog] (
        EventType, ObjectName, ObjectType, SchemaName, 
        TSQLCommand, LoginName, HostName, EventXML
    )
    VALUES (
        @EventType, @ObjectName, @ObjectType, @SchemaName,
        @TSQLCommand, @LoginName, HOST_NAME(), @EventData
    );
END;
"@
    Invoke-SqlScript -Query $createDDLTrigger -Description "Create DDL trigger for schema changes"
}

function Install-MCPServer {
    Write-Log "Installing MCP server for AI integration..."
    
    # Check Node.js
    $nodeVersion = & node --version 2>$null
    if (-not $nodeVersion) {
        Write-Log "Node.js not found. Please install Node.js 18+ first." -Level "ERROR"
        return
    }
    Write-Log "Found Node.js: $nodeVersion"
    
    # Install MCP server package
    Write-Log "Installing @executeautomation/database-server..."
    & npm install -g @executeautomation/database-server
    
    # Create MCP configuration
    $mcpConfig = @{
        mcpServers = @{
            mssql = @{
                command = "npx"
                args = @("-y", "@executeautomation/database-server")
                env = @{
                    MSSQL_CONNECTION_STRING = Get-SqlConnection
                }
            }
        }
    }
    
    # Determine config path based on OS
    if ($IsWindows -or $env:OS -match "Windows") {
        $configPath = "$env:APPDATA\Claude\claude_desktop_config.json"
    } else {
        $configPath = "$HOME/Library/Application Support/Claude/claude_desktop_config.json"
    }
    
    $configDir = Split-Path $configPath -Parent
    if (-not (Test-Path $configDir)) {
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    }
    
    # Merge with existing config if present
    if (Test-Path $configPath) {
        $existingConfig = Get-Content $configPath | ConvertFrom-Json -AsHashtable
        $existingConfig.mcpServers = $mcpConfig.mcpServers
        $mcpConfig = $existingConfig
    }
    
    $mcpConfig | ConvertTo-Json -Depth 10 | Set-Content $configPath
    Write-Log "MCP configuration saved to: $configPath" -Level "SUCCESS"
}

function Generate-InitialDocumentation {
    Write-Log "Generating initial AI documentation..."
    
    if (-not $AzureOpenAIEndpoint -or -not $AzureOpenAIKey) {
        Write-Log "Azure OpenAI credentials not provided. Skipping AI documentation." -Level "WARN"
        return
    }
    
    # Get undocumented objects
    $getUndocumented = @"
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    OBJECT_DEFINITION(o.object_id) AS ObjectDefinition
FROM sys.objects o
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
LEFT JOIN sys.extended_properties ep 
    ON ep.major_id = o.object_id 
    AND ep.minor_id = 0 
    AND ep.name = 'MS_Description'
WHERE o.is_ms_shipped = 0
  AND o.type IN ('U', 'V', 'P', 'FN')
  AND ep.value IS NULL
ORDER BY o.type_desc, s.name, o.name;
"@
    
    $connectionString = Get-SqlConnection
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $command = New-Object System.Data.SqlClient.SqlCommand($getUndocumented, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null
    
    $objects = $dataset.Tables[0]
    Write-Log "Found $($objects.Rows.Count) undocumented objects"
    
    foreach ($row in $objects.Rows) {
        $schemaName = $row.SchemaName
        $objectName = $row.ObjectName
        $objectType = $row.ObjectType
        $definition = $row.ObjectDefinition
        
        Write-Log "Generating documentation for $schemaName.$objectName ($objectType)..."
        
        # Call Azure OpenAI
        $prompt = @"
Generate a concise technical description (max 500 characters) for this SQL Server $objectType.
Focus on: purpose, key functionality, and business context.

Object: $schemaName.$objectName

$(if ($definition) { "Definition:`n$definition" } else { "Table structure available in database" })
"@
        
        try {
            $body = @{
                messages = @(
                    @{ role = "user"; content = $prompt }
                )
                max_tokens = 200
                temperature = 0.3
            } | ConvertTo-Json -Depth 5
            
            $response = Invoke-RestMethod `
                -Uri "$AzureOpenAIEndpoint/openai/deployments/gpt-4/chat/completions?api-version=2024-02-15-preview" `
                -Method POST `
                -Headers @{ "api-key" = $AzureOpenAIKey; "Content-Type" = "application/json" } `
                -Body $body
            
            $description = $response.choices[0].message.content
            
            # Update extended property
            $updateQuery = @"
EXEC [$Schema].[usp_UpdateDocumentation] 
    @SchemaName = '$schemaName',
    @ObjectName = '$objectName',
    @ObjectType = '$(switch($objectType) { "USER_TABLE" { "TABLE" } "VIEW" { "VIEW" } "SQL_STORED_PROCEDURE" { "PROCEDURE" } default { "TABLE" } })',
    @Description = N'$($description.Replace("'", "''"))',
    @UpdateSource = 'AI';
"@
            Invoke-SqlScript -Query $updateQuery -Description "Update $schemaName.$objectName"
            
            # Rate limiting
            Start-Sleep -Milliseconds 500
        }
        catch {
            Write-Log "Failed to generate docs for $schemaName.$objectName : $($_.Exception.Message)" -Level "WARN"
        }
    }
    
    $connection.Close()
}

function Show-DocumentationReport {
    Write-Log "Generating documentation coverage report..."
    
    $reportQuery = @"
SELECT * FROM [$Schema].[vw_DocumentationCoverage]
ORDER BY SchemaName, ObjectType;
"@
    
    $connectionString = Get-SqlConnection
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $command = New-Object System.Data.SqlClient.SqlCommand($reportQuery, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $adapter.Fill($dataset) | Out-Null
    
    Write-Host "`n============================================"
    Write-Host "DOCUMENTATION COVERAGE REPORT"
    Write-Host "============================================"
    Write-Host "Database: $Database"
    Write-Host "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Host "--------------------------------------------"
    
    $dataset.Tables[0] | Format-Table -AutoSize
    
    $connection.Close()
}

# Main execution
Write-Log "Starting SQL Server Documentation Automation Deployment"
Write-Log "Server: $SqlServer, Database: $Database"

try {
    # Install database objects
    Install-DatabaseObjects
    
    # Install DDL triggers if requested
    if ($SetupDDLTriggers) {
        Install-DDLTriggers
    }
    
    # Install MCP server if requested
    if ($InstallMCPServer) {
        Install-MCPServer
    }
    
    # Generate initial documentation if requested
    if ($GenerateInitialDocs) {
        Generate-InitialDocumentation
    }
    
    # Show coverage report
    Show-DocumentationReport
    
    Write-Log "Deployment completed successfully!" -Level "SUCCESS"
}
catch {
    Write-Log "Deployment failed: $($_.Exception.Message)" -Level "ERROR"
    throw
}

Write-Host "`nDeployment log saved to: $Script:LogFile"
