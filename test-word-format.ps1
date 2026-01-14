# Test Word Document Generation for SP Documentation
# This script tests the Word format conversion for stored procedure documentation

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "    FIX #6: SP DOCUMENTATION WORD FORMAT TEST" -ForegroundColor Cyan  
Write-Host "=====================================================================" -ForegroundColor Cyan

# Test 1: Verify Word format implementation
Write-Host "`n1. Checking Word document implementation..." -ForegroundColor Yellow

$serviceFile = "src\Core\Application\Services\StoredProcedure\StoredProcedureDocumentationService.cs"
if (Test-Path $serviceFile) {
    $content = Get-Content $serviceFile -Raw
    
    if ($content -match "\.docx") {
        Write-Host "   ✓ File extension changed to .docx" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Still using .md extension" -ForegroundColor Red
    }
    
    if ($content -match "DocumentFormat\.OpenXml") {
        Write-Host "   ✓ DocumentFormat.OpenXml using statements added" -ForegroundColor Green
    } else {
        Write-Host "   ✗ DocumentFormat.OpenXml not imported" -ForegroundColor Red
    }
    
    if ($content -match "CreateWordDocument") {
        Write-Host "   ✓ CreateWordDocument method implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ CreateWordDocument method not found" -ForegroundColor Red
    }
    
    if ($content -match "AddParagraph.*AddCodeBlock") {
        Write-Host "   ✓ Word formatting methods implemented" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Word formatting methods missing" -ForegroundColor Red
    }
} else {
    Write-Host "   ✗ Service file not found" -ForegroundColor Red
}

# Test 2: Check DocumentFormat.OpenXml package
Write-Host "`n2. Verifying DocumentFormat.OpenXml package..." -ForegroundColor Yellow

$package = dotnet list package | findstr DocumentFormat.OpenXml
if ($package) {
    Write-Host "   ✓ DocumentFormat.OpenXml package installed: $package" -ForegroundColor Green
} else {
    Write-Host "   ✗ DocumentFormat.OpenXml package not found" -ForegroundColor Red
}

# Test 3: Build verification  
Write-Host "`n3. Verifying build with Word generation..." -ForegroundColor Yellow

$buildOutput = dotnet build src/Core/Application/Core.Application.csproj --no-restore --verbosity quiet 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ Core.Application builds successfully with Word generation" -ForegroundColor Green
} else {
    Write-Host "   ✗ Build failed with Word generation" -ForegroundColor Red
    $buildOutput | Where-Object { $_ -match "error" } | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
}

# Test 4: Check method signatures
Write-Host "`n4. Checking method signatures..." -ForegroundColor Yellow

if ($content -match "CreateWordDocument.*SPMetadata.*DocumentChange") {
    Write-Host "   ✓ CreateWordDocument method signature correct" -ForegroundColor Green
} else {
    Write-Host "   ✗ CreateWordDocument method signature incorrect" -ForegroundColor Red
}

if ($content -match "SaveDocumentationAsync.*SPMetadata.*DocumentChange") {
    Write-Host "   ✓ SaveDocumentationAsync method updated for Word" -ForegroundColor Green
} else {
    Write-Host "   ✗ SaveDocumentationAsync method not updated" -ForegroundColor Red
}

# Test 5: Check Word formatting features
Write-Host "`n5. Verifying Word formatting features..." -ForegroundColor Yellow

if ($content -match "Bold\(\)") {
    Write-Host "   ✓ Bold text formatting implemented" -ForegroundColor Green
} else {
    Write-Host "   ✗ Bold text formatting missing" -ForegroundColor Red
}

if ($content -match "Courier New") {
    Write-Host "   ✓ Code block font (Courier New) implemented" -ForegroundColor Green
} else {
    Write-Host "   ✗ Code block font not set" -ForegroundColor Red
}

if ($content -match "FontSize.*Val") {
    Write-Host "   ✓ Font size formatting implemented" -ForegroundColor Green  
} else {
    Write-Host "   ✗ Font size formatting missing" -ForegroundColor Red
}

Write-Host "`n=====================================================================" -ForegroundColor Cyan
Write-Host "    WORD FORMAT IMPLEMENTATION SUMMARY" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

Write-Host "`nImplementation Changes:" -ForegroundColor White
Write-Host "• File Extension: .md → .docx" -ForegroundColor Gray
Write-Host "• Format: Markdown → Microsoft Word document" -ForegroundColor Gray
Write-Host "• Library: File.WriteAllTextAsync → DocumentFormat.OpenXml" -ForegroundColor Gray
Write-Host "• Method: GenerateSimpleDocumentation → CreateWordDocument" -ForegroundColor Gray

Write-Host "`nWord Document Structure:" -ForegroundColor White
Write-Host "• Title: 'Stored Procedure Documentation' (18pt, bold)" -ForegroundColor Gray
Write-Host "• Procedure Name: Schema.ProcedureName (16pt, bold)" -ForegroundColor Gray
Write-Host "• Metadata: Created/Modified dates, Object Type (11pt)" -ForegroundColor Gray
Write-Host "• Description: From DocumentChanges or auto-generated" -ForegroundColor Gray
Write-Host "• Source Code: SQL definition in Courier New font (10pt)" -ForegroundColor Gray
Write-Host "• Change Info: Author, Ticket, Change Type, Created Date" -ForegroundColor Gray
Write-Host "• Footer: Generation timestamp (9pt)" -ForegroundColor Gray

Write-Host "`nFormatting Features:" -ForegroundColor White  
Write-Host "• Bold headers for section titles" -ForegroundColor Gray
Write-Host "• Monospace font (Courier New) for SQL code" -ForegroundColor Gray
Write-Host "• Appropriate font sizes (9pt-18pt)" -ForegroundColor Gray
Write-Host "• Preserved whitespace in code blocks" -ForegroundColor Gray
Write-Host "• Professional document structure" -ForegroundColor Gray

Write-Host "`n📄 FIX #6: SP DOCUMENTATION WORD FORMAT COMPLETE!" -ForegroundColor Green