using System.Threading;
using System.Threading.Task;

namespace Enterprise.Documentation.Core.Application.Services.DocumentGeneration;

/// <summary>
/// Generates DocIds following the naming convention:
/// {DocumentType}-{SequentialNumber}-{ObjectName}[-{ColumnName}]-{JiraNumber}
/// </summary>
public interface IDocIdGeneratorService
{
    Task<string> GenerateDocIdAsync(DocIdRequest request, CancellationToken cancellationToken = default);
}

public class DocIdRequest
{
    public required string ChangeType { get; set; }          // "Business Request", "Enhancement", "Defect Fix"
    public required string Table { get; set; }                // "gwpc.irf_policy"
    public string? Column { get; set; }                       // "PolicyNumber" (optional)
    public required string JiraNumber { get; set; }           // "BAS-123"
    public string UpdatedBy { get; set; } = "ExcelSyncService";
}
