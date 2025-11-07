using MediatR;
using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Application.Commands.Documents;
using Enterprise.Documentation.Core.Application.Queries.Documents;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Shared.Contracts.DTOs;

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

    public DocumentsController(IMediator mediator, ILogger<DocumentsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
}