# ============================================================================
# BUILD APPROVAL WORKFLOW - PART 4
# Expanded MasterIndex Model and Excel Watcher Service
# ============================================================================

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Projects\EnterpriseDocumentationPlatform.V2"

Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  APPROVAL WORKFLOW - PART 4" -ForegroundColor White
Write-Host "  MasterIndex Model (120+ fields) + Excel Watcher" -ForegroundColor Gray
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# ============================================================================
# FILE 1: Expanded MasterIndex Model
# ============================================================================
Write-Host "[1/3] Creating expanded MasterIndex.cs..." -ForegroundColor Yellow

$modelsDir = Join-Path $projectRoot "src\Core\Domain\Models"
if (-not (Test-Path $modelsDir)) {
    New-Item -ItemType Directory -Path $modelsDir -Force | Out-Null
}

$masterIndexContent = @'
namespace Enterprise.Documentation.Core.Domain.Models;

/// <summary>
/// Master Index for documentation tracking - 120+ industry-standard fields.
/// Supports CAB tracking, audit history, and comprehensive metadata.
/// </summary>
public class MasterIndex
{
    #region Primary Identifiers

    public int Id { get; set; }
    public string? DocumentId { get; set; }  // TYPE-YYYY-NNN format
    public string? CABNumber { get; set; }   // Change Advisory Board number
    public string? TicketNumber { get; set; }
    public string? RequestId { get; set; }

    #endregion

    #region Object Identification

    public string ObjectName { get; set; } = string.Empty;
    public string? ObjectType { get; set; }  // StoredProcedure, Table, View, Function, etc.
    public string? SchemaName { get; set; }
    public string? DatabaseName { get; set; }
    public string? ServerName { get; set; }
    public string? FullyQualifiedName { get; set; }  // [Server].[Database].[Schema].[Object]

    #endregion

    #region Classification and Categorization

    public string? TierClassification { get; set; }  // Tier1, Tier2, Tier3
    public string? ComplexityScore { get; set; }     // Low, Medium, High, Critical
    public string? BusinessDomain { get; set; }
    public string? SubDomain { get; set; }
    public string? ApplicationArea { get; set; }
    public string? ModuleName { get; set; }
    public string? ComponentName { get; set; }
    public string? FeatureArea { get; set; }

    #endregion

    #region Business Context

    public string? BusinessPurpose { get; set; }
    public string? BusinessOwner { get; set; }
    public string? BusinessOwnerEmail { get; set; }
    public string? TechnicalOwner { get; set; }
    public string? TechnicalOwnerEmail { get; set; }
    public string? DataSteward { get; set; }
    public string? DataStewardEmail { get; set; }
    public string? CostCenter { get; set; }
    public string? Department { get; set; }
    public string? Division { get; set; }

    #endregion

    #region Technical Details

    public int? LineCount { get; set; }
    public int? ParameterCount { get; set; }
    public int? DependencyCount { get; set; }
    public int? CalledByCount { get; set; }
    public int? TableCount { get; set; }
    public int? ColumnCount { get; set; }
    public int? IndexCount { get; set; }
    public string? ReturnType { get; set; }
    public bool? HasDynamicSQL { get; set; }
    public bool? HasCursor { get; set; }
    public bool? HasTempTables { get; set; }
    public bool? HasTransaction { get; set; }
    public bool? HasErrorHandling { get; set; }
    public string? ExecutionFrequency { get; set; }
    public decimal? AvgExecutionTimeMs { get; set; }
    public decimal? MaxExecutionTimeMs { get; set; }

    #endregion

    #region Data Sensitivity and Compliance

    public string? DataClassification { get; set; }  // Public, Internal, Confidential, Restricted
    public bool? ContainsPII { get; set; }
    public bool? ContainsPHI { get; set; }
    public bool? ContainsPCI { get; set; }
    public bool? ContainsSOX { get; set; }
    public string? ComplianceRequirements { get; set; }
    public string? RetentionPolicy { get; set; }
    public string? EncryptionRequirement { get; set; }
    public string? AccessRestrictions { get; set; }
    public bool? RequiresAudit { get; set; }

    #endregion

    #region Documentation Status

