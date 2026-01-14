namespace Enterprise.Documentation.Api.Models;

public class ApprovalDecisionRequest
{
    public string? Comments { get; set; }
    public string? ApprovedBy { get; set; }
}

public class RejectionRequest
{
    public string Reason { get; set; } = string.Empty;
    public string? RejectedBy { get; set; }
}

public class EditRequest
{
    public object? Content { get; set; }
    public string? Reason { get; set; }
    public string? EditedBy { get; set; }
}

public class RepromptRequest
{
    public string Guidance { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
}

public class SuggestionRequest
{
    public string Content { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? SuggestedBy { get; set; }
}

public class DocumentGenerationRequest
{
    public int EntryId { get; set; }
    public bool ForceRegeneration { get; set; } = false;
    public string? AdditionalGuidance { get; set; }
    public string? RequestedBy { get; set; }
}

public class BulkApprovalRequest
{
    public List<Guid> ApprovalIds { get; set; } = new();
    public string Action { get; set; } = string.Empty; // "approve", "reject"
    public string? Comments { get; set; }
    public string? Reason { get; set; } // For rejections
    public string? PerformedBy { get; set; }
}

public class ApprovalFilterRequest
{
    public string? Status { get; set; }
    public string? DocumentType { get; set; }
    public string? Priority { get; set; }
    public int? Tier { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public bool? IsOverdue { get; set; }
    public string? JiraNumber { get; set; }
    public string? ObjectName { get; set; }
}

public class TierClassificationRequest
{
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string? DatabaseName { get; set; }
    public string? ChangeType { get; set; }
    public string? Description { get; set; }
}

public class MetadataUpdateRequest
{
    public Dictionary<string, object?> Fields { get; set; } = new();
    public string? UpdatedBy { get; set; }
    public string? Reason { get; set; }
}

public class CustomPropertiesRequest
{
    public string FilePath { get; set; } = string.Empty;
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public string? BusinessDomain { get; set; }
    public string? ObjectType { get; set; }
    public bool IncludeArchived { get; set; } = false;
    public int MaxResults { get; set; } = 50;
}