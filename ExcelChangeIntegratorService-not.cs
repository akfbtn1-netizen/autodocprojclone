// ExcelChangeIntegratorService.cs
// PURPOSE: Sync Excel spreadsheet to DaQa.DocumentChanges table (INSERT or UPDATE)
// DOES NOT: Generate DocId, trigger workflows, create documents
// Step 2 (separate watcher) handles DocId generation and workflow triggering

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Linq;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

public class ExcelChangeIntegratorService : BackgroundService, IExcelChangeIntegratorService
{
    private readonly ILogger<ExcelChangeIntegratorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _excelPath;
    private readonly string _connectionString;
    private readonly TimeSpan _pollInterval;

    public ExcelChangeIntegratorService(
        ILogger<ExcelChangeIntegratorService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        _excelPath = _configuration["ExcelChangeIntegrator:ExcelPath"] 
            ?? @"C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx";
        
        _connectionString = _configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=ibidb2003dv;Database=IRFS1;Integrated Security=true;TrustServerCertificate=true;";
        
        var pollMinutes = int.TryParse(_configuration["ExcelChangeIntegrator:PollIntervalMinutes"], out var minutes) ? minutes : 1;
        _pollInterval = TimeSpan.FromMinutes(pollMinutes);
        
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExcelChangeIntegrator started. Watching: {Path}", _excelPath);
        _logger.LogInformation("Poll interval: {Interval} minutes", _pollInterval.TotalMinutes);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExcelChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Excel changes");
            }
            
            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessExcelChangesAsync(CancellationToken ct)
    {
        if (!File.Exists(_excelPath))
        {
            _logger.LogWarning("Excel file not found: {Path}", _excelPath);
            return;
        }

        // Check if file is locked (Excel is open)
        if (IsFileLocked(_excelPath))
        {
            _logger.LogWarning("Excel file is locked (currently open). Will retry next poll cycle.");
            return;
        }

        using var package = new ExcelPackage(new FileInfo(_excelPath));
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        
        if (worksheet == null)
        {
            _logger.LogWarning("No worksheet found in Excel file");
            return;
        }

        var headers = GetHeaders(worksheet);
        var rowsProcessed = 0;
        var rowsInserted = 0;
        var rowsUpdated = 0;
        var rowsSkipped = 0;

        // Process ALL rows from Excel (starting from row 4 since headers are in row 3)
        for (int row = 4; row <= worksheet.Dimension.End.Row; row++)
        {
            // Skip rows without JIRA numbers (empty rows)
            var jiraCheck = GetCellValue(worksheet, row, headers, "JIRA #");
            if (string.IsNullOrWhiteSpace(jiraCheck))
            {
                continue;
            }

            try
            {
                var excelRow = ParseRow(worksheet, row, headers);
                if (excelRow == null)
                {
                    rowsSkipped++;
                    continue;
                }

                // Check if this row has a GENERATED DocId (not TBD or blank)
                var hasGeneratedDocId = !string.IsNullOrWhiteSpace(excelRow.DocId) && !excelRow.DocId.Equals("TBD", StringComparison.OrdinalIgnoreCase);
                
                _logger.LogDebug("Processing row {Row}: JIRA={Jira}, DocId='{DocId}', HasGeneratedDocId={HasGeneratedDocId}", 
                    excelRow.RowNumber, excelRow.JiraNumber, excelRow.DocId ?? "NULL", hasGeneratedDocId);

                if (hasGeneratedDocId)
                {
                    // UPDATE existing row in DocumentChanges
                    var updated = await UpdateDocumentChangesAsync(excelRow, ct);
                    if (updated)
                        rowsUpdated++;
                    else
                        rowsSkipped++;
                }
                else
                {
                    // INSERT new row into DocumentChanges (DocId will be NULL)
                    var inserted = await InsertDocumentChangesAsync(excelRow, ct);
                    if (inserted)
                        rowsInserted++;
                    else
                        rowsSkipped++;
                }

                rowsProcessed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Excel row {Row}", row);
                rowsSkipped++;
            }
        }

        if (rowsProcessed > 0)
        {
            _logger.LogInformation(
                "Processed {Total} Excel rows: {Inserted} inserted, {Updated} updated, {Skipped} skipped",
                rowsProcessed, rowsInserted, rowsUpdated, rowsSkipped);
        }
    }

