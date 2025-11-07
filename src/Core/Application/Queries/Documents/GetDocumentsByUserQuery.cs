
using MediatR;
using AutoMapper;
using FluentValidation;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.Behaviors;
using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Domain.Specifications;
using Enterprise.Documentation.Core.Domain.Exceptions;
using Enterprise.Documentation.Shared.Contracts.DTOs;

namespace Enterprise.Documentation.Core.Application.Queries.Documents;

/// <summary>
/// Query to get documents created by or assigned to a specific user.
/// </summary>
public record GetDocumentsByUserQuery(
    Guid? UserId = null, // If null, gets documents for current user
    string FilterType = "Created", // "Created", "Assigned", "All"
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<DocumentDto>>, IAuthorizedRequest
{
    public string[] RequiredPermissions => new[] { "Documents.Read" };
    public object? Resource => UserId;
}

/// <summary>
/// Validator for GetDocumentsByUserQuery.
/// </summary>
public class GetDocumentsByUserQueryValidator : AbstractValidator<GetDocumentsByUserQuery>
{
    public GetDocumentsByUserQueryValidator()
    {
        RuleFor(x => x.FilterType)
            .NotEmpty()
            .WithMessage("Filter type is required")
            .Must(type => new[] { "Created", "Assigned", "All" }.Contains(type))
            .WithMessage("Filter type must be one of: Created, Assigned, All");

        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Page size cannot exceed 100");
    }
}

/// <summary>
/// Handler for GetDocumentsByUserQuery.
/// </summary>
public class GetDocumentsByUserQueryHandler : IRequestHandler<GetDocumentsByUserQuery, PagedResult<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IMapper _mapper;

    public GetDocumentsByUserQueryHandler(
        IDocumentRepository documentRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        IMapper mapper)
    {
        _documentRepository = documentRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _mapper = mapper;
    }

    public async Task<PagedResult<DocumentDto>> Handle(GetDocumentsByUserQuery request, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        // Determine target user
        User targetUser;
        if (request.UserId.HasValue)
        {
            // Check if current user can access other user's documents
            if (!currentUser.Id.Equals(new UserId(request.UserId.Value)) &&
                !currentUser.HasAnyRole(UserRole.Manager, UserRole.Administrator))
            {
                throw new ForbiddenAccessException("User does not have permission to access other users' documents");
            }

            targetUser = await _userRepository.GetByIdAsync(
                new UserId(request.UserId.Value), cancellationToken)
                ?? throw new EntityNotFoundException($"User with ID {request.UserId} not found");
        }
        else
        {
            targetUser = currentUser;
        }

        // Build specifications based on filter type
        var specifications = new List<Specification<Document>>();

        // Always include user access specification
        specifications.Add(new DocumentsAccessibleByUserSpecification(currentUser));

        // Add user-specific specifications
        switch (request.FilterType)
        {
            case "Created":
                specifications.Add(new DocumentsCreatedByUserSpecification(targetUser.Id));
                break;
            case "Assigned":
                // For assigned documents, we look for documents in approval workflow for this user
                specifications.Add(new DocumentsAssignedToUserSpecification(targetUser.Id));
                break;
            case "All":
                var createdSpec = new DocumentsCreatedByUserSpecification(targetUser.Id);
                var assignedSpec = new DocumentsAssignedToUserSpecification(targetUser.Id);
                specifications.Add(createdSpec.Or(assignedSpec));
                break;
        }

        // Combine all specifications
        var combinedSpec = specifications.Aggregate((spec1, spec2) => spec1.And(spec2));

        // Execute query with pagination
        var documents = await _documentRepository.FindAsync(
            combinedSpec, 
            request.PageNumber, 
            request.PageSize, 
            cancellationToken);

        var totalCount = await _documentRepository.CountAsync(combinedSpec, cancellationToken);

        // Map to DTOs
        var documentDtos = _mapper.Map<IReadOnlyList<DocumentDto>>(documents);

        return new PagedResult<DocumentDto>(documentDtos, totalCount, request.PageNumber, request.PageSize);
    }
}