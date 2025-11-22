using System.Threading;
using System.Threading.Tasks;

namespace Enterprise.Documentation.Core.Application.Services.ExcelSync;

/// <summary>
/// Updates Excel spreadsheet with DocId and DocumentationLink
/// </summary>
public interface IExcelUpdateService
{
    Task UpdateDocIdAsync(string cabNumber, string docId, CancellationToken cancellationToken = default);
    Task UpdateDocumentationLinkAsync(string docId, string sharePointUrl, CancellationToken cancellationToken = default);
}
