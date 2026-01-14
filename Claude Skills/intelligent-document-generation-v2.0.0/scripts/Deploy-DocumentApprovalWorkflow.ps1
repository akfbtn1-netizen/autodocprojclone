<#
.SYNOPSIS
    Configures SharePoint library and Power Automate flow for document approval workflow.

.DESCRIPTION
    This script sets up the SharePoint document library with custom columns for
    Shadow Metadata tracking and configures the approval workflow integration.

.PARAMETER SiteUrl
    SharePoint site URL (e.g., https://tenant.sharepoint.com/sites/Documentation)

.PARAMETER LibraryName
    Name of the document library (default: "Generated Documentation")

.PARAMETER ApproverEmails
    Comma-separated list of approver email addresses

.EXAMPLE
    .\Deploy-DocumentApprovalWorkflow.ps1 -SiteUrl "https://contoso.sharepoint.com/sites/docs" -ApproverEmails "approver@contoso.com"

.NOTES
    Requires: PnP.PowerShell module
    Author: Documentation Automation System
    Version: 1.0.0
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SiteUrl,

    [Parameter(Mandatory = $false)]
    [string]$LibraryName = "Generated Documentation",

    [Parameter(Mandatory = $true)]
    [string]$ApproverEmails,

    [Parameter(Mandatory = $false)]
    [switch]$CreateLibrary,

    [Parameter(Mandatory = $false)]
    [switch]$EnableApprovals
)

$ErrorActionPreference = "Stop"

