namespace Enterprise.Documentation.Core.Domain.Entities.Search;

/// <summary>
/// AI-generated category suggestions awaiting human approval.
/// Implements human-in-loop pattern for continuous learning.
/// </summary>
public class CategorySuggestion : BaseEntity
{
    public Guid SuggestionId { get; private set; }
    public string DocumentId { get; private set; } = string.Empty;
    public string? CurrentCategory { get; private set; }
    public string SuggestedCategory { get; private set; } = string.Empty;
    public decimal ConfidenceScore { get; private set; }
    public string? Reasoning { get; private set; }
    public string Status { get; private set; } = SuggestionStatuses.Pending;
    public string? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private CategorySuggestion() { } // EF Core

    public static CategorySuggestion Create(
        string documentId,
        string? currentCategory,
        string suggestedCategory,
        decimal confidenceScore,
        string? reasoning = null)
    {
        return new CategorySuggestion
        {
            SuggestionId = Guid.NewGuid(),
            DocumentId = documentId,
            CurrentCategory = currentCategory,
            SuggestedCategory = suggestedCategory,
            ConfidenceScore = confidenceScore,
            Reasoning = reasoning,
            Status = SuggestionStatuses.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(string reviewedBy, string? notes = null)
    {
        Status = SuggestionStatuses.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
    }

    public void Reject(string reviewedBy, string? notes = null)
    {
        Status = SuggestionStatuses.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
    }

    public bool IsPending => Status == SuggestionStatuses.Pending;
    public bool IsHighConfidence => ConfidenceScore >= 0.9m;
}

/// <summary>
/// Suggestion status constants
/// </summary>
public static class SuggestionStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
