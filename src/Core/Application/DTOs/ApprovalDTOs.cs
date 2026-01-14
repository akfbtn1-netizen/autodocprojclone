using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Application.Models;

namespace Enterprise.Documentation.Core.Application.DTOs;

public class CreateApprovalRequest
{
    public string JiraNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public int SLAHours { get; set; } = 72;
    public string RequesterEmail { get; set; } = string.Empty;
    public int? MetadataId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public ApprovalMetadata? Metadata { get; set; }
}

public class ApprovalDecision
{
    public string Comments { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedDate { get; set; } = DateTime.UtcNow;
}

public class RejectionDecision
{
    public string Reason { get; set; } = string.Empty;
    public string RejectedBy { get; set; } = string.Empty;
    public DateTime RejectedDate { get; set; } = DateTime.UtcNow;
    public List<string> RequiredChanges { get; set; } = new();
}

public class EditDecision
{
    public string Changes { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string EditedBy { get; set; } = string.Empty;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
    public string? NewDocumentPath { get; set; }
    public string? NewObjectName { get; set; }
    public string? NewDocumentType { get; set; }
    public string? NewPriority { get; set; }
}

public class UpdateDocumentRequest
{
    public string DocumentPath { get; set; } = string.Empty;
    public string? NewDocumentPath { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
}

public class Suggestion
{
    public string Content { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string SuggestedBy { get; set; } = string.Empty;
    public DateTime SuggestedDate { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty; // Content, Format, Process, etc.
}

public class ApprovalResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? ApprovalId { get; set; }
    public ApprovalEntity? Approval { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class EditResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? ApprovalId { get; set; }
    public ApprovalEntity? UpdatedApproval { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class SuggestionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? SuggestionId { get; set; }
    public Suggestion? AddedSuggestion { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public class ApprovalSummary
{
    public Guid Id { get; set; }
    public string JiraNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysRemaining { get; set; }
}

public class ApprovalDetails
{
    public Guid Id { get; set; }
    public string JiraNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int SLAHours { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime DueDate { get; set; }
    public string RequesterEmail { get; set; } = string.Empty;
    public int? MetadataId { get; set; }
    public string Comments { get; set; } = string.Empty;
    public ApprovalEntity Approval { get; set; } = null!;
    public List<Suggestion> Suggestions { get; set; } = new();
    public List<string> History { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ApprovalStats
{
    public int TotalApprovals { get; set; }
    public int PendingApprovals { get; set; }
    public int TotalPending { get; set; }
    public int TotalApproved { get; set; }
    public int TotalRejected { get; set; }
    public int TotalOverdue { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int OverdueCount { get; set; }
    public double AverageProcessingDays { get; set; }
    public double AverageProcessingTime { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, int> ByPriority { get; set; } = new();
    public Dictionary<string, int> ByDocumentType { get; set; } = new();
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
    
    // Default constructor
    public PagedResult() { }
    
    // Constructor that accepts the common 4-parameter pattern
    public PagedResult(List<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        HasPrevious = pageNumber > 1;
        HasNext = pageNumber < TotalPages;
    }
}

public class SchemaStats
{
    public string SchemaName { get; set; } = string.Empty;
    public int TotalObjects { get; set; }
    public int TotalSchemas { get; set; }
    public int TotalTables { get; set; }
    public int TotalStoredProcedures { get; set; }
    public int TotalColumns { get; set; }
    public int TableCount { get; set; }
    public int ViewCount { get; set; }
    public int ProcedureCount { get; set; }
    public int FunctionCount { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime LastAnalyzed { get; set; }
    public Dictionary<string, int> TablesPerSchema { get; set; } = new();
}

public record SchemaStatsRecord(
    string SchemaName,
    int TableCount,
    int StoredProcedureCount,
    int ViewCount = 0,
    int FunctionCount = 0
);