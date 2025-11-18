# ============================================================================
# FIX BUILD ERRORS - Approval Workflow
# ============================================================================
# Fixes:
# 1. MasterIndex ambiguity - rename new one to MasterIndexExpanded
# 2. Missing DTOs - add missing response types
# 3. Move IApprovalRepository interface to Application layer
# 4. Add missing NuGet packages
# 5. Fix ApprovalService to match interface signatures
# ============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  FIXING BUILD ERRORS" -ForegroundColor White
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# FIX 1: Rename MasterIndex.cs to avoid ambiguity
# ============================================================================
Write-Host "[1/5] Fixing MasterIndex ambiguity..." -ForegroundColor Yellow

$masterIndexPath = Join-Path $projectRoot "src\Core\Domain\Models\MasterIndex.cs"
if (Test-Path $masterIndexPath) {
    # Rename to MasterIndexExpanded or delete since they already have one
    Remove-Item $masterIndexPath -Force
    Write-Host "  Removed duplicate MasterIndex.cs (using existing one)" -ForegroundColor Green
}

# ============================================================================
# FIX 2: Add missing DTOs to ApprovalDTOs.cs
# ============================================================================
Write-Host "[2/5] Adding missing DTOs..." -ForegroundColor Yellow

$additionalDtos = @'

// ============================================================================
// Additional Response DTOs (added for ApprovalService)
// ============================================================================

/// <summary>
/// Response when queuing a document for approval.
/// </summary>
public record QueueApprovalResponse
{
    public Guid ApprovalId { get; init; }
    public string DocumentId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string AssignedApprover { get; init; } = string.Empty;
    public DateTime QueuedAt { get; init; }
    public string DestinationPath { get; init; } = string.Empty;
}

