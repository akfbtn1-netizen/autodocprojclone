# ============================================================================
# MasterIndex Fix - Manual Guide
# ============================================================================
# Shows exactly what changes to make to fix the MasterIndex namespace conflict
# ============================================================================

$ErrorActionPreference = "Stop"

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "MasterIndex Namespace Conflict - Manual Fix Guide" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

Write-Host "You need to add ONE line to TWO files:" -ForegroundColor Yellow
Write-Host ""

# File 1
Write-Host "FILE 1: TemplateSelector.cs" -ForegroundColor Cyan
Write-Host "Location: src\Core\Application\Services\TemplateSelector.cs" -ForegroundColor Gray
Write-Host ""
Write-Host "Add this line AFTER the existing using statements (before namespace):" -ForegroundColor White
Write-Host ""
Write-Host "  using MasterIndexEntity = Enterprise.Documentation.Core.Domain.Entities.MasterIndex;" -ForegroundColor Green
Write-Host ""
Write-Host "Then Find and Replace in this file only:" -ForegroundColor White
Write-Host "  Find:    " -NoNewline -ForegroundColor White
Write-Host "Task<string> SelectTemplate(MasterIndex " -ForegroundColor Yellow
Write-Host "  Replace: " -NoNewline -ForegroundColor White
Write-Host "Task<string> SelectTemplate(MasterIndexEntity " -ForegroundColor Green
Write-Host ""
Write-Host "Press Enter to see File 2..." -ForegroundColor Gray
Read-Host
Write-Host ""

# File 2
Write-Host "FILE 2: DocGeneratorService.cs" -ForegroundColor Cyan
Write-Host "Location: src\Core\Application\Services\DocGeneratorService.cs" -ForegroundColor Gray
Write-Host ""
Write-Host "Add this line AFTER the existing using statements (before namespace):" -ForegroundColor White
Write-Host ""
Write-Host "  using MasterIndexEntity = Enterprise.Documentation.Core.Domain.Entities.MasterIndex;" -ForegroundColor Green
Write-Host ""
Write-Host "Then Find and Replace in this file only (6 replacements):" -ForegroundColor White
Write-Host ""
Write-Host "  Find:    " -NoNewline -ForegroundColor White
Write-Host "new MasterIndex" -ForegroundColor Yellow
Write-Host "  Replace: " -NoNewline -ForegroundColor White
Write-Host "new MasterIndexEntity" -ForegroundColor Green
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "DETAILED STEPS:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Open Visual Studio" -ForegroundColor White
Write-Host "2. Open: src\Core\Application\Services\TemplateSelector.cs" -ForegroundColor White
Write-Host "3. Find the using statements at the top (should be around lines 1-5)" -ForegroundColor White
Write-Host "4. After the LAST using statement, add a new line:" -ForegroundColor White
Write-Host "   using MasterIndexEntity = Enterprise.Documentation.Core.Domain.Entities.MasterIndex;" -ForegroundColor Green
Write-Host "5. Press Ctrl+H (Find and Replace)" -ForegroundColor White
Write-Host "6. Find: 'Task<string> SelectTemplate(MasterIndex '" -ForegroundColor White
Write-Host "7. Replace with: 'Task<string> SelectTemplate(MasterIndexEntity '" -ForegroundColor White
Write-Host "8. Save the file" -ForegroundColor White
Write-Host ""
Write-Host "9. Open: src\Core\Application\Services\DocGeneratorService.cs" -ForegroundColor White
Write-Host "10. Add the same using alias after the last using statement" -ForegroundColor White
Write-Host "11. Press Ctrl+H (Find and Replace)" -ForegroundColor White
Write-Host "12. Find: 'new MasterIndex'" -ForegroundColor White
Write-Host "13. Replace with: 'new MasterIndexEntity'" -ForegroundColor White
Write-Host "14. Click 'Replace All' (should find 6 instances)" -ForegroundColor White
Write-Host "15. Save the file" -ForegroundColor White
Write-Host ""
Write-Host "16. Build the solution: Ctrl+Shift+B" -ForegroundColor White
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "EXAMPLE of what the using statements should look like:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  using System;" -ForegroundColor Gray
Write-Host "  using System.Collections.Generic;" -ForegroundColor Gray
Write-Host "  using System.Threading.Tasks;" -ForegroundColor Gray
Write-Host "  using MasterIndexEntity = Enterprise.Documentation.Core.Domain.Entities.MasterIndex;" -ForegroundColor Green
Write-Host ""
Write-Host "  namespace Enterprise.Documentation.Core.Application.Services;" -ForegroundColor Gray
Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

Read-Host "Press Enter to exit"
