using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync
{
    public class ExcelChangeIntegratorService : BackgroundService, IExcelChangeIntegratorService
    {
        private readonly ILogger<ExcelChangeIntegratorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _excelFilePath;
        private readonly string _connectionString;
        private readonly TimeSpan _pollInterval;

        public ExcelChangeIntegratorService(
            ILogger<ExcelChangeIntegratorService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            _excelFilePath = _configuration["ExcelChangeIntegrator:ExcelPath"] 
                ?? @"C:\Users\Alexander.Kirby\Desktop\Change Spreadsheet\BI Analytics Change Spreadsheet.xlsx";
            
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Database connection string not configured");
            
            var pollMinutes = int.Parse(_configuration["ExcelChangeIntegrator:PollIntervalMinutes"] ?? "1");
            _pollInterval = TimeSpan.FromMinutes(pollMinutes);

            // Set EPPlus license
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExcelChangeIntegrator started. Watching: {Path}", _excelFilePath);
            _logger.LogInformation("Poll interval: {Interval} minutes", _pollInterval.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExcelFileAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Excel file");
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        private async Task ProcessExcelFileAsync(CancellationToken ct)
        {
            if (!File.Exists(_excelFilePath))
            {
                _logger.LogWarning("Excel file not found: {Path}", _excelFilePath);
                return;
            }

            FileInfo fileInfo = new FileInfo(_excelFilePath);

            using var package = new ExcelPackage(fileInfo);
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet.Dimension == null)
            {
                _logger.LogWarning("Excel worksheet is empty");
                return;
            }

            // Headers in row 3, data starts row 4
            const int headerRow = 3;
            const int firstDataRow = 4;
            int lastRow = worksheet.Dimension.End.Row;

            _logger.LogInformation("📊 Excel has {TotalRows} rows (rows {Start}-{End})", 
                lastRow - firstDataRow + 1, firstDataRow, lastRow);

            // Read column indices
            var columnMap = ReadColumnHeaders(worksheet, headerRow);

            int insertedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            // Process each data row
            for (int rowNum = firstDataRow; rowNum <= lastRow; rowNum++)
            {
                try
                {
                    var rowData = ReadExcelRow(worksheet, rowNum, columnMap);
                    
                    // Add debug logging
                    _logger.LogInformation("🔍 Processing row {Row}: JIRA={Jira}, Status={Status}, Table={Table}, Column={Column}", 
                        rowNum, rowData.JiraNumber, rowData.Status, rowData.Table, rowData.Column);

                    // Only process rows with "Completed" status
                    if (!string.Equals(rowData.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("⏭️ Skipping row {Row} - Status is '{Status}', not 'Completed'", rowNum, rowData.Status);
                        skippedCount++;
                        continue;
                    }

                    // Check if row already exists
                    var uniqueKey = GenerateUniqueKey(rowData.JiraNumber, rowData.Table, rowData.Column);
                    _logger.LogInformation("🔑 Generated unique key for row {Row}: {UniqueKey}", rowNum, uniqueKey);
                    
                    var exists = await RowExistsAsync(uniqueKey, ct);
                    _logger.LogInformation("🗄️ Row {Row} exists check: {Exists}", rowNum, exists);

                    if (exists)
                    {
                        // Check if content changed
                        var contentHash = GenerateContentHash(rowData);
                        var hasChanged = await ContentHasChangedAsync(uniqueKey, contentHash, ct);
                        
                        _logger.LogInformation("🔄 Content changed check for row {Row}: {HasChanged}", rowNum, hasChanged);

                        if (hasChanged)
                        {
                            await UpdateDocumentChangesAsync(rowData, uniqueKey, contentHash, rowNum, ct);
                            updatedCount++;
                            _logger.LogInformation("Updated row for JIRA {Jira}", rowData.JiraNumber);
                        }
                        else
                        {
                            _logger.LogInformation("⏭️ Skipping row {Row} - No content changes detected", rowNum);
                            skippedCount++;
                        }
                    }
                    else
                    {
                        var contentHash = GenerateContentHash(rowData);
                        _logger.LogInformation("➕ Inserting new row {Row} with hash {Hash}", rowNum, contentHash);
                        await InsertDocumentChangesAsync(rowData, uniqueKey, contentHash, rowNum, ct);
                        insertedCount++;
                        _logger.LogInformation("Inserted new row for JIRA {Jira}", rowData.JiraNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing Excel row {Row}", rowNum);
                }
            }

            _logger.LogInformation("📊 Processed {Total} completed rows: {Inserted} inserted, {Updated} updated, {Skipped} skipped",
                insertedCount + updatedCount + skippedCount, insertedCount, updatedCount, skippedCount);
        }

        private Dictionary<string, int> ReadColumnHeaders(ExcelWorksheet worksheet, int headerRow)
        {
            var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var headerValue = worksheet.Cells[headerRow, col].Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(headerValue))
                {
                    columnMap[headerValue] = col;
                }
            }

            _logger.LogInformation("📋 Found columns: {Columns}", string.Join(", ", columnMap.Keys));

            return columnMap;
        }

        private ExcelRowData ReadExcelRow(ExcelWorksheet worksheet, int rowNum, Dictionary<string, int> columnMap)
        {
            string GetCellValue(string columnName)
            {
                if (columnMap.TryGetValue(columnName, out int colIndex))
                {
                    return worksheet.Cells[rowNum, colIndex].Value?.ToString()?.Trim() ?? "";
                }
                return "";
            }

            DateTime? GetDateValue(string columnName)
            {
                if (columnMap.TryGetValue(columnName, out int colIndex))
                {
                    var cellValue = worksheet.Cells[rowNum, colIndex].Value;
                    if (cellValue is DateTime dt)
                        return dt;
                    if (DateTime.TryParse(cellValue?.ToString(), out DateTime parsed))
                        return parsed;
                }
                return null;
            }

            return new ExcelRowData
            {
                DocID = GetCellValue("DocID"),  // Will be blank, "TBD", or actual DocId
                Date = GetDateValue("Date"),
                JiraNumber = GetCellValue("JIRA #"),
                CABNumber = GetCellValue("CAB #"),
                SprintNumber = GetCellValue("Sprint #"),
                Status = GetCellValue("Status"),
                Priority = GetCellValue("Priority"),
                Severity = GetCellValue("Severity"),
                Table = GetCellValue("Table"),
                Column = GetCellValue("Column"),
                ChangeType = GetCellValue("Change Type"),
                Description = GetCellValue("Description"),
                ReportedBy = GetCellValue("Reported By"),
                AssignedTo = GetCellValue("Assigned to"),
                ChangeApplied = GetCellValue("Change Applied"),
                LocationOfCodeChange = GetCellValue("Location of Changed Code")
            };
        }

        private async Task<bool> RowExistsAsync(string uniqueKey, CancellationToken ct)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = "SELECT COUNT(1) FROM DaQa.DocumentChanges WHERE UniqueKey = @UniqueKey";
            var count = await connection.ExecuteScalarAsync<int>(sql, new { UniqueKey = uniqueKey });
            return count > 0;
        }

        private async Task<bool> ContentHasChangedAsync(string uniqueKey, string contentHash, CancellationToken ct)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = "SELECT ContentHash FROM DaQa.DocumentChanges WHERE UniqueKey = @UniqueKey";
            var existingHash = await connection.ExecuteScalarAsync<string>(sql, new { UniqueKey = uniqueKey });
            return existingHash != contentHash;
        }

        private async Task InsertDocumentChangesAsync(ExcelRowData row, string uniqueKey, string contentHash, int excelRowNum, CancellationToken ct)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"
                INSERT INTO DaQa.DocumentChanges (
                    DocId,
                    Date,
                    JiraNumber,
                    CABNumber,
                    SprintNumber,
                    Status,
                    Priority,
                    Severity,
                    TableName,
                    ColumnName,
                    ChangeType,
                    Description,
                    ReportedBy,
                    AssignedTo,
                    ChangeApplied,
                    LocationOfCodeChange,
                    ExcelRowNumber,
                    LastSyncedFromExcel,
                    SyncStatus,
                    UniqueKey,
                    ContentHash,
                    CreatedAt
                ) VALUES (
                    @DocId,
                    @Date,
                    @JiraNumber,
                    @CABNumber,
                    @SprintNumber,
                    @Status,
                    @Priority,
                    @Severity,
                    @TableName,
                    @ColumnName,
                    @ChangeType,
                    @Description,
                    @ReportedBy,
                    @AssignedTo,
                    @ChangeApplied,
                    @LocationOfCodeChange,
                    @ExcelRowNumber,
                    @LastSyncedFromExcel,
                    @SyncStatus,
                    @UniqueKey,
                    @ContentHash,
                    @CreatedAt
                )";

            var parameters = new
            {
                // DocId: Store as-is from Excel (blank, "TBD", or actual DocId)
                DocId = string.IsNullOrWhiteSpace(row.DocID) ? (string?)null : row.DocID,
                Date = row.Date,
                JiraNumber = TruncateIfNeeded(row.JiraNumber, 50),
                CABNumber = TruncateIfNeeded(row.CABNumber, 50),
                SprintNumber = TruncateIfNeeded(row.SprintNumber, 50),
                Status = TruncateIfNeeded(row.Status, 50),
                Priority = TruncateIfNeeded(row.Priority, 50),
                Severity = TruncateIfNeeded(row.Severity, 50),
                TableName = TruncateIfNeeded(row.Table, 255),
                ColumnName = TruncateIfNeeded(row.Column, 255),
                ChangeType = TruncateIfNeeded(row.ChangeType, 100),
                Description = TruncateIfNeeded(row.Description, 1000),
                ReportedBy = TruncateIfNeeded(row.ReportedBy, 100),
                AssignedTo = TruncateIfNeeded(row.AssignedTo, 100),
                ChangeApplied = TruncateIfNeeded(row.ChangeApplied, 1000),
                LocationOfCodeChange = TruncateIfNeeded(row.LocationOfCodeChange, 500),
                // System fields
                ExcelRowNumber = excelRowNum,
                LastSyncedFromExcel = DateTime.UtcNow,
                SyncStatus = "Synced",
                UniqueKey = uniqueKey,
                ContentHash = contentHash,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await connection.ExecuteAsync(sql, parameters);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "❌ SQL ERROR inserting JIRA {Jira}: {Message}", row.JiraNumber, ex.Message);
                throw;
            }
        }

        private async Task UpdateDocumentChangesAsync(ExcelRowData row, string uniqueKey, string contentHash, int excelRowNum, CancellationToken ct)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"
                UPDATE DaQa.DocumentChanges
                SET 
                    DocId = @DocId,
                    Date = @Date,
                    CABNumber = @CABNumber,
                    SprintNumber = @SprintNumber,
                    Status = @Status,
                    Priority = @Priority,
                    Severity = @Severity,
                    TableName = @TableName,
                    ColumnName = @ColumnName,
                    ChangeType = @ChangeType,
                    Description = @Description,
                    ReportedBy = @ReportedBy,
                    AssignedTo = @AssignedTo,
                    ChangeApplied = @ChangeApplied,
                    LocationOfCodeChange = @LocationOfCodeChange,
                    ExcelRowNumber = @ExcelRowNumber,
                    LastSyncedFromExcel = @LastSyncedFromExcel,
                    ContentHash = @ContentHash,
                    UpdatedAt = @UpdatedAt,
                    SyncStatus = 'Updated'
                WHERE UniqueKey = @UniqueKey";

            var parameters = new
            {
                DocId = string.IsNullOrWhiteSpace(row.DocID) ? (string?)null : row.DocID,
                Date = row.Date,
                CABNumber = TruncateIfNeeded(row.CABNumber, 50),
                SprintNumber = TruncateIfNeeded(row.SprintNumber, 50),
                Status = TruncateIfNeeded(row.Status, 50),
                Priority = TruncateIfNeeded(row.Priority, 50),
                Severity = TruncateIfNeeded(row.Severity, 50),
                TableName = TruncateIfNeeded(row.Table, 255),
                ColumnName = TruncateIfNeeded(row.Column, 255),
                ChangeType = TruncateIfNeeded(row.ChangeType, 100),
                Description = TruncateIfNeeded(row.Description, 1000),
                ReportedBy = TruncateIfNeeded(row.ReportedBy, 100),
                AssignedTo = TruncateIfNeeded(row.AssignedTo, 100),
                ChangeApplied = TruncateIfNeeded(row.ChangeApplied, 1000),
                LocationOfCodeChange = TruncateIfNeeded(row.LocationOfCodeChange, 500),
                ExcelRowNumber = excelRowNum,
                LastSyncedFromExcel = DateTime.UtcNow,
                ContentHash = contentHash,
                UpdatedAt = DateTime.UtcNow,
                UniqueKey = uniqueKey
            };

            await connection.ExecuteAsync(sql, parameters);
        }

        public async Task WriteDocIdToExcelAsync(string jiraNumber, string docId, CancellationToken cancellationToken = default)
        {
			    _logger.LogInformation("Excel writeback DISABLED - preventing file corruption");
    return; // EXIT IMMEDIATELY - DON'T RUN REST OF CODE
	  /*
            try
            {
                _logger.LogInformation("🔥 EXCEL WRITEBACK CALLED 🔥 JiraNumber: {Jira}, DocId: {DocId}, FilePath: {Path}",
                    jiraNumber, docId, _excelFilePath);

                var fileInfo = new FileInfo(_excelFilePath);

                // Retry up to 3 times if file is locked
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        using var package = new ExcelPackage(fileInfo);
                        var worksheet = package.Workbook.Worksheets[0];

                        const int headerRow = 3;
                        const int firstDataRow = 4;

                        // Find DocID and JIRA columns
                        int docIdColumn = -1;
                        int jiraColumn = -1;

                        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                        {
                            var header = worksheet.Cells[headerRow, col].Value?.ToString()?.Trim() ?? "";

                            if (string.Equals(header, "DocID", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(header, "Doc_ID", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(header, "Doc ID", StringComparison.OrdinalIgnoreCase))
                            {
                                docIdColumn = col;
                                _logger.LogInformation("Found DocId column at position {Col} with header '{Header}'", col, header);
                            }

                            if (string.Equals(header, "JIRA #", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(header, "JiraNumber", StringComparison.OrdinalIgnoreCase))
                            {
                                jiraColumn = col;
                                _logger.LogInformation("Found JIRA column at position {Col} with header '{Header}'", col, header);
                            }
                        }

                        if (docIdColumn == -1 || jiraColumn == -1)
                        {
                            _logger.LogError("Required columns not found. DocIdColumn={DocId}, JiraColumn={Jira}", docIdColumn, jiraColumn);
                            return;
                        }

                        // Find the row with matching JIRA number
                        _logger.LogInformation("Searching for JIRA {Jira} in {Rows} rows", jiraNumber, worksheet.Dimension.End.Row);

                        for (int row = firstDataRow; row <= worksheet.Dimension.End.Row; row++)
                        {
                            var jiraValue = worksheet.Cells[row, jiraColumn].Value?.ToString()?.Trim() ?? "";

                            if (string.Equals(jiraValue, jiraNumber, StringComparison.OrdinalIgnoreCase))
                            {
                                // Found the row - write DocId
                                worksheet.Cells[row, docIdColumn].Value = docId;
                                _logger.LogInformation("✅ DocId {DocId} written to Excel row {Row} for JIRA {Jira}", docId, row, jiraNumber);
                                
                                await package.SaveAsync(cancellationToken);
                                _logger.LogInformation("Excel file saved with DocId {DocId} for JIRA {Jira}", docId, jiraNumber);
                                return;
                            }
                        }

                        _logger.LogWarning("JIRA {Jira} not found in Excel file", jiraNumber);
                        return;
                    }
                    catch (IOException) when (attempt < 3)
                    {
                        _logger.LogWarning("Excel file locked, attempt {Attempt}/3. Retrying in 2 seconds...", attempt);
                        await Task.Delay(2000, cancellationToken);
                    }
                }

                _logger.LogError("Could not open Excel file after 3 attempts (file locked)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write DocId back to Excel (non-critical)");
                // Don't throw - this is a nice-to-have feature
            }
			*/
        }

        private string GenerateUniqueKey(string? jiraNumber, string? tableName, string? columnName)
        {
            var combined = $"{jiraNumber ?? ""}_{tableName ?? ""}_{columnName ?? ""}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 32);
        }

        private string GenerateContentHash(ExcelRowData row)
        {
            var content = $"{row.JiraNumber}{row.Date}{row.Status}{row.Table}{row.Column}{row.Description}{row.ChangeApplied}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 32);
        }

        private string TruncateIfNeeded(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";
            if (value.Length <= maxLength) return value;

            _logger.LogWarning("⚠️ Truncating value from {Original} to {Max} chars", value.Length, maxLength);
            return value.Substring(0, maxLength);
        }

        private class ExcelRowData
        {
            public string DocID { get; set; } = "";
            public DateTime? Date { get; set; }
            public string JiraNumber { get; set; } = "";
            public string CABNumber { get; set; } = "";
            public string SprintNumber { get; set; } = "";
            public string Status { get; set; } = "";
            public string Priority { get; set; } = "";
            public string Severity { get; set; } = "";
            public string Table { get; set; } = "";
            public string Column { get; set; } = "";
            public string ChangeType { get; set; } = "";
            public string Description { get; set; } = "";
            public string ReportedBy { get; set; } = "";
            public string AssignedTo { get; set; } = "";
            public string ChangeApplied { get; set; } = "";
            public string LocationOfCodeChange { get; set; } = "";
        }
    }
}
