# Deploy DTO Namespace Fix Script - Corrected Version
# This script fixes namespace collisions by properly separating DTOs from interfaces

Write-Host "Starting DTO Namespace Fix Deployment..." -ForegroundColor Green

# Stop any running processes
Write-Host "Stopping any running API processes..." -ForegroundColor Yellow
Get-Process -Name "Api" -ErrorAction SilentlyContinue | Stop-Process -Force

# Navigate to project root
Set-Location "C:\Projects\EnterpriseDocumentationPlatform.V2"

# Clean build artifacts
Write-Host "Cleaning build artifacts..." -ForegroundColor Yellow
Remove-Item -Path "src\*/bin", "src\*/obj", "src\*\*/bin", "src\*\*/obj" -Recurse -Force -ErrorAction SilentlyContinue

# Create backup of existing interfaces
$backupDir = "src\Core\Application\Interfaces.backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
if (Test-Path "src\Core\Application\Interfaces") {
    Copy-Item -Path "src\Core\Application\Interfaces" -Destination $backupDir -Recurse -Force
    Write-Host "Backup created at: $backupDir" -ForegroundColor Cyan
}

# Create DTOs directory if it doesn't exist
$dtosPath = "src\Core\Application\DTOs"
if (!(Test-Path $dtosPath)) {
    New-Item -ItemType Directory -Path $dtosPath -Force | Out-Null
    Write-Host "Created DTOs directory: $dtosPath" -ForegroundColor Cyan
}

# Create comprehensive DTOs file
$dtoContent = @"
using System;
using System.Collections.Generic;

namespace Enterprise.Documentation.Core.Application.DTOs
{
    public record CreateApprovalRequest(
        Guid DocumentId,
        string Title,
        string Description,
        string RequestedBy,
        DateTime RequestedAt,
        string Priority = "Normal",
        Dictionary<string, object>? Metadata = null
    );

    public record ApprovalDecision(
        Guid ApprovalId,
        string Decision,
        string Comments,
        string ApprovedBy,
        DateTime ApprovedAt,
        Dictionary<string, object>? Metadata = null
    );

    public record RejectionDecision(
        Guid ApprovalId,
        string Reason,
        string Comments,
        string RejectedBy,
        DateTime RejectedAt
    );

    public record ApprovalResult(
        bool IsSuccess,
        string Message,
        Guid? ApprovalId = null,
        Dictionary<string, object>? Data = null
    );

    public record PagedResult<T>(
        IEnumerable<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize,
        bool HasNextPage = false,
        bool HasPreviousPage = false
    );

    public record ApprovalSummary(
        Guid Id,
        string Title,
        string Status,
        string RequestedBy,
        DateTime RequestedAt,
        string? AssignedTo = null,
        DateTime? DueDate = null
    );

    public record EditDecision(
        Guid ApprovalId,
        string EditType,
        string Changes,
        string EditedBy,
        DateTime EditedAt
    );

    public record EditResult(
        bool IsSuccess,
        string Message,
        Guid? EditId = null
    );

    public record UpdateDocumentRequest(
        Guid DocumentId,
        string Content,
        string UpdatedBy,
        string? Reason = null
    );

    public record Suggestion(
        Guid Id,
        string Content,
        string SuggestedBy,
        DateTime SuggestedAt
    );

    public record SuggestionResult(
        bool IsSuccess,
        string Message,
        Guid? SuggestionId = null
    );

    public record ApprovalDetails(
        Guid Id,
        string Title,
        string Description,
        string Status,
        string RequestedBy,
        DateTime RequestedAt,
        string? AssignedTo = null,
        DateTime? DueDate = null,
        List<string>? Comments = null,
        Dictionary<string, object>? Metadata = null
    );

    public record ApprovalStats(
        int TotalApprovals,
        int PendingApprovals,
        int CompletedApprovals,
        int RejectedApprovals,
        double AverageProcessingTime
    );

    public record SchemaMetadata(
        string SchemaName,
        string DatabaseName,
        DateTime CreatedDate,
        DateTime ModifiedDate,
        int TableCount,
        int ViewCount,
        int ProcedureCount,
        Dictionary<string, object>? Properties = null
    );

    public record SchemaStats(
        string SchemaName,
        int TotalObjects,
        int DocumentedObjects,
        int UndocumentedObjects,
        double DocumentationCoverage
    );

    public record TemplateExecutionRequest(
        string TemplatePath,
        Dictionary<string, object> Data,
        string OutputFormat = "docx",
        Dictionary<string, object>? Options = null
    );

