using MediatR;
using AutoMapper;
using FluentValidation;
using Enterprise.Documentation.Core.Application.Interfaces;
using Enterprise.Documentation.Core.Application.DTOs;
using Enterprise.Documentation.Shared.Contracts.DTOs;
using Enterprise.Documentation.Core.Domain.ValueObjects;
using Enterprise.Documentation.Core.Application.Specifications;

namespace Enterprise.Documentation.Core.Application.Queries.Documents;

/// <summary>
/// Query to get documents awaiting approval.
/// </summary>
public record GetPendingApprovalsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<DocumentDto>>
{
    public static GetPendingApprovalsQuery Default => new();
}

/// <summary>
/// Validator for GetPendingApprovalsQuery.
/// </summary>
public class GetPendingApprovalsQueryValidator : AbstractValidator<GetPendingApprovalsQuery>
{
    public GetPendingApprovalsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must be between 1 and 100");
    }
}

/// <summary>
/// Handler for GetPendingApprovalsQuery.
/// </summary>
public class GetPendingApprovalsQueryHandler : IRequestHandler<GetPendingApprovalsQuery, PagedResult<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IMapper _mapper;

    public GetPendingApprovalsQueryHandler(
        IDocumentRepository documentRepository,
        IMapper mapper)
    {
        _documentRepository = documentRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<DocumentDto>> Handle(GetPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        // Get paginated results with pending approval filter
        var pagedResult = await _documentRepository.GetPagedAsync(
            request.PageNumber, 
            request.PageSize, 
            "Pending", 
            cancellationToken);

        var documentDtos = _mapper.Map<List<DocumentDto>>(pagedResult.Items);

        return new PagedResult<DocumentDto>(
            documentDtos, 
            pagedResult.TotalCount, 
            pagedResult.PageNumber, 
            pagedResult.PageSize);
    }
}