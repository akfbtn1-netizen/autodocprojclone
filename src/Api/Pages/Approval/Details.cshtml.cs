using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Enterprise.Documentation.Core.Application.Queries.Documents;
using Enterprise.Documentation.Core.Application.Commands.Documents;
using MediatR;

namespace Enterprise.Documentation.Api.Pages.Approval;

public class DetailsModel : PageModel
{
    private readonly IMediator _mediator;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(IMediator mediator, ILogger<DetailsModel> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public ApprovalDocumentDetails? Document { get; set; }

    [BindProperty]
    public string ApprovalComments { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        try
        {
            _logger.LogInformation("Loading document {DocumentId} for approval review", id);
            
            var query = new GetDocumentQuery(id.Value);
            var document = await _mediator.Send(query);
            
            if (document == null)
            {
                return NotFound();
            }

            // Create a simple document details object
            Document = new ApprovalDocumentDetails
            {
                Id = id.Value,
                Title = document.Title,
                Content = document.Content ?? "No content available",
                DocumentType = DetermineDocumentType(document.Title),
                CreatedDate = DateTime.Now, // Simplified for now
                CreatedBy = "System", // Simplified for now
                Status = "Pending Approval",
                Version = "1.0"
            };

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading document {DocumentId} for approval", id);
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Approving document {DocumentId} with comments: {Comments}", id, ApprovalComments);
            
            var approveCommand = new ApproveDocumentCommand(id);
            await _mediator.Send(approveCommand);
            
            TempData["Message"] = "Document approved successfully";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving document {DocumentId}", id);
            TempData["Error"] = "Failed to approve document";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Rejecting document {DocumentId} with comments: {Comments}", id, ApprovalComments);
            
            // Create a reject command - this might need to be implemented
            var rejectCommand = new RejectDocumentCommand(id, ApprovalComments ?? "No reason provided");
            await _mediator.Send(rejectCommand);
            
            TempData["Message"] = "Document rejected successfully";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting document {DocumentId}", id);
            TempData["Error"] = "Failed to reject document";
            return RedirectToPage();
        }
    }

    private string DetermineDocumentType(string title)
    {
        if (title.Contains("Enhancement")) return "Enhancement Request";
        if (title.Contains("Business Request")) return "Business Request"; 
        if (title.Contains("Defect")) return "Defect Fix";
        return "Document";
    }
}

public class ApprovalDocumentDetails
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}