/// <summary>
/// DTO for pending approval list items.
/// </summary>
public record PendingApprovalDto
{
    public Guid ApprovalId { get; init; }
    public string DocumentId { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string SchemaName { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public DateTime QueuedAt { get; init; }
    public string AssignedApprover { get; init; } = string.Empty;
    public string PreviewUrl { get; init; } = string.Empty;
    public int EditCount { get; init; }
    public int RegenerationCount { get; init; }
}

/// <summary>
/// Detailed approval information.
/// </summary>
public record ApprovalDetailDto
{
    public Guid ApprovalId { get; init; }
    public string DocumentId { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public string SchemaName { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime QueuedAt { get; init; }
    public string AssignedApprover { get; init; } = string.Empty;
    public string TempFilePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public int GenerationDurationMs { get; init; }
    public string? TemplateUsed { get; init; }
    public int TokensUsed { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? RejectedAt { get; init; }
    public string? RejectedBy { get; init; }
    public string? RejectionReason { get; init; }
    public string? FinalFilePath { get; init; }
    public string? SharePointUrl { get; init; }
    public int EditCount { get; init; }
    public int RegenerationCount { get; init; }
    public List<ApprovalHistoryDto> History { get; init; } = new();
    public List<DocumentEditDto> Edits { get; init; } = new();
}

/// <summary>
/// Response for approval actions (approve/reject/edit).
/// </summary>
public record ApprovalActionResponse
{
    public bool Success { get; init; }
    public string DocumentId { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? FinalFilePath { get; init; }
}

/// <summary>
/// Response for regeneration requests.
/// </summary>
public record RegenerationResponse
{
    public bool Success { get; init; }
    public Guid RegenerationRequestId { get; init; }
    public string Message { get; init; } = string.Empty;
    public string OriginalDocumentId { get; init; } = string.Empty;
}

/// <summary>
/// Approval history entry DTO.
/// </summary>
public record ApprovalHistoryDto
{
    public string Action { get; init; } = string.Empty;
    public string PerformedBy { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? Details { get; init; }
    public string? PreviousStatus { get; init; }
    public string? NewStatus { get; init; }
}
'@

$dtosPath = Join-Path $projectRoot "src\Core\Application\DTOs\Approval\ApprovalDTOs.cs"
if (Test-Path $dtosPath) {
    $existingContent = [System.IO.File]::ReadAllText($dtosPath)
    # Append new DTOs before the closing namespace brace would go
    $newContent = $existingContent + $additionalDtos
    [System.IO.File]::WriteAllText($dtosPath, $newContent, $utf8NoBom)
    Write-Host "  Added missing DTOs to ApprovalDTOs.cs" -ForegroundColor Green
}

# ============================================================================
# FIX 3: Move IApprovalRepository to Application layer
# ============================================================================
Write-Host "[3/5] Moving IApprovalRepository interface to Application..." -ForegroundColor Yellow

$repoInterfacePath = Join-Path $projectRoot "src\Core\Application\Interfaces\Approval\IApprovalRepository.cs"

$repoInterfaceContent = @'
using Enterprise.Documentation.Core.Domain.Models.Approval;

namespace Enterprise.Documentation.Core.Application.Interfaces.Approval;

public interface IApprovalRepository
{
    // Document Approvals
    Task<DocumentApproval?> GetByIdAsync(Guid id);
    Task<DocumentApproval?> GetByDocumentIdAsync(string documentId);
    Task<IEnumerable<DocumentApproval>> GetPendingApprovalsAsync();
    Task<IEnumerable<DocumentApproval>> GetApprovalsByStatusAsync(string status);
    Task<IEnumerable<DocumentApproval>> GetApprovalsByApproverAsync(string approverEmail);
    Task<Guid> CreateAsync(DocumentApproval approval);
    Task UpdateAsync(DocumentApproval approval);

    // Approval History
    Task<IEnumerable<ApprovalHistoryEntry>> GetHistoryAsync(Guid approvalId);
    Task AddHistoryEntryAsync(ApprovalHistoryEntry entry);

    // Document Edits
    Task<IEnumerable<DocumentEdit>> GetEditsAsync(string documentId);
    Task AddEditAsync(DocumentEdit edit);

    // Regeneration Requests
    Task<RegenerationRequest?> GetRegenerationRequestAsync(Guid id);
    Task<Guid> CreateRegenerationRequestAsync(RegenerationRequest request);
    Task UpdateRegenerationRequestAsync(RegenerationRequest request);

    // Approvers
    Task<IEnumerable<Approver>> GetActiveApproversAsync();
    Task<Approver?> GetApproverByEmailAsync(string email);

    // Document ID Generation
    Task<int> GetNextSequenceNumberAsync(string documentType, int year);
}
'@

[System.IO.File]::WriteAllText($repoInterfacePath, $repoInterfaceContent, $utf8NoBom)
Write-Host "  Created IApprovalRepository.cs in Application layer" -ForegroundColor Green

# ============================================================================
# FIX 4: Update ApprovalService.cs to use correct namespace
# ============================================================================
Write-Host "[4/5] Updating ApprovalService.cs imports..." -ForegroundColor Yellow

$approvalServicePath = Join-Path $projectRoot "src\Core\Application\Services\Approval\ApprovalService.cs"
if (Test-Path $approvalServicePath) {
    $content = [System.IO.File]::ReadAllText($approvalServicePath)

    # Replace Infrastructure import with Application
    $content = $content -replace 'using Enterprise\.Documentation\.Infrastructure\.Persistence\.Repositories;', 'using Enterprise.Documentation.Core.Application.Interfaces.Approval;'

    [System.IO.File]::WriteAllText($approvalServicePath, $content, $utf8NoBom)
    Write-Host "  Updated ApprovalService.cs imports" -ForegroundColor Green
}

# ============================================================================
# FIX 5: Update ApprovalRepository.cs to implement interface from Application
# ============================================================================
Write-Host "[5/5] Updating ApprovalRepository.cs..." -ForegroundColor Yellow

$repoPath = Join-Path $projectRoot "src\Infrastructure\Persistence\Repositories\ApprovalRepository.cs"
if (Test-Path $repoPath) {
    $content = [System.IO.File]::ReadAllText($repoPath)

    # Remove the interface definition from this file (it's now in Application)
    # Add import for the interface
    $content = $content -replace 'namespace Enterprise\.Documentation\.Infrastructure\.Persistence\.Repositories;', @'
using Enterprise.Documentation.Core.Application.Interfaces.Approval;

namespace Enterprise.Documentation.Infrastructure.Persistence.Repositories;
'@

    # Remove the interface definition block
    $content = $content -replace '(?s)public interface IApprovalRepository\s*\{[^}]+\}', ''

    [System.IO.File]::WriteAllText($repoPath, $content, $utf8NoBom)
    Write-Host "  Updated ApprovalRepository.cs" -ForegroundColor Green
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  BUILD FIXES COMPLETE" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Additional manual steps needed:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Add NuGet packages to Core.Application.csproj:" -ForegroundColor White
Write-Host "   dotnet add src/Core/Application package ClosedXML" -ForegroundColor Cyan
Write-Host "   dotnet add src/Core/Application package Dapper" -ForegroundColor Cyan
Write-Host "   dotnet add src/Core/Application package Microsoft.Data.SqlClient" -ForegroundColor Cyan
Write-Host "   dotnet add src/Core/Application package Microsoft.Extensions.Hosting.Abstractions" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Or move ExcelWatcherService to Infrastructure project" -ForegroundColor White
Write-Host ""
Write-Host "3. Rebuild: dotnet build" -ForegroundColor White
Write-Host ""
