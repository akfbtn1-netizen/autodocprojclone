
using MediatR;
using AutoMapper;
using FluentValidation;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.Behaviors;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Exceptions;
using Enterprise.Documentation.Shared.Contracts.DTOs;

namespace Enterprise.Documentation.Core.Application.Commands.Documents;

/// <summary>
/// Command to update an existing document.
/// </summary>
public record UpdateDocumentCommand(
    Guid DocumentId,
    string? Title = null,
    string? Category = null,
    string? Description = null,
    List<string>? Tags = null,
    Enterprise.Documentation.Core.Domain.ValueObjects.SecurityClassification? SecurityClassification = null,
    string? ContentType = null) : IRequest<DocumentDto>, IAuthorizedRequest
{
    public string[] RequiredPermissions => new[] { "Documents.Update" };
    public object? Resource => DocumentId;
}

/// <summary>
/// Validator for UpdateDocumentCommand.
/// </summary>
public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required");

        RuleFor(x => x.Title)
            .MaximumLength(255)
            .WithMessage("Title cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Category)
            .MaximumLength(100)
            .WithMessage("Category cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Category));

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description cannot exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= 20)
            .WithMessage("Cannot have more than 20 tags")
            .Must(tags => tags == null || tags.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 50))
            .WithMessage("Each tag must be non-empty and not exceed 50 characters")
            .When(x => x.Tags != null);

        RuleFor(x => x.ContentType)
            .Must(contentType => contentType == null || new[] { "markdown", "html", "text", "json" }.Contains(contentType))
            .WithMessage("Content type must be one of: markdown, html, text, json")
            .When(x => !string.IsNullOrEmpty(x.ContentType));
    }
}

/// <summary>
/// Handler for UpdateDocumentCommand.
/// </summary>
public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, DocumentDto>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdateDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<DocumentDto> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        // Get document
        var document = await _documentRepository.GetByIdAsync(
            new DocumentId(request.DocumentId), cancellationToken)
            ?? throw new EntityNotFoundException($"Document with ID {request.DocumentId} not found");

        // Check user can modify this document
        var primaryRole = currentUser.Roles.Count > 0 ? currentUser.Roles[0] : UserRole.Reader;
        if (!document.CanUserModifyDocument(currentUser.Id, primaryRole))
            throw new ForbiddenAccessException($"User does not have permission to modify document {document.Title}");

        // Check if document is in a state that can be modified
        if (document.Status == Domain.Entities.DocumentStatus.Archived)
            throw new InvalidDocumentStatusException("Cannot modify archived documents");

        // Update fields if provided
        if (!string.IsNullOrEmpty(request.Title))
            document.UpdateTitle(request.Title, currentUser.Id);

        if (!string.IsNullOrEmpty(request.Category))
            document.UpdateCategory(request.Category, currentUser.Id);

        if (request.Description != null)
            document.UpdateDescription(request.Description, currentUser.Id);

        if (request.Tags != null)
            document.UpdateTags(request.Tags, currentUser.Id);

        if (request.SecurityClassification != null)
        {
            // Validate user can set this security level
            if (!currentUser.CanAccessSecurityLevel(request.SecurityClassification))
                throw new ForbiddenAccessException("Insufficient security clearance to set this classification level");

            document.UpdateSecurityClassification(request.SecurityClassification, currentUser.Id);
        }

        if (!string.IsNullOrEmpty(request.ContentType))
            document.UpdateContentType(request.ContentType, currentUser.Id);

        // Save changes
        await _documentRepository.UpdateAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return updated document
        return _mapper.Map<DocumentDto>(document);
    }
}