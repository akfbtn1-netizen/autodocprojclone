using System.ComponentModel.DataAnnotations;

namespace Enterprise.Documentation.Core.Application.Services.Approval;

/// <summary>
/// Service for handling approval workflow tracking and post-approval processing.
/// Extended with new API methods for Step 8.
/// </summary>
public interface IApprovalTrackingService
{
    // Existing methods  
    Task<ApprovalResponse> ApproveDocumentAsync(int approvalId, ApproveDocumentRequest request, CancellationToken cancellationToken = default);
    Task<ApprovalResponse> RejectDocumentAsync(int approvalId, RejectDocumentRequest request, CancellationToken cancellationToken = default);
    
    // New API methods for Step 8
    Task<IEnumerable<ApprovalDto>> GetPendingApprovalsAsync();
    Task<ApprovalDto?> GetApprovalAsync(Guid approvalId);
    Task<ApprovalResult> ProcessApprovalAsync(Guid approvalId, ApprovalRequest request);
    Task<ApprovalStats> GetApprovalStatsAsync();
}

// DTOs for legacy methods
public class ApprovalResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ApproveDocumentRequest
{
    public string ApprovedBy { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

public class RejectDocumentRequest  
{
    public string RejectedBy { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
}

// DTOs for Step 8 API methods
/// <summary>
/// DTO for approval workflow records
/// </summary>
public class ApprovalDto
{
    public Guid ApprovalId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedDate { get; set; }
    public string ApproverEmail { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = "Pending";
    public DateTime? ApprovedDate { get; set; }
    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }
    public string? ApprovedBy { get; set; }
}

/// <summary>
/// Request DTO for processing approvals
/// </summary>
public class ApprovalRequest
{
    [Required]
    public bool IsApproved { get; set; }
    
    [Required]
    public string ApprovedBy { get; set; } = string.Empty;
    
    public string? Comments { get; set; }
    
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Result DTO for approval operations
/// </summary>
public class ApprovalResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Statistics DTO for approval dashboard
/// </summary>
public class ApprovalStats
{
    public int TotalApprovals { get; set; }
    public int PendingApprovals { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
}