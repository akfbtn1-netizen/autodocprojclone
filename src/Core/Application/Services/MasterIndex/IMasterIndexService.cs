using Enterprise.Documentation.Core.Domain.Models;

namespace Enterprise.Documentation.Core.Application.Services.MasterIndex;

/// <summary>
/// Populates the DaQa.MasterIndex table with comprehensive metadata after document approval
/// </summary>
public interface IMasterIndexService
{
    Task<int> PopulateIndexAsync(MasterIndexEntry entry, CancellationToken cancellationToken = default);
    Task UpdateDocumentationLinkAsync(string docId, string sharePointUrl, CancellationToken cancellationToken = default);
}

public class MasterIndexEntry
{
    // Core Identity
    public required string DocId { get; set; }
    public required string DocumentTitle { get; set; }
    public required string DocumentType { get; set; }
    public required string LocalFilePath { get; set; }

    // From Excel
    public required string CABNumber { get; set; }
    public string? JiraNumber { get; set; }
    public required string Table { get; set; }
    public string? Column { get; set; }
    public required string ChangeType { get; set; }
    public required string Description { get; set; }
    public required string Documentation { get; set; }
    public string? ModifiedStoredProcedures { get; set; }
    public required string ReportedBy { get; set; }
    public required string AssignedTo { get; set; }
    public required DateTime DateEntered { get; set; }
    public string? Priority { get; set; }
    public string? Severity { get; set; }
    public string? Sprint { get; set; }

    // AI-Enhanced
    public string? EnhancedDescription { get; set; }
    public List<string>? AIGeneratedTags { get; set; }
    public string? SemanticCategory { get; set; }

    // Approval Info
    public required string ApprovedBy { get; set; }
    public required DateTime ApprovedDate { get; set; }
}
