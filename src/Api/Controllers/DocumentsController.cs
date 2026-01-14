using MediatR;
using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Application.Commands.Documents;
using Enterprise.Documentation.Core.Application.Queries.Documents;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Shared.Contracts.DTOs;
using System.IO;

namespace Enterprise.Documentation.Api.Controllers;

/// <summary>
/// API Controller for document management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IConfiguration _configuration;

    public DocumentsController(IMediator mediator, ILogger<DocumentsController> logger, IConfiguration configuration)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Creates a new document
    /// </summary>
    /// <param name="command">Document creation command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created document</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentDto>> CreateDocument(
        [FromBody] CreateDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteCreateDocumentAsync(command, cancellationToken);
            return CreatedAtAction(nameof(GetDocument), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when creating document");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document");
            return StatusCode(500, new { error = "An error occurred while creating the document" });
        }
    }

    private async Task<DocumentDto> ExecuteCreateDocumentAsync(CreateDocumentCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new document with title: {Title}", command.Title);
        var result = await _mediator.Send(command, cancellationToken);
        _logger.LogInformation("Successfully created document with ID: {DocumentId}", result.Id);
        return result;
    }

    /// <summary>
    /// Gets a document by ID
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Document details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentDto>> GetDocument(Guid id)
    {
        try
        {
            _logger.LogInformation("Retrieving document with ID: {DocumentId}", id);
            
            var query = new GetDocumentQuery(id);
            var result = await _mediator.Send(query);

            if (result == null)
            {
                _logger.LogWarning("Document not found with ID: {DocumentId}", id);
                return NotFound(new { error = $"Document with ID {id} not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document with ID: {DocumentId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the document" });
        }
    }

    /// <summary>
    /// Searches documents with pagination
    /// </summary>
    /// <param name="searchTerm">Search term</param>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated search results</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResult<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<DocumentDto>>> SearchDocuments(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { error = "Invalid pagination parameters" });
            }

            _logger.LogInformation("Searching documents with term: {SearchTerm}, Page: {PageNumber}, Size: {PageSize}", 
                searchTerm, pageNumber, pageSize);
            
            var query = new SearchDocumentsQuery(
                SearchTerm: searchTerm,
                PageNumber: pageNumber,
                PageSize: pageSize);
            var result = await _mediator.Send(query);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return StatusCode(500, new { error = "An error occurred while searching documents" });
        }
    }

    /// <summary>
    /// Updates an existing document
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <param name="command">Update command</param>
    /// <returns>Updated document</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentDto>> UpdateDocument(
        [FromRoute] Guid id,
        [FromBody] UpdateDocumentCommand command)
    {
        if (command.DocumentId != id)
        {
            return BadRequest(new { error = "Route ID does not match command ID" });
        }

        try
        {
            var result = await ExecuteUpdateDocumentAsync(command, id);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when updating document");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document with ID: {DocumentId}", id);
            return StatusCode(500, new { error = "An error occurred while updating the document" });
        }
    }

    private async Task<DocumentDto> ExecuteUpdateDocumentAsync(UpdateDocumentCommand command, Guid id)
    {
        _logger.LogInformation("Updating document with ID: {DocumentId}", id);
        var result = await _mediator.Send(command);
        _logger.LogInformation("Successfully updated document with ID: {DocumentId}", id);
        return result;
    }

    /// <summary>
    /// Approves a document
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Approved document</returns>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DocumentDto>> ApproveDocument(
        [FromRoute] Guid id)
    {
        try
        {
            _logger.LogInformation("Approving document with ID: {DocumentId}", id);
            
            var command = new ApproveDocumentCommand(id);
            var result = await _mediator.Send(command);
            
            _logger.LogInformation("Successfully approved document with ID: {DocumentId}", id);
            
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument when approving document");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving document with ID: {DocumentId}", id);
            return StatusCode(500, new { error = "An error occurred while approving the document" });
        }
    }

    /// <summary>
    /// Downloads a draft document for preview
    /// </summary>
    /// <param name="documentId">The document ID to download</param>
    /// <returns>The draft document file or error response</returns>
    [HttpGet("download/draft/{documentId}")]
    public async Task<IActionResult> DownloadDraft(string documentId)
    {
        try
        {
            _logger.LogInformation("Downloading draft document with ID: {DocumentId}", documentId);
            
            // Configuration-based drafts path
            var draftsPath = _configuration.GetConnectionString("DraftsPath") ?? @"C:\Projects\Drafts";
            
            if (!Directory.Exists(draftsPath))
            {
                _logger.LogWarning("Drafts directory does not exist: {DraftsPath}", draftsPath);
                return NotFound(new { error = "Drafts directory not found" });
            }
            
            // Search for files with documentId in the filename
            var files = Directory.GetFiles(draftsPath, $"*{documentId}*", SearchOption.AllDirectories);
            
            if (files.Length == 0)
            {
                _logger.LogWarning("No draft file found for document ID: {DocumentId}", documentId);
                return NotFound(new { error = $"No draft file found for document ID: {documentId}" });
            }
            
            // Take the first match (or you could implement more sophisticated matching)
            var filePath = files[0];
            var fileName = Path.GetFileName(filePath);
            
            _logger.LogInformation("Found draft file: {FilePath}", filePath);
            
            // Determine content type based on file extension
            var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword", 
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".md" => "text/markdown",
                _ => "application/octet-stream"
            };
            
            // Read file and return as downloadable content
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            
            _logger.LogInformation("Successfully downloaded draft document: {FileName} ({FileSize} bytes)", fileName, fileBytes.Length);
            
            return File(fileBytes, contentType, fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when downloading draft document: {DocumentId}", documentId);
            return StatusCode(403, new { error = "Access denied to draft file" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading draft document with ID: {DocumentId}", documentId);
            return StatusCode(500, new { error = "An error occurred while downloading the draft document" });
        }
    }

    /// <summary>
    /// Gets recent documents
    /// </summary>
    /// <param name="limit">Number of documents to return</param>
    /// <returns>Recent documents</returns>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<object>>> GetRecentDocuments([FromQuery] int limit = 5)
    {
        try
        {
            _logger.LogInformation("Retrieving {Limit} recent documents", limit);
            
            // Return mock recent documents for now
            var recentDocuments = new[]
            {
                new { id = "DOC-2026-001", title = "API Documentation Update", lastModified = DateTime.UtcNow.AddHours(-1), status = "approved" },
                new { id = "DOC-2026-002", title = "Database Schema Changes", lastModified = DateTime.UtcNow.AddHours(-2), status = "pending" },
                new { id = "DOC-2026-003", title = "User Interface Guidelines", lastModified = DateTime.UtcNow.AddHours(-3), status = "draft" },
                new { id = "DOC-2026-004", title = "Security Protocol Update", lastModified = DateTime.UtcNow.AddHours(-4), status = "approved" },
                new { id = "DOC-2026-005", title = "Integration Testing Guide", lastModified = DateTime.UtcNow.AddHours(-5), status = "review" }
            }.Take(limit);

            return Ok(recentDocuments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent documents");
            return StatusCode(500, new { error = "An error occurred while retrieving recent documents" });
        }
    }
}