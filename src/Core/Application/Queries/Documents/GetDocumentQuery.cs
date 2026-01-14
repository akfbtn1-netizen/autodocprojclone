
using MediatR;
using AutoMapper;
using FluentValidation;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.Behaviors;

using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Exceptions;
using Enterprise.Documentation.Shared.Contracts.DTOs;

namespace Enterprise.Documentation.Core.Application.Queries.Documents;

/// <summary>
/// Query to get a document by ID.
/// </summary>
public record GetDocumentQuery(Guid DocumentId) : IRequest<DocumentDto>, IAuthorizedRequest
{
    public string[] RequiredPermissions => new[] { "Documents.Read" };
    public object? Resource => DocumentId;
}

/// <summary>
/// Validator for GetDocumentQuery.
/// </summary>
public class GetDocumentQueryValidator : AbstractValidator<GetDocumentQuery>
{
    public GetDocumentQueryValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required");
    }
}

/// <summary>
/// Handler for GetDocumentQuery.
/// </summary>
public class GetDocumentQueryHandler : IRequestHandler<GetDocumentQuery, DocumentDto>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IMapper _mapper;

    public GetDocumentQueryHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        IMapper mapper)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    public async Task<DocumentDto> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        // Get document
        var document = await _documentRepository.GetByIdAsync(
            new DocumentId(request.DocumentId), cancellationToken)
            ?? throw new EntityNotFoundException($"Document with ID {request.DocumentId} not found");

        // Check user can access this document
        var canAccess = await _authorizationService.CanAccessDocumentAsync(
            currentUser, document, cancellationToken);
        
        if (!canAccess)
            throw new ForbiddenAccessException($"User does not have permission to access document {document.Title}");

        // Return DTO
        return _mapper.Map<DocumentDto>(document);
    }
}