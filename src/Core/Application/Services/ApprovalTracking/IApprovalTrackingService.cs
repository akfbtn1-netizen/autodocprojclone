using Enterprise.Documentation.Core.Application.DTOs.Approval;

namespace Enterprise.Documentation.Core.Application.Services.ApprovalTracking;

/// <summary>
/// Service for handling approval workflow tracking and post-approval processing.
/// Includes both legacy methods and new Step 8 API methods.
/// </summary>
public interface IApprovalTrackingService
{
    // Legacy methods for document processing
    Task ProcessApprovedDocumentAsync(
        string documentId,
        string approvedBy,
        string? comments,
        CancellationToken cancellationToken = default);

    Task RejectDocumentAsync(
        string documentId,
        string rejectionReason,
        string rejectedBy,
        CancellationToken cancellationToken = default);

    Task<ApprovalWorkflowStatus?> GetApprovalStatusAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    // Step 8 API methods
    Task<IEnumerable<ApprovalDto>> GetPendingApprovalsAsync();
    Task<ApprovalDto?> GetApprovalAsync(Guid approvalId);
    Task<ApprovalResult> ProcessApprovalAsync(Guid approvalId, ApprovalRequest request);
    Task<ApprovalStats> GetApprovalStatsAsync();
}

/// <summary>
/// Status DTO for approval workflow
/// </summary>
public class ApprovalWorkflowStatus
{
    public Guid ApprovalId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }
    public int RejectionCount { get; set; }
}