    public string? DocumentationStatus { get; set; }  // NotStarted, InProgress, PendingReview, Approved, Published
    public DateTime? DocumentationStartDate { get; set; }
    public DateTime? DocumentationCompletedDate { get; set; }
    public DateTime? LastDocumentationUpdate { get; set; }
    public string? DocumentationAuthor { get; set; }
    public string? DocumentationReviewer { get; set; }
    public int? DocumentationVersion { get; set; }
    public string? DocumentationQualityScore { get; set; }
    public int? EditCount { get; set; }
    public int? RegenerationCount { get; set; }

    #endregion

    #region Generation Metadata

    public string? TemplateUsed { get; set; }
    public int? TokensUsed { get; set; }
    public int? GenerationDurationMs { get; set; }
    public string? AIModel { get; set; }
    public decimal? TokenCost { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public string? GeneratedBy { get; set; }

    #endregion

    #region File and Storage

    public string? TempFilePath { get; set; }
    public string? FinalFilePath { get; set; }
    public string? SharePointUrl { get; set; }
    public string? SharePointLibrary { get; set; }
    public string? SharePointFolder { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? FileChecksum { get; set; }
    public string? FileFormat { get; set; }  // docx, pdf, md

    #endregion

    #region Version Control

    public string? SourceControlPath { get; set; }
    public string? GitRepository { get; set; }
    public string? GitBranch { get; set; }
    public string? LastCommitHash { get; set; }
    public DateTime? LastCommitDate { get; set; }
    public string? LastCommitAuthor { get; set; }
    public string? LastCommitMessage { get; set; }

    #endregion

    #region Dependencies and Relationships

    public string? UpstreamDependencies { get; set; }   // JSON array
    public string? DownstreamDependencies { get; set; } // JSON array
    public string? CalledBy { get; set; }               // JSON array
    public string? Calls { get; set; }                  // JSON array
    public string? RelatedObjects { get; set; }         // JSON array
    public string? ParentObject { get; set; }
    public string? ChildObjects { get; set; }           // JSON array

    #endregion

    #region Change Management

    public DateTime? CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? ChangeReason { get; set; }
    public string? ChangeType { get; set; }  // New, Enhancement, BugFix, Refactor, Deprecation
    public DateTime? EffectiveDate { get; set; }
    public DateTime? SunsetDate { get; set; }
    public string? MigrationStatus { get; set; }

    #endregion

    #region Approval Workflow

    public Guid? ApprovalId { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? AssignedApprover { get; set; }
    public DateTime? QueuedForApprovalAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }

    #endregion

    #region Testing and Quality

    public bool? HasUnitTests { get; set; }
    public int? UnitTestCount { get; set; }
    public decimal? CodeCoverage { get; set; }
    public string? TestStatus { get; set; }
    public DateTime? LastTestRun { get; set; }
    public bool? TestsPassed { get; set; }
    public string? QualityGate { get; set; }
    public int? TechnicalDebtMinutes { get; set; }
    public string? CodeSmells { get; set; }
    public int? Vulnerabilities { get; set; }
    public int? Bugs { get; set; }

    #endregion

    #region Performance Metrics

    public decimal? CPUUsagePercent { get; set; }
    public decimal? MemoryUsageMB { get; set; }
    public decimal? IOReadsPerExec { get; set; }
    public decimal? IOWritesPerExec { get; set; }
    public int? ExecutionCount24h { get; set; }
    public int? ExecutionCount7d { get; set; }
    public int? ExecutionCount30d { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public string? PerformanceTier { get; set; }

    #endregion

    #region Environment and Deployment

    public string? Environment { get; set; }  // Dev, QA, UAT, Prod
    public string? DeploymentStatus { get; set; }
    public DateTime? LastDeployedDate { get; set; }
    public string? DeployedBy { get; set; }
    public string? DeploymentPackage { get; set; }
    public string? ReleaseVersion { get; set; }
    public string? ReleaseNotes { get; set; }

    #endregion

    #region External References

    public string? JiraEpic { get; set; }
    public string? JiraStory { get; set; }
    public string? ConfluencePageUrl { get; set; }
    public string? WikiUrl { get; set; }
    public string? RunbookUrl { get; set; }
    public string? ArchitectureDiagramUrl { get; set; }
    public string? DataFlowDiagramUrl { get; set; }
    public string? ERDiagramUrl { get; set; }

    #endregion

    #region Tags and Custom Fields

    public string? Tags { get; set; }              // JSON array
    public string? CustomField1 { get; set; }
    public string? CustomField2 { get; set; }
    public string? CustomField3 { get; set; }
    public string? CustomField4 { get; set; }
    public string? CustomField5 { get; set; }
    public string? Metadata { get; set; }         // JSON object for extensibility
    public string? Notes { get; set; }
    public string? Comments { get; set; }

    #endregion

    #region Audit Fields

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public int RowVersion { get; set; } = 1;

    #endregion
}
'@

[System.IO.File]::WriteAllText((Join-Path $modelsDir "MasterIndex.cs"), $masterIndexContent, $utf8NoBom)
Write-Host "  Created: MasterIndex.cs (120+ fields)" -ForegroundColor Green

# ============================================================================
# FILE 2: Excel Watcher Service
# ============================================================================
Write-Host "[2/3] Creating ExcelWatcherService.cs..." -ForegroundColor Yellow

$watcherDir = Join-Path $projectRoot "src\Core\Application\Services\Watcher"
if (-not (Test-Path $watcherDir)) {
    New-Item -ItemType Directory -Path $watcherDir -Force | Out-Null
}

$excelWatcherContent = @'
using System.Data;
using ClosedXML.Excel;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.Watcher;

/// <summary>
/// Background service that watches Excel files for changes and syncs to SQL database.
/// Monitors the MasterIndex Excel file and updates database when changes detected.
/// </summary>
public class ExcelWatcherService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExcelWatcherService> _logger;
    private readonly string _connectionString;
    private readonly string _excelFilePath;
    private readonly int _pollIntervalSeconds;
    private FileSystemWatcher? _fileWatcher;
    private DateTime _lastProcessedTime = DateTime.MinValue;

    public ExcelWatcherService(
        IConfiguration configuration,
        ILogger<ExcelWatcherService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");

        _excelFilePath = configuration["ExcelWatcher:FilePath"]
            ?? @"C:\Projects\EnterpriseDocumentationPlatform.V2\MasterIndex.xlsx";

        _pollIntervalSeconds = configuration.GetValue<int>("ExcelWatcher:PollIntervalSeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Excel Watcher Service starting. Monitoring: {FilePath}", _excelFilePath);

        // Set up file system watcher for immediate detection
        SetupFileWatcher();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSyncExcelAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Excel sync check");
            }

            await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), stoppingToken);
        }

        _fileWatcher?.Dispose();
        _logger.LogInformation("Excel Watcher Service stopped");
    }

    private void SetupFileWatcher()
    {
        var directory = Path.GetDirectoryName(_excelFilePath);
        var fileName = Path.GetFileName(_excelFilePath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _logger.LogWarning("Excel directory not found: {Directory}", directory);
            return;
        }

        _fileWatcher = new FileSystemWatcher(directory)
        {
            Filter = fileName,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += async (sender, e) =>
        {
            _logger.LogInformation("File change detected: {FileName}", e.Name);
            // Add small delay to ensure file is fully written
            await Task.Delay(1000);
            await CheckAndSyncExcelAsync(CancellationToken.None);
        };
    }

    private async Task CheckAndSyncExcelAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_excelFilePath))
        {
            _logger.LogWarning("Excel file not found: {FilePath}", _excelFilePath);
            return;
        }

        var lastWriteTime = File.GetLastWriteTimeUtc(_excelFilePath);

        if (lastWriteTime <= _lastProcessedTime)
        {
            return; // No changes
        }

        _logger.LogInformation("Processing Excel changes from {LastWrite}", lastWriteTime);

        try
        {
            await SyncExcelToDatabase(cancellationToken);
            _lastProcessedTime = lastWriteTime;
            _logger.LogInformation("Excel sync completed successfully");
        }
        catch (IOException ex) when (ex.Message.Contains("being used by another process"))
        {
            _logger.LogWarning("File is locked, will retry on next poll");
        }
    }

    private async Task SyncExcelToDatabase(CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(_excelFilePath);
        var worksheet = workbook.Worksheet(1); // First worksheet

        var rows = worksheet.RowsUsed().Skip(1); // Skip header row
        var syncedCount = 0;
        var errorCount = 0;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var row in rows)
        {
            try
            {
                var objectName = row.Cell(1).GetString().Trim();
                if (string.IsNullOrEmpty(objectName)) continue;

                // Map Excel columns to MasterIndex fields
                var record = new
                {
                    ObjectName = objectName,
                    ObjectType = row.Cell(2).GetString(),
                    SchemaName = row.Cell(3).GetString(),
                    DatabaseName = row.Cell(4).GetString(),
                    CABNumber = row.Cell(5).GetString(),
                    TierClassification = row.Cell(6).GetString(),
                    BusinessPurpose = row.Cell(7).GetString(),
                    BusinessOwner = row.Cell(8).GetString(),
                    TechnicalOwner = row.Cell(9).GetString(),
                    DataClassification = row.Cell(10).GetString(),
                    DocumentationStatus = row.Cell(11).GetString(),
                    Tags = row.Cell(12).GetString(),
                    Notes = row.Cell(13).GetString(),
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "ExcelWatcher"
                };

                // Upsert logic - insert or update based on ObjectName
                var sql = @"
                    MERGE INTO DaQa.MasterIndex AS target
                    USING (SELECT @ObjectName AS ObjectName) AS source
                    ON target.ObjectName = source.ObjectName
                    WHEN MATCHED THEN
                        UPDATE SET
                            ObjectType = @ObjectType,
                            SchemaName = @SchemaName,
                            DatabaseName = @DatabaseName,
                            CABNumber = @CABNumber,
                            TierClassification = @TierClassification,
                            BusinessPurpose = @BusinessPurpose,
                            BusinessOwner = @BusinessOwner,
                            TechnicalOwner = @TechnicalOwner,
                            DataClassification = @DataClassification,
                            DocumentationStatus = @DocumentationStatus,
                            Tags = @Tags,
                            Notes = @Notes,
                            UpdatedAt = @UpdatedAt,
                            UpdatedBy = @UpdatedBy,
                            RowVersion = RowVersion + 1
                    WHEN NOT MATCHED THEN
                        INSERT (ObjectName, ObjectType, SchemaName, DatabaseName, CABNumber,
                                TierClassification, BusinessPurpose, BusinessOwner, TechnicalOwner,
                                DataClassification, DocumentationStatus, Tags, Notes,
                                CreatedAt, UpdatedAt, UpdatedBy)
                        VALUES (@ObjectName, @ObjectType, @SchemaName, @DatabaseName, @CABNumber,
                                @TierClassification, @BusinessPurpose, @BusinessOwner, @TechnicalOwner,
                                @DataClassification, @DocumentationStatus, @Tags, @Notes,
                                @UpdatedAt, @UpdatedAt, @UpdatedBy);";

                await connection.ExecuteAsync(sql, record);
                syncedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Error syncing row {RowNumber}", row.RowNumber());
            }
        }

        _logger.LogInformation("Excel sync complete. Synced: {Synced}, Errors: {Errors}",
            syncedCount, errorCount);
    }
}

