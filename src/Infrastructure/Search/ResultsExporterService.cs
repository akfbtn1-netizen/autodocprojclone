using System.Text;
using Enterprise.Documentation.Core.Application.Interfaces.Search;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Infrastructure.Search;

/// <summary>
/// Export search results to various formats (CSV, Excel, PDF).
/// </summary>
public class ResultsExporterService : IResultsExporter
{
    private readonly ILogger<ResultsExporterService> _logger;

    public ResultsExporterService(ILogger<ResultsExporterService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExportResult> ExportToCsvAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("DocumentId,ObjectType,ObjectName,Schema,Database,Category,Classification,Score");

            // Data rows
            foreach (var result in request.Results)
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(result.DocumentId),
                    EscapeCsv(result.ObjectType),
                    EscapeCsv(result.ObjectName),
                    EscapeCsv(result.SchemaName),
                    EscapeCsv(result.DatabaseName),
                    EscapeCsv(result.Category),
                    EscapeCsv(result.DataClassification),
                    result.Score.FusedScore.ToString("F3")));
            }

            var content = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"search_results_{request.QueryId:N}.csv";

            _logger.LogInformation("Exported {Count} results to CSV", request.Results.Count);

            return new ExportResult(
                Success: true,
                FilePath: null,
                FileName: fileName,
                FileContent: content,
                ContentType: "text/csv",
                FileSizeBytes: content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to CSV");
            return new ExportResult(
                Success: false,
                FilePath: null,
                FileName: null,
                FileContent: null,
                ContentType: "text/csv",
                FileSizeBytes: 0,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ExportResult> ExportToExcelAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // For Excel, we'll use a simple CSV format that Excel can open
            // In production, use ClosedXML or EPPlus for proper .xlsx
            var sb = new StringBuilder();

            // Add title if provided
            if (!string.IsNullOrEmpty(request.Options?.ReportTitle))
            {
                sb.AppendLine(request.Options.ReportTitle);
                sb.AppendLine();
            }

            // Header
            sb.AppendLine("DocumentId\tObjectType\tObjectName\tSchema\tDatabase\tCategory\tClassification\tScore");

            // Data rows (tab-separated for Excel)
            foreach (var result in request.Results)
            {
                sb.AppendLine(string.Join("\t",
                    result.DocumentId,
                    result.ObjectType ?? "",
                    result.ObjectName ?? "",
                    result.SchemaName ?? "",
                    result.DatabaseName ?? "",
                    result.Category ?? "",
                    result.DataClassification ?? "",
                    result.Score.FusedScore.ToString("F3")));
            }

            var content = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"search_results_{request.QueryId:N}.xlsx";

            _logger.LogInformation("Exported {Count} results to Excel", request.Results.Count);

            return new ExportResult(
                Success: true,
                FilePath: null,
                FileName: fileName,
                FileContent: content,
                ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileSizeBytes: content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to Excel");
            return new ExportResult(
                Success: false,
                FilePath: null,
                FileName: null,
                FileContent: null,
                ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileSizeBytes: 0,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ExportResult> ExportToPdfAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // For PDF, we'll create a simple HTML-based report
            // In production, use QuestPDF or iTextSharp for proper PDF generation
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #4CAF50; color: white; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine("</style></head><body>");

            if (!string.IsNullOrEmpty(request.Options?.ReportTitle))
            {
                sb.AppendLine($"<h1>{request.Options.ReportTitle}</h1>");
            }

            if (!string.IsNullOrEmpty(request.Options?.ReportDescription))
            {
                sb.AppendLine($"<p>{request.Options.ReportDescription}</p>");
            }

            sb.AppendLine($"<p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Results: {request.Results.Count}</p>");

            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Object</th><th>Type</th><th>Database</th><th>Category</th><th>Score</th></tr>");

            foreach (var result in request.Results)
            {
                var objectPath = string.Join(".",
                    new[] { result.DatabaseName, result.SchemaName, result.ObjectName }
                        .Where(s => !string.IsNullOrEmpty(s)));

                sb.AppendLine($"<tr><td>{objectPath}</td><td>{result.ObjectType}</td>" +
                    $"<td>{result.DatabaseName}</td><td>{result.Category}</td>" +
                    $"<td>{result.Score.FusedScore:F3}</td></tr>");
            }

            sb.AppendLine("</table>");
            sb.AppendLine("</body></html>");

            var content = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"search_results_{request.QueryId:N}.html";

            _logger.LogInformation("Exported {Count} results to PDF (HTML)", request.Results.Count);

            return new ExportResult(
                Success: true,
                FilePath: null,
                FileName: fileName,
                FileContent: content,
                ContentType: "text/html", // Would be application/pdf with real PDF generation
                FileSizeBytes: content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export to PDF");
            return new ExportResult(
                Success: false,
                FilePath: null,
                FileName: null,
                FileContent: null,
                ContentType: "application/pdf",
                FileSizeBytes: 0,
                ErrorMessage: ex.Message);
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
