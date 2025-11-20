# Sync-ExcelToSql.ps1
# One-time sync from BI Analytics Change Spreadsheet to SQL Server

param(
    [string]$ExcelPath = "C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx",
    [string]$ServerName = "localhost",
    [string]$DatabaseName = "YourDatabaseName",
    [switch]$UseWindowsAuth
)

# Install ImportExcel module if needed
if (-not (Get-Module -ListAvailable -Name ImportExcel)) {
    Write-Host "Installing ImportExcel module..." -ForegroundColor Yellow
    Install-Module -Name ImportExcel -Force -Scope CurrentUser
}

Import-Module ImportExcel
Import-Module SqlServer -ErrorAction SilentlyContinue

# Verify Excel file exists
if (-not (Test-Path $ExcelPath)) {
    Write-Error "Excel file not found: $ExcelPath"
    exit 1
}

Write-Host "Reading Excel file: $ExcelPath" -ForegroundColor Cyan

# Read Excel data
$excelData = Import-Excel -Path $ExcelPath

Write-Host "Found $($excelData.Count) rows in Excel" -ForegroundColor Green

# Build connection string
if ($UseWindowsAuth) {
    $connectionString = "Server=$ServerName;Database=$DatabaseName;Integrated Security=True;TrustServerCertificate=True"
} else {
    $cred = Get-Credential -Message "Enter SQL Server credentials"
    $connectionString = "Server=$ServerName;Database=$DatabaseName;User Id=$($cred.UserName);Password=$($cred.GetNetworkCredential().Password);TrustServerCertificate=True"
}

# Connect to SQL
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()
Write-Host "Connected to SQL Server" -ForegroundColor Green

$inserted = 0
$skipped = 0
$errors = 0
$rowNum = 1

foreach ($row in $excelData) {
    $rowNum++

    # Skip empty rows
    if (-not $row.'CAB #' -and -not $row.'JIRA #' -and -not $row.Table) {
        continue
    }

    try {
        # Generate unique key for deduplication
        $uniqueKey = @($row.'CAB #', $row.Table, $row.Column) |
            Where-Object { $_ } |
            ForEach-Object { $_.ToString().Trim().ToUpper() }
        $uniqueKey = $uniqueKey -join "|"

        # Check if already exists
        $checkCmd = $connection.CreateCommand()
        $checkCmd.CommandText = "SELECT COUNT(*) FROM daqa.DocumentChanges WHERE UniqueKey = @UniqueKey"
        $checkCmd.Parameters.AddWithValue("@UniqueKey", $uniqueKey) | Out-Null
        $exists = $checkCmd.ExecuteScalar()

        if ($exists -gt 0) {
            $skipped++
            continue
        }

        # Generate content hash
        $hashContent = "$($row.'CAB #')|$($row.'JIRA #')|$($row.Table)|$($row.Column)|$($row.'Change Type')|$($row.Description)"
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($hashContent))
        $contentHash = [BitConverter]::ToString($hashBytes) -replace '-', ''

        # Insert
        $insertCmd = $connection.CreateCommand()
        $insertCmd.CommandText = @"
            INSERT INTO daqa.DocumentChanges (
                Date, JiraNumber, CABNumber, SprintNumber, Status, Priority, Severity,
                TableName, ColumnName, ChangeType, Description, ReportedBy, AssignedTo,
                Documentation, DocumentationLink, DocId, ExcelRowNumber, LastSyncedFromExcel,
                SyncStatus, UniqueKey, ContentHash
            ) VALUES (
                @Date, @JiraNumber, @CABNumber, @SprintNumber, @Status, @Priority, @Severity,
                @TableName, @ColumnName, @ChangeType, @Description, @ReportedBy, @AssignedTo,
                @Documentation, @DocumentationLink, @DocId, @ExcelRowNumber, GETUTCDATE(),
                'Success', @UniqueKey, @ContentHash
            )
"@

        # Add parameters (handle nulls)
        $insertCmd.Parameters.AddWithValue("@Date", $(if ($row.Date) { $row.Date } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@JiraNumber", $(if ($row.'JIRA #') { $row.'JIRA #' } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@CABNumber", $(if ($row.'CAB #') { $row.'CAB #' } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@SprintNumber", $(if ($row.'Sprint #') { $row.'Sprint #' } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@Status", $(if ($row.Status) { $row.Status } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@Priority", $(if ($row.Priority) { $row.Priority } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@Severity", $(if ($row.Severity) { $row.Severity } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@TableName", $(if ($row.Table) { $row.Table } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@ColumnName", $(if ($row.Column) { $row.Column } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@ChangeType", $(if ($row.'Change Type') { $row.'Change Type' } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@Description", $(if ($row.Description) { $row.Description } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@ReportedBy", $(if ($row.'Reported By') { $row.'Reported By' } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@AssignedTo", $(if ($row.'Assigned to') { $row.'Assigned to' } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@Documentation", $(if ($row.Documentation) { $row.Documentation } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@DocumentationLink", $(if ($row.'Documentation Link') { $row.'Documentation Link' } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@DocId", $(if ($row.DocId) { $row.DocId } else { [DBNull]::Value })) | Out-Null
        $insertCmd.Parameters.AddWithValue("@ExcelRowNumber", $rowNum) | Out-Null
        $insertCmd.Parameters.AddWithValue("@UniqueKey", $uniqueKey) | Out-Null
        $insertCmd.Parameters.AddWithValue("@ContentHash", $contentHash) | Out-Null

        $insertCmd.ExecuteNonQuery() | Out-Null
        $inserted++

    } catch {
        $errors++
        Write-Warning "Error on row $rowNum : $_"
    }
}

$connection.Close()

Write-Host "`n=== Sync Complete ===" -ForegroundColor Green
Write-Host "Inserted: $inserted" -ForegroundColor Cyan
Write-Host "Skipped (duplicates): $skipped" -ForegroundColor Yellow
Write-Host "Errors: $errors" -ForegroundColor $(if ($errors -gt 0) { "Red" } else { "Green" })