    public record DocumentGenerationResult(
        bool IsSuccess,
        string? FilePath = null,
        string? ErrorMessage = null,
        byte[]? Content = null,
        Dictionary<string, object>? Metadata = null
    );

    public record TemplateInfo(
        string Id,
        string Name,
        string Path,
        string Description,
        string[] SupportedFormats,
        Dictionary<string, object>? Schema = null
    );

    public record TierConfig(
        string TierName,
        int Priority,
        Dictionary<string, object> Criteria,
        Dictionary<string, object>? Settings = null
    );
}
"@

$dtoFilePath = Join-Path $dtosPath "ApplicationDTOs.cs"
Set-Content -Path $dtoFilePath -Value $dtoContent -Encoding UTF8
Write-Host "Created comprehensive DTOs file: $dtoFilePath" -ForegroundColor Cyan

# Create clean interfaces directory
$interfacesPath = "src\Core\Application\Interfaces"
if (Test-Path $interfacesPath) {
    Remove-Item -Path $interfacesPath -Recurse -Force
}
New-Item -ItemType Directory -Path $interfacesPath -Force | Out-Null
Write-Host "Recreated clean interfaces directory" -ForegroundColor Cyan

# Create clean IApprovalService interface
$approvalServiceContent = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Application.DTOs;

namespace Enterprise.Documentation.Core.Application.Interfaces
{
    public interface IApprovalService
    {
        Task<ApprovalResult> CreateAsync(CreateApprovalRequest request, CancellationToken cancellationToken = default);
        Task<ApprovalDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ApprovalResult> ApproveAsync(Guid id, ApprovalDecision decision, CancellationToken cancellationToken = default);
        Task<ApprovalResult> RejectAsync(Guid id, RejectionDecision decision, CancellationToken cancellationToken = default);
        Task<PagedResult<ApprovalSummary>> GetApprovalsAsync(int pageNumber, int pageSize, string? filter = null, CancellationToken cancellationToken = default);
        Task<EditResult> EditAsync(Guid id, EditDecision decision, CancellationToken cancellationToken = default);
        Task<ApprovalDetails?> GetOriginalEntryAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ApprovalResult> UpdateDocumentAsync(Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken = default);
        Task<SuggestionResult> AddSuggestionAsync(Guid approvalId, Suggestion suggestion, CancellationToken cancellationToken = default);
        Task<ApprovalDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ApprovalStats> GetStatsAsync(CancellationToken cancellationToken = default);
        Task CreateApprovalRequestAsync(CreateApprovalRequest request);
    }
}
"@

Set-Content -Path (Join-Path $interfacesPath "IApprovalService.cs") -Value $approvalServiceContent -Encoding UTF8
Write-Host "Created clean IApprovalService interface" -ForegroundColor Cyan

# Create ISchemaMetadataService interface
$schemaServiceContent = @"
using System.Threading;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Application.DTOs;

namespace Enterprise.Documentation.Core.Application.Interfaces
{
    public interface ISchemaMetadataService
    {
        Task<SchemaMetadata?> GetSchemaMetadataAsync(string schemaName, CancellationToken cancellationToken = default);
        Task<SchemaStats> GetSchemaStatsAsync(string schemaName, CancellationToken cancellationToken = default);
        Task<SchemaMetadata> ExtractSchemaMetadataAsync(string connectionString, string schemaName);
    }
}
"@

Set-Content -Path (Join-Path $interfacesPath "ISchemaMetadataService.cs") -Value $schemaServiceContent -Encoding UTF8
Write-Host "Created clean ISchemaMetadataService interface" -ForegroundColor Cyan

# Create other required interfaces
$otherInterfacesContent = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enterprise.Documentation.Core.Application.DTOs;
using Enterprise.Documentation.Core.Domain.Entities;

