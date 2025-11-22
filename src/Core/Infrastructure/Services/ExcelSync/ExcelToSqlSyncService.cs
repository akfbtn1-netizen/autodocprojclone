using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Dapper;
using OfficeOpenXml;
using Enterprise.Documentation.Core.Domain.Models;
using Enterprise.Documentation.Core.Application.Services.DocumentGeneration;
using Enterprise.Documentation.Core.Application.Services.ExcelSync;

namespace Enterprise.Documentation.Core.Infrastructure.Services.ExcelSync;

/// <summary>
/// Background service that syncs data from the BI Analytics Change Spreadsheet to SQL.
/// Monitors the Excel file and automatically syncs changes.
/// Auto-generates drafts for completed entries.
/// </summary>
public class ExcelToSqlSyncService : BackgroundService, IExcelToSqlSyncService
{
    private readonly ILogger<ExcelToSqlSyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connectionString;
    private readonly string _excelFilePath;
    private readonly int _syncIntervalSeconds;
    private FileSystemWatcher? _fileWatcher;

    public ExcelToSqlSyncService(
        ILogger<ExcelToSqlSyncService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
        _excelFilePath = configuration["ExcelSync:LocalFilePath"]
            ?? throw new InvalidOperationException("ExcelSync:LocalFilePath not configured");
        _syncIntervalSeconds = int.TryParse(configuration["ExcelSync:SyncIntervalSeconds"], out var interval) ? interval : 60;

        // EPPlus license context removed - not needed in v7+
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

            // Process completed entries for auto-draft generation
            await ProcessCompletedEntriesAsync(entries, cancellationToken);
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

    private async Task ProcessCompletedEntriesAsync(List<DocumentChangeEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            // Find entries with Status="Completed" and no DocId
            var completedWithoutDocId = entries
                .Where(e => string.Equals(e.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                         && string.IsNullOrWhiteSpace(e.DocId))
                .ToList();

            if (completedWithoutDocId.Count == 0)
            {
                _logger.LogDebug("No completed entries without DocId found");
                return;
            }

            _logger.LogInformation("Found {Count} completed entries without DocId, creating drafts", completedWithoutDocId.Count);

            // Create scoped service provider for auto-draft service
            using var scope = _serviceProvider.CreateScope();
            var autoDraftService = scope.ServiceProvider.GetRequiredService<IAutoDraftService>();

            var draftsCreated = 0;
            var draftsFailed = 0;

            foreach (var entry in completedWithoutDocId)
            {
                try
                {
                    _logger.LogInformation("Creating draft for CAB {CABNumber}, Jira {JiraNumber}",
                        entry.CABNumber, entry.JiraNumber);

                    var result = await autoDraftService.CreateDraftForCompletedEntryAsync(entry, cancellationToken);

                    if (result.Success)
                    {
                        // Update Excel with generated DocId
                        await UpdateExcelWithDocIdAsync(entry.ExcelRowNumber, result.DocId, cancellationToken);

                        // Update database with DocId
                        await UpdateDatabaseWithDocIdAsync(entry.CABNumber, entry.JiraNumber, result.DocId, cancellationToken);

                        draftsCreated++;
                        _logger.LogInformation("Draft created successfully: {DocId} at {FilePath}",
                            result.DocId, result.FilePath);
                    }
                    else
                    {
                        draftsFailed++;
                        _logger.LogWarning("Failed to create draft for CAB {CABNumber}: {ErrorMessage}",
                            entry.CABNumber, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    draftsFailed++;
                    _logger.LogError(ex, "Error creating draft for CAB {CABNumber}", entry.CABNumber);
                }
            }

            _logger.LogInformation("Draft creation complete. Created: {Created}, Failed: {Failed}",
                draftsCreated, draftsFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing completed entries for auto-draft");
        }
    }

    private async Task UpdateExcelWithDocIdAsync(int rowNumber, string? docId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(docId))
        {
            return;
        }

        try
        {
            await using var stream = new FileStream(_excelFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var package = new ExcelPackage(stream);

            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                _logger.LogWarning("No worksheet found in Excel file for DocId update");
                return;
            }

            // Find DocId column (assuming it's one of the columns, need to find it in header row)
            int docIdColumn = -1;
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var header = worksheet.Cells[3, col].Text?.Trim(); // Row 3 is headers
                if (string.Equals(header, "DocId", StringComparison.OrdinalIgnoreCase))
                {
                    docIdColumn = col;
                    break;
                }
            }

            if (docIdColumn == -1)
            {
                _logger.LogWarning("DocId column not found in Excel file");
                return;
            }

            // Update the cell
            worksheet.Cells[rowNumber, docIdColumn].Value = docId;
            await package.SaveAsync(cancellationToken);

            _logger.LogInformation("Updated Excel row {RowNumber} with DocId: {DocId}", rowNumber, docId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Excel with DocId for row {RowNumber}", rowNumber);
        }
    }

    private async Task UpdateDatabaseWithDocIdAsync(string? cabNumber, string? jiraNumber, string? docId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(docId))
        {
            return;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                UPDATE daqa.DocumentChanges
                SET DocId = @DocId,
                    LastSyncedFromExcel = GETUTCDATE()
                WHERE CABNumber = @CABNumber
                  AND JiraNumber = @JiraNumber";

            await connection.ExecuteAsync(sql, new { DocId = docId, CABNumber = cabNumber, JiraNumber = jiraNumber });

            _logger.LogDebug("Updated database with DocId {DocId} for CAB {CABNumber}", docId, cabNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating database with DocId for CAB {CABNumber}", cabNumber);
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

        // Build column index map from header row (row 3, since rows 1-2 are title/subtitle)
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int headerRow = 3;
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

        // Read data rows (starting from row 4, since rows 1-3 are title/subtitle/headers)
        int dataStartRow = 4;
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
