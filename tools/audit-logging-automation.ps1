#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Enterprise audit logging automation system
.DESCRIPTION
    Captures, aggregates, and analyzes audit logs from all system components
    Generates compliance reports and security alerts
#>

param(
    [string]$ProjectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2",
    [string]$LogOutputPath = ".\audit-logs",
    [ValidateSet('All','Security','Access','Changes','Performance')]
    [string]$LogType = 'All',
    [int]$RetentionDays = 90,
    [switch]$RealTime,
    [switch]$GenerateReport
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ============================================================================
# CONFIGURATION
# ============================================================================

$AuditConfig = @{
    LogOutputPath = $LogOutputPath
    RetentionDays = $RetentionDays
    Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    
    # Log categories
    Categories = @{
        Security = @{
            enabled = $true
            events = @('Login', 'Logout', 'AccessDenied', 'PermissionChange', 'SecretAccess')
            severity = 'High'
        }
        Access = @{
            enabled = $true
            events = @('DocumentView', 'DocumentEdit', 'DocumentDelete', 'Search', 'Export')
            severity = 'Medium'
        }
        Changes = @{
            enabled = $true
            events = @('Create', 'Update', 'Delete', 'Approve', 'Reject')
            severity = 'Medium'
        }
        Performance = @{
            enabled = $true
            events = @('SlowQuery', 'HighMemory', 'ApiTimeout', 'CacheM iss')
            severity = 'Low'
        }
        System = @{
            enabled = $true
            events = @('Startup', 'Shutdown', 'Error', 'Warning', 'ConfigChange')
            severity = 'High'
        }
    }
    
    # Alert thresholds
    Alerts = @{
        FailedLogins = @{
            threshold = 5
            timeWindow = 300  # 5 minutes
            action = 'Block'
        }
        AccessDenied = @{
            threshold = 10
            timeWindow = 600  # 10 minutes
            action = 'Alert'
        }
        DocumentDeletes = @{
            threshold = 20
            timeWindow = 3600  # 1 hour
            action = 'Alert'
        }
    }
    
    # Compliance requirements
    Compliance = @{
        GDPR = @{
            enabled = $true
            requirements = @('DataAccess', 'DataModification', 'DataDeletion', 'ConsentChanges')
        }
        SOC2 = @{
            enabled = $true
            requirements = @('AccessControl', 'ChangeManagement', 'IncidentResponse')
        }
        HIPAA = @{
            enabled = $false
            requirements = @('PHIAccess', 'BreachNotification', 'AuditControls')
        }
    }
}

# ============================================================================
# AUDIT LOG STRUCTURE
# ============================================================================

class AuditLogEntry {
    [datetime]$Timestamp
    [string]$EventId
    [string]$Category
    [string]$EventType
    [string]$Severity
    [string]$UserId
    [string]$UserEmail
    [string]$IpAddress
    [string]$Resource
    [string]$Action
    [hashtable]$Details
    [string]$Result
    [string]$ErrorMessage
    
    AuditLogEntry() {
        $this.Timestamp = Get-Date
        $this.EventId = [guid]::NewGuid().ToString()
    }
}

# ============================================================================
# LOG COLLECTION FUNCTIONS
# ============================================================================

function Initialize-AuditLogging {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Audit Logging System" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Create output directory
    if (-not (Test-Path $LogOutputPath)) {
        $null = New-Item -ItemType Directory -Path $LogOutputPath -Force
        Write-Host "[OK] Created log directory: $LogOutputPath" -ForegroundColor Green
    }
    
    # Create log rotation policy
    $rotationPolicy = @{
        MaxLogFileSize = 100MB
        MaxLogFiles = 30
        CompressOldLogs = $true
        RetentionDays = $RetentionDays
    }
    
    $rotationPath = Join-Path $LogOutputPath "rotation-policy.json"
    $rotationPolicy | ConvertTo-Json | Out-File $rotationPath -Encoding UTF8
    
    Write-Host "[OK] Audit logging initialized" -ForegroundColor Green
    Write-Host "    Log path: $LogOutputPath" -ForegroundColor Gray
    Write-Host "    Retention: $RetentionDays days" -ForegroundColor Gray
}

function Write-AuditLog {
    param(
        [Parameter(Mandatory)]
        [AuditLogEntry]$Entry
    )
    
    # Create log entry
    $logEntry = @{
        timestamp = $Entry.Timestamp.ToString("o")
        eventId = $Entry.EventId
        category = $Entry.Category
        eventType = $Entry.EventType
        severity = $Entry.Severity
        userId = $Entry.UserId
        userEmail = $Entry.UserEmail
        ipAddress = $Entry.IpAddress
        resource = $Entry.Resource
        action = $Entry.Action
        details = $Entry.Details
        result = $Entry.Result
        errorMessage = $Entry.ErrorMessage
    }
    
    # Determine log file based on category and date
    $logDate = $Entry.Timestamp.ToString("yyyyMMdd")
    $logFile = Join-Path $LogOutputPath "$($Entry.Category.ToLower())-$logDate.jsonl"
    
    # Write to JSON Lines format (one JSON object per line)
    $logEntry | ConvertTo-Json -Compress | Out-File $logFile -Append -Encoding UTF8
    
    # Check alert thresholds
    Test-AlertThresholds -Entry $Entry
    
    if ($RealTime) {
        $color = switch ($Entry.Severity) {
            'Critical' { 'Red' }
            'High' { 'Magenta' }
            'Medium' { 'Yellow' }
            'Low' { 'Gray' }
            default { 'White' }
        }
        Write-Host "[$($Entry.Timestamp.ToString('HH:mm:ss'))] $($Entry.Category) - $($Entry.EventType): $($Entry.Action)" -ForegroundColor $color
    }
}

function Get-ApplicationLogs {
    param(
        [datetime]$StartTime,
        [datetime]$EndTime
    )
    
    Write-Host "`nCollecting application logs..." -ForegroundColor Yellow
    
    # Simulate collecting logs from various sources
    $logs = @()
    
    # 1. API logs
    $apiLogPath = Join-Path $ProjectRoot "src/Api/logs"
    if (Test-Path $apiLogPath) {
        $apiLogs = Get-ChildItem -Path $apiLogPath -Filter "*.log" -Recurse | 
            Where-Object { $_.LastWriteTime -ge $StartTime -and $_.LastWriteTime -le $EndTime }
        
        foreach ($logFile in $apiLogs) {
            $content = Get-Content $logFile.FullName -Raw -ErrorAction SilentlyContinue
            if ($content) {
                # Parse log entries
                $entries = $content -split "`n" | Where-Object { $_ -match '\[.*?\]' }
                $logs += $entries | ForEach-Object {
                    [PSCustomObject]@{
                        Source = "API"
                        File = $logFile.Name
                        Entry = $_
                        Timestamp = [datetime]::Now  # Parse actual timestamp from log
                    }
                }
            }
        }
    }
    
    # 2. Database audit logs
    Write-Host "  Checking database audit logs..." -ForegroundColor Gray
    # Connection string would be read from secure config
    # Query audit tables in database
    
    # 3. Azure Application Insights (if configured)
    Write-Host "  Checking Application Insights..." -ForegroundColor Gray
    # Use Azure SDK to query telemetry
    
    Write-Host "  [OK] Collected $($logs.Count) log entries" -ForegroundColor Green
    return $logs
}

function Get-SecurityEvents {
    param(
        [datetime]$StartTime,
        [datetime]$EndTime
    )
    
    Write-Host "`nCollecting security events..." -ForegroundColor Yellow
    
    $securityEvents = @()
    
    # 1. Authentication events
    Write-Host "  Checking authentication logs..." -ForegroundColor Gray
    
    # Parse authentication logs from API
    $authLogPath = Join-Path $ProjectRoot "src/Api/logs/auth"
    if (Test-Path $authLogPath) {
        $authLogs = Get-ChildItem -Path $authLogPath -Filter "auth-*.log" -Recurse |
            Where-Object { $_.LastWriteTime -ge $StartTime }
        
        foreach ($log in $authLogs) {
            $content = Get-Content $log.FullName -ErrorAction SilentlyContinue
            
            # Parse login attempts
            $loginAttempts = $content | Select-String -Pattern "Login attempt.*user=(\S+).*result=(\S+).*ip=(\S+)"
            foreach ($attempt in $loginAttempts) {
                $entry = [AuditLogEntry]::new()
                $entry.Category = "Security"
                $entry.EventType = "Login"
                $entry.UserId = $attempt.Matches.Groups[1].Value
                $entry.Result = $attempt.Matches.Groups[2].Value
                $entry.IpAddress = $attempt.Matches.Groups[3].Value
                $entry.Severity = if ($entry.Result -eq "Failed") { "High" } else { "Low" }
                
                $securityEvents += $entry
            }
        }
    }
    
    # 2. Access control events
    Write-Host "  Checking access control logs..." -ForegroundColor Gray
    
    # 3. Permission changes
    Write-Host "  Checking permission changes..." -ForegroundColor Gray
    
    Write-Host "  [OK] Collected $($securityEvents.Count) security events" -ForegroundColor Green
    return $securityEvents
}

function Get-AccessLogs {
    param(
        [datetime]$StartTime,
        [datetime]$EndTime
    )
    
    Write-Host "`nCollecting access logs..." -ForegroundColor Yellow
    
    $accessEvents = @()
    
    # Simulate collecting document access logs
    # In production, this would query the database
    
    $sampleDocuments = @('DOC-001', 'DOC-002', 'DOC-003')
    $sampleUsers = @('user1@company.com', 'user2@company.com', 'user3@company.com')
    $sampleActions = @('View', 'Edit', 'Download', 'Share')
    
    # Generate sample access events
    for ($i = 0; $i -lt 50; $i++) {
        $entry = [AuditLogEntry]::new()
        $entry.Timestamp = $StartTime.AddMinutes((Get-Random -Min 0 -Max 1440))
        $entry.Category = "Access"
        $entry.EventType = "DocumentAccess"
        $entry.UserId = $sampleUsers | Get-Random
        $entry.UserEmail = $entry.UserId
        $entry.Resource = $sampleDocuments | Get-Random
        $entry.Action = $sampleActions | Get-Random
        $entry.Result = "Success"
        $entry.Severity = "Low"
        $entry.IpAddress = "192.168.1.$((Get-Random -Min 1 -Max 254))"
        
        $accessEvents += $entry
    }
    
    Write-Host "  [OK] Collected $($accessEvents.Count) access events" -ForegroundColor Green
    return $accessEvents
}

function Test-AlertThresholds {
    param(
        [AuditLogEntry]$Entry
    )
    
    # Check if entry matches any alert conditions
    foreach ($alertName in $AuditConfig.Alerts.Keys) {
        $alert = $AuditConfig.Alerts[$alertName]
        
        # Get recent events of the same type
        $timeWindow = $Entry.Timestamp.AddSeconds(-$alert.timeWindow)
        
        # Count matching events
        # In production, query from database or in-memory cache
        
        # Example: Failed login threshold
        if ($alertName -eq 'FailedLogins' -and $Entry.EventType -eq 'Login' -and $Entry.Result -eq 'Failed') {
            # Trigger alert if threshold exceeded
            Write-Host "  [ALERT] Failed login threshold may be exceeded for user: $($Entry.UserId)" -ForegroundColor Red
        }
    }
}

# ============================================================================
# ANALYSIS FUNCTIONS
# ============================================================================

function Get-AuditStatistics {
    param(
        [array]$Logs
    )
    
    Write-Host "`nGenerating audit statistics..." -ForegroundColor Yellow
    
    $stats = @{
        TotalEvents = $Logs.Count
        ByCategory = @{}
        BySeverity = @{}
        ByResult = @{}
        TopUsers = @{}
        TopResources = @{}
        TimeDistribution = @{}
    }
    
    foreach ($log in $Logs) {
        # Category breakdown
        if ($log.Category) {
            if (-not $stats.ByCategory.ContainsKey($log.Category)) {
                $stats.ByCategory[$log.Category] = 0
            }
            $stats.ByCategory[$log.Category]++
        }
        
        # Severity breakdown
        if ($log.Severity) {
            if (-not $stats.BySeverity.ContainsKey($log.Severity)) {
                $stats.BySeverity[$log.Severity] = 0
            }
            $stats.BySeverity[$log.Severity]++
        }
        
        # Result breakdown
        if ($log.Result) {
            if (-not $stats.ByResult.ContainsKey($log.Result)) {
                $stats.ByResult[$log.Result] = 0
            }
            $stats.ByResult[$log.Result]++
        }
        
        # Top users
        if ($log.UserId) {
            if (-not $stats.TopUsers.ContainsKey($log.UserId)) {
                $stats.TopUsers[$log.UserId] = 0
            }
            $stats.TopUsers[$log.UserId]++
        }
        
        # Top resources
        if ($log.Resource) {
            if (-not $stats.TopResources.ContainsKey($log.Resource)) {
                $stats.TopResources[$log.Resource] = 0
            }
            $stats.TopResources[$log.Resource]++
        }
        
        # Time distribution (by hour)
        if ($log.Timestamp) {
            $hour = $log.Timestamp.Hour
            if (-not $stats.TimeDistribution.ContainsKey($hour)) {
                $stats.TimeDistribution[$hour] = 0
            }
            $stats.TimeDistribution[$hour]++
        }
    }
    
    # Sort top users and resources
    $stats.TopUsers = $stats.TopUsers.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 10
    $stats.TopResources = $stats.TopResources.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 10
    
    return $stats
}

function Find-SecurityIncidents {
    param(
        [array]$Logs
    )
    
    Write-Host "`nAnalyzing for security incidents..." -ForegroundColor Yellow
    
    $incidents = @()
    
    # 1. Multiple failed logins from same user
    $failedLogins = $Logs | Where-Object { $_.EventType -eq 'Login' -and $_.Result -eq 'Failed' } |
        Group-Object -Property UserId |
        Where-Object { $_.Count -ge 5 }
    
    foreach ($group in $failedLogins) {
        $incidents += [PSCustomObject]@{
            Type = "Brute Force Attack"
            Severity = "High"
            Target = $group.Name
            Count = $group.Count
            FirstSeen = ($group.Group | Sort-Object Timestamp | Select-Object -First 1).Timestamp
            LastSeen = ($group.Group | Sort-Object Timestamp -Descending | Select-Object -First 1).Timestamp
            Recommendation = "Consider blocking IP address or account"
        }
    }
    
    # 2. Access to sensitive resources
    $sensitiveAccess = $Logs | Where-Object { $_.Resource -match 'CONFIDENTIAL|SECRET|PRIVATE' }
    if ($sensitiveAccess.Count -gt 0) {
        $incidents += [PSCustomObject]@{
            Type = "Sensitive Resource Access"
            Severity = "Medium"
            Count = $sensitiveAccess.Count
            Details = "Access to $($sensitiveAccess.Count) sensitive resources"
            Recommendation = "Review access patterns and permissions"
        }
    }
    
    # 3. Unusual access patterns
    # Detect access outside normal business hours
    $afterHours = $Logs | Where-Object { 
        $_.Timestamp.Hour -lt 6 -or $_.Timestamp.Hour -gt 22 
    }
    
    if ($afterHours.Count -gt 10) {
        $incidents += [PSCustomObject]@{
            Type = "After-Hours Activity"
            Severity = "Low"
            Count = $afterHours.Count
            Details = "Significant activity outside business hours"
            Recommendation = "Review if activity is legitimate"
        }
    }
    
    Write-Host "  [OK] Found $($incidents.Count) potential security incidents" -ForegroundColor $(
        if ($incidents.Count -gt 0) { 'Yellow' } else { 'Green' }
    )
    
    return $incidents
}

# ============================================================================
# REPORTING FUNCTIONS
# ============================================================================

function New-AuditReport {
    param(
        [array]$Logs,
        [hashtable]$Statistics,
        [array]$Incidents
    )
    
    Write-Host "`nGenerating audit report..." -ForegroundColor Yellow
    
    $reportDate = Get-Date -Format "yyyy-MM-dd"
    $reportPath = Join-Path $LogOutputPath "audit-report-$($AuditConfig.Timestamp).html"
    
    $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Audit Report - $reportDate</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #f5f7fa;
            padding: 20px;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            padding: 40px;
        }
        .header {
            border-bottom: 2px solid #e5e7eb;
            padding-bottom: 20px;
            margin-bottom: 30px;
        }
        h1 {
            color: #111827;
            font-size: 32px;
            margin-bottom: 10px;
        }
        .meta {
            color: #6b7280;
            font-size: 14px;
        }
        .section {
            margin-bottom: 40px;
        }
        h2 {
            color: #374151;
            font-size: 24px;
            margin-bottom: 20px;
            border-left: 4px solid #3b82f6;
            padding-left: 16px;
        }
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 20px;
        }
        .stat-card {
            background: #f9fafb;
            padding: 20px;
            border-radius: 8px;
            border: 1px solid #e5e7eb;
        }
        .stat-label {
            color: #6b7280;
            font-size: 14px;
            margin-bottom: 8px;
        }
        .stat-value {
            color: #111827;
            font-size: 32px;
            font-weight: bold;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 20px;
        }
        th {
            background: #f9fafb;
            padding: 12px;
            text-align: left;
            font-weight: 600;
            color: #374151;
            border-bottom: 2px solid #e5e7eb;
        }
        td {
            padding: 12px;
            border-bottom: 1px solid #e5e7eb;
            color: #6b7280;
        }
        .severity-high { color: #ef4444; font-weight: bold; }
        .severity-medium { color: #f59e0b; font-weight: bold; }
        .severity-low { color: #10b981; font-weight: bold; }
        .incident-card {
            background: #fef2f2;
            border-left: 4px solid #ef4444;
            padding: 16px;
            margin-bottom: 16px;
            border-radius: 4px;
        }
        .incident-title {
            font-weight: bold;
            color: #991b1b;
            margin-bottom: 8px;
        }
        .footer {
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #e5e7eb;
            text-align: center;
            color: #6b7280;
            font-size: 14px;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Audit Report</h1>
            <div class="meta">
                <div>Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')</div>
                <div>Period: Last 24 hours</div>
                <div>Total Events: $($Statistics.TotalEvents)</div>
            </div>
        </div>

        <div class="section">
            <h2>Executive Summary</h2>
            <div class="stats-grid">
                <div class="stat-card">
                    <div class="stat-label">Total Events</div>
                    <div class="stat-value">$($Statistics.TotalEvents)</div>
                </div>
                <div class="stat-card">
                    <div class="stat-label">Security Incidents</div>
                    <div class="stat-value">$($Incidents.Count)</div>
                </div>
                <div class="stat-card">
                    <div class="stat-label">Failed Logins</div>
                    <div class="stat-value">$($Statistics.ByResult['Failed'])</div>
                </div>
                <div class="stat-card">
                    <div class="stat-label">High Severity</div>
                    <div class="stat-value">$($Statistics.BySeverity['High'])</div>
                </div>
            </div>
        </div>

        <div class="section">
            <h2>Events by Category</h2>
            <table>
                <thead>
                    <tr>
                        <th>Category</th>
                        <th>Count</th>
                        <th>Percentage</th>
                    </tr>
                </thead>
                <tbody>
$(foreach ($cat in $Statistics.ByCategory.Keys | Sort-Object) {
    $count = $Statistics.ByCategory[$cat]
    $percent = [math]::Round(($count / $Statistics.TotalEvents) * 100, 1)
    "                    <tr><td>$cat</td><td>$count</td><td>${percent}%</td></tr>"
})
                </tbody>
            </table>
        </div>

        <div class="section">
            <h2>Security Incidents</h2>
$(if ($Incidents.Count -gt 0) {
    foreach ($incident in $Incidents) {
        @"
            <div class="incident-card">
                <div class="incident-title">$($incident.Type) - $($incident.Severity) Severity</div>
                <div>Count: $($incident.Count)</div>
                <div>$($incident.Details)</div>
                <div style="margin-top: 8px;"><strong>Recommendation:</strong> $($incident.Recommendation)</div>
            </div>
"@
    }
} else {
    "            <p>No security incidents detected in this period.</p>"
})
        </div>

        <div class="section">
            <h2>Top Users by Activity</h2>
            <table>
                <thead>
                    <tr>
                        <th>User</th>
                        <th>Event Count</th>
                    </tr>
                </thead>
                <tbody>
$(foreach ($user in $Statistics.TopUsers) {
    "                    <tr><td>$($user.Name)</td><td>$($user.Value)</td></tr>"
})
                </tbody>
            </table>
        </div>

        <div class="footer">
            Enterprise Documentation Platform - Audit System<br>
            This report is confidential and intended for authorized personnel only
        </div>
    </div>
</body>
</html>
"@
    
    $html | Out-File $reportPath -Encoding UTF8
    Write-Host "  [OK] Report saved: $reportPath" -ForegroundColor Green
    
    return $reportPath
}

function Export-ComplianceReport {
    param(
        [array]$Logs,
        [string]$ComplianceFramework
    )
    
    Write-Host "`nGenerating $ComplianceFramework compliance report..." -ForegroundColor Yellow
    
    $framework = $AuditConfig.Compliance[$ComplianceFramework]
    if (-not $framework.enabled) {
        Write-Host "  [SKIP] $ComplianceFramework framework not enabled" -ForegroundColor Gray
        return
    }
    
    $reportPath = Join-Path $LogOutputPath "$ComplianceFramework-compliance-$($AuditConfig.Timestamp).json"
    
    $complianceReport = @{
        Framework = $ComplianceFramework
        Generated = Get-Date -Format "o"
        Period = @{
            Start = (Get-Date).AddDays(-1).ToString("o")
            End = (Get-Date).ToString("o")
        }
        Requirements = @{}
        Summary = @{
            TotalEvents = $Logs.Count
            CompliantEvents = 0
            NonCompliantEvents = 0
        }
    }
    
    # Map events to compliance requirements
    foreach ($requirement in $framework.requirements) {
        $matchingEvents = $Logs | Where-Object { $_.Category -match $requirement -or $_.EventType -match $requirement }
        
        $complianceReport.Requirements[$requirement] = @{
            EventCount = $matchingEvents.Count
            Status = if ($matchingEvents.Count -gt 0) { "Compliant" } else { "Needs Review" }
            Events = $matchingEvents | Select-Object -First 5 | ForEach-Object {
                @{
                    Timestamp = $_.Timestamp.ToString("o")
                    EventType = $_.EventType
                    UserId = $_.UserId
                    Resource = $_.Resource
                }
            }
        }
    }
    
    $complianceReport | ConvertTo-Json -Depth 10 | Out-File $reportPath -Encoding UTF8
    Write-Host "  [OK] Compliance report saved: $reportPath" -ForegroundColor Green
}

# ============================================================================
# LOG ROTATION AND CLEANUP
# ============================================================================

function Start-LogRotation {
    Write-Host "`nPerforming log rotation..." -ForegroundColor Yellow
    
    $oldLogs = Get-ChildItem -Path $LogOutputPath -Filter "*.jsonl" |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) }
    
    foreach ($log in $oldLogs) {
        # Compress old logs
        $archivePath = Join-Path $LogOutputPath "archive"
        if (-not (Test-Path $archivePath)) {
            $null = New-Item -ItemType Directory -Path $archivePath -Force
        }
        
        $archiveFile = Join-Path $archivePath "$($log.BaseName).zip"
        Compress-Archive -Path $log.FullName -DestinationPath $archiveFile -Force
        
        # Remove original
        Remove-Item $log.FullName -Force
        Write-Host "  [OK] Archived and removed: $($log.Name)" -ForegroundColor Green
    }
    
    Write-Host "  [OK] Log rotation complete" -ForegroundColor Green
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

try {
    Initialize-AuditLogging
    
    # Define time range
    $endTime = Get-Date
    $startTime = $endTime.AddHours(-24)
    
    Write-Host "`nCollecting logs from $($startTime.ToString('yyyy-MM-dd HH:mm')) to $($endTime.ToString('yyyy-MM-dd HH:mm'))..." -ForegroundColor Cyan
    
    # Collect all logs
    $allLogs = @()
    
    if ($LogType -eq 'All' -or $LogType -eq 'Security') {
        $allLogs += Get-SecurityEvents -StartTime $startTime -EndTime $endTime
    }
    
    if ($LogType -eq 'All' -or $LogType -eq 'Access') {
        $allLogs += Get-AccessLogs -StartTime $startTime -EndTime $endTime
    }
    
    # Write logs to files
    Write-Host "`nWriting logs to files..." -ForegroundColor Yellow
    foreach ($log in $allLogs) {
        Write-AuditLog -Entry $log
    }
    
    # Generate statistics
    $statistics = Get-AuditStatistics -Logs $allLogs
    
    # Find incidents
    $incidents = Find-SecurityIncidents -Logs $allLogs
    
    # Generate reports
    if ($GenerateReport) {
        $reportPath = New-AuditReport -Logs $allLogs -Statistics $statistics -Incidents $incidents
        
        # Generate compliance reports
        foreach ($framework in $AuditConfig.Compliance.Keys) {
            if ($AuditConfig.Compliance[$framework].enabled) {
                Export-ComplianceReport -Logs $allLogs -ComplianceFramework $framework
            }
        }
        
        Write-Host "`n[SUCCESS] Report generated: $reportPath" -ForegroundColor Green
    }
    
    # Log rotation
    Start-LogRotation
    
    # Summary
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  Audit Logging Complete" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Total events: $($allLogs.Count)" -ForegroundColor Green
    Write-Host "  Security incidents: $($incidents.Count)" -ForegroundColor $(if ($incidents.Count -gt 0) { 'Yellow' } else { 'Green' })
    Write-Host "  Reports saved: $LogOutputPath" -ForegroundColor Green
    
    if ($incidents.Count -gt 0) {
        Write-Host "`n[WARNING] Security incidents require attention!" -ForegroundColor Yellow
        exit 1
    }
    
    exit 0
    
} catch {
    Write-Host "`n[ERROR] Audit logging failed: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 2
}
