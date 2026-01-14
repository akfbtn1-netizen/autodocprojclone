using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Core.Application.Services;

/// <summary>
/// Service for managing document approval workflows with extended functionality
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly ILogger<ApprovalService> _logger;
    private readonly List<ApprovalEntity> _approvals; // Mock storage

    public ApprovalService(ILogger<ApprovalService> logger)
    {
        _logger = logger;
        _approvals = new List<ApprovalEntity>();
    }

    /// <summary>
    /// Creates a new approval request
    /// </summary>
    public async Task<ApprovalEntity> CreateAsync(CreateApprovalRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = new ApprovalEntity
            {
                Id = Guid.NewGuid(),
                JiraNumber = request.JiraNumber,
                DocumentType = request.DocumentType,
                ObjectName = request.ObjectName,
                SchemaName = request.SchemaName,
                DocumentPath = request.DocumentPath,
                Status = "Pending",
                Priority = request.Priority ?? "Medium",
                SLAHours = request.SLAHours,
                CreatedDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddHours(request.SLAHours),
                RequesterEmail = request.RequesterEmail,
                MetadataId = request.MetadataId
            };

            _approvals.Add(approval);
            
            _logger.LogInformation("Created approval {ApprovalId} for {JiraNumber}", approval.Id, approval.JiraNumber);
            return approval;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating approval for {JiraNumber}", request.JiraNumber);
            throw;
        }
    }

    /// <summary>
    /// Retrieves an approval by ID
    /// </summary>
    public async Task<ApprovalEntity?> GetByIdAsync(Guid approvalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = _approvals.FirstOrDefault(a => a.Id == approvalId);
            _logger.LogDebug("Retrieved approval {ApprovalId}: {Found}", approvalId, approval != null);
            return approval;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval {ApprovalId}", approvalId);
            throw;
        }
    }

    /// <summary>
    /// Approves a document with decision details
    /// </summary>
    public async Task<ApprovalResult> ApproveAsync(Guid approvalId, ApprovalDecision decision, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await GetByIdAsync(approvalId, cancellationToken);
            if (approval == null)
            {
                return new ApprovalResult { Success = false, Message = "Approval not found" };
            }

            approval.Status = "Approved";
            
            _logger.LogInformation("Approved document {ApprovalId} by {ApprovedBy}", approvalId, decision.ApprovedBy);
            
            return new ApprovalResult 
            { 
                Success = true, 
                Message = "Document approved successfully",
                ApprovalId = approvalId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving document {ApprovalId}", approvalId);
            return new ApprovalResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Rejects a document with reason
    /// </summary>
    public async Task<ApprovalResult> RejectAsync(Guid approvalId, RejectionDecision decision, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await GetByIdAsync(approvalId, cancellationToken);
            if (approval == null)
            {
                return new ApprovalResult { Success = false, Message = "Approval not found" };
            }

            approval.Status = "Rejected";
            
            _logger.LogInformation("Rejected document {ApprovalId} by {RejectedBy}: {Reason}", 
                approvalId, decision.RejectedBy, decision.Reason);
            
            return new ApprovalResult 
            { 
                Success = true, 
                Message = "Document rejected",
                ApprovalId = approvalId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting document {ApprovalId}", approvalId);
            return new ApprovalResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Gets paginated list of approvals with optional status filter
    /// </summary>
    public async Task<PagedResult<ApprovalSummary>> GetApprovalsAsync(int page, int pageSize, string? status, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _approvals.AsQueryable();
            
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            var total = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new ApprovalSummary
                {
                    Id = a.Id,
                    JiraNumber = a.JiraNumber ?? string.Empty,
                    DocumentType = a.DocumentType,
                    ObjectName = a.ObjectName,
                    Status = a.Status,
                    Priority = a.Priority,
                    CreatedDate = a.CreatedDate,
                    DueDate = a.DueDate ?? DateTime.UtcNow.AddDays(3)
                })
                .ToList();

            _logger.LogDebug("Retrieved {Count} approvals (page {Page}/{PageSize})", items.Count, page, pageSize);
            
            return new PagedResult<ApprovalSummary>
            {
                Items = items,
                TotalCount = total,
                PageNumber = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approvals");
            throw;
        }
    }

    /// <summary>
    /// Edits an approval with new information
    /// </summary>
    public async Task<EditResult> EditAsync(Guid approvalId, EditDecision decision, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await GetByIdAsync(approvalId, cancellationToken);
            if (approval == null)
            {
                return new EditResult { Success = false, Message = "Approval not found" };
            }

            // Apply edits
            if (!string.IsNullOrEmpty(decision.NewObjectName))
                approval.ObjectName = decision.NewObjectName;
            
            if (!string.IsNullOrEmpty(decision.NewDocumentType))
                approval.DocumentType = decision.NewDocumentType;
            
            if (!string.IsNullOrEmpty(decision.NewPriority))
                approval.Priority = decision.NewPriority;

            _logger.LogInformation("Edited approval {ApprovalId} by {EditedBy}", approvalId, decision.EditedBy);
            
            return new EditResult
            {
                Success = true,
                Message = "Approval updated successfully",
                ApprovalId = approvalId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing approval {ApprovalId}", approvalId);
            return new EditResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Gets the original Excel entry that created this approval
    /// </summary>
    public async Task<ExcelChangeEntry?> GetOriginalEntryAsync(Guid approvalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await GetByIdAsync(approvalId, cancellationToken);
            if (approval == null) return null;

            // Mock creation of Excel entry from approval data
            var entry = new ExcelChangeEntry
            {
                JiraNumber = approval.JiraNumber ?? string.Empty,
                DocumentType = approval.DocumentType ?? string.Empty,
                ObjectName = approval.ObjectName ?? string.Empty,
                SchemaName = approval.SchemaName ?? string.Empty,
                Description = $"Original entry for {approval.ObjectName}",
                RequesterEmail = approval.RequesterEmail ?? string.Empty,
                Status = "Processed"
            };

            _logger.LogDebug("Retrieved original entry for approval {ApprovalId}", approvalId);
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving original entry for approval {ApprovalId}", approvalId);
            return null;
        }
    }

    /// <summary>
    /// Updates the generated document for an approval
    /// </summary>
    public async Task<ApprovalResult> UpdateDocumentAsync(Guid approvalId, UpdateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await GetByIdAsync(approvalId, cancellationToken);
            if (approval == null)
            {
                return new ApprovalResult { Success = false, Message = "Approval not found" };
            }

            approval.DocumentPath = request.NewDocumentPath;
            
            _logger.LogInformation("Updated document for approval {ApprovalId} to {DocumentPath}", 
                approvalId, request.NewDocumentPath);
            
            return new ApprovalResult
            {
                Success = true,
                Message = "Document updated successfully",
                ApprovalId = approvalId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document for approval {ApprovalId}", approvalId);
            return new ApprovalResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Adds a suggestion to an approval
    /// </summary>
    public async Task<SuggestionResult> AddSuggestionAsync(Guid approvalId, Suggestion suggestion, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await GetByIdAsync(approvalId, cancellationToken);
            if (approval == null)
            {
                return new SuggestionResult { Success = false, Message = "Approval not found" };
            }

            _logger.LogInformation("Added suggestion to approval {ApprovalId} by {SuggestedBy}: {Text}", 
                approvalId, suggestion.SuggestedBy, suggestion.Text);
            
            return new SuggestionResult
            {
                Success = true,
                Message = "Suggestion added successfully",
                SuggestionId = Guid.NewGuid()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding suggestion to approval {ApprovalId}", approvalId);
            return new SuggestionResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Gets detailed information about an approval
    /// </summary>
    public async Task<ApprovalDetails?> GetDetailsAsync(Guid approvalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var approval = await GetByIdAsync(approvalId, cancellationToken);
            if (approval == null) return null;

            var details = new ApprovalDetails
            {
                Id = approval.Id,
                JiraNumber = approval.JiraNumber ?? string.Empty,
                DocumentType = approval.DocumentType ?? string.Empty,
                ObjectName = approval.ObjectName ?? string.Empty,
                SchemaName = approval.SchemaName ?? string.Empty,
                DocumentPath = approval.DocumentPath ?? string.Empty,
                Status = approval.Status,
                Priority = approval.Priority,
                SLAHours = approval.SLAHours,
                CreatedDate = approval.CreatedDate,
                DueDate = approval.DueDate ?? DateTime.UtcNow.AddDays(3),
                RequesterEmail = approval.RequesterEmail ?? string.Empty,
                MetadataId = approval.MetadataId,
                History = new List<string> { $"Created on {approval.CreatedDate:yyyy-MM-dd HH:mm}" },
                Comments = string.Empty,
                Suggestions = new List<Suggestion>()
            };

            _logger.LogDebug("Retrieved details for approval {ApprovalId}", approvalId);
            return details;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving details for approval {ApprovalId}", approvalId);
            return null;
        }
    }

    /// <summary>
    /// Gets approval statistics and metrics
    /// </summary>
    public async Task<ApprovalStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = new ApprovalStats
            {
                TotalApprovals = _approvals.Count,
                PendingApprovals = _approvals.Count(a => a.Status == "Pending"),
                ApprovedCount = _approvals.Count(a => a.Status == "Approved"),
                RejectedCount = _approvals.Count(a => a.Status == "Rejected"),
                OverdueCount = _approvals.Count(a => a.DueDate.HasValue && a.DueDate < DateTime.UtcNow && a.Status == "Pending"),
                AverageProcessingTime = TimeSpan.FromHours(24).TotalHours, // Mock average
                GeneratedAt = DateTime.UtcNow
            };

            _logger.LogDebug("Generated approval stats: {Total} total, {Pending} pending", 
                stats.TotalApprovals, stats.PendingApprovals);
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approval stats");
            throw;
        }
    }

    // Interface wrapper methods to match IApprovalService
    public async Task<ApprovalEntity?> GetByIdAsync(int approvalId)
    {
        return _approvals.FirstOrDefault(a => a.Id.ToString() == approvalId.ToString());
    }

    public async Task<List<ApprovalEntity>> GetAllPendingAsync()
    {
        return _approvals.Where(a => a.Status == "Pending").ToList();
    }

    public async Task<ApprovalEntity> CreateAsync(ApprovalEntity approval)
    {
        _approvals.Add(approval);
        return approval;
    }

    /// <summary>
    /// Creates a new approval request and returns the ID
    /// </summary>
    public async Task<Guid> CreateApprovalRequestAsync(CreateApprovalRequest request)
    {
        var approval = await CreateAsync(request);
        return approval.Id;
    }

    public async Task<bool> CancelAsync(int approvalId)
    {
        var approval = _approvals.FirstOrDefault(a => a.Id.ToString() == approvalId.ToString());
        if (approval != null)
        {
            approval.Status = "Cancelled";
            return true;
        }
        return false;
    }

    public async Task<List<ApprovalEntity>> GetByStatusAsync(string status)
    {
        return _approvals.Where(a => a.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<ApprovalEntity?> GetByDocumentAsync(string jiraNumber, string documentType, string objectName, string schemaName)
    {
        return _approvals.FirstOrDefault(a => 
            a.JiraNumber == jiraNumber && 
            a.DocumentType == documentType && 
            a.ObjectName == objectName && 
            a.SchemaName == schemaName);
    }
}