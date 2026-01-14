# Deploy-DTO-Namespace-Fix.ps1
# Master script to fix all DTO namespace collisions

$ErrorActionPreference = "Stop"

Write-Host "╔══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  DTO Namespace Collision Fix - Production Deployment    ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

# Change to project root
Set-Location $projectRoot

Write-Host "`n[PHASE 1] Creating Missing DTOs" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Gray

$dtosPath = "$projectRoot\src\Core\Application\DTOs"

# Ensure DTOs directory exists
if (-not (Test-Path $dtosPath)) {
    New-Item -ItemType Directory -Path $dtosPath -Force | Out-Null
    Write-Host "✓ Created DTOs directory" -ForegroundColor Green
} else {
    Write-Host "✓ DTOs directory exists" -ForegroundColor Green
}

# Create ApprovalDTOs.cs
Write-Host "`nCreating ApprovalDTOs.cs..." -ForegroundColor Cyan

$approvalDTOsContent = @'
// src/Core/Application/DTOs/ApprovalDTOs.cs
namespace Enterprise.Documentation.Core.Application.DTOs;

public record CreateApprovalRequest(
    Guid DocumentId,
    string DocumentType,
    string RequestedBy,
    string? Comments = null,
    List<string>? Approvers = null
);

public record ApprovalDecision(
    string ApprovedBy,
    string? Comments = null,
    DateTime? ApprovedAt = null
);

public record RejectionDecision(
    string RejectedBy,
    string RejectionReason,
    string? Comments = null,
    DateTime? RejectedAt = null
);

public record ApprovalResult(
    bool Success,
    string Message,
    Guid? ApprovalId = null,
    string? Status = null
);

public record ApprovalSummary(
    Guid ApprovalId,
    Guid DocumentId,
    string DocumentType,
    string Status,
    string RequestedBy,
    DateTime RequestedDate,
    string? ApprovedBy = null,
    DateTime? ApprovedDate = null
);

public record EditDecision(
    string EditedBy,
    Dictionary<string, object> Changes,
    string? Comments = null
);

public record EditResult(
    bool Success,
    string Message,
    Dictionary<string, object>? UpdatedFields = null
);

public record UpdateDocumentRequest(
    string? Title = null,
    string? Description = null,
    Dictionary<string, object>? Metadata = null,
    string? UpdatedBy = null
);

public record Suggestion(
    string SuggestedBy,
    string SuggestionText,
    string? Category = null,
    int? Priority = null
);

public record SuggestionResult(
    bool Success,
    string Message,
    Guid? SuggestionId = null
);

public record ApprovalDetails(
    Guid ApprovalId,
    Guid DocumentId,
    string DocumentType,
    string Status,
    string RequestedBy,
    DateTime RequestedDate,
    string? ApprovedBy = null,
    DateTime? ApprovedDate = null,
    string? RejectedBy = null,
    DateTime? RejectedDate = null,
    string? RejectionReason = null,
    List<Suggestion>? Suggestions = null,
    Dictionary<string, object>? Metadata = null
);

public record ApprovalStats(
    int TotalApprovals,
    int PendingApprovals,
    int ApprovedCount,
    int RejectedCount,
    int AverageApprovalTimeHours,
    Dictionary<string, int>? ApprovalsByType = null
);

