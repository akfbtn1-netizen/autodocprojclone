<#
.SYNOPSIS
    Deploy Azure OpenAI resources with enterprise configuration
.DESCRIPTION
    Creates Azure OpenAI resource with private endpoint, managed identity,
    and model deployments following enterprise best practices.
.PARAMETER ResourceGroupName
    Name of the resource group
.PARAMETER Location
    Azure region (e.g., eastus2, swedencentral)
.PARAMETER OpenAIName
    Name for the Azure OpenAI resource
.PARAMETER VNetName
    Name of the existing VNet for private endpoint
.PARAMETER SubnetName
    Name of the subnet for private endpoint
.EXAMPLE
    .\Deploy-AzureOpenAI.ps1 -ResourceGroupName "rg-ai-prod" -Location "eastus2" -OpenAIName "aoai-enterprise"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $true)]
    [ValidateSet("eastus", "eastus2", "westus", "westus3", "northcentralus", "southcentralus", "swedencentral", "westeurope")]
    [string]$Location,
    
    [Parameter(Mandatory = $true)]
    [string]$OpenAIName,
    
    [Parameter(Mandatory = $false)]
    [string]$VNetName,
    
    [Parameter(Mandatory = $false)]
    [string]$SubnetName = "snet-openai",
    
    [Parameter(Mandatory = $false)]
    [switch]$EnablePrivateEndpoint,
    
    [Parameter(Mandatory = $false)]
    [hashtable]$Tags = @{
        Environment = "Production"
        ManagedBy   = "Automation"
        CostCenter  = "AI-Platform"
    }
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Azure OpenAI Enterprise Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verify Azure CLI login
Write-Host "Verifying Azure CLI authentication..." -ForegroundColor Yellow
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Error "Not logged into Azure CLI. Run 'az login' first."
    exit 1
}
Write-Host "  Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "  Subscription: $($account.name)" -ForegroundColor Green

# Create resource group if needed
Write-Host ""
Write-Host "Checking resource group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -eq "false") {
    Write-Host "  Creating resource group: $ResourceGroupName" -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location --tags $($Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })
}
Write-Host "  Resource group ready: $ResourceGroupName" -ForegroundColor Green

# Create Azure OpenAI resource
Write-Host ""
Write-Host "Creating Azure OpenAI resource..." -ForegroundColor Yellow

$openaiParams = @(
    "cognitiveservices", "account", "create",
    "--name", $OpenAIName,
    "--resource-group", $ResourceGroupName,
    "--location", $Location,
    "--kind", "OpenAI",
    "--sku", "S0",
    "--custom-domain", $OpenAIName
)

# Add network rules if private endpoint requested
if ($EnablePrivateEndpoint) {
    $openaiParams += "--public-network-access", "Disabled"
} else {
    $openaiParams += "--public-network-access", "Enabled"
}

az @openaiParams

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create Azure OpenAI resource"
    exit 1
}
Write-Host "  Azure OpenAI resource created: $OpenAIName" -ForegroundColor Green

# Enable managed identity
Write-Host ""
Write-Host "Enabling system-assigned managed identity..." -ForegroundColor Yellow
az cognitiveservices account identity assign `
    --name $OpenAIName `
    --resource-group $ResourceGroupName

Write-Host "  Managed identity enabled" -ForegroundColor Green

# Create private endpoint if requested
if ($EnablePrivateEndpoint -and $VNetName) {
    Write-Host ""
    Write-Host "Creating private endpoint..." -ForegroundColor Yellow
    
    $openaiId = az cognitiveservices account show `
        --name $OpenAIName `
        --resource-group $ResourceGroupName `
        --query id -o tsv
    
    $subnetId = az network vnet subnet show `
        --resource-group $ResourceGroupName `
        --vnet-name $VNetName `
        --name $SubnetName `
        --query id -o tsv
    
    az network private-endpoint create `
        --name "pe-$OpenAIName" `
        --resource-group $ResourceGroupName `
        --vnet-name $VNetName `
        --subnet $SubnetName `
        --private-connection-resource-id $openaiId `
        --group-id "account" `
        --connection-name "conn-$OpenAIName"
    
    # Create private DNS zone
    Write-Host "  Creating private DNS zone..." -ForegroundColor Yellow
    az network private-dns zone create `
        --resource-group $ResourceGroupName `
        --name "privatelink.openai.azure.com"
    
    az network private-dns link vnet create `
        --resource-group $ResourceGroupName `
        --zone-name "privatelink.openai.azure.com" `
        --name "link-$VNetName" `
        --virtual-network $VNetName `
        --registration-enabled false
    
    az network private-endpoint dns-zone-group create `
        --resource-group $ResourceGroupName `
        --endpoint-name "pe-$OpenAIName" `
        --name "default" `
        --private-dns-zone "privatelink.openai.azure.com" `
        --zone-name "openai"
    
    Write-Host "  Private endpoint configured" -ForegroundColor Green
}

# Deploy models
Write-Host ""
Write-Host "Deploying models..." -ForegroundColor Yellow

$models = @(
    @{ Name = "gpt-5"; Model = "gpt-5"; Version = "2025-08-07"; Capacity = 10 },
    @{ Name = "gpt-5-mini"; Model = "gpt-5-mini"; Version = "2025-08-07"; Capacity = 20 },
    @{ Name = "text-embedding-3-large"; Model = "text-embedding-3-large"; Version = "1"; Capacity = 30 }
)

foreach ($model in $models) {
    Write-Host "  Deploying $($model.Name)..." -ForegroundColor Yellow
    
    az cognitiveservices account deployment create `
        --name $OpenAIName `
        --resource-group $ResourceGroupName `
        --deployment-name $model.Name `
        --model-name $model.Model `
        --model-version $model.Version `
        --model-format OpenAI `
        --sku-capacity $model.Capacity `
        --sku-name "GlobalStandard" 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    $($model.Name) deployed" -ForegroundColor Green
    } else {
        Write-Host "    $($model.Name) deployment skipped (may already exist or quota issue)" -ForegroundColor Yellow
    }
}

# Get connection information
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$endpoint = az cognitiveservices account show `
    --name $OpenAIName `
    --resource-group $ResourceGroupName `
    --query "properties.endpoint" -o tsv

$keys = az cognitiveservices account keys list `
    --name $OpenAIName `
    --resource-group $ResourceGroupName | ConvertFrom-Json

Write-Host "Connection Information:" -ForegroundColor Green
Write-Host "  Endpoint: $endpoint" -ForegroundColor White
Write-Host "  Key 1: $($keys.key1.Substring(0, 8))..." -ForegroundColor White
Write-Host ""
Write-Host "Environment Variables:" -ForegroundColor Green
Write-Host "  AZURE_OPENAI_ENDPOINT=$endpoint" -ForegroundColor White
Write-Host "  AZURE_OPENAI_API_KEY=$($keys.key1)" -ForegroundColor White
Write-Host ""
Write-Host "For Entra ID authentication (recommended):" -ForegroundColor Green
Write-Host "  Use DefaultAzureCredential() in your code" -ForegroundColor White
Write-Host "  Assign 'Cognitive Services OpenAI User' role to your identity" -ForegroundColor White
Write-Host ""
