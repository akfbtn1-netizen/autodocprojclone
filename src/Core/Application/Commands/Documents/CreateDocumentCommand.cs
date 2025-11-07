
using MediatR;
using AutoMapper;
using FluentValidation;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.Behaviors;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Shared.Contracts.DTOs;

namespace Enterprise.Documentation.Core.Application.Commands.Documents;

/// <summary>
/// Command to create a new document.
/// </summary>
public record CreateDocumentCommand(
    string Title,
    string Category,
    string? Description = null,
    List<string>? Tags = null,
    Guid? TemplateId = null,
    string ContentType = "markdown") : IRequest<DocumentDto>, IAuthorizedRequest
{
    public string[] RequiredPermissions => new[] { "Documents.Create" };
    public object? Resource => null;
}

/// <summary>
/// Validator for CreateDocumentCommand.
/// </summary>
public class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MaximumLength(255)
            .WithMessage("Title cannot exceed 255 characters");

        RuleFor(x => x.Category)
            .NotEmpty()
            .WithMessage("Category is required")
            .MaximumLength(100)
            .WithMessage("Category cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= 20)
            .WithMessage("Cannot have more than 20 tags")
            .Must(tags => tags == null || tags.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 50))
            .WithMessage("Each tag must be non-empty and not exceed 50 characters");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .WithMessage("Content type is required")
            .Must(contentType => new[] { "markdown", "html", "text", "json" }.Contains(contentType))
            .WithMessage("Content type must be one of: markdown, html, text, json");
    }
}

/// <summary>
/// Handler for CreateDocumentCommand.
/// </summary>
public class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, DocumentDto>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ITemplateRepository _templateRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ITemplateRepository templateRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _documentRepository = documentRepository;
        _templateRepository = templateRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<DocumentDto> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        // Validate template if provided
        Template? template = null;
        if (request.TemplateId.HasValue)
        {
            template = await _templateRepository.GetByIdAsync(
                new TemplateId(request.TemplateId.Value), cancellationToken);
            
            if (template == null)
                throw new ArgumentException($"Template with ID {request.TemplateId} not found");

            if (!template.IsActive)
                throw new InvalidOperationException($"Template {template.Name} is not active");

            // Check user can access template's security level
            if (!currentUser.CanAccessSecurityLevel(template.DefaultSecurityClassification))
                throw new ForbiddenAccessException("Insufficient security clearance for this template");
        }

        // Determine security classification
        var securityClassification = template?.DefaultSecurityClassification 
            ?? Domain.ValueObjects.SecurityClassification.Internal(currentUser.Id);

        // Create document
        var document = new Document(
            DocumentId.New<DocumentId>(),
            request.Title,
            request.Category,
            securityClassification,
            currentUser.Id,
            request.Description,
            request.Tags ?? new List<string>(),
            template?.Id,
            request.ContentType);

        // Save document
        await _documentRepository.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return DTO
        return _mapper.Map<DocumentDto>(document);
    }
}