public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
)
{
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
'@

Set-Content -Path "$dtosPath\ApprovalDTOs.cs" -Value $approvalDTOsContent -Encoding UTF8
Write-Host "✓ ApprovalDTOs.cs created" -ForegroundColor Green

# Create SchemaDTOs.cs
Write-Host "Creating SchemaDTOs.cs..." -ForegroundColor Cyan

$schemaDTOsContent = @'
// src/Core/Application/DTOs/SchemaDTOs.cs
namespace Enterprise.Documentation.Core.Application.DTOs;

public record SchemaMetadata(
    string SchemaName,
    string DatabaseName,
    int TableCount,
    int ColumnCount,
    int StoredProcedureCount,
    DateTime LastAnalyzed,
    Dictionary<string, object>? AdditionalMetadata = null
);

public record SchemaStats(
    string SchemaName,
    int TotalTables,
    int TotalColumns,
    int TotalStoredProcedures,
    int TotalViews,
    long TotalRows,
    decimal SizeInMB,
    DateTime LastUpdated
);
'@

Set-Content -Path "$dtosPath\SchemaDTOs.cs" -Value $schemaDTOsContent -Encoding UTF8
Write-Host "✓ SchemaDTOs.cs created" -ForegroundColor Green

Write-Host "`n[PHASE 2] Fixing Interface Files" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Gray

$interfacesPath = "$projectRoot\src\Core\Application\Interfaces"

# Backup Interfaces folder
Write-Host "`nCreating backup..." -ForegroundColor Cyan
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupPath = "$interfacesPath.backup_$timestamp"
Copy-Item -Path $interfacesPath -Destination $backupPath -Recurse -Force
Write-Host "✓ Backup created at: $backupPath" -ForegroundColor Green

# Create clean IDocumentServices.cs
Write-Host "Creating clean IDocumentServices.cs..." -ForegroundColor Cyan

$cleanInterfaceContent = @'
// src/Core/Application/Interfaces/IDocumentServices.cs
using Enterprise.Documentation.Core.Application.DTOs;

namespace Enterprise.Documentation.Core.Application.Interfaces;

/// <summary>
/// Document approval workflow service
/// </summary>
public interface IApprovalService
{
    Task<ApprovalResult> CreateAsync(CreateApprovalRequest request, CancellationToken cancellationToken = default);
    Task<ApprovalResult> ApproveAsync(Guid approvalId, ApprovalDecision decision, CancellationToken cancellationToken = default);
    Task<ApprovalResult> RejectAsync(Guid approvalId, RejectionDecision decision, CancellationToken cancellationToken = default);
    Task<PagedResult<ApprovalSummary>> GetApprovalsAsync(int page, int pageSize, string? status = null, CancellationToken cancellationToken = default);
    Task<EditResult> EditAsync(Guid approvalId, EditDecision decision, CancellationToken cancellationToken = default);
    Task<ApprovalResult> UpdateDocumentAsync(Guid approvalId, UpdateDocumentRequest request, CancellationToken cancellationToken = default);
    Task<SuggestionResult> AddSuggestionAsync(Guid approvalId, Suggestion suggestion, CancellationToken cancellationToken = default);
    Task<ApprovalDetails?> GetDetailsAsync(Guid approvalId, CancellationToken cancellationToken = default);
    Task<ApprovalStats> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateApprovalRequestAsync(CreateApprovalRequest request);
}

/// <summary>
/// Schema metadata extraction and management service
/// </summary>
public interface ISchemaMetadataService
{
    Task<SchemaMetadata> GetSchemaMetadataAsync(string schemaName, CancellationToken cancellationToken = default);
    Task<SchemaStats> GetSchemaStatsAsync(string schemaName, CancellationToken cancellationToken = default);
    Task<SchemaMetadata> ExtractSchemaMetadataAsync(string connectionString, string schemaName);
}
'@

Set-Content -Path "$interfacesPath\IDocumentServices.cs" -Value $cleanInterfaceContent -Encoding UTF8
Write-Host "✓ IDocumentServices.cs cleaned" -ForegroundColor Green

# Remove problematic files
$problematicFiles = @(
    "$interfacesPath\IMissingServices.cs",
    "$interfacesPath\MissingInterfaces.cs"
)

foreach ($file in $problematicFiles) {
    if (Test-Path $file) {
        Write-Host "Removing $file..." -ForegroundColor Cyan
        Remove-Item -Path $file -Force
        Write-Host "✓ Removed duplicate file" -ForegroundColor Green
    }
}

Write-Host "`n[PHASE 3] Building Solution" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Gray

Write-Host "`nCleaning solution..." -ForegroundColor Cyan
dotnet clean --verbosity quiet
Write-Host "✓ Clean complete" -ForegroundColor Green

Write-Host "`nBuilding solution..." -ForegroundColor Cyan
$buildOutput = dotnet build --verbosity minimal 2>&1
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -eq 0) {
    Write-Host "`n╔══════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║              ✓ BUILD SUCCESSFUL                          ║" -ForegroundColor Green
    Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Green
    
    Write-Host "`nDeployment Summary:" -ForegroundColor Cyan
    Write-Host "  ✓ Created ApprovalDTOs.cs with 13 DTO types" -ForegroundColor White
    Write-Host "  ✓ Created SchemaDTOs.cs with 2 DTO types" -ForegroundColor White
    Write-Host "  ✓ Cleaned IDocumentServices.cs (interfaces only)" -ForegroundColor White
    Write-Host "  ✓ Removed duplicate interface files" -ForegroundColor White
    Write-Host "  ✓ Solution builds successfully" -ForegroundColor White
    Write-Host "`nBackup location: $backupPath" -ForegroundColor Gray
} else {
    Write-Host "`n╔══════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║              ✗ BUILD FAILED                              ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host "`nBuild errors:" -ForegroundColor Red
    Write-Host $buildOutput -ForegroundColor Yellow
    Write-Host "`nRestore from backup: $backupPath" -ForegroundColor Yellow
    exit 1
}