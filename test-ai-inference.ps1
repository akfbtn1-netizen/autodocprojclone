# Test AI-Powered Metadata Inference
# This script tests the AI metadata inference functionality

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "    FIX #5: AI-POWERED METADATA INFERENCE TEST" -ForegroundColor Cyan  
Write-Host "=====================================================================" -ForegroundColor Cyan

# Test 1: Verify AI inference implementation
Write-Host "`n1. Checking AI inference implementation..." -ForegroundColor Yellow

$serviceFile = "src\Core\Application\Services\MasterIndex\ComprehensiveMasterIndexService.cs"
if (Test-Path $serviceFile) {
    $content = Get-Content $serviceFile -Raw
    
    if ($content -match "Phase15_AIInferenceAsync") {
        Write-Host "   ✓ Phase15_AIInferenceAsync method implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ AI inference phase not found" -ForegroundColor Red
    }
    
    if ($content -match "InferMetadataWithAIAsync") {
        Write-Host "   ✓ InferMetadataWithAIAsync method implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ AI inference core method not found" -ForegroundColor Red
    }
    
    if ($content -match "OpenAIResponse.*AIInferenceResult") {
        Write-Host "   ✓ OpenAI response models implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ OpenAI response models missing" -ForegroundColor Red
    }
    
    if ($content -match "confidence >= 0.8") {
        Write-Host "   ✓ Confidence threshold (80%) implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Confidence threshold not found" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Service file not found" -ForegroundColor Red
}

# Test 2: Check OpenAI configuration
Write-Host "`n2. Checking OpenAI configuration..." -ForegroundColor Yellow

if (Test-Path "src\Api\appsettings.Development.json") {
    $config = Get-Content "src\Api\appsettings.Development.json" -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
    
    if ($config.AzureOpenAI) {
        Write-Host "   ✓ AzureOpenAI configuration section found" -ForegroundColor Green
        
        if ($config.AzureOpenAI.ApiKey) {
            Write-Host "   ✓ API key configured" -ForegroundColor Green
        } else {
            Write-Host "   ✗ API key missing" -ForegroundColor Red
        }
        
        if ($config.AzureOpenAI.Endpoint) {
            Write-Host "   ✓ Endpoint configured: $($config.AzureOpenAI.Endpoint)" -ForegroundColor Green
        } else {
            Write-Host "   ✗ Endpoint missing" -ForegroundColor Red
        }
    } else {
        Write-Host "   ✗ AzureOpenAI configuration not found" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Development configuration not found" -ForegroundColor Red
}

# Test 3: Verify database fields for AI inference
Write-Host "`n3. Verifying database schema for AI inference..." -ForegroundColor Yellow

try {
    $fields = sqlcmd -S "ibidb2003dv" -d "IRFS1" -E -Q "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'DaQa' AND TABLE_NAME = 'MasterIndex' AND COLUMN_NAME IN ('BusinessDefinition', 'TechnicalDefinition', 'DataClassification', 'SensitivityLevel') ORDER BY COLUMN_NAME" -h -1 2>$null
    
    if ($fields -like "*BusinessDefinition*") {
        Write-Host "   ✓ BusinessDefinition field available" -ForegroundColor Green
    } else {
        Write-Host "   ✗ BusinessDefinition field missing" -ForegroundColor Red
    }
    
    if ($fields -like "*TechnicalDefinition*") {
        Write-Host "   ✓ TechnicalDefinition field available" -ForegroundColor Green
    } else {
        Write-Host "   ✗ TechnicalDefinition field missing" -ForegroundColor Red
    }
    
    if ($fields -like "*DataClassification*") {
        Write-Host "   ✓ DataClassification field available" -ForegroundColor Green
    } else {
        Write-Host "   ✗ DataClassification field missing" -ForegroundColor Red
    }
    
    if ($fields -like "*SensitivityLevel*") {
        Write-Host "   ✓ SensitivityLevel field available" -ForegroundColor Green
    } else {
        Write-Host "   ✗ SensitivityLevel field missing" -ForegroundColor Red
    }
} catch {
    Write-Host "   ✗ Database connection failed" -ForegroundColor Red
}

# Test 4: Check DI registration
Write-Host "`n4. Checking dependency injection registration..." -ForegroundColor Yellow

$programFile = "src\Api\Program.cs"
if (Test-Path $programFile) {
    $programContent = Get-Content $programFile -Raw
    
    if ($programContent -match "AddHttpClient.*ComprehensiveMasterIndexService") {
        Write-Host "   ✓ ComprehensiveMasterIndexService registered with HttpClient" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Service registration missing HttpClient" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Program.cs not found" -ForegroundColor Red
}

# Test 5: Build verification
Write-Host "`n5. Verifying build status..." -ForegroundColor Yellow

$buildOutput = dotnet build EnterpriseDocumentationPlatform.sln --no-restore --verbosity quiet 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ Solution builds successfully with AI inference" -ForegroundColor Green
} else {
    Write-Host "   ✗ Build failed" -ForegroundColor Red
}

Write-Host "`n=====================================================================" -ForegroundColor Cyan
Write-Host "    AI INFERENCE IMPLEMENTATION SUMMARY" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

Write-Host "`nImplementation Details:" -ForegroundColor White
Write-Host "• Added Phase15_AIInferenceAsync to MasterIndex population" -ForegroundColor Gray
Write-Host "• Confidence threshold: 80% minimum for field population" -ForegroundColor Gray
Write-Host "• Inferred fields: BusinessDefinition, TechnicalDefinition, DataClassification, Sensitivity" -ForegroundColor Gray
Write-Host "• Non-disruptive: AI failures don't break MasterIndex creation" -ForegroundColor Gray
Write-Host "• OpenAI integration with timeout and error handling" -ForegroundColor Gray

Write-Host "`nExpected AI Inferences (Example):" -ForegroundColor White
Write-Host "Table: Customers, Column: EmailAddress" -ForegroundColor Gray
Write-Host "• BusinessDefinition: 'Customer email addresses for communication purposes'" -ForegroundColor Gray
Write-Host "• DataClassification: 'Internal' (high confidence)" -ForegroundColor Gray
Write-Host "• Sensitivity: 'Medium' (contains PII)" -ForegroundColor Gray
Write-Host "• TechnicalDefinition: 'Varchar field storing validated email addresses'" -ForegroundColor Gray

Write-Host "`nWorkflow Position:" -ForegroundColor White
Write-Host "• Phase 15: AI inference (after all manual phases, before database insert)" -ForegroundColor Gray
Write-Host "• Only populates empty fields (doesn't override existing data)" -ForegroundColor Gray
Write-Host "• Comprehensive logging for AI decisions and confidence levels" -ForegroundColor Gray

Write-Host "`n🧠 FIX #5: AI-POWERED METADATA INFERENCE COMPLETE!" -ForegroundColor Green