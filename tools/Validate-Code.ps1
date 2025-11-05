# Validate-Code.ps1
# Wrapper for EnterpriseAIQualitySystem

param(
    [Parameter(Mandatory=$true)]
    [string]$AgentPath,
    
    [switch]$ForceApprove
)

$ErrorActionPreference = "Stop"

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$configPath = Join-Path $toolsDir "ai-quality-config.json"
$systemPath = Join-Path $toolsDir "EnterpriseAIQualitySystem.cs"

# Compile the quality system
Write-Host "Compiling AI Quality System..." -ForegroundColor Yellow
Add-Type -Path $systemPath -ReferencedAssemblies @(
    "System.Text.Json",
    "System.Collections",
    "System.Linq",
    "netstandard"
) -IgnoreWarnings

# Load configuration
$config = Get-Content $configPath | ConvertFrom-Json

# Create system
$systemConfig = New-Object EnterpriseAIQuality.SystemConfiguration
$systemConfig.CodebasePath = $config.CodebasePath
$systemConfig.ApprovalHistoryPath = $config.ApprovalHistoryPath
$systemConfig.EnableMasterIndex = $config.EnableMasterIndex

$riskPolicy = New-Object EnterpriseAIQuality.RiskPolicy
$riskPolicy.AccountableOfficer = $config.RiskPolicy.AccountableOfficer
$riskPolicy.AuditRetentionYears = $config.RiskPolicy.AuditRetentionYears
$riskPolicy.SecurityThreshold = $config.RiskPolicy.SecurityThreshold
$riskPolicy.QualityThreshold = $config.RiskPolicy.QualityThreshold
$systemConfig.RiskPolicy = $riskPolicy

$system = New-Object EnterpriseAIQuality.EnterpriseAIQualitySystem($systemConfig)

# Validate agent
Write-Host "Validating: $AgentPath" -ForegroundColor Cyan
$assessment = $system.ValidateAndAssess($AgentPath).Result

# Display results
$system.PrintAssessmentSummary($assessment)

# Determine pass/fail
if ($assessment.RiskAssessment.RiskLevel -eq "Critical") {
    Write-Host "`nðŸš¨ BLOCKED - Critical risk" -ForegroundColor Red
    exit 2
}

if ($assessment.ValidationResult.OverallQuality -lt 70) {
    Write-Host "`nâŒ REJECTED - Quality too low" -ForegroundColor Red
    if ($ForceApprove) {
        Write-Host "âš ï¸ ForceApprove set - proceeding anyway" -ForegroundColor Yellow
        exit 0
    }
    exit 1
}

if ($assessment.RiskAssessment.RequiresHumanApproval) {
    Write-Host "`nâš ï¸ HUMAN APPROVAL REQUIRED" -ForegroundColor Yellow
    if ($ForceApprove) {
        Write-Host "âœ… ForceApprove set - bypassing approval" -ForegroundColor Green
        exit 0
    }
    
    $response = Read-Host "Approve for deployment? (yes/no)"
    if ($response -eq "yes") {
        Write-Host "âœ… APPROVED" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "âŒ REJECTED" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`nâœ… APPROVED - Quality meets standards" -ForegroundColor Green
exit 0