# ============================================================================
# FUNCTIONS
# ============================================================================

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARN"  { "Yellow" }
        "SUCCESS" { "Green" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

function Test-PnPModule {
    if (-not (Get-Module -ListAvailable -Name "PnP.PowerShell")) {
        Write-Log "PnP.PowerShell module not found. Installing..." "WARN"
        Install-Module -Name "PnP.PowerShell" -Scope CurrentUser -Force
    }
    Import-Module PnP.PowerShell -ErrorAction Stop
}

function Connect-ToSharePoint {
    param([string]$Url)
    
    Write-Log "Connecting to SharePoint: $Url"
    
    try {
        Connect-PnPOnline -Url $Url -Interactive
        Write-Log "Connected successfully" "SUCCESS"
    }
    catch {
        Write-Log "Failed to connect: $_" "ERROR"
        throw
    }
}

function New-DocumentLibrary {
    param([string]$Name)
    
    Write-Log "Creating document library: $Name"
    
    $existingList = Get-PnPList -Identity $Name -ErrorAction SilentlyContinue
    
    if ($existingList) {
        Write-Log "Library already exists: $Name" "WARN"
        return $existingList
    }
    
    $library = New-PnPList -Title $Name -Template DocumentLibrary
    Write-Log "Created library: $Name" "SUCCESS"
    
    return $library
}

function Add-ShadowMetadataColumns {
    param([string]$LibraryName)
    
    Write-Log "Adding Shadow Metadata columns to: $LibraryName"
    
    # Shadow Metadata Columns
    $columns = @(
        @{
            DisplayName = "DB Object ID"
            InternalName = "DBObjectID"
            Type = "Text"
            Description = "Database object identifier for sync tracking"
        },
        @{
            DisplayName = "Content Hash"
            InternalName = "ContentHash"
            Type = "Text"
            Description = "SHA-256 hash for change detection"
        },
        @{
            DisplayName = "Sync Status"
            InternalName = "SyncStatus"
            Type = "Choice"
            Choices = @("CURRENT", "STALE", "PENDING", "CONFLICT", "ORPHANED", "DRAFT")
            Description = "Document synchronization status with database"
        },
        @{
            DisplayName = "Schema Version"
            InternalName = "SchemaVersion"
            Type = "Text"
            Description = "Database schema version at generation time"
        },
        @{
            DisplayName = "Last Sync"
            InternalName = "LastSync"
            Type = "DateTime"
            Description = "Last synchronization timestamp"
        },
        @{
            DisplayName = "Master Index ID"
            InternalName = "MasterIndexID"
            Type = "Number"
            Description = "Reference to Master Index record"
        },
        @{
            DisplayName = "Audience Type"
            InternalName = "AudienceType"
            Type = "Choice"
            Choices = @("TECHNICAL_DBA", "DEVELOPER", "BUSINESS_ANALYST", "EXECUTIVE", "COMPLIANCE")
            Description = "Target audience for this documentation variant"
        },
        # Approval Workflow Columns
        @{
            DisplayName = "Approval Status"
            InternalName = "ApprovalStatus"
            Type = "Choice"
            Choices = @("Pending", "Approved", "Rejected", "Not Required")
            Description = "Document approval workflow status"
        },
        @{
            DisplayName = "Approved By"
            InternalName = "ApprovedBy"
            Type = "User"
            Description = "User who approved the document"
        },
        @{
            DisplayName = "Approved Date"
            InternalName = "ApprovedDate"
            Type = "DateTime"
            Description = "Date and time of approval"
        },
        @{
            DisplayName = "Approval Comments"
            InternalName = "ApprovalComments"
            Type = "Note"
            Description = "Comments from approval workflow"
        }
    )
    
    foreach ($col in $columns) {
        $existingField = Get-PnPField -List $LibraryName -Identity $col.InternalName -ErrorAction SilentlyContinue
        
        if ($existingField) {
            Write-Log "Column already exists: $($col.DisplayName)" "WARN"
            continue
        }
        
        try {
            switch ($col.Type) {
                "Text" {
                    Add-PnPField -List $LibraryName `
                        -DisplayName $col.DisplayName `
                        -InternalName $col.InternalName `
                        -Type Text `
                        -AddToDefaultView
                }
                "Choice" {
                    Add-PnPField -List $LibraryName `
                        -DisplayName $col.DisplayName `
                        -InternalName $col.InternalName `
                        -Type Choice `
                        -Choices $col.Choices `
                        -AddToDefaultView
                }
                "DateTime" {
                    Add-PnPField -List $LibraryName `
                        -DisplayName $col.DisplayName `
                        -InternalName $col.InternalName `
                        -Type DateTime `
                        -AddToDefaultView
                }
                "Number" {
                    Add-PnPField -List $LibraryName `
                        -DisplayName $col.DisplayName `
                        -InternalName $col.InternalName `
                        -Type Number `
                        -AddToDefaultView
                }
                "User" {
                    Add-PnPField -List $LibraryName `
                        -DisplayName $col.DisplayName `
                        -InternalName $col.InternalName `
                        -Type User `
                        -AddToDefaultView
                }
                "Note" {
                    Add-PnPField -List $LibraryName `
                        -DisplayName $col.DisplayName `
                        -InternalName $col.InternalName `
                        -Type Note `
                        -AddToDefaultView
                }
            }
            Write-Log "Added column: $($col.DisplayName)" "SUCCESS"
        }
        catch {
            Write-Log "Failed to add column $($col.DisplayName): $_" "ERROR"
        }
    }
}

function Enable-Versioning {
    param([string]$LibraryName)
    
    Write-Log "Enabling versioning on: $LibraryName"
    
    Set-PnPList -Identity $LibraryName `
        -EnableVersioning $true `
        -MajorVersions 50 `
        -EnableMinorVersions $false
    
    Write-Log "Versioning enabled with 50 major versions" "SUCCESS"
}

function Create-CustomViews {
    param([string]$LibraryName)
    
    Write-Log "Creating custom views for: $LibraryName"
    
    # Pending Approval View
    $pendingQuery = "<Where><Eq><FieldRef Name='ApprovalStatus'/><Value Type='Choice'>Pending</Value></Eq></Where>"
    $pendingFields = @("LinkFilename", "DBObjectID", "SyncStatus", "AudienceType", "Created", "Modified")
    
    $existingView = Get-PnPView -List $LibraryName -Identity "Pending Approval" -ErrorAction SilentlyContinue
    if (-not $existingView) {
        Add-PnPView -List $LibraryName `
            -Title "Pending Approval" `
            -Fields $pendingFields `
            -Query $pendingQuery
        Write-Log "Created view: Pending Approval" "SUCCESS"
    }
    
    # Stale Documents View
    $staleQuery = "<Where><Eq><FieldRef Name='SyncStatus'/><Value Type='Choice'>STALE</Value></Eq></Where>"
    $staleFields = @("LinkFilename", "DBObjectID", "LastSync", "ContentHash", "Modified")
    
    $existingView = Get-PnPView -List $LibraryName -Identity "Stale Documents" -ErrorAction SilentlyContinue
    if (-not $existingView) {
        Add-PnPView -List $LibraryName `
            -Title "Stale Documents" `
            -Fields $staleFields `
            -Query $staleQuery
        Write-Log "Created view: Stale Documents" "SUCCESS"
    }
    
    # By Audience View
    $audienceFields = @("LinkFilename", "DBObjectID", "AudienceType", "ApprovalStatus", "Modified")
    
    $existingView = Get-PnPView -List $LibraryName -Identity "By Audience" -ErrorAction SilentlyContinue
    if (-not $existingView) {
        Add-PnPView -List $LibraryName `
            -Title "By Audience" `
            -Fields $audienceFields `
            -Query "<GroupBy Collapse='TRUE'><FieldRef Name='AudienceType'/></GroupBy>"
        Write-Log "Created view: By Audience" "SUCCESS"
    }
    
    # Recently Updated View
    $recentQuery = "<OrderBy><FieldRef Name='Modified' Ascending='FALSE'/></OrderBy>"
    $recentFields = @("LinkFilename", "DBObjectID", "SyncStatus", "Modified", "ApprovalStatus")
    
    $existingView = Get-PnPView -List $LibraryName -Identity "Recently Updated" -ErrorAction SilentlyContinue
    if (-not $existingView) {
        Add-PnPView -List $LibraryName `
            -Title "Recently Updated" `
            -Fields $recentFields `
            -Query $recentQuery `
            -RowLimit 50
        Write-Log "Created view: Recently Updated" "SUCCESS"
    }
}

function Create-SubFolders {
    param([string]$LibraryName)
    
    Write-Log "Creating folder structure in: $LibraryName"
    
    $folders = @(
        "Pending",
        "Approved",
        "Rejected",
        "Archive",
        "Templates"
    )
    
    foreach ($folder in $folders) {
        $existingFolder = Get-PnPFolder -Url "$LibraryName/$folder" -ErrorAction SilentlyContinue
        
        if (-not $existingFolder) {
            Add-PnPFolder -Name $folder -Folder $LibraryName
            Write-Log "Created folder: $folder" "SUCCESS"
        }
        else {
            Write-Log "Folder already exists: $folder" "WARN"
        }
    }
}

function Export-FlowDefinition {
    param(
        [string]$SiteUrl,
        [string]$LibraryName,
        [string]$ApproverEmails
    )
    
    Write-Log "Generating Power Automate flow definition"
    
    $flowDefinition = @{
        "name" = "Document Approval Workflow - $LibraryName"
        "description" = "Automated approval workflow for generated documentation"
        "trigger" = @{
            "type" = "SharePoint"
            "action" = "When a file is created (properties only)"
            "parameters" = @{
                "siteUrl" = $SiteUrl
                "libraryName" = $LibraryName
                "folderPath" = "/Pending"
            }
        }
        "actions" = @(
            @{
                "name" = "Get file properties"
                "type" = "SharePoint.GetFileProperties"
            },
            @{
                "name" = "Start approval"
                "type" = "Approvals.StartAndWaitForApproval"
                "parameters" = @{
                    "approvalType" = "Basic"
                    "title" = "Review Documentation: @{triggerOutputs()?['body/FileLeafRef']}"
                    "assignedTo" = $ApproverEmails
                    "details" = "Please review the generated documentation.`n`nDB Object: @{triggerOutputs()?['body/DBObjectID']}`nAudience: @{triggerOutputs()?['body/AudienceType']}`nSync Status: @{triggerOutputs()?['body/SyncStatus']}"
                }
            },
            @{
                "name" = "Condition - Approved"
                "type" = "If"
                "expression" = "@equals(body('Start_approval')?['outcome'], 'Approve')"
                "actions" = @{
                    "true" = @(
                        @{
                            "name" = "Update approval status"
                            "type" = "SharePoint.UpdateFileProperties"
                            "parameters" = @{
                                "ApprovalStatus" = "Approved"
                                "ApprovedBy" = "@{body('Start_approval')?['responses'][0]['responder']['email']}"
                                "ApprovedDate" = "@{utcNow()}"
                            }
                        },
                        @{
                            "name" = "Move to Approved folder"
                            "type" = "SharePoint.MoveFile"
                            "parameters" = @{
                                "destinationPath" = "/Approved/@{triggerOutputs()?['body/FileLeafRef']}"
                            }
                        }
                    )
                    "false" = @(
                        @{
                            "name" = "Update rejection status"
                            "type" = "SharePoint.UpdateFileProperties"
                            "parameters" = @{
                                "ApprovalStatus" = "Rejected"
                                "ApprovalComments" = "@{body('Start_approval')?['responses'][0]['comments']}"
                            }
                        },
                        @{
                            "name" = "Move to Rejected folder"
                            "type" = "SharePoint.MoveFile"
                            "parameters" = @{
                                "destinationPath" = "/Rejected/@{triggerOutputs()?['body/FileLeafRef']}"
                            }
                        }
                    )
                }
            }
        )
    }
    
    $outputPath = Join-Path $PSScriptRoot "PowerAutomate_FlowDefinition.json"
    $flowDefinition | ConvertTo-Json -Depth 10 | Out-File $outputPath -Encoding UTF8
    
    Write-Log "Flow definition exported to: $outputPath" "SUCCESS"
    Write-Log "Import this JSON into Power Automate to create the approval workflow" "INFO"
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

Write-Log "=========================================="
Write-Log "Document Approval Workflow Deployment"
Write-Log "=========================================="

# Check prerequisites
Test-PnPModule

# Connect to SharePoint
Connect-ToSharePoint -Url $SiteUrl

# Create library if requested
if ($CreateLibrary) {
    New-DocumentLibrary -Name $LibraryName
}

# Add Shadow Metadata columns
Add-ShadowMetadataColumns -LibraryName $LibraryName

# Enable versioning
Enable-Versioning -LibraryName $LibraryName

# Create custom views
Create-CustomViews -LibraryName $LibraryName

# Create folder structure
Create-SubFolders -LibraryName $LibraryName

# Export Power Automate flow definition
Export-FlowDefinition -SiteUrl $SiteUrl -LibraryName $LibraryName -ApproverEmails $ApproverEmails

Write-Log "=========================================="
Write-Log "Deployment completed successfully!" "SUCCESS"
Write-Log "=========================================="
Write-Log ""
Write-Log "Next Steps:" "INFO"
Write-Log "1. Review the PowerAutomate_FlowDefinition.json file"
Write-Log "2. Import the flow into Power Automate"
Write-Log "3. Configure the flow connections"
Write-Log "4. Test with a sample document"
