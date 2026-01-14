# PowerShell script to test Azure OpenAI deployment availability
$endpoint = "https://your-endpoint-placeholder.openai.azure.com"
$apiKey = "placeholder-key-value"
$deployments = @("gpt-4", "gpt-4.1", "gpt-4o", "gpt-35-turbo")

Write-Host "Testing Azure OpenAI Deployment Availability" -ForegroundColor Yellow
Write-Host "Endpoint: $endpoint" -ForegroundColor Cyan
Write-Host ""

foreach ($deployment in $deployments) {
    Write-Host "Testing deployment: $deployment" -ForegroundColor White -NoNewline
    
    try {
        $url = "$endpoint/openai/deployments/$deployment/chat/completions?api-version=2024-08-01-preview"
        
        $headers = @{
            "Content-Type" = "application/json"
            "api-key" = $apiKey
        }
        
        $body = @{
            messages = @(
                @{
                    role = "user"
                    content = "Hello"
                }
            )
            max_tokens = 5
        } | ConvertTo-Json -Depth 3
        
        $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body $body -TimeoutSec 10
        Write-Host " - ✅ AVAILABLE" -ForegroundColor Green
    }
    catch {
        if ($_.Exception.Message -like "*404*" -or $_.Exception.Message -like "*DeploymentNotFound*") {
            Write-Host " - ❌ NOT FOUND (404)" -ForegroundColor Red
        }
        elseif ($_.Exception.Message -like "*401*" -or $_.Exception.Message -like "*403*") {
            Write-Host " - 🔐 AUTH ISSUE" -ForegroundColor Yellow
        }
        else {
            Write-Host " - ⚠️ ERROR: $($_.Exception.Message)" -ForegroundColor Magenta
        }
    }
}

Write-Host ""
Write-Host "✅ Test completed" -ForegroundColor Green