/// <summary>
/// Extension methods for registering Excel watcher service.
/// </summary>
public static class ExcelWatcherExtensions
{
    public static IServiceCollection AddExcelWatcher(this IServiceCollection services)
    {
        services.AddHostedService<ExcelWatcherService>();
        return services;
    }
}
'@

[System.IO.File]::WriteAllText((Join-Path $watcherDir "ExcelWatcherService.cs"), $excelWatcherContent, $utf8NoBom)
Write-Host "  Created: ExcelWatcherService.cs" -ForegroundColor Green

# ============================================================================
# FILE 3: SQL Script for MasterIndex Table
# ============================================================================
Write-Host "[3/3] Creating MasterIndex SQL script..." -ForegroundColor Yellow

$sqlDir = Join-Path $projectRoot "sql\masterindex"
if (-not (Test-Path $sqlDir)) {
    New-Item -ItemType Directory -Path $sqlDir -Force | Out-Null
}

$masterIndexSql = @'
-- ============================================================================
-- CREATE MasterIndex Table - 120+ Industry-Standard Fields
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DaQa')
    EXEC('CREATE SCHEMA DaQa')
GO

IF OBJECT_ID('DaQa.MasterIndex', 'U') IS NOT NULL
    DROP TABLE DaQa.MasterIndex
