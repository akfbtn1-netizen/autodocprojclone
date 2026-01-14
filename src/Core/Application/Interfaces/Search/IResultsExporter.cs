using Enterprise.Documentation.Core.Application.DTOs.Search;

namespace Enterprise.Documentation.Core.Application.Interfaces.Search;

/// <summary>
/// Export search results to various formats (CSV, Excel, PDF).
/// </summary>
public interface IResultsExporter
{
    /// <summary>
    /// Export search results to CSV format.
    /// </summary>
    Task<ExportResult> ExportToCsvAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export search results to Excel format with rich formatting.
    /// </summary>
    Task<ExportResult> ExportToExcelAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export search results to PDF report format.
    /// </summary>
    Task<ExportResult> ExportToPdfAsync(
        ExportRequest request,
        CancellationToken cancellationToken = default);
}

public record ExportRequest(
    Guid QueryId,
    string UserId,
    ExportFormat Format,
    List<SearchResultItem> Results,
    ExportOptions? Options = null);

public record ExportOptions(
    bool IncludeLineageGraph = false,
    bool IncludeMetadata = true,
    bool IncludeChangeHistory = false,
    string? ReportTitle = null,
    string? ReportDescription = null);

public record ExportResult(
    bool Success,
    string? FilePath,
    string? FileName,
    byte[]? FileContent,
    string ContentType,
    long FileSizeBytes,
    string? ErrorMessage = null);

public enum ExportFormat
{
    Csv,
    Excel,
    Pdf
}
