using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enterprise.Documentation.Core.Application.Services.ApprovalTracking;

/// <summary>
/// Tracks approval actions for AI training and quality improvement
/// </summary>
public interface IApprovalTrackingService
{
    Task TrackApprovalAsync(ApprovalAction action, CancellationToken cancellationToken = default);
    Task<List<ApprovalFeedback>> GetFeedbackForTrainingAsync(int limit = 100, CancellationToken cancellationToken = default);
}

public class ApprovalAction
{
    public required string DocId { get; set; }
    public required string Action { get; set; } // "Approved", "Edited", "Rejected", "Rerequested"
    public required string ApproverUserId { get; set; }
    public required string ApproverName { get; set; }
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;

    // For Edited actions
    public string? OriginalContent { get; set; }
    public string? EditedContent { get; set; }
    public List<string>? ChangedFields { get; set; }

    // For Rejected actions
    public string? RejectionReason { get; set; }

    // For Rerequested actions
    public string? RerequestPrompt { get; set; }

    // For training
    public string? ApproverFeedback { get; set; }
    public int? QualityRating { get; set; } // 1-5 scale

    // Context for AI learning
    public string? DocumentType { get; set; }
    public string? ChangeType { get; set; }
    public bool? WasAIEnhanced { get; set; }
}

public class ApprovalFeedback
{
    public int TrackingId { get; set; }
    public required string DocId { get; set; }
    public required string Action { get; set; }
    public required string DocumentType { get; set; }
    public string? ChangeType { get; set; }
    public bool WasAIEnhanced { get; set; }
    public int? QualityRating { get; set; }
    public string? Feedback { get; set; }
    public List<string>? ChangedFields { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime ActionDate { get; set; }
}