namespace Enterprise.Documentation.Core.Application.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(Guid id);
        Task<IEnumerable<Document>> GetAllAsync();
        Task<Document> AddAsync(Document document);
        Task UpdateAsync(Document document);
        Task DeleteAsync(Guid id);
    }

    public interface ITemplateRepository
    {
        Task<Template?> GetByIdAsync(Guid id);
        Task<IEnumerable<Template>> GetAllAsync();
        Task<Template> AddAsync(Template template);
        Task UpdateAsync(Template template);
        Task DeleteAsync(Guid id);
    }

    public interface ICurrentUserService
    {
        string UserId { get; }
        string UserName { get; }
        bool IsAuthenticated { get; }
    }

    public interface IUnitOfWork : IDisposable
    {
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }

    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByUsernameAsync(string username);
        Task<IEnumerable<User>> GetAllAsync();
    }

    public interface IAuthorizationService
    {
        Task<bool> CanAccessAsync(string resource, string action);
        Task<bool> IsAuthorizedAsync(string permission);
    }

    public interface IExcelSyncService
    {
        Task SyncToExcelAsync(Guid documentId);
        Task SyncFromExcelAsync(string filePath);
    }

    public interface IAzureOpenAIService
    {
        Task<string> GenerateTextAsync(string prompt);
        Task<string> EnhanceDocumentationAsync(string content);
    }

    public interface ITierClassifierService
    {
        Task<string> ClassifyTierAsync(string content);
        Task<string> ClassifyAsync(string content);
        Task<TierConfig> GetTierConfigAsync(string tierName);
        Task ValidateTierAsync(string content);
    }

    public interface ITemplateSelector
    {
        Task<TemplateInfo> SelectTemplateAsync(string content);
        Task<IEnumerable<TemplateInfo>> GetAvailableTemplatesAsync();
        Task ValidateTemplateAsync(string templatePath);
    }

    public interface IMasterIndexRepository
    {
        Task<MasterIndex?> GetByIdAsync(Guid id);
        Task<IEnumerable<MasterIndex>> GetAllAsync();
        Task<MasterIndex> AddAsync(MasterIndex index);
        Task UpdateAsync(MasterIndex index);
        Task DeleteAsync(Guid id);
    }

    public interface INodeJsTemplateExecutor
    {
        Task<DocumentGenerationResult> ExecuteTemplateAsync(TemplateExecutionRequest request);
        Task<DocumentGenerationResult> GenerateDocumentAsync(string templatePath, object data);
        Task ValidateEnvironmentAsync();
        Task<IEnumerable<TemplateInfo>> GetAvailableTemplatesAsync();
    }

    public interface IAuditLogRepository
    {
        Task<AuditLog?> GetByIdAsync(Guid id);
        Task<IEnumerable<AuditLog>> GetByEntityAsync(Guid entityId);
        Task<IEnumerable<AuditLog>> GetByUserAsync(string userId);
        Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<AuditLog> AddAsync(AuditLog auditLog);
        Task<int> CountAsync();
    }

    public interface IVersionRepository
    {
        Task<DocumentVersion?> GetByIdAsync(Guid id);
        Task<IEnumerable<DocumentVersion>> GetByDocumentIdAsync(Guid documentId);
        Task<DocumentVersion?> GetCurrentVersionAsync(Guid documentId);
        Task<IEnumerable<DocumentVersion>> GetApprovalsAsync(Guid documentId);
        Task<DocumentVersion> AddAsync(DocumentVersion version);
        Task<DocumentVersion> UpdateAsync(DocumentVersion version);
    }

    public interface IDocumentGenerationPipeline
    {
        Task<DocumentGenerationResult> GenerateDocumentAsync(TemplateExecutionRequest request);
        Task ValidateRequestAsync(TemplateExecutionRequest request);
    }
}
"@

Set-Content -Path (Join-Path $interfacesPath "MissingInterfaces.cs") -Value $otherInterfacesContent -Encoding UTF8
Write-Host "Created missing interfaces file" -ForegroundColor Cyan

# Build the solution
Write-Host "Building solution..." -ForegroundColor Yellow
$buildResult = dotnet build --verbosity minimal 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful! DTO namespace fix completed." -ForegroundColor Green
    
    # Test API startup
    Write-Host "Testing API startup..." -ForegroundColor Yellow
    Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "src\Api" -WindowStyle Hidden
    Start-Sleep -Seconds 10
    
    $apiProcess = Get-Process -Name "Api" -ErrorAction SilentlyContinue
    if ($apiProcess) {
        Write-Host "API started successfully!" -ForegroundColor Green
        $apiProcess | Stop-Process -Force
    } else {
        Write-Host "API startup test failed" -ForegroundColor Red
    }
} else {
    Write-Host "Build failed. Errors:" -ForegroundColor Red
    Write-Host $buildResult
    
    # Restore from backup if needed
    if (Test-Path $backupDir) {
        Write-Host "Restoring from backup: $backupDir" -ForegroundColor Yellow
        Remove-Item -Path $interfacesPath -Recurse -Force
        Copy-Item -Path $backupDir -Destination $interfacesPath -Recurse -Force
    }
}

Write-Host "DTO Namespace Fix deployment completed." -ForegroundColor Green