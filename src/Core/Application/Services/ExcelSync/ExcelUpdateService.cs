using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

/// <summary>
/// Updates Excel spreadsheet with DocId and DocumentationLink
/// </summary>
public interface IExcelUpdateService
{
    Task UpdateDocIdAsync(string cabNumber, string docId, CancellationToken cancellationToken = default);
    Task UpdateDocumentationLinkAsync(string docId, string sharePointUrl, CancellationToken cancellationToken = default);
}

public class ExcelUpdateService : IExcelUpdateService
{
    private readonly ILogger<ExcelUpdateService> _logger;
    private readonly string _excelFilePath;
    private readonly string _connectionString;
    private static readonly SemaphoreSlim _excelLock = new SemaphoreSlim(1, 1);

    public ExcelUpdateService(
        ILogger<ExcelUpdateService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _excelFilePath = configuration["ExcelSync:LocalFilePath"]
            ?? throw new InvalidOperationException("ExcelSync:LocalFilePath not configured");
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured");
    }

    public async Task UpdateDocIdAsync(string cabNumber, string docId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating Excel with DocId: {DocId} for CAB: {CABNumber}", docId, cabNumber);

        await _excelLock.WaitAsync(cancellationToken);
        try
        {
            // First update database
            await UpdateDocIdInDatabaseAsync(cabNumber, docId, cancellationToken);

            // Then update Excel
            await UpdateExcelFileAsync(cabNumber, (worksheet, row) =>
            {
                // Find DocId column (typically column O or 15)
                int docIdColumn = FindColumn(worksheet, "DocId") ?? 15;
                worksheet.Cells[row, docIdColumn].Value = docId;

                _logger.LogInformation("Updated DocId in Excel at row {Row}, column {Column}", row, docIdColumn);
            }, cancellationToken);
        }
        finally
        {
            _excelLock.Release();
        }
    }

    public async Task UpdateDocumentationLinkAsync(string docId, string sharePointUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating Excel with SharePoint URL for DocId: {DocId}", docId);

        await _excelLock.WaitAsync(cancellationToken);
        try
        {
            // First update database
            await UpdateDocumentationLinkInDatabaseAsync(docId, sharePointUrl, cancellationToken);

            // Then update Excel
            await UpdateExcelFileAsync(docId, (worksheet, row) =>
            {
                // Find DocumentationLink column (typically column N or 14)
                int linkColumn = FindColumn(worksheet, "Documentation Link") ?? 14;
                worksheet.Cells[row, linkColumn].Hyperlink = new Uri(sharePointUrl);
                worksheet.Cells[row, linkColumn].Value = "View Document";

                _logger.LogInformation("Updated Documentation Link in Excel at row {Row}, column {Column}", row, linkColumn);
            }, cancellationToken);
        }
        finally
        {
            _excelLock.Release();
        }
    }

    private async Task UpdateDocIdInDatabaseAsync(string cabNumber, string docId, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE DaQa.DocumentChanges
            SET DocId = @DocId,
                UpdatedDate = @UpdatedDate
            WHERE CABNumber = @CABNumber";

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            DocId = docId,
            CABNumber = cabNumber,
            UpdatedDate = DateTime.UtcNow
        });

        _logger.LogInformation("Updated {Count} database record(s) with DocId for CAB: {CABNumber}", rowsAffected, cabNumber);
    }

    private async Task UpdateDocumentationLinkInDatabaseAsync(string docId, string sharePointUrl, CancellationToken cancellationToken)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            UPDATE DaQa.DocumentChanges
            SET DocumentationLink = @SharePointUrl,
                UpdatedDate = @UpdatedDate
            WHERE DocId = @DocId";

        var rowsAffected = await connection.ExecuteAsync(sql, new
        {
            SharePointUrl = sharePointUrl,
            DocId = docId,
            UpdatedDate = DateTime.UtcNow
        });

        _logger.LogInformation("Updated {Count} database record(s) with SharePoint URL for DocId: {DocId}", rowsAffected, docId);
    }

    private async Task UpdateExcelFileAsync(
        string searchValue,
        Action<ExcelWorksheet, int> updateAction,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_excelFilePath))
        {
            _logger.LogError("Excel file not found: {Path}", _excelFilePath);
            throw new FileNotFoundException($"Excel file not found: {_excelFilePath}");
        }

        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage(new FileInfo(_excelFilePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("No worksheets found in Excel file");

            // Find the row containing the search value
            int? targetRow = null;
            for (int row = 4; row <= worksheet.Dimension.End.Row; row++) // Start from row 4 (data rows)
            {
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Text?.Trim();
                    if (cellValue == searchValue)
                    {
                        targetRow = row;
                        break;
                    }
                }

                if (targetRow.HasValue)
                    break;
            }

            if (!targetRow.HasValue)
            {
                _logger.LogWarning("Could not find row with value: {SearchValue}", searchValue);
                return;
            }

            // Perform the update
            updateAction(worksheet, targetRow.Value);

            // Save the Excel file
            await package.SaveAsync(cancellationToken);

            _logger.LogInformation("Successfully updated Excel file for: {SearchValue}", searchValue);
        }
        catch (IOException ex) when (ex.Message.Contains("being used by another process"))
        {
            _logger.LogWarning("Excel file is locked, will retry later: {Path}", _excelFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Excel file");
            throw;
        }
    }

    private int? FindColumn(ExcelWorksheet worksheet, string columnName)
    {
        // Search header row (row 3) for column name
        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
        {
            var headerValue = worksheet.Cells[3, col].Text?.Trim();
            if (string.Equals(headerValue, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return col;
            }
        }

        return null;
    }
}
