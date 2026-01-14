// IApprovalTrackingService.cs
// Interface for approval workflow tracking and post-approval processing

namespace DocGenerator.Services;

public interface IApprovalTrackingService
{
    /// <summary>
    /// Process approved document - handles all post-approval steps
    /// Steps: Generate final doc, populate MasterIndex, embed properties, update SP doc
    /// </summary>
    Task ProcessApprovedDocumentAsync(
        string documentId,
        string approvedBy,
        string? comments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject document with reason - triggers AI refinement workflow
    /// </summary>
    Task RejectDocumentAsync(
        string documentId,
        string rejectionReason,
        string rejectedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approval workflow status for a document
    /// </summary>
    Task<ApprovalWorkflowStatus?> GetApprovalStatusAsync(
        string documentId,
        CancellationToken cancellationToken = default);
}

public class ApprovalWorkflowStatus
{
    public Guid ApprovalId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }
    public int RejectionCount { get; set; }
}
