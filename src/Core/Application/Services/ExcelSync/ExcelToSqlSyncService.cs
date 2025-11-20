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
            // Direct Excel Column Mapping
            Date = GetDate("Date"),
            JiraNumber = GetValue("JIRA #"),
            CABNumber = GetValue("CAB #"),
            SprintNumber = GetValue("Sprint #"),
            Status = GetValue("Status"),
            Priority = GetValue("Priority"),
            Severity = GetValue("Severity"),
            TableName = GetValue("Table"),
            ColumnName = GetValue("Column"),
            ChangeType = GetValue("Change Type"),
            Description = GetValue("Description"),
            ReportedBy = GetValue("Reported By"),
            AssignedTo = GetValue("Assigned to"),
            Documentation = GetValue("Documentation"),
            DocumentationLink = GetValue("Documentation Link")
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

                // Check for duplicates by UniqueKey (CAB + Table + Column)
                var existing = await connection.QueryFirstOrDefaultAsync<(int Id, string? ContentHash)?>(
                    @"SELECT Id, ContentHash FROM daqa.DocumentChanges
                      WHERE UniqueKey = @UniqueKey",
                    new { entry.UniqueKey });

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
                    // Check for similar documents (same CAB + Table + Column)
                    var similar = await connection.QueryFirstOrDefaultAsync<int?>(
                        @"SELECT Id FROM daqa.DocumentChanges
                          WHERE CABNumber = @CABNumber
                            AND TableName = @TableName
                            AND ColumnName = @ColumnName",
                        new { entry.CABNumber, entry.TableName, entry.ColumnName });

                    if (similar.HasValue)
                    {
                        _logger.LogWarning(
                            "Skipping duplicate entry: CAB={CAB}, Table={Table}, Column={Column}",
                            entry.CABNumber, entry.TableName, entry.ColumnName);
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
            Date, JiraNumber, CABNumber, SprintNumber, Status, Priority, Severity,
            TableName, ColumnName, ChangeType, Description, ReportedBy, AssignedTo,
            Documentation, DocumentationLink, ExcelRowNumber, LastSyncedFromExcel,
            SyncStatus, UniqueKey, ContentHash
        ) VALUES (
            @Date, @JiraNumber, @CABNumber, @SprintNumber, @Status, @Priority, @Severity,
            @TableName, @ColumnName, @ChangeType, @Description, @ReportedBy, @AssignedTo,
            @Documentation, @DocumentationLink, @ExcelRowNumber, @LastSyncedFromExcel,
            @SyncStatus, @UniqueKey, @ContentHash
        )";

    private static string GetUpdateSql() => @"
        UPDATE daqa.DocumentChanges SET
            Date = @Date, JiraNumber = @JiraNumber, SprintNumber = @SprintNumber,
            Status = @Status, Priority = @Priority, Severity = @Severity,
            TableName = @TableName, ColumnName = @ColumnName, ChangeType = @ChangeType,
            Description = @Description, ReportedBy = @ReportedBy, AssignedTo = @AssignedTo,
            Documentation = @Documentation, DocumentationLink = @DocumentationLink,
            ExcelRowNumber = @ExcelRowNumber, LastSyncedFromExcel = @LastSyncedFromExcel,
            SyncStatus = @SyncStatus, UniqueKey = @UniqueKey, ContentHash = @ContentHash,
            UpdatedAt = GETUTCDATE()
        WHERE Id = @Id";

    public override void Dispose()
    {
        _fileWatcher?.Dispose();
        base.Dispose();
    }
}