    private Dictionary<string, int> GetHeaders(ExcelWorksheet worksheet)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        _logger.LogInformation("🔍 EXCEL HEADERS DEBUG - Reading from row 3:");
        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var header = worksheet.Cells[3, col].Text?.Trim();  // Headers are in row 3
            if (!string.IsNullOrEmpty(header))
            {
                headers[header] = col;
                _logger.LogInformation("  Column {Col}: '{Header}'", col, header);
            }
        }
        
        _logger.LogInformation("🔍 LOOKING FOR CRITICAL COLUMNS:");
        _logger.LogInformation("  Description: {Found}", headers.ContainsKey("Description") ? "✅ FOUND" : "❌ MISSING");
        _logger.LogInformation("  Change Applied: {Found}", headers.ContainsKey("Change Applied") ? "✅ FOUND" : "❌ MISSING");
        _logger.LogInformation("  Location of Changed Code: {Found}", headers.ContainsKey("Location of Changed Code") ? "✅ FOUND" : "❌ MISSING");
        _logger.LogInformation("  Reported By: {Found}", headers.ContainsKey("Reported By") ? "✅ FOUND" : "❌ MISSING");
        _logger.LogInformation("  Assigned to: {Found}", headers.ContainsKey("Assigned to") ? "✅ FOUND" : "❌ MISSING");
        
        return headers;
    }

    private string? GetCellValue(ExcelWorksheet worksheet, int row, Dictionary<string, int> headers, string columnName)
    {
        if (headers.TryGetValue(columnName, out int col))
        {
            return worksheet.Cells[row, col].Text?.Trim();
        }
        return null;
    }

    private DateTime? GetDateValue(ExcelWorksheet worksheet, int row, Dictionary<string, int> headers, string columnName)
    {
        if (headers.TryGetValue(columnName, out int col))
        {
            var cell = worksheet.Cells[row, col];
            if (cell.Value != null && DateTime.TryParse(cell.Text, out var date))
            {
                return date;
            }
        }
        return null;
    }

    private ExcelRow? ParseRow(ExcelWorksheet worksheet, int rowNumber, Dictionary<string, int> headers)
    {
        try
        {
            var jiraNumber = GetCellValue(worksheet, rowNumber, headers, "JIRA #");
            
            // JIRA # is required
            if (string.IsNullOrWhiteSpace(jiraNumber))
            {
                _logger.LogWarning("Row {Row} missing JIRA #, skipping", rowNumber);
                return null;
            }

            var docIdValue = GetCellValue(worksheet, rowNumber, headers, "DocID") ?? GetCellValue(worksheet, rowNumber, headers, "Doc_ID") ?? GetCellValue(worksheet, rowNumber, headers, "Doc ID");
            
            // Debug: Log what DocId values we're seeing
            _logger.LogInformation("📋 Row {Row}: Status='{Status}', DocId='{DocId}'", rowNumber, 
                GetCellValue(worksheet, rowNumber, headers, "Status") ?? "NULL", docIdValue ?? "NULL");
            
            // Handle Excel date parsing issue - if DocId looks like a date (12/31/1899), treat as empty
            if (docIdValue != null && (docIdValue.Contains("/1899") || DateTime.TryParse(docIdValue, out var _)))
            {
                docIdValue = null; // Treat Excel date values as empty DocId
            }
            
            // CRITICAL: Keep TBD values - they must be stored in database so DocumentChangeWatcher can skip them
            // TBD should go to database as \"TBD\", not NULL

            return new ExcelRow
            {
                RowNumber = rowNumber,
                DocId = docIdValue,  // This will include TBD values
                JiraNumber = jiraNumber,
                CabNumber = GetCellValue(worksheet, rowNumber, headers, "CAB #"),
                SprintNumber = GetCellValue(worksheet, rowNumber, headers, "Sprint #"),
                Status = GetCellValue(worksheet, rowNumber, headers, "Status"),
                Priority = GetCellValue(worksheet, rowNumber, headers, "Priority"),
                Severity = GetCellValue(worksheet, rowNumber, headers, "Severity"),
                TableName = GetCellValue(worksheet, rowNumber, headers, "Table"),  // Exact match from Excel
                ColumnName = GetCellValue(worksheet, rowNumber, headers, "Column"),  // Exact match from Excel
                ChangeType = GetCellValue(worksheet, rowNumber, headers, "Change Type"),
                Description = GetCellValue(worksheet, rowNumber, headers, "Description"),  // This should work
                ChangeApplied = GetCellValue(worksheet, rowNumber, headers, "Change Applied") ?? GetCellValue(worksheet, rowNumber, headers, "Changes Applied"),  // Try both
                LocationOfCodeChange = GetCellValue(worksheet, rowNumber, headers, "Location of Changed Code") ?? GetCellValue(worksheet, rowNumber, headers, "Location of Code Change"),  // Exact match first
                ReportedBy = GetCellValue(worksheet, rowNumber, headers, "Reported By"),
                AssignedTo = GetCellValue(worksheet, rowNumber, headers, "Assigned To") ?? GetCellValue(worksheet, rowNumber, headers, "Assigned to"),  // Try both cases
                DateRequested = GetDateValue(worksheet, rowNumber, headers, "Date"),
                StoredProcedureName = GetCellValue(worksheet, rowNumber, headers, "Stored Procedure") ?? GetCellValue(worksheet, rowNumber, headers, "SP Name"),
                Documentation = GetCellValue(worksheet, rowNumber, headers, "Documentation"),
                DocumentationLink = GetCellValue(worksheet, rowNumber, headers, "Documentation Link")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing row {Row}", rowNumber);
            return null;
        }
    }

    private async Task<bool> InsertDocumentChangesAsync(ExcelRow row, CancellationToken ct)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            
            // Check if this exact Excel row was already synced
            var existingCheck = await connection.QueryFirstOrDefaultAsync<int>(
                @"SELECT COUNT(*) FROM DaQa.DocumentChanges 
                  WHERE ExcelRowNumber = @ExcelRowNumber",
                new { 
                    ExcelRowNumber = row.RowNumber 
                });
                
            if (existingCheck > 0)
            {
                _logger.LogDebug("Duplicate row detected for JIRA {JiraNumber}, Table {TableName}, Column {ColumnName}, Status {Status} - skipping insert", 
                    row.JiraNumber, row.TableName, row.ColumnName, row.Status);
                return false;
            }

            var sql = @"
                INSERT INTO DaQa.DocumentChanges (
                    DocId, JiraNumber, CABNumber, SprintNumber, Status,
                    Priority, Severity, TableName, ColumnName, ChangeType,
                    Description, ChangeApplied, LocationOfCodeChange, ReportedBy, AssignedTo, Date,
                    ExcelRowNumber, LastSyncedFromExcel, SyncStatus, UniqueKey, ContentHash, CreatedAt
                ) VALUES (
                    @DocId,  -- Store actual DocId from Excel (NULL, 'TBD', or actual DocId)
                    @JiraNumber, @CabNumber, @SprintNumber, @Status,
                    @Priority, @Severity, @TableName, @ColumnName, @ChangeType,
                    @Description, @ChangeApplied, @LocationOfCodeChange, @ReportedBy, @AssignedTo, @Date,
                    @ExcelRowNumber, @LastSyncedFromExcel, @SyncStatus, @UniqueKey, @ContentHash, @CreatedAt
                )";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                DocId = string.IsNullOrWhiteSpace(row.DocId) ? (string?)null : row.DocId,  // Blank becomes NULL, "TBD" stays "TBD"
                row.JiraNumber,
                row.CabNumber,
                row.SprintNumber,
                row.Status,
                row.Priority,
                row.Severity,
                row.TableName,
                row.ColumnName,
                row.ChangeType,
                row.Description,
                row.ChangeApplied,  // Critical field
                row.LocationOfCodeChange,  // Critical field
                row.ReportedBy,
                row.AssignedTo,
                Date = row.DateRequested,
                ExcelRowNumber = row.RowNumber,
                // SYSTEM PARAMETERS:
                LastSyncedFromExcel = DateTime.UtcNow,
                SyncStatus = "Synced",
                UniqueKey = GenerateUniqueKey(row.JiraNumber, row.TableName, row.ColumnName),
                ContentHash = GenerateContentHash(row),
                CreatedAt = DateTime.UtcNow
            });

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Inserted new row for JIRA {JiraNumber} (Excel row {ExcelRow})", row.JiraNumber, row.RowNumber);
                
                // Log WorkflowEvent
                await LogWorkflowEventAsync(connection, null, "ExcelRowSynced", 
                    $"New Excel row synced: {row.JiraNumber}", ct);
                
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting row for JIRA {JiraNumber}", row.JiraNumber);
            return false;
        }
    }

    private async Task<bool> UpdateDocumentChangesAsync(ExcelRow row, CancellationToken ct)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // Check if DocId exists in database
            var exists = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM DaQa.DocumentChanges WHERE DocId = @DocId",
                new { row.DocId });

            if (exists == 0)
            {
                _logger.LogWarning("DocId {DocId} not found in database, skipping update (Excel row {ExcelRow})", row.DocId, row.RowNumber);
                return false;
            }

            var sql = @"
                UPDATE DaQa.DocumentChanges
                SET JiraNumber = @JiraNumber,
                    CABNumber = @CabNumber,
                    SprintNumber = @SprintNumber,
                    Status = @Status,
                    Priority = @Priority,
                    Severity = @Severity,
                    TableName = @TableName,
                    ColumnName = @ColumnName,
                    ChangeType = @ChangeType,
                    Description = @Description,
                    ChangeApplied = @ChangeApplied,
                    LocationOfCodeChange = @LocationOfCodeChange,
                    ReportedBy = @ReportedBy,
                    AssignedTo = @AssignedTo,
                    Date = @Date,
                    ExcelRowNumber = @ExcelRowNumber,
                    LastSyncedFromExcel = @LastSyncedFromExcel,
                    ContentHash = @ContentHash,
                    SyncStatus = @SyncStatus,
                    UpdatedAt = @UpdatedAt
                WHERE DocId = @DocId";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                row.DocId,
                row.JiraNumber,
                row.CabNumber,
                row.SprintNumber,
                row.Status,
                row.Priority,
                row.Severity,
                row.TableName,
                row.ColumnName,
                row.ChangeType,
                row.Description,
                row.ChangeApplied,  // Critical field
                row.LocationOfCodeChange,  // Critical field
                row.ReportedBy,
                row.AssignedTo,
                Date = row.DateRequested,
                ExcelRowNumber = row.RowNumber,
                // SYSTEM PARAMETERS:
                LastSyncedFromExcel = DateTime.UtcNow,
                ContentHash = GenerateContentHash(row),
                SyncStatus = "Updated",
                UpdatedAt = DateTime.UtcNow
            });

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Updated existing row for DocId {DocId} (Excel row {ExcelRow})", row.DocId, row.RowNumber);
                
                // Log WorkflowEvent
                await LogWorkflowEventAsync(connection, row.DocId, "ExcelRowUpdated",
                    $"Excel row updated: {row.DocId}", ct);
                
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating row for DocId {DocId}", row.DocId);
            return false;
        }
    }

    private async Task LogWorkflowEventAsync(
        SqlConnection connection, 
        string? workflowId, 
        string eventType,
        string message,
        CancellationToken ct)
    {
        try
        {
            var sql = @"
                INSERT INTO DaQa.WorkflowEvents (
                    WorkflowId, EventType, Status, Message, Timestamp
                ) VALUES (
                    @WorkflowId, @EventType, 'Completed', @Message, GETUTCDATE()
                )";

            await connection.ExecuteAsync(sql, new
            {
                WorkflowId = workflowId ?? "ExcelSync",
                EventType = eventType,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log workflow event (non-critical)");
        }
    }

    private bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// Writes DocId back to Excel for the specified JIRA number row.
    /// </summary>
    public async Task WriteDocIdToExcelAsync(string jiraNumber, string docId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔥 EXCEL WRITEBACK CALLED 🔥 JiraNumber: {JiraNumber}, DocId: {DocId}, FilePath: {Path}", 
                jiraNumber, docId, _excelPath);
            _logger.LogInformation("Writing DocId {DocId} back to Excel for JIRA {JiraNumber}", docId, jiraNumber);
            
            var fileInfo = new FileInfo(_excelPath);
            
            if (!fileInfo.Exists)
            {
                _logger.LogWarning("Excel file not found for DocId write-back: {Path}", _excelPath);
                return;
            }
            
            using var package = await OpenExcelWithRetryAsync(fileInfo);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            
            if (worksheet == null)
            {
                _logger.LogWarning("No worksheet found for DocId write-back");
                return;
            }
            
            var headerRow = 3; // Headers are actually in row 3, not row 1

            // Find DocId column - try multiple variations  
            var docIdColumn = -1;
            for (int col = 1; col <= worksheet.Dimension.Columns; col++)
            {
                var headerValue = worksheet.Cells[headerRow, col].Value?.ToString()?.Trim() ?? "";
                
                // Check multiple variations (case-insensitive)
                if (string.Equals(headerValue, "DocID", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerValue, "Doc_ID", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerValue, "Doc ID", StringComparison.OrdinalIgnoreCase))
                {
                    docIdColumn = col;
                    _logger.LogInformation("Found DocId column at position {Col} with header '{Header}'", col, headerValue);
                    break;
                }
            }

            if (docIdColumn == -1)
            {
                _logger.LogError("DocId column not found in Excel. Available headers: {Headers}", 
                    string.Join(", ", Enumerable.Range(1, worksheet.Dimension.Columns)
                        .Select(c => worksheet.Cells[headerRow, c].Value?.ToString() ?? "null")));
                return;
            }

            // Find JIRA column - try multiple variations
            var jiraColumn = -1;
            for (int col = 1; col <= worksheet.Dimension.Columns; col++)
            {
                var headerValue = worksheet.Cells[headerRow, col].Value?.ToString()?.Trim() ?? "";
                if (string.Equals(headerValue, "JIRA #", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerValue, "JiraNumber", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerValue, "JIRA", StringComparison.OrdinalIgnoreCase))
                {
                    jiraColumn = col;
                    _logger.LogInformation("Found JIRA column at position {Col} with header '{Header}'", col, headerValue);
                    break;
                }
            }

            if (jiraColumn == -1)
            {
                _logger.LogError("JIRA column not found in Excel. Available headers: {Headers}", 
                    string.Join(", ", Enumerable.Range(1, worksheet.Dimension.Columns)
                        .Select(c => worksheet.Cells[headerRow, c].Value?.ToString() ?? "null")));
                return;
            }            // Find the row with matching JIRA number
            var rowCount = worksheet.Dimension?.Rows ?? 0;
            bool rowFound = false;
            
            _logger.LogInformation("Searching for JIRA {JiraNumber} in {RowCount} rows", jiraNumber, rowCount);

            for (int row = 4; row <= rowCount; row++) // Start at 4 since headers are in row 3
            {
                var rowJiraNumber = worksheet.Cells[row, jiraColumn].Value?.ToString()?.Trim() ?? "";
                
                _logger.LogDebug("Row {Row}: Comparing '{RowJira}' with '{TargetJira}'", row, rowJiraNumber, jiraNumber);

                if (string.Equals(rowJiraNumber, jiraNumber, StringComparison.OrdinalIgnoreCase))
                {
                    // Found the row - write DocId
                    worksheet.Cells[row, docIdColumn].Value = docId;
                    _logger.LogInformation("✅ DocId {DocId} written to Excel row {Row} for JIRA {JiraNumber}",
                        docId, row, jiraNumber);
                    rowFound = true;
                    break;
                }
            }

            if (!rowFound)
            {
                _logger.LogWarning("JIRA number {JiraNumber} not found in Excel. Searched {RowCount} rows in column {Col}", 
                    jiraNumber, rowCount - 1, jiraColumn);
                return;
            }            await package.SaveAsync(cancellationToken);
            _logger.LogInformation("Excel file saved with DocId {DocId} for JIRA {JiraNumber}", docId, jiraNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write DocId {DocId} back to Excel for JIRA {JiraNumber} (non-critical)", 
                docId, jiraNumber);
            // Don't throw - this is a nice-to-have feature that shouldn't break the workflow
        }
    }

    /// <summary>
    /// Opens Excel file with retry logic to handle file locking.
    /// </summary>
    private async Task<ExcelPackage> OpenExcelWithRetryAsync(FileInfo fileInfo, int maxAttempts = 3)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return new ExcelPackage(fileInfo);
            }
            catch (IOException ex) when (attempt < maxAttempts && ex.Message.Contains("being used by another process"))
            {
                _logger.LogWarning("Excel file locked, attempt {Attempt}/{Max}. Retrying in 2 seconds... Error: {Error}", 
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(2000, CancellationToken.None);
            }
        }
        
        throw new IOException($"Could not open Excel file after {maxAttempts} attempts. File may be locked by another process.");
    }

    private string GenerateUniqueKey(string? jiraNumber, string? tableName, string? columnName)
    {
        var combined = $"{jiraNumber ?? ""}_{tableName ?? ""}_{columnName ?? ""}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
        return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 32);
    }

    private string GenerateContentHash(ExcelRow row)
    {
        var content = $"{row.JiraNumber}{row.DateRequested}{row.Status}{row.TableName}{row.ColumnName}{row.Description}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 32);
    }
}

public class ExcelRow
{
    public int RowNumber { get; set; }
    public string? DocId { get; set; }  // May be NULL for new rows
    public string JiraNumber { get; set; } = string.Empty;
    public string? CabNumber { get; set; }
    public string? SprintNumber { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Severity { get; set; }
    public string? TableName { get; set; }
    public string? ColumnName { get; set; }
    public string? ChangeType { get; set; }
    public string? Description { get; set; }
    public string? ChangeApplied { get; set; }  // This is critical - what change was applied
    public string? LocationOfCodeChange { get; set; }  // Critical field - location of code changes
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? DateRequested { get; set; }
    public string? StoredProcedureName { get; set; }
    public string? Documentation { get; set; }
    public string? DocumentationLink { get; set; }
}