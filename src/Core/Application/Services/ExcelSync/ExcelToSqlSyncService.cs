using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Dapper;
using OfficeOpenXml;
using Enterprise.Documentation.Core.Domain.Models;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

/// <summary>
/// Background service that syncs data from the BI Analytics Change Spreadsheet to SQL.
/// Monitors the Excel file and automatically syncs changes.
/// </summary>
public class ExcelToSqlSyncService : BackgroundService
{
    private readonly ILogger<ExcelToSqlSyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;
    private readonly string _excelFilePath;
    private readonly int _syncIntervalSeconds;
    private FileSystemWatcher? _fileWatcher;

    public ExcelToSqlSyncService(
        ILogger<ExcelToSqlSyncService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
        _excelFilePath = configuration["ExcelSync:LocalFilePath"]
            ?? throw new InvalidOperationException("ExcelSync:LocalFilePath not configured");
        _syncIntervalSeconds = int.TryParse(configuration["ExcelSync:SyncIntervalSeconds"], out var interval) ? interval : 60;

        // Set EPPlus 8 license for noncommercial use
        ExcelPackage.License.SetNonCommercialPersonal("Enterprise Documentation Platform");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Excel-to-SQL Sync Service starting. Monitoring: {FilePath}", _excelFilePath);

        // Initial sync on startup
        await SyncExcelToSqlAsync(stoppingToken);

        // Set up file watcher for immediate sync on changes
        SetupFileWatcher();

        // Periodic sync as backup
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_syncIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncExcelToSqlAsync(stoppingToken);
        }
    }

    private void SetupFileWatcher()
    {
        var directory = Path.GetDirectoryName(_excelFilePath);
        var fileName = Path.GetFileName(_excelFilePath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _logger.LogWarning("Cannot set up file watcher - directory does not exist: {Directory}", directory);
            return;
        }

        _fileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += async (sender, e) =>
        {
            _logger.LogInformation("Excel file changed, triggering sync...");
            // Small delay to ensure file is fully written
            await Task.Delay(1000);
            await SyncExcelToSqlAsync(CancellationToken.None);
        };

        _logger.LogInformation("File watcher set up for: {FilePath}", _excelFilePath);
    }

    public async Task SyncExcelToSqlAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting Excel-to-SQL sync from: {FilePath}", _excelFilePath);

            if (!File.Exists(_excelFilePath))
            {
                _logger.LogWarning("Excel file not found: {FilePath}", _excelFilePath);
                return;
            }

            var entries = await ReadExcelFileAsync(cancellationToken);
            _logger.LogInformation("Read {Count} entries from Excel", entries.Count);

            if (entries.Count == 0)
            {
                _logger.LogWarning("No valid entries found in Excel file");
                return;
            }

            var (inserted, updated, skipped) = await UpsertEntriesAsync(entries, cancellationToken);
            _logger.LogInformation("Sync complete. Inserted: {Inserted}, Updated: {Updated}, Skipped: {Skipped}",
                inserted, updated, skipped);
        }
        catch (IOException ex) when (ex.Message.Contains("being used by another process"))
        {
            _logger.LogWarning("Excel file is locked by another process, will retry on next sync");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Excel-to-SQL sync");
        }
    }

    private async Task<List<DocumentChangeEntry>> ReadExcelFileAsync(CancellationToken cancellationToken)
    {
        var entries = new List<DocumentChangeEntry>();

        await using var stream = new FileStream(_excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var package = new ExcelPackage(stream);

        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            _logger.LogWarning("No worksheets found in Excel file");
            return entries;
        }

        _logger.LogInformation("Reading worksheet: {WorksheetName}, Dimensions: {Dimensions}",
            worksheet.Name, worksheet.Dimension?.Address ?? "null");

        if (worksheet.Dimension == null)
        {
            _logger.LogWarning("Worksheet has no data (Dimension is null)");
            return entries;
        }

        // Build column index map from header row (row 2, since row 1 is typically a title)
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int headerRow = 2;
        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var header = worksheet.Cells[headerRow, col].Text?.Trim();
            if (!string.IsNullOrEmpty(header))
            {
                columnMap[header] = col;
            }
        }

        _logger.LogInformation("Found {ColumnCount} columns in row {HeaderRow}: {Columns}",
            columnMap.Count, headerRow, string.Join(", ", columnMap.Keys));

        // Read data rows (starting from row 3, since row 1 is title and row 2 is headers)
        int dataStartRow = 3;
        int totalRows = worksheet.Dimension.End.Row - dataStartRow + 1;
        int skippedRows = 0;
        _logger.LogInformation("Processing {TotalRows} data rows (rows {StartRow}-{EndRow})",
            totalRows, dataStartRow, worksheet.Dimension.End.Row);

        for (int row = dataStartRow; row <= worksheet.Dimension.End.Row; row++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                string? GetValue(string columnName) =>
                    columnMap.TryGetValue(columnName, out var col)
                        ? worksheet.Cells[row, col].Text?.Trim()
                        : null;

                DateTime? GetDate(string columnName)
                {
                    if (!columnMap.TryGetValue(columnName, out var col)) return null;
                    var cell = worksheet.Cells[row, col];
                    if (cell.Value is DateTime dt) return dt;
                    if (DateTime.TryParse(cell.Text, out var parsed)) return parsed;
                    return null;
                }

                var entry = new DocumentChangeEntry
                {
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
                    DocumentationLink = GetValue("Documentation Link"),
                    DocId = GetValue("DocId"),
                    ExcelRowNumber = row,
                    LastSyncedFromExcel = DateTime.UtcNow,
                    SyncStatus = "Synced"
                };

                // Generate deduplication keys
                entry.UniqueKey = entry.GenerateUniqueKey();
                entry.ContentHash = entry.GenerateContentHash();

                // Only add if entry has meaningful data
                if (!string.IsNullOrEmpty(entry.CABNumber) ||
                    !string.IsNullOrEmpty(entry.JiraNumber) ||
                    !string.IsNullOrEmpty(entry.TableName))
                {
                    entries.Add(entry);
                }
                else
                {
                    skippedRows++;
                    _logger.LogDebug("Skipped row {Row}: no CAB#, JIRA#, or Table", row);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading row {Row}", row);
                skippedRows++;
            }
        }

        _logger.LogInformation("Parsed {ValidEntries} valid entries, skipped {SkippedRows} rows", entries.Count, skippedRows);

        return entries;
    }

    private async Task<(int inserted, int updated, int skipped)> UpsertEntriesAsync(
        List<DocumentChangeEntry> entries,
        CancellationToken cancellationToken)
    {
        int inserted = 0, updated = 0, skipped = 0;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Check for existing entry by UniqueKey
                var existing = await connection.QueryFirstOrDefaultAsync<DocumentChangeEntry>(
                    "SELECT Id, ContentHash FROM daqa.DocumentChanges WHERE UniqueKey = @UniqueKey",
                    new { entry.UniqueKey });

                if (existing == null)
                {
                    // Insert new entry
                    await connection.ExecuteAsync(GetInsertSql(), entry);
                    inserted++;
                }
                else if (existing.ContentHash != entry.ContentHash)
                {
                    // Update existing entry (content changed)
                    entry.Id = existing.Id;
                    await connection.ExecuteAsync(GetUpdateSql(), entry);
                    updated++;
                }
                else
                {
                    // Skip - no changes
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting entry CAB={CAB}, Row={Row}",
                    entry.CABNumber, entry.ExcelRowNumber);
            }
        }

        return (inserted, updated, skipped);
    }

    private static string GetInsertSql() => @"
        INSERT INTO daqa.DocumentChanges (
            Date, JiraNumber, CABNumber, SprintNumber, Status, Priority, Severity,
            TableName, ColumnName, ChangeType, Description, ReportedBy, AssignedTo,
            Documentation, DocumentationLink, DocId, ExcelRowNumber, LastSyncedFromExcel,
            SyncStatus, UniqueKey, ContentHash
        ) VALUES (
            @Date, @JiraNumber, @CABNumber, @SprintNumber, @Status, @Priority, @Severity,
            @TableName, @ColumnName, @ChangeType, @Description, @ReportedBy, @AssignedTo,
            @Documentation, @DocumentationLink, @DocId, @ExcelRowNumber, @LastSyncedFromExcel,
            @SyncStatus, @UniqueKey, @ContentHash
        )";

    private static string GetUpdateSql() => @"
        UPDATE daqa.DocumentChanges SET
            Date = @Date, JiraNumber = @JiraNumber, CABNumber = @CABNumber,
            SprintNumber = @SprintNumber, Status = @Status, Priority = @Priority,
            Severity = @Severity, TableName = @TableName, ColumnName = @ColumnName,
            ChangeType = @ChangeType, Description = @Description, ReportedBy = @ReportedBy,
            AssignedTo = @AssignedTo, Documentation = @Documentation,
            DocumentationLink = @DocumentationLink, DocId = @DocId,
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
