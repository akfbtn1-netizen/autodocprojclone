using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Services;

/// <summary>
/// Domain service for document validation operations
/// Centralizes validation logic and error handling
/// </summary>
public static class DocumentValidationService
{
    /// <summary>
    /// Validates parameters for document creation
    /// </summary>
    /// <param name="title">Document title</param>
    /// <param name="category">Document category</param>
    /// <param name="securityClassification">Security classification</param>
    /// <param name="createdBy">User creating the document</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    public static void ValidateDocumentCreation(
        string title,
        string category,
        SecurityClassification securityClassification,
        UserId createdBy)
    {
        ValidateTitle(title);
        ValidateCategory(category);
        ValidateNotNull(securityClassification, nameof(securityClassification));
        ValidateNotNull(createdBy, nameof(createdBy));
    }

    /// <summary>
    /// Validates document title
    /// </summary>
    /// <param name="title">Title to validate</param>
    /// <exception cref="ArgumentException">Thrown when title is invalid</exception>
    public static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));
            
        if (title.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));
    }

    /// <summary>
    /// Validates document category
    /// </summary>
    /// <param name="category">Category to validate</param>
    /// <exception cref="ArgumentException">Thrown when category is invalid</exception>
    public static void ValidateCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));
            
        if (category.Length > 100)
            throw new ArgumentException("Category cannot exceed 100 characters", nameof(category));
    }

    /// <summary>
    /// Validates content type
    /// </summary>
    /// <param name="contentType">Content type to validate</param>
    /// <exception cref="ArgumentException">Thrown when content type is invalid</exception>
    public static void ValidateContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty", nameof(contentType));
    }

    /// <summary>
    /// Validates that document is not archived before modification
    /// </summary>
    /// <param name="document">Document to check</param>
    /// <exception cref="InvalidOperationException">Thrown when document is archived</exception>
    public static void ValidateNotArchived(Document document)
    {
        if (document.Status == DocumentStatus.Archived)
            throw new InvalidOperationException("Cannot modify archived document");
    }

    /// <summary>
    /// Validates document approval transition
    /// </summary>
    /// <param name="currentStatus">Current approval status</param>
    /// <param name="newStatus">New approval status</param>
    /// <exception cref="InvalidOperationException">Thrown when transition is invalid</exception>
    public static void ValidateApprovalTransition(ApprovalStatus currentStatus, ApprovalStatus newStatus)
    {
        if (!currentStatus.CanTransitionTo(newStatus.Status))
            throw new InvalidOperationException(
                $"Cannot transition from {currentStatus.Status} to {newStatus.Status}");
    }

    /// <summary>
    /// Validates that a document can be published
    /// </summary>
    /// <param name="document">Document to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when document cannot be published</exception>
    public static void ValidateCanPublish(Document document)
    {
        if (!document.ApprovalStatus.IsApproved)
            throw new InvalidOperationException("Document must be approved before publishing");

        if (document.Status == DocumentStatus.Published)
            throw new InvalidOperationException("Document is already published");
    }

    /// <summary>
    /// Validates that a document can be archived
    /// </summary>
    /// <param name="document">Document to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when document cannot be archived</exception>
    public static void ValidateCanArchive(Document document)
    {
        if (document.Status == DocumentStatus.Archived)
            throw new InvalidOperationException("Document is already archived");
    }

    /// <summary>
    /// Validates related document relationship
    /// </summary>
    /// <param name="documentId">Current document ID</param>
    /// <param name="relatedDocumentId">Related document ID</param>
    /// <exception cref="ArgumentNullException">Thrown when ID is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when relationship is invalid</exception>
    public static void ValidateRelatedDocument(DocumentId documentId, DocumentId relatedDocumentId)
    {
        ValidateNotNull(relatedDocumentId, nameof(relatedDocumentId));

        if (relatedDocumentId.Equals(documentId))
            throw new InvalidOperationException("Document cannot be related to itself");
    }

    /// <summary>
    /// Validates security classification change
    /// </summary>
    /// <param name="currentClassification">Current classification</param>
    /// <param name="newClassification">New classification</param>
    /// <exception cref="ArgumentNullException">Thrown when classification is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when change is not allowed</exception>
    public static void ValidateSecurityClassificationChange(
        SecurityClassification currentClassification,
        SecurityClassification newClassification)
    {
        ValidateNotNull(newClassification, nameof(newClassification));

        if (!currentClassification.CanDowngradeTo(newClassification))
            throw new InvalidOperationException("Cannot upgrade security classification level");
    }

    /// <summary>
    /// Generic null validation helper
    /// </summary>
    /// <param name="value">Value to check</param>
    /// <param name="parameterName">Parameter name for exception</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
    private static void ValidateNotNull(object? value, string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
    }
}