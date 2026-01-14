using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Enterprise.Documentation.Api.Pages.Approval
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IConfiguration configuration, ILogger<IndexModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public List<PendingApprovalDocument> PendingDocuments { get; set; } = new();
        
        [BindProperty]
        public Guid Id { get; set; }
        
        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading pending approval documents from ApprovalWorkflow");
                
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                
                var sql = @"
                    SELECT 
                        aw.ApprovalId as Id,
                        aw.DocIdString as Title,
                        aw.DocumentType,
                        aw.RequestedDate as CreatedDate,
                        aw.RequestedBy as CreatedBy,
                        aw.ApprovalStatus as Status,
                        dq.DocumentUrl as FilePath
                    FROM DaQa.ApprovalWorkflow aw
                    LEFT JOIN DaQa.DocumentationQueue dq ON aw.DocIdString = dq.DocIdString
                    WHERE aw.ApprovalStatus = 'Pending'
                    ORDER BY aw.RequestedDate DESC";
                
                var results = await connection.QueryAsync<PendingApprovalDocument>(sql);
                PendingDocuments = results.ToList();
                
                _logger.LogInformation("Found {Count} documents awaiting approval", PendingDocuments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pending approval documents");
                PendingDocuments = new List<PendingApprovalDocument>();
            }
        }

        public async Task<IActionResult> OnGetDownloadAsync(Guid id)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                
                var result = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT dq.DocumentUrl as FilePath, aw.DocIdString
                    FROM DaQa.ApprovalWorkflow aw
                    LEFT JOIN DaQa.DocumentationQueue dq ON aw.DocIdString = dq.DocIdString
                    WHERE aw.ApprovalId = @ApprovalId",
                    new { ApprovalId = id });
                
                if (result == null)
                {
                    return NotFound("Document not found");
                }
                
                string? filePath = result.FilePath as string;
                
                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("No file path found for approval {ApprovalId}", id);
                    return NotFound("Document file path not found");
                }
                
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.LogWarning("File not found at path: {FilePath}", filePath);
                    return NotFound($"File not found: {filePath}");
                }
                
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var fileName = System.IO.Path.GetFileName(filePath);
                
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {ApprovalId}", id);
                return StatusCode(500, "Error downloading document");
            }
        }

public async Task<IActionResult> OnPostAsync()  // Remove "Approve"
        {
            _logger.LogWarning("🔥 APPROVE METHOD CALLED! Id = {Id}", Id);
            
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                
                // Get the approval details
                var approval = await connection.QueryFirstOrDefaultAsync<ApprovalDetails>(@"
                    SELECT 
                        ApprovalId,
                        DocIdString,
                        DocumentType,
                        RequestedBy
                    FROM DaQa.ApprovalWorkflow
                    WHERE ApprovalId = @ApprovalId",
                    new { ApprovalId = Id });
                
                _logger.LogWarning("🔍 Found approval: {Approval}", approval?.DocIdString ?? "NULL");
                
                if (approval == null)
                {
                    _logger.LogError("❌ Approval not found for Id: {Id}", Id);
                    TempData["Error"] = "Approval not found";
                    return RedirectToPage();
                }
                
                _logger.LogWarning("🔥 About to update ApprovalId: {Id}", Id);
                
                // Update approval status
                var rowsAffected = await connection.ExecuteAsync(@"
                    UPDATE DaQa.ApprovalWorkflow
                    SET 
                        ApprovalStatus = 'Approved',
                        ApprovedDate = GETUTCDATE(),
                        ApprovedBy = @ApprovedBy
                    WHERE ApprovalId = @ApprovalId",
                    new 
                    { 
                        ApprovalId = Id,
                        ApprovedBy = User.Identity?.Name ?? "System"
                    });
                
                _logger.LogWarning("🔥 SQL UPDATE: {RowsAffected} rows affected for ApprovalId {Id}", rowsAffected, Id);
                
                if (rowsAffected == 0)
                {
                    _logger.LogError("❌ NO ROWS UPDATED! ApprovalId {Id} not found in database", Id);
                    TempData["Error"] = "Failed to update approval status";
                    return RedirectToPage();
                }
                
                _logger.LogInformation("✅ Document {DocId} approved successfully", approval.DocIdString);
                TempData["Message"] = $"Document {approval.DocIdString} approved successfully";
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error approving document {ApprovalId}", Id);
                TempData["Error"] = "Failed to approve document";
                return RedirectToPage();
            }
        }
        
        public async Task<IActionResult> OnPostRejectAsync(Guid id, string rejectionReason)
        {
            try
            {
                _logger.LogInformation("Rejecting document {ApprovalId}", id);
                
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                
                await connection.ExecuteAsync(@"
                    UPDATE DaQa.ApprovalWorkflow
                    SET 
                        ApprovalStatus = 'Rejected',
                        ApprovedDate = GETUTCDATE(),
                        ApprovedBy = @ApprovedBy,
                        RejectionReason = @RejectionReason
                    WHERE ApprovalId = @ApprovalId",
                    new 
                    { 
                        ApprovalId = id,
                        ApprovedBy = User.Identity?.Name ?? "System",
                        RejectionReason = rejectionReason ?? "No reason provided"
                    });
                
                TempData["Message"] = "Document rejected";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting document {ApprovalId}", id);
                TempData["Error"] = "Failed to reject document";
                return RedirectToPage();
            }
        }
    }

    public class PendingApprovalDocument
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? FilePath { get; set; }
    }
    
    public class ApprovalDetails
    {
        public Guid ApprovalId { get; set; }
        public string DocIdString { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
    }
}
