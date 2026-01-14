using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Enterprise.Documentation.Core.Application.Services.Approval;

/// <summary>
/// Main approval service that delegates all operations to ApprovalTrackingService.
/// This service acts as a facade/proxy to maintain interface compatibility.
/// The ApprovalTrackingService handles the complete metadata creation workflow.
/// </summary>
public class ApprovalService
{
    private readonly IApprovalTrackingService _trackingService;
    private readonly ILogger<ApprovalService> _logger;

    public ApprovalService(
        IApprovalTrackingService trackingService,
        ILogger<ApprovalService> logger)
    {
        _trackingService = trackingService ?? throw new ArgumentNullException(nameof(trackingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Delegates approval to ApprovalTrackingService which handles the complete workflow:
    /// - Document approval and status updates
    /// - MasterIndex population (89/116 fields across 14 phases) 
    /// - CustomProperties embedding in Word documents
    /// - Stored procedure documentation updates
    /// - Workflow event publishing
    /// </summary>
    public async Task<ApprovalResponse> ApproveDocumentAsync(
        int approvalId,
        ApproveDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing approval request for ApprovalId: {ApprovalId} - delegating to ApprovalTrackingService", approvalId);
        
        // Delegate to the comprehensive approval tracking service
        // which implements the complete metadata creation pipeline
        return await _trackingService.ApproveDocumentAsync(approvalId, request, cancellationToken);
    }

    /// <summary>
    /// Delegates rejection to ApprovalTrackingService.
    /// </summary>
    public async Task<ApprovalResponse> RejectDocumentAsync(
        int approvalId,
        RejectDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing rejection request for ApprovalId: {ApprovalId} - delegating to ApprovalTrackingService", approvalId);
        
        // Delegate to the comprehensive approval tracking service
        return await _trackingService.RejectDocumentAsync(approvalId, request, cancellationToken);
    }
}