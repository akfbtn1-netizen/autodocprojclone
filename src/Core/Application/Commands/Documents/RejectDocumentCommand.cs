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
/// Command to reject a document.
/// </summary>
public record RejectDocumentCommand(
    Guid DocumentId,
    string RejectionReason) : IRequest<DocumentDto>, IAuthorizedRequest
{
    public string[] RequiredPermissions => new[] { "Documents.Approve" };
    public object? Resource => DocumentId;
}

/// <summary>
/// Validator for RejectDocumentCommand.
/// </summary>
public class RejectDocumentCommandValidator : AbstractValidator<RejectDocumentCommand>
{
    public RejectDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required");

        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .WithMessage("Rejection reason is required")
            .MaximumLength(2000)
            .WithMessage("Rejection reason cannot exceed 2000 characters");
    }
}

/// <summary>
/// Handler for RejectDocumentCommand.
/// </summary>
public class RejectDocumentCommandHandler : IRequestHandler<RejectDocumentCommand, DocumentDto>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public RejectDocumentCommandHandler(
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

    public async Task<DocumentDto> Handle(RejectDocumentCommand request, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        // Get document
        var document = await _documentRepository.GetByIdAsync(
            new DocumentId(request.DocumentId), cancellationToken)
            ?? throw new EntityNotFoundException($"Document with ID {request.DocumentId} not found");

        // Reject document
        var rejectedStatus = Core.Domain.ValueObjects.ApprovalStatus.Rejected(currentUser.Id, request.RejectionReason);
        document.UpdateApprovalStatus(rejectedStatus, currentUser.Id);

        // Save changes
        await _documentRepository.UpdateAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return updated document
        return _mapper.Map<DocumentDto>(document);
    }
}