GO

CREATE TABLE DaQa.MasterIndex (
    -- Primary Identifiers
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DocumentId NVARCHAR(50) NULL,           -- TYPE-YYYY-NNN format
    CABNumber NVARCHAR(50) NULL,            -- Change Advisory Board number
    TicketNumber NVARCHAR(100) NULL,
    RequestId NVARCHAR(100) NULL,

    -- Object Identification
    ObjectName NVARCHAR(256) NOT NULL,
    ObjectType NVARCHAR(50) NULL,
    SchemaName NVARCHAR(128) NULL,
    DatabaseName NVARCHAR(128) NULL,
    ServerName NVARCHAR(256) NULL,
    FullyQualifiedName NVARCHAR(1000) NULL,

    -- Classification
    TierClassification NVARCHAR(20) NULL,
    ComplexityScore NVARCHAR(20) NULL,
    BusinessDomain NVARCHAR(100) NULL,
    SubDomain NVARCHAR(100) NULL,
    ApplicationArea NVARCHAR(100) NULL,
    ModuleName NVARCHAR(100) NULL,
    ComponentName NVARCHAR(100) NULL,
    FeatureArea NVARCHAR(100) NULL,

    -- Business Context
    BusinessPurpose NVARCHAR(MAX) NULL,
    BusinessOwner NVARCHAR(256) NULL,
    BusinessOwnerEmail NVARCHAR(256) NULL,
    TechnicalOwner NVARCHAR(256) NULL,
    TechnicalOwnerEmail NVARCHAR(256) NULL,
    DataSteward NVARCHAR(256) NULL,
    DataStewardEmail NVARCHAR(256) NULL,
    CostCenter NVARCHAR(50) NULL,
    Department NVARCHAR(100) NULL,
    Division NVARCHAR(100) NULL,

    -- Technical Details
    LineCount INT NULL,
    ParameterCount INT NULL,
    DependencyCount INT NULL,
    CalledByCount INT NULL,
    TableCount INT NULL,
    ColumnCount INT NULL,
    IndexCount INT NULL,
    ReturnType NVARCHAR(100) NULL,
    HasDynamicSQL BIT NULL,
    HasCursor BIT NULL,
    HasTempTables BIT NULL,
    HasTransaction BIT NULL,
    HasErrorHandling BIT NULL,
    ExecutionFrequency NVARCHAR(50) NULL,
    AvgExecutionTimeMs DECIMAL(18,2) NULL,
    MaxExecutionTimeMs DECIMAL(18,2) NULL,

    -- Data Sensitivity
    DataClassification NVARCHAR(50) NULL,
    ContainsPII BIT NULL,
    ContainsPHI BIT NULL,
    ContainsPCI BIT NULL,
    ContainsSOX BIT NULL,
    ComplianceRequirements NVARCHAR(500) NULL,
    RetentionPolicy NVARCHAR(100) NULL,
    EncryptionRequirement NVARCHAR(100) NULL,
    AccessRestrictions NVARCHAR(500) NULL,
    RequiresAudit BIT NULL,

    -- Documentation Status
    DocumentationStatus NVARCHAR(50) NULL,
    DocumentationStartDate DATETIME2 NULL,
    DocumentationCompletedDate DATETIME2 NULL,
    LastDocumentationUpdate DATETIME2 NULL,
    DocumentationAuthor NVARCHAR(256) NULL,
    DocumentationReviewer NVARCHAR(256) NULL,
    DocumentationVersion INT NULL,
    DocumentationQualityScore NVARCHAR(20) NULL,
    EditCount INT DEFAULT 0,
    RegenerationCount INT DEFAULT 0,

    -- Generation Metadata
    TemplateUsed NVARCHAR(256) NULL,
    TokensUsed INT NULL,
    GenerationDurationMs INT NULL,
    AIModel NVARCHAR(100) NULL,
    TokenCost DECIMAL(10,4) NULL,
    GeneratedAt DATETIME2 NULL,
    GeneratedBy NVARCHAR(256) NULL,

    -- File Storage
    TempFilePath NVARCHAR(1000) NULL,
    FinalFilePath NVARCHAR(1000) NULL,
    SharePointUrl NVARCHAR(2000) NULL,
    SharePointLibrary NVARCHAR(256) NULL,
    SharePointFolder NVARCHAR(1000) NULL,
    FileSizeBytes BIGINT NULL,
    FileChecksum NVARCHAR(64) NULL,
    FileFormat NVARCHAR(20) NULL,

    -- Version Control
    SourceControlPath NVARCHAR(1000) NULL,
    GitRepository NVARCHAR(500) NULL,
    GitBranch NVARCHAR(256) NULL,
    LastCommitHash NVARCHAR(64) NULL,
    LastCommitDate DATETIME2 NULL,
    LastCommitAuthor NVARCHAR(256) NULL,
    LastCommitMessage NVARCHAR(1000) NULL,

    -- Dependencies (JSON)
    UpstreamDependencies NVARCHAR(MAX) NULL,
    DownstreamDependencies NVARCHAR(MAX) NULL,
    CalledBy NVARCHAR(MAX) NULL,
    Calls NVARCHAR(MAX) NULL,
    RelatedObjects NVARCHAR(MAX) NULL,
    ParentObject NVARCHAR(256) NULL,
    ChildObjects NVARCHAR(MAX) NULL,

    -- Change Management
    CreatedDate DATETIME2 NULL,
    CreatedBy NVARCHAR(256) NULL,
    LastModifiedDate DATETIME2 NULL,
    LastModifiedBy NVARCHAR(256) NULL,
    ChangeReason NVARCHAR(500) NULL,
    ChangeType NVARCHAR(50) NULL,
    EffectiveDate DATETIME2 NULL,
    SunsetDate DATETIME2 NULL,
    MigrationStatus NVARCHAR(50) NULL,

    -- Approval Workflow
    ApprovalId UNIQUEIDENTIFIER NULL,
    ApprovalStatus NVARCHAR(50) NULL,
    AssignedApprover NVARCHAR(256) NULL,
    QueuedForApprovalAt DATETIME2 NULL,
    ApprovedAt DATETIME2 NULL,
    ApprovedBy NVARCHAR(256) NULL,
    RejectedAt DATETIME2 NULL,
    RejectedBy NVARCHAR(256) NULL,
    RejectionReason NVARCHAR(1000) NULL,

    -- Testing and Quality
    HasUnitTests BIT NULL,
    UnitTestCount INT NULL,
    CodeCoverage DECIMAL(5,2) NULL,
    TestStatus NVARCHAR(50) NULL,
    LastTestRun DATETIME2 NULL,
    TestsPassed BIT NULL,
    QualityGate NVARCHAR(50) NULL,
    TechnicalDebtMinutes INT NULL,
    CodeSmells NVARCHAR(MAX) NULL,
    Vulnerabilities INT NULL,
    Bugs INT NULL,

    -- Performance Metrics
    CPUUsagePercent DECIMAL(5,2) NULL,
    MemoryUsageMB DECIMAL(10,2) NULL,
    IOReadsPerExec DECIMAL(18,2) NULL,
    IOWritesPerExec DECIMAL(18,2) NULL,
    ExecutionCount24h INT NULL,
    ExecutionCount7d INT NULL,
    ExecutionCount30d INT NULL,
    LastExecutionTime DATETIME2 NULL,
    PerformanceTier NVARCHAR(20) NULL,

    -- Environment
    Environment NVARCHAR(20) NULL,
    DeploymentStatus NVARCHAR(50) NULL,
    LastDeployedDate DATETIME2 NULL,
    DeployedBy NVARCHAR(256) NULL,
    DeploymentPackage NVARCHAR(256) NULL,
    ReleaseVersion NVARCHAR(50) NULL,
    ReleaseNotes NVARCHAR(MAX) NULL,

    -- External References
    JiraEpic NVARCHAR(50) NULL,
    JiraStory NVARCHAR(50) NULL,
    ConfluencePageUrl NVARCHAR(2000) NULL,
    WikiUrl NVARCHAR(2000) NULL,
    RunbookUrl NVARCHAR(2000) NULL,
    ArchitectureDiagramUrl NVARCHAR(2000) NULL,
    DataFlowDiagramUrl NVARCHAR(2000) NULL,
    ERDiagramUrl NVARCHAR(2000) NULL,

    -- Tags and Custom
    Tags NVARCHAR(MAX) NULL,
    CustomField1 NVARCHAR(500) NULL,
    CustomField2 NVARCHAR(500) NULL,
    CustomField3 NVARCHAR(500) NULL,
    CustomField4 NVARCHAR(500) NULL,
    CustomField5 NVARCHAR(500) NULL,
    Metadata NVARCHAR(MAX) NULL,
    Notes NVARCHAR(MAX) NULL,
    Comments NVARCHAR(MAX) NULL,

    -- Audit
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(256) NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    DeletedBy NVARCHAR(256) NULL,
    RowVersion INT NOT NULL DEFAULT 1
)
GO

