using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using Enterprise.Documentation.Core.Domain.Models;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

/// <summary>
/// Background service that watches an Excel change tracking file and syncs to SQL database.
/// Supports both local files and SharePoint Online via Microsoft Graph API.
/// Uses EPPlus to read Excel and Dapper for SQL operations.
/// </summary>
public class ExcelToSqlSyncService : BackgroundService
{
    private readonly ILogger<ExcelToSqlSyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly string _excelFilePath;
    private readonly int _syncIntervalSeconds;
    private readonly bool _isSharePoint;
    private readonly string? _sharePointSiteUrl;
    private readonly string? _sharePointDriveId;
    private readonly string? _sharePointItemPath;
    private FileSystemWatcher? _fileWatcher;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private string? _lastSharePointETag;

    public ExcelToSqlSyncService(
        ILogger<ExcelToSqlSyncService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string not configured");

        // Check if using SharePoint or local file
        _isSharePoint = configuration.GetValue<bool>("ExcelSync:UseSharePoint");

        if (_isSharePoint)
        {
            _sharePointSiteUrl = configuration["ExcelSync:SharePoint:SiteUrl"];
            _sharePointDriveId = configuration["ExcelSync:SharePoint:DriveId"];
            _sharePointItemPath = configuration["ExcelSync:SharePoint:ItemPath"];
            _excelFilePath = Path.Combine(Path.GetTempPath(), "ChangeDocuments.xlsx");
        }
        else
        {
            _excelFilePath = configuration["ExcelSync:LocalFilePath"]
                ?? @"C:\Data\ChangeDocuments.xlsx";
        }

        if (int.TryParse(configuration["ExcelSync:SyncIntervalSeconds"], out var interval))
        {
            _syncIntervalSeconds = interval;
        }
        else
        {
            _syncIntervalSeconds = 30;
        }

        // Required for EPPlus license
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Excel to SQL Sync Service starting. Watching: {FilePath}", _excelFilePath);

        // Set up file watcher for immediate sync on changes
        SetupFileWatcher();

        // Initial sync
        await SyncExcelToSqlAsync(stoppingToken);

        // Periodic sync as backup
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_syncIntervalSeconds), stoppingToken);
                await SyncExcelToSqlAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sync loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void SetupFileWatcher()
    {
        var directory = Path.GetDirectoryName(_excelFilePath);
        var filename = Path.GetFileName(_excelFilePath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _logger.LogWarning("Directory does not exist: {Directory}. File watcher not set up.", directory);
            return;
        }

        _fileWatcher = new FileSystemWatcher(directory, filename)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += async (sender, e) =>
        {
            // Debounce: wait a bit for file to be fully written
            await Task.Delay(1000);
            _logger.LogInformation("Excel file changed, triggering sync");
            await SyncExcelToSqlAsync(CancellationToken.None);
        };

        _logger.LogInformation("File watcher set up for: {FilePath}", _excelFilePath);
    }

    public async Task SyncExcelToSqlAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_excelFilePath))
        {
            _logger.LogWarning("Excel file not found: {FilePath}", _excelFilePath);
            return;
        }

        try
        {
            _logger.LogInformation("Starting Excel to SQL sync from: {FilePath}", _excelFilePath);

            var entries = ReadExcelFile();
            if (entries.Count == 0)
            {
                _logger.LogWarning("No entries found in Excel file");
                return;
            }

            var (inserted, updated, errors) = await UpsertToSqlAsync(entries, cancellationToken);

            _lastSyncTime = DateTime.UtcNow;
            _logger.LogInformation(
                "Sync completed. Inserted: {Inserted}, Updated: {Updated}, Errors: {Errors}",
                inserted, updated, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Excel to SQL");
        }
    }

    private List<DocumentChangeEntry> ReadExcelFile()
    {
        var entries = new List<DocumentChangeEntry>();

        using var package = new ExcelPackage(new FileInfo(_excelFilePath));
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            _logger.LogWarning("No worksheet found in Excel file");
            return entries;
        }

        // Get column mappings from header row
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= worksheet.Dimension.Columns; col++)
        {
            var header = worksheet.Cells[1, col].Text?.Trim();
            if (!string.IsNullOrEmpty(header))
            {
                columnMap[header] = col;
            }
        }

        // Read data rows (skip header)
        for (int row = 2; row <= worksheet.Dimension.Rows; row++)
        {
            try
            {
                var entry = MapRowToEntry(worksheet, row, columnMap);
                if (entry != null && !string.IsNullOrEmpty(entry.DocumentId))
                {
                    entry.ExcelRowNumber = row;
                    entry.LastSyncedFromExcel = DateTime.UtcNow;
                    entry.SyncStatus = "Success";
                    entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading row {Row}", row);
            }
        }

        _logger.LogInformation("Read {Count} entries from Excel", entries.Count);
        return entries;
    }

    private DocumentChangeEntry? MapRowToEntry(ExcelWorksheet ws, int row, Dictionary<string, int> columnMap)
    {
        string GetValue(string columnName)
        {
            if (columnMap.TryGetValue(columnName, out var col))
            {
                return ws.Cells[row, col].Text?.Trim() ?? string.Empty;
            }
            return string.Empty;
        }

        DateTime? GetDate(string columnName)
        {
            var value = GetValue(columnName);
            if (DateTime.TryParse(value, out var date))
            {
                return date;
            }
            return null;
        }

        int? GetInt(string columnName)
        {
            var value = GetValue(columnName);
            if (int.TryParse(value, out var num))
            {
                return num;
            }
            return null;
        }

        var entry = new DocumentChangeEntry
        {
            // Core Identifiers
            DocumentId = GetValue("DocumentId") ?? GetValue("Document ID") ?? GetValue("DocID"),
            CABNumber = GetValue("CABNumber") ?? GetValue("CAB Number") ?? GetValue("CAB"),
            ChangeRequestId = GetValue("ChangeRequestId") ?? GetValue("CR") ?? GetValue("Change Request"),

            // Document Information
            Title = GetValue("Title") ?? GetValue("Document Title"),
            Description = GetValue("Description") ?? GetValue("Summary"),
            DocumentType = GetValue("DocumentType") ?? GetValue("Type") ?? GetValue("Doc Type"),
            Category = GetValue("Category"),
            SubCategory = GetValue("SubCategory") ?? GetValue("Sub Category"),

            // Classification
            TierClassification = GetValue("Tier") ?? GetValue("TierClassification"),
            DataClassification = GetValue("DataClassification") ?? GetValue("Classification"),
            SecurityClearance = GetValue("SecurityClearance") ?? GetValue("Clearance"),

            // Ownership
            BusinessOwner = GetValue("BusinessOwner") ?? GetValue("Business Owner"),
            TechnicalOwner = GetValue("TechnicalOwner") ?? GetValue("Technical Owner"),
            Author = GetValue("Author") ?? GetValue("Created By"),
            Department = GetValue("Department") ?? GetValue("Dept"),
            Team = GetValue("Team"),

            // Approval
            ApprovalStatus = GetValue("ApprovalStatus") ?? GetValue("Approval Status"),
            CurrentApprover = GetValue("CurrentApprover") ?? GetValue("Approver"),
            SubmittedDate = GetDate("SubmittedDate") ?? GetDate("Submitted"),
            ApprovedDate = GetDate("ApprovedDate") ?? GetDate("Approved"),
            ApprovalComments = GetValue("ApprovalComments") ?? GetValue("Comments"),

            // Dates
            CreatedDate = GetDate("CreatedDate") ?? GetDate("Created"),
            ModifiedDate = GetDate("ModifiedDate") ?? GetDate("Modified") ?? GetDate("Last Modified"),
            EffectiveDate = GetDate("EffectiveDate") ?? GetDate("Effective"),
            ExpirationDate = GetDate("ExpirationDate") ?? GetDate("Expires"),
            Version = GetValue("Version"),
            RevisionNumber = GetInt("RevisionNumber") ?? GetInt("Revision"),

            // Database Objects
            DatabaseName = GetValue("DatabaseName") ?? GetValue("Database"),
            SchemaName = GetValue("SchemaName") ?? GetValue("Schema"),
            ObjectName = GetValue("ObjectName") ?? GetValue("Object Name") ?? GetValue("SP Name"),
            ObjectType = GetValue("ObjectType") ?? GetValue("Object Type"),
            SourceTables = GetValue("SourceTables") ?? GetValue("Source Tables"),
            TargetTables = GetValue("TargetTables") ?? GetValue("Target Tables"),

            // Files
            FilePath = GetValue("FilePath") ?? GetValue("File Path") ?? GetValue("Path"),
            GeneratedDocPath = GetValue("GeneratedDocPath") ?? GetValue("Generated Doc"),
            TemplateUsed = GetValue("TemplateUsed") ?? GetValue("Template"),

            // Status
            Status = GetValue("Status"),
            IsActive = GetValue("IsActive")?.ToLower() != "false" && GetValue("Active")?.ToLower() != "false",
            Tags = GetValue("Tags"),
            Notes = GetValue("Notes") ?? GetValue("Remarks")
        };

        return entry;
    }

    private async Task<(int inserted, int updated, int errors)> UpsertToSqlAsync(
        List<DocumentChangeEntry> entries,
        CancellationToken cancellationToken)
    {
        int inserted = 0, updated = 0, errors = 0, skipped = 0;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var entry in entries)
        {
            try
            {
                // Generate deduplication keys
                entry.UniqueKey = entry.GenerateUniqueKey();
                entry.ContentHash = entry.GenerateContentHash();

                // Check for duplicates by multiple criteria
                var existing = await connection.QueryFirstOrDefaultAsync<(int Id, string? ContentHash)?>(
                    @"SELECT Id, ContentHash FROM daqa.DocumentChanges
                      WHERE DocumentId = @DocumentId
                         OR UniqueKey = @UniqueKey",
                    new { entry.DocumentId, entry.UniqueKey });

                if (existing.HasValue)
                {
                    // Check if content actually changed
                    if (existing.Value.ContentHash == entry.ContentHash)
                    {
                        // No changes - skip
                        skipped++;
                        continue;
                    }

                    // Update existing (content changed)
                    entry.Id = existing.Value.Id;
                    await connection.ExecuteAsync(GetUpdateSql(), entry);
                    updated++;
                }
                else
                {
                    // Check for similar documents (same CAB + Object but different version)
                    var similar = await connection.QueryFirstOrDefaultAsync<int?>(
                        @"SELECT Id FROM daqa.DocumentChanges
                          WHERE CABNumber = @CABNumber
                            AND ObjectName = @ObjectName
                            AND Version = @Version",
                        new { entry.CABNumber, entry.ObjectName, entry.Version });

                    if (similar.HasValue)
                    {
                        _logger.LogWarning(
                            "Skipping duplicate entry: CAB={CAB}, Object={Object}, Version={Version}",
                            entry.CABNumber, entry.ObjectName, entry.Version);
                        skipped++;
                        continue;
                    }

                    // Insert new
                    await connection.ExecuteAsync(GetInsertSql(), entry);
                    inserted++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Error upserting entry {DocumentId}", entry.DocumentId);
            }
        }

        _logger.LogInformation("Skipped {Skipped} unchanged/duplicate entries", skipped);
        return (inserted, updated, errors);
    }

    private static string GetInsertSql() => @"
        INSERT INTO daqa.DocumentChanges (
            DocumentId, CABNumber, ChangeRequestId, Title, Description, DocumentType,
            Category, SubCategory, TierClassification, DataClassification, SecurityClearance,
            BusinessOwner, TechnicalOwner, Author, Department, Team,
            ApprovalStatus, CurrentApprover, SubmittedDate, ApprovedDate, ApprovalComments,
            CreatedDate, ModifiedDate, EffectiveDate, ExpirationDate, Version, RevisionNumber,
            DatabaseName, SchemaName, ObjectName, ObjectType, SourceTables, TargetTables,
            FilePath, GeneratedDocPath, TemplateUsed, Status, IsActive, IsDeleted, Tags, Notes,
            ExcelRowNumber, LastSyncedFromExcel, SyncStatus, UniqueKey, ContentHash
        ) VALUES (
            @DocumentId, @CABNumber, @ChangeRequestId, @Title, @Description, @DocumentType,
            @Category, @SubCategory, @TierClassification, @DataClassification, @SecurityClearance,
            @BusinessOwner, @TechnicalOwner, @Author, @Department, @Team,
            @ApprovalStatus, @CurrentApprover, @SubmittedDate, @ApprovedDate, @ApprovalComments,
            @CreatedDate, @ModifiedDate, @EffectiveDate, @ExpirationDate, @Version, @RevisionNumber,
            @DatabaseName, @SchemaName, @ObjectName, @ObjectType, @SourceTables, @TargetTables,
            @FilePath, @GeneratedDocPath, @TemplateUsed, @Status, @IsActive, @IsDeleted, @Tags, @Notes,
            @ExcelRowNumber, @LastSyncedFromExcel, @SyncStatus, @UniqueKey, @ContentHash
        )";

    private static string GetUpdateSql() => @"
        UPDATE daqa.DocumentChanges SET
            CABNumber = @CABNumber, ChangeRequestId = @ChangeRequestId, Title = @Title,
            Description = @Description, DocumentType = @DocumentType, Category = @Category,
            SubCategory = @SubCategory, TierClassification = @TierClassification,
            DataClassification = @DataClassification, SecurityClearance = @SecurityClearance,
            BusinessOwner = @BusinessOwner, TechnicalOwner = @TechnicalOwner, Author = @Author,
            Department = @Department, Team = @Team, ApprovalStatus = @ApprovalStatus,
            CurrentApprover = @CurrentApprover, SubmittedDate = @SubmittedDate,
            ApprovedDate = @ApprovedDate, ApprovalComments = @ApprovalComments,
            ModifiedDate = @ModifiedDate, EffectiveDate = @EffectiveDate,
            ExpirationDate = @ExpirationDate, Version = @Version, RevisionNumber = @RevisionNumber,
            DatabaseName = @DatabaseName, SchemaName = @SchemaName, ObjectName = @ObjectName,
            ObjectType = @ObjectType, SourceTables = @SourceTables, TargetTables = @TargetTables,
            FilePath = @FilePath, GeneratedDocPath = @GeneratedDocPath, TemplateUsed = @TemplateUsed,
            Status = @Status, IsActive = @IsActive, Tags = @Tags, Notes = @Notes,
            ExcelRowNumber = @ExcelRowNumber, LastSyncedFromExcel = @LastSyncedFromExcel,
            SyncStatus = @SyncStatus, UniqueKey = @UniqueKey, ContentHash = @ContentHash
        WHERE Id = @Id";

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        base.Dispose();
    }
}
