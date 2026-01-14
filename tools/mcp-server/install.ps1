# Enterprise Documentation Platform MCP Server - Quick Install
# Run this script from the mcp-server directory

Write-Host ""
Write-Host "🚀 Installing Enterprise Documentation Platform MCP Server..." -ForegroundColor Cyan
Write-Host ""

# Step 1: Install dependencies
Write-Host "📦 Step 1/4: Installing npm dependencies..." -ForegroundColor Yellow
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to install dependencies" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Dependencies installed" -ForegroundColor Green
Write-Host ""

# Step 2: Build
Write-Host "🔨 Step 2/4: Building TypeScript..." -ForegroundColor Yellow
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build successful" -ForegroundColor Green
Write-Host ""

# Step 3: Configure Claude Desktop
Write-Host "⚙️  Step 3/4: Configuring Claude Desktop..." -ForegroundColor Yellow

$claudeConfigDir = "$env:APPDATA\Claude"
$configFile = "$claudeConfigDir\claude_desktop_config.json"

# Create directory if it doesn't exist
New-Item -Path $claudeConfigDir -ItemType Directory -Force | Out-Null

# Get current directory
$currentDir = (Get-Location).Path
$indexPath = Join-Path $currentDir "dist\index.js"

# Create config
$config = @{
    mcpServers = @{
        "enterprise-doc-platform" = @{
            command = "node"
            args = @($indexPath)
        }
    }
} | ConvertTo-Json -Depth 10

Set-Content -Path $configFile -Value $config

Write-Host "✅ Configuration written to: $configFile" -ForegroundColor Green
Write-Host ""

# Step 4: Verify
Write-Host "🔍 Step 4/4: Verifying installation..." -ForegroundColor Yellow

if (Test-Path $indexPath) {
    Write-Host "✅ Server compiled successfully" -ForegroundColor Green
} else {
    Write-Host "❌ Server not found at: $indexPath" -ForegroundColor Red
    exit 1
}

if (Test-Path $configFile) {
    Write-Host "✅ Claude Desktop configured" -ForegroundColor Green
} else {
    Write-Host "❌ Config file not created" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "✨ Installation Complete!" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Close Claude Desktop completely" -ForegroundColor White
Write-Host "  2. Reopen Claude Desktop" -ForegroundColor White
Write-Host "  3. Test with: 'Can you check git status?'" -ForegroundColor White
Write-Host ""
Write-Host "Available Tools:" -ForegroundColor Yellow
Write-Host "  🔄 Git: git_status, git_diff, git_log" -ForegroundColor Cyan
Write-Host "  🧪 Tests: run_tests, get_test_coverage" -ForegroundColor Cyan
Write-Host "  🌐 API: list_endpoints" -ForegroundColor Cyan
Write-Host "  📚 Code: index_handlers, index_entities, find_usages" -ForegroundColor Cyan
Write-Host "  🗄️  DB: get_db_schema, check_migrations" -ForegroundColor Cyan
Write-Host "  🧠 Memory: update_memory, read_memory" -ForegroundColor Cyan
Write-Host ""
Write-Host "Documentation: See README.md for detailed usage" -ForegroundColor White
Write-Host ""
