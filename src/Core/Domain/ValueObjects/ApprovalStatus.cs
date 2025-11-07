
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing the approval status of a document or template.
/// Encapsulates business rules around approval workflows.
/// </summary>
public sealed class ApprovalStatus : BaseValueObject
{
    public string Status { get; private set; } = string.Empty;
    public string? Comments { get; private set; }
    public DateTime StatusChangedAt { get; private set; }
    public UserId? ApprovedBy { get; private set; }

    // Parameterless constructor for EF Core
    private ApprovalStatus()
    {
    }

    private ApprovalStatus(string status, string? comments, DateTime statusChangedAt, UserId? approvedBy)
    {
        Status = status ?? throw new ArgumentNullException(nameof(status));
        Comments = comments;
        StatusChangedAt = statusChangedAt;
        ApprovedBy = approvedBy;
    }

    /// <summary>
    /// Creates a new approval status indicating approval is not required.
    /// </summary>
    public static ApprovalStatus NotRequired()
    {
        return new ApprovalStatus("NotRequired", null, DateTime.UtcNow, null);
    }

    /// <summary>
    /// Creates a new approval status indicating approval is pending.
    /// </summary>
    public static ApprovalStatus Pending(string? comments = null)
    {
        return new ApprovalStatus("Pending", comments, DateTime.UtcNow, null);
    }

    /// <summary>
    /// Creates a new approval status indicating the item was approved.
    /// </summary>
    public static ApprovalStatus Approved(UserId approvedBy, string? comments = null)
    {
        if (approvedBy == null)
            throw new ArgumentNullException(nameof(approvedBy));

        return new ApprovalStatus("Approved", comments, DateTime.UtcNow, approvedBy);
    }

    /// <summary>
    /// Creates a new approval status indicating the item was rejected.
    /// </summary>
    public static ApprovalStatus Rejected(UserId rejectedBy, string? comments = null)
    {
        if (rejectedBy == null)
            throw new ArgumentNullException(nameof(rejectedBy));

        return new ApprovalStatus("Rejected", comments, DateTime.UtcNow, rejectedBy);
    }

    /// <summary>
    /// Creates a new approval status indicating the approval has expired.
    /// </summary>
    public static ApprovalStatus Expired(string? comments = null)
    {
        return new ApprovalStatus("Expired", comments, DateTime.UtcNow, null);
    }

    // Business rule methods
    public bool IsApproved => Status == "Approved";
    public bool IsPending => Status == "Pending";
    public bool IsRejected => Status == "Rejected";
    public bool RequiresAction => Status == "Pending";
    public bool IsTerminal => Status is "Approved" or "Rejected" or "Expired";

    /// <summary>
    /// Determines if this status can transition to the specified new status.
    /// Implements business rules for approval workflow transitions.
    /// </summary>
    public bool CanTransitionTo(string newStatus)
    {
        return newStatus switch
        {
            "NotRequired" => false, // Cannot transition to NotRequired once in workflow
            "Pending" => Status is "NotRequired" or "Rejected", // Can request approval again after rejection
            "Approved" => Status == "Pending",
            "Rejected" => Status == "Pending",
            "Expired" => Status == "Pending",
            _ => false
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Status;
        yield return Comments;
        yield return StatusChangedAt;
        yield return ApprovedBy?.Value;
    }

    public override string ToString() => 
        ApprovedBy != null 
            ? $"{Status} by {ApprovedBy} at {StatusChangedAt:yyyy-MM-dd HH:mm}"
            : $"{Status} at {StatusChangedAt:yyyy-MM-dd HH:mm}";
}