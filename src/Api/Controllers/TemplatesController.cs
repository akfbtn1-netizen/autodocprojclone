using Microsoft.AspNetCore.Mvc;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Application.Interfaces;

namespace Enterprise.Documentation.Api.Controllers;

public class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class UpdateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateRepository _templateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(ITemplateRepository templateRepository, IUnitOfWork unitOfWork, ILogger<TemplatesController> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a template by ID
    /// </summary>
    /// <param name="id">The template ID</param>
    /// <returns>The template details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTemplate(Guid id)
    {
        try
        {
            _logger.LogInformation("Retrieving template: {TemplateId}", id);
            
            var templateId = new TemplateId(id);
            var template = await _templateRepository.GetByIdAsync(templateId);
            
            if (template == null)
            {
                _logger.LogWarning("Template not found: {TemplateId}", id);
                return NotFound($"Template with ID {id} not found.");
            }
            
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving template {TemplateId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
        }
    }
}