-- Indexes for common queries
CREATE NONCLUSTERED INDEX IX_MasterIndex_ObjectName ON DaQa.MasterIndex(ObjectName)
CREATE NONCLUSTERED INDEX IX_MasterIndex_DocumentId ON DaQa.MasterIndex(DocumentId)
CREATE NONCLUSTERED INDEX IX_MasterIndex_CABNumber ON DaQa.MasterIndex(CABNumber)
CREATE NONCLUSTERED INDEX IX_MasterIndex_ObjectType ON DaQa.MasterIndex(ObjectType)
CREATE NONCLUSTERED INDEX IX_MasterIndex_SchemaDatabase ON DaQa.MasterIndex(SchemaName, DatabaseName)
CREATE NONCLUSTERED INDEX IX_MasterIndex_ApprovalStatus ON DaQa.MasterIndex(ApprovalStatus)
CREATE NONCLUSTERED INDEX IX_MasterIndex_DocumentationStatus ON DaQa.MasterIndex(DocumentationStatus)
CREATE NONCLUSTERED INDEX IX_MasterIndex_TierClassification ON DaQa.MasterIndex(TierClassification)
GO

PRINT 'MasterIndex table created with 120+ fields'
GO
'@

[System.IO.File]::WriteAllText((Join-Path $sqlDir "create-masterindex-table.sql"), $masterIndexSql, $utf8NoBom)
Write-Host "  Created: create-masterindex-table.sql" -ForegroundColor Green

# ============================================================================
# Summary
# ============================================================================
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host "  PART 4 COMPLETE!" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Green
Write-Host ""
Write-Host "Created files:" -ForegroundColor White
Write-Host "  1. MasterIndex.cs - C# model with 120+ industry-standard fields" -ForegroundColor Gray
Write-Host "  2. ExcelWatcherService.cs - Background service for Excel sync" -ForegroundColor Gray
Write-Host "  3. create-masterindex-table.sql - SQL script for MasterIndex table" -ForegroundColor Gray
Write-Host ""
Write-Host "Key fields in MasterIndex:" -ForegroundColor Yellow
Write-Host "  - DocumentId (TYPE-YYYY-NNN format)" -ForegroundColor Cyan
Write-Host "  - CABNumber (Change Advisory Board)" -ForegroundColor Cyan
Write-Host "  - TierClassification, ComplexityScore" -ForegroundColor Cyan
Write-Host "  - BusinessOwner, TechnicalOwner, DataSteward" -ForegroundColor Cyan
Write-Host "  - Data sensitivity (PII, PHI, PCI, SOX)" -ForegroundColor Cyan
Write-Host "  - Token usage, generation metrics" -ForegroundColor Cyan
Write-Host "  - Edit tracking for AI improvement" -ForegroundColor Cyan
Write-Host "  - Performance metrics, quality gates" -ForegroundColor Cyan
Write-Host ""
Write-Host "Excel Watcher features:" -ForegroundColor Yellow
Write-Host "  - FileSystemWatcher for immediate detection" -ForegroundColor Cyan
Write-Host "  - Configurable poll interval (default 30s)" -ForegroundColor Cyan
Write-Host "  - MERGE upsert to sync changes" -ForegroundColor Cyan
Write-Host "  - Maps Excel columns to MasterIndex fields" -ForegroundColor Cyan
Write-Host ""
Write-Host "To enable Excel watcher, add to Program.cs:" -ForegroundColor Yellow
Write-Host "  builder.Services.AddExcelWatcher();" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration needed in appsettings.json:" -ForegroundColor Yellow
Write-Host @"
  "ExcelWatcher": {
    "FilePath": "C:\\Projects\\...\\MasterIndex.xlsx",
    "PollIntervalSeconds": 30
  }
"@ -ForegroundColor Cyan
Write-Host ""
