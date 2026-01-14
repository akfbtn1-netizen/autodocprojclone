# Deploy-DTO-Namespace-Fix.ps1
# Master script to fix all DTO namespace collisions

$ErrorActionPreference = "Stop"

Write-Host "`nDTO Namespace Collision Fix - Production Deployment" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"
Set-Location $projectRoot

Write-Host "`n[PHASE 1] Creating Missing DTOs" -ForegroundColor Yellow
$dtosPath = "$projectRoot\src\Core\Application\DTOs"

# Ensure DTOs directory exists
if (-not (Test-Path $dtosPath)) {
    New-Item -ItemType Directory -Path $dtosPath -Force | Out-Null
    Write-Host "Created DTOs directory" -ForegroundColor Green
} else {
    Write-Host "DTOs directory exists" -ForegroundColor Green
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
Write-Host "ApprovalDTOs.cs created" -ForegroundColor Green

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
Write-Host "SchemaDTOs.cs created" -ForegroundColor Green

Write-Host "`n[PHASE 2] Fixing Interface Files" -ForegroundColor Yellow

$interfacesPath = "$projectRoot\src\Core\Application\Interfaces"

# Create backup
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupPath = "$interfacesPath.backup_$timestamp"
Copy-Item -Path $interfacesPath -Destination $backupPath -Recurse -Force
Write-Host "Backup created at: $backupPath" -ForegroundColor Green

# Create clean IDocumentServices.cs
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
    Task<PagedResult<object>> GetApprovalsAsync(int page, int pageSize, string? status = null, CancellationToken cancellationToken = default);
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
Write-Host "IDocumentServices.cs cleaned" -ForegroundColor Green

# Remove problematic files
$problematicFiles = @(
    "$interfacesPath\IMissingServices.cs",
    "$interfacesPath\MissingInterfaces.cs"
)

foreach ($file in $problematicFiles) {
    if (Test-Path $file) {
        Write-Host "Removing $file..."
        Remove-Item -Path $file -Force
        Write-Host "Removed duplicate file" -ForegroundColor Green
    }
}

Write-Host "`n[PHASE 3] Building Solution" -ForegroundColor Yellow

Write-Host "Cleaning solution..." -ForegroundColor Cyan
dotnet clean --verbosity quiet

Write-Host "Building solution..." -ForegroundColor Cyan
$buildOutput = dotnet build --verbosity minimal 2>&1
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -eq 0) {
    Write-Host "`nBUILD SUCCESSFUL" -ForegroundColor Green
    Write-Host "`nDeployment Summary:" -ForegroundColor Cyan
    Write-Host "- Created ApprovalDTOs.cs with DTO types" -ForegroundColor White
    Write-Host "- Created SchemaDTOs.cs with schema types" -ForegroundColor White
    Write-Host "- Cleaned IDocumentServices.cs" -ForegroundColor White
    Write-Host "- Removed duplicate interface files" -ForegroundColor White
    Write-Host "- Solution builds successfully" -ForegroundColor White
    Write-Host "`nBackup location: $backupPath" -ForegroundColor Gray
} else {
    Write-Host "`nBUILD FAILED" -ForegroundColor Red
    Write-Host "`nBuild errors:" -ForegroundColor Red
    Write-Host $buildOutput -ForegroundColor Yellow
    Write-Host "`nRestore from backup: $backupPath" -ForegroundColor Yellow
    exit 1
}