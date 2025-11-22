
namespace Enterprise.Documentation.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing the approval status of a document or template.
/// Encapsulates business rules around approval workflows.
/// </summary>
public sealed class ApprovalStatus : BaseValueObject
{
    // Status constants
    private const string PendingStatus = "Pending";
    private const string ApprovedStatus = "Approved";
    private const string RejectedStatus = "Rejected";
    private const string ExpiredStatus = "Expired";
    private const string NotRequiredStatus = "NotRequired";

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
        return new ApprovalStatus(NotRequiredStatus, null, DateTime.UtcNow, null);
    }

    /// <summary>
    /// Creates a new approval status indicating approval is pending.
    /// </summary>
    public static ApprovalStatus Pending(string? comments = null)
    {
        return new ApprovalStatus(PendingStatus, comments, DateTime.UtcNow, null);
    }

    /// <summary>
    /// Creates a new approval status indicating the item was approved.
    /// </summary>
    public static ApprovalStatus Approved(UserId approvedBy, string? comments = null)
    {
        if (approvedBy == null)
            throw new ArgumentNullException(nameof(approvedBy));

        return new ApprovalStatus(ApprovedStatus, comments, DateTime.UtcNow, approvedBy);
    }

    /// <summary>
    /// Creates a new approval status indicating the item was rejected.
    /// </summary>
    public static ApprovalStatus Rejected(UserId rejectedBy, string? comments = null)
    {
        if (rejectedBy == null)
            throw new ArgumentNullException(nameof(rejectedBy));

        return new ApprovalStatus(RejectedStatus, comments, DateTime.UtcNow, rejectedBy);
    }

    /// <summary>
    /// Creates a new approval status indicating the approval has expired.
    /// </summary>
    public static ApprovalStatus Expired(string? comments = null)
    {
        return new ApprovalStatus(ExpiredStatus, comments, DateTime.UtcNow, null);
    }

    /// <summary>Whether the status is approved</summary>
    public bool IsApproved => Status == ApprovedStatus;
    /// <summary>Whether the status is pending approval</summary>
    public bool IsPending => Status == PendingStatus;
    /// <summary>Whether the status is rejected</summary>
    public bool IsRejected => Status == RejectedStatus;
    /// <summary>Whether the status requires action</summary>
    public bool RequiresAction => Status == PendingStatus;
    /// <summary>Whether the status is in a terminal state</summary>
    public bool IsTerminal => Status is ApprovedStatus or RejectedStatus or ExpiredStatus;

    /// <summary>
    /// Determines if this status can transition to the specified new status.
    /// Implements business rules for approval workflow transitions.
    /// </summary>
    public bool CanTransitionTo(string newStatus)
    {
        return newStatus switch
        {
            NotRequiredStatus => false, // Cannot transition to NotRequired once in workflow
            PendingStatus => Status is NotRequiredStatus or RejectedStatus, // Can request approval again after rejection
            ApprovedStatus => Status == PendingStatus,
            RejectedStatus => Status == PendingStatus,
            ExpiredStatus => Status == PendingStatus,
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