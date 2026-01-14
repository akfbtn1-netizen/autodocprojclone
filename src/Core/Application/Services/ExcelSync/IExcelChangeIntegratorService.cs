namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

/// <summary>
/// Interface for Excel integration service that handles reading from and writing back to Excel files.
/// </summary>
public interface IExcelChangeIntegratorService
{
    /// <summary>
    /// Writes a generated DocId back to the corresponding Excel row.
    /// </summary>
    /// <param name="jiraNumber">The JIRA number to identify the row</param>
    /// <param name="docId">The generated DocId to write back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task WriteDocIdToExcelAsync(string jiraNumber, string docId, CancellationToken cancellationToken = default);
}