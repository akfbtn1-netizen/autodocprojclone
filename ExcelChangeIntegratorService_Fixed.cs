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

namespace ExcelChangeIntegrator;

public class ExcelChangeIntegratorService : BackgroundService
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
            ?? @"C:\Users\Alexander.Kirby\Desktop\Doctest\BI Analytics Change Spreadsheet.xlsx";
        
        _connectionString = _configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=ibidb2003dv;Database=IRFS1;Integrated Security=true;TrustServerCertificate=true;";
        
        _pollInterval = TimeSpan.FromMinutes(
            _configuration.GetValue<int>("ExcelChangeIntegrator:PollIntervalMinutes", 1));
        
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

        // Process ALL rows with Status = "Completed"
        for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
        {
            var status = GetCellValue(worksheet, row, headers, "Status");
            
            // Only process rows with Status = "Completed"
            if (status?.Equals("Completed", StringComparison.OrdinalIgnoreCase) != true)
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

                // Check if this row has a DocId
                var hasDocId = !string.IsNullOrWhiteSpace(excelRow.DocId);

                if (hasDocId)
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
                "Processed {Total} completed rows: {Inserted} inserted, {Updated} updated, {Skipped} skipped",
                rowsProcessed, rowsInserted, rowsUpdated, rowsSkipped);
        }
    }

    private Dictionary<string, int> GetHeaders(ExcelWorksheet worksheet)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var header = worksheet.Cells[1, col].Text?.Trim();
            if (!string.IsNullOrEmpty(header))
            {
                headers[header] = col;
            }
        }
        
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

            return new ExcelRow
            {
                RowNumber = rowNumber,
                DocId = GetCellValue(worksheet, rowNumber, headers, "Doc_ID"),
                JiraNumber = jiraNumber,
                CabNumber = GetCellValue(worksheet, rowNumber, headers, "CAB #"),
                SprintNumber = GetCellValue(worksheet, rowNumber, headers, "Sprint #"),
                Status = GetCellValue(worksheet, rowNumber, headers, "Status"),
                Priority = GetCellValue(worksheet, rowNumber, headers, "Priority"),
                Severity = GetCellValue(worksheet, rowNumber, headers, "Severity"),
                TableName = GetCellValue(worksheet, rowNumber, headers, "Table"),
                ColumnName = GetCellValue(worksheet, rowNumber, headers, "Column"),
                SchemaName = GetCellValue(worksheet, rowNumber, headers, "Schema") ?? "dbo",
                ChangeType = GetCellValue(worksheet, rowNumber, headers, "Change Type"),
                ChangeApplied = GetCellValue(worksheet, rowNumber, headers, "Description"),
                ReportedBy = GetCellValue(worksheet, rowNumber, headers, "Reported By"),
                AssignedTo = GetCellValue(worksheet, rowNumber, headers, "Assigned to"),
                DateRequested = GetDateValue(worksheet, rowNumber, headers, "Date"),
                StoredProcedureName = GetCellValue(worksheet, rowNumber, headers, "Stored Procedure")
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

            var sql = @"
                INSERT INTO DaQa.DocumentChanges (
                    DocId, JiraNumber, CABNumber, SprintNumber, Status,
                    Priority, Severity, TableName, ColumnName, SchemaName,
                    ChangeType, ChangeApplied, ReportedBy, AssignedTo,
                    DateRequested, StoredProcedureName, CreatedDate
                ) VALUES (
                    NULL,  -- DocId is NULL initially, Step 2 will generate it
                    @JiraNumber, @CabNumber, @SprintNumber, @Status,
                    @Priority, @Severity, @TableName, @ColumnName, @SchemaName,
                    @ChangeType, @ChangeApplied, @ReportedBy, @AssignedTo,
                    @DateRequested, @StoredProcedureName, GETUTCDATE()
                )";

            var rowsAffected = await connection.ExecuteAsync(sql, new
            {
                row.JiraNumber,
                row.CabNumber,
                row.SprintNumber,
                row.Status,
                row.Priority,
                row.Severity,
                row.TableName,
                row.ColumnName,
                row.SchemaName,
                row.ChangeType,
                row.ChangeApplied,
                row.ReportedBy,
                row.AssignedTo,
                row.DateRequested,
                row.StoredProcedureName
            });

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Inserted new row for JIRA {JiraNumber}", row.JiraNumber);
                
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
                _logger.LogWarning("DocId {DocId} not found in database, skipping update", row.DocId);
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
                    SchemaName = @SchemaName,
                    ChangeType = @ChangeType,
                    ChangeApplied = @ChangeApplied,
                    ReportedBy = @ReportedBy,
                    AssignedTo = @AssignedTo,
                    DateRequested = @DateRequested,
                    StoredProcedureName = @StoredProcedureName,
                    ModifiedDate = GETUTCDATE()
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
                row.SchemaName,
                row.ChangeType,
                row.ChangeApplied,
                row.ReportedBy,
                row.AssignedTo,
                row.DateRequested,
                row.StoredProcedureName
            });

            if (rowsAffected > 0)
            {
                _logger.LogInformation("Updated existing row for DocId {DocId}", row.DocId);
                
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
    public string? SchemaName { get; set; }
    public string? ChangeType { get; set; }
    public string? ChangeApplied { get; set; }
    public string? ReportedBy { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? DateRequested { get; set; }
    public string? StoredProcedureName { get; set; }
}
