
using MediatR;
using AutoMapper;
using FluentValidation;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.DTOs;
using Enterprise.Documentation.Core.Application.Behaviors;
using Enterprise.Documentation.Core.Domain.Entities;

using Enterprise.Documentation.Core.Domain.Specifications;
using Enterprise.Documentation.Shared.Contracts.DTOs;

namespace Enterprise.Documentation.Core.Application.Queries.Documents;

/// <summary>
/// Query to search documents with filtering and pagination.
/// </summary>
public record SearchDocumentsQuery(
    string? SearchTerm = null,
    string? Category = null,
    List<string>? Tags = null,
    string? Status = null,
    string? SecurityLevel = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<DocumentDto>>, IAuthorizedRequest
{
    public string[] RequiredPermissions => new[] { "Documents.Read" };
    public object? Resource => null;
}

/// <summary>
/// Validator for SearchDocumentsQuery.
/// </summary>
public class SearchDocumentsQueryValidator : AbstractValidator<SearchDocumentsQuery>
{
    public SearchDocumentsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Page size cannot exceed 100");

        RuleFor(x => x.SearchTerm)
            .MaximumLength(500)
            .WithMessage("Search term cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.SearchTerm));

        RuleFor(x => x.Category)
            .MaximumLength(100)
            .WithMessage("Category cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Category));

        RuleFor(x => x.Status)
            .Must(status => status == null || new[] { "Draft", "UnderReview", "Published", "Archived" }.Contains(status))
            .WithMessage("Status must be one of: Draft, UnderReview, Published, Archived")
            .When(x => !string.IsNullOrEmpty(x.Status));

        RuleFor(x => x.SecurityLevel)
            .Must(level => level == null || new[] { "Public", "Internal", "Confidential", "Restricted" }.Contains(level))
            .WithMessage("Security level must be one of: Public, Internal, Confidential, Restricted")
            .When(x => !string.IsNullOrEmpty(x.SecurityLevel));

        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= 10)
            .WithMessage("Cannot filter by more than 10 tags")
            .Must(tags => tags == null || tags.All(tag => !string.IsNullOrWhiteSpace(tag) && tag.Length <= 50))
            .WithMessage("Each tag must be non-empty and not exceed 50 characters")
            .When(x => x.Tags != null);
    }
}

/// <summary>
/// Handler for SearchDocumentsQuery.
/// </summary>
public class SearchDocumentsQueryHandler : IRequestHandler<SearchDocumentsQuery, PagedResult<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public SearchDocumentsQueryHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<PagedResult<DocumentDto>> Handle(SearchDocumentsQuery request, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        // Build specifications based on query parameters
        var specifications = new List<Specification<Document>>();

        // Add user access specification (documents user can access based on security clearance)
        specifications.Add(new DocumentsAccessibleByUserSpecification(currentUser));

        // Add search term specification
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            specifications.Add(new DocumentsContainingTextSpecification(request.SearchTerm));
        }

        // Add category specification
        if (!string.IsNullOrEmpty(request.Category))
        {
            specifications.Add(new DocumentsInCategorySpecification(request.Category));
        }

        // Add tags specification
        if (request.Tags?.Any() == true)
        {
            specifications.Add(new DocumentsWithTagsSpecification(request.Tags));
        }

        // Add status specification
        if (!string.IsNullOrEmpty(request.Status))
        {
            try 
            {
                var status = Domain.Entities.DocumentStatus.FromString(request.Status);
                specifications.Add(new DocumentsWithStatusSpecification(status));
            }
            catch (ArgumentException)
            {
                // Invalid status value - skip the specification
            }
        }

        // Add security level specification
        if (!string.IsNullOrEmpty(request.SecurityLevel))
        {
            specifications.Add(new DocumentsWithSecurityLevelSpecification(request.SecurityLevel));
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
        var documentDtos = _mapper.Map<List<DocumentDto>>(documents);

        return new PagedResult<DocumentDto>(documentDtos, totalCount, request.PageNumber, request.PageSize);
    }
}