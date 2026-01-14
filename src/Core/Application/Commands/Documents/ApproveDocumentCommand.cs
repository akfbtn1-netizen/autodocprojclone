
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
/// Command to approve a document for publication.
/// </summary>
public record ApproveDocumentCommand(
    Guid DocumentId,
    string? ApprovalComments = null) : IRequest<DocumentDto>, IAuthorizedRequest
{
    public string[] RequiredPermissions => new[] { "Documents.Approve" };
    public object? Resource => DocumentId;
}

/// <summary>
/// Validator for ApproveDocumentCommand.
/// </summary>
public class ApproveDocumentCommandValidator : AbstractValidator<ApproveDocumentCommand>
{
    public ApproveDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required");

        RuleFor(x => x.ApprovalComments)
            .MaximumLength(2000)
            .WithMessage("Approval comments cannot exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.ApprovalComments));
    }
}

/// <summary>
/// Handler for ApproveDocumentCommand.
/// </summary>
public class ApproveDocumentCommandHandler : IRequestHandler<ApproveDocumentCommand, DocumentDto>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IVersionRepository _versionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ApproveDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IVersionRepository versionRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _documentRepository = documentRepository;
        _versionRepository = versionRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<DocumentDto> Handle(ApproveDocumentCommand request, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("User must be authenticated");

        // Get document
        var document = await _documentRepository.GetByIdAsync(
            new DocumentId(request.DocumentId), cancellationToken)
            ?? throw new EntityNotFoundException($"Document with ID {request.DocumentId} not found");

        // Check user can approve this document
        var primaryRole = currentUser.Roles.Count > 0 ? currentUser.Roles[0] : UserRole.Reader;
        if (!document.CanUserApproveDocument(currentUser.Id, primaryRole))
            throw new ForbiddenAccessException($"User does not have permission to approve document {document.Title}");

        // Check document status allows approval
        if (document.Status != Domain.Entities.DocumentStatus.UnderReview)
            throw new InvalidDocumentStatusException($"Document must be under review to be approved. Current status: {document.Status}");

        // Check user has sufficient security clearance
        if (!currentUser.CanAccessSecurityLevel(document.SecurityClassification))
            throw new ForbiddenAccessException("Insufficient security clearance to approve this document");

        // Get the current version being reviewed
        var currentVersion = await _versionRepository.GetCurrentVersionAsync(document.Id, cancellationToken)
            ?? throw new InvalidOperationException("No current version found for document under review");

        // Validate version is ready for approval
        if (currentVersion.Status != VersionStatus.UnderReview)
            throw new InvalidVersionStatusException($"Version must be under review to be approved. Current status: {currentVersion.Status}");

        // Check for required approvals based on security classification
        if (document.SecurityClassification.Level == "Confidential" || 
            document.SecurityClassification.Level == "Restricted")
        {
            var existingApprovals = await _versionRepository.GetApprovalsAsync(currentVersion.Id, cancellationToken);
            var requiredApprovalCount = document.SecurityClassification.Level == "Restricted" ? 2 : 1;
            
            if (existingApprovals.Count < requiredApprovalCount)
                throw new InsufficientApprovalsException(
                    $"Document requires {requiredApprovalCount} approvals but only has {existingApprovals.Count}");
        }

        // Approve the document
        document.Approve(currentUser.Id, request.ApprovalComments);

        // Approve the current version
        currentVersion.Approve(currentUser.Id, request.ApprovalComments);

        // Update repositories
        await _documentRepository.UpdateAsync(document, cancellationToken);
        await _versionRepository.UpdateAsync(currentVersion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return updated document
        return _mapper.Map<DocumentDto>(document);
    }
}