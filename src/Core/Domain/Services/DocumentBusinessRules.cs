using Enterprise.Documentation.Core.Domain.Entities;
using Enterprise.Documentation.Core.Domain.ValueObjects;

namespace Enterprise.Documentation.Core.Domain.Services;

/// <summary>
/// Domain service containing document business rules and permission logic
/// Centralizes complex business rules for document operations
/// </summary>
public static class DocumentBusinessRules
{
    /// <summary>
    /// Validates if a document state transition is allowed
    /// </summary>
    /// <param name="currentStatus">Current document status</param>
    /// <param name="targetStatus">Target document status</param>
    /// <returns>True if transition is valid</returns>
    public static bool CanTransitionToStatus(DocumentStatus currentStatus, DocumentStatus targetStatus)
    {
        return currentStatus switch
        {
            DocumentStatus.Draft => targetStatus == DocumentStatus.UnderReview,
            DocumentStatus.UnderReview => targetStatus is DocumentStatus.Published or DocumentStatus.Draft,
            DocumentStatus.Published => targetStatus == DocumentStatus.Archived,
            DocumentStatus.Archived => false, // Archived documents cannot transition
            _ => false
        };
    }

    /// <summary>
    /// Checks if a user can modify a document based on business rules
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <param name="userId">User attempting the modification</param>
    /// <param name="userRole">User's role</param>
    /// <returns>True if user can modify the document</returns>
    public static bool CanUserModifyDocument(Document document, UserId userId, UserRole userRole)
    {
        // Archived documents cannot be modified by anyone
        if (document.Status == DocumentStatus.Archived)
            return false;

        // Creator can always modify their own documents
        if (document.CreatedBy.Equals(userId))
            return true;

        // Admins and contributors can modify non-archived documents
        return userRole is UserRole.Administrator or UserRole.Contributor;
    }

    /// <summary>
    /// Checks if a user can approve a document based on business rules
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <param name="userId">User attempting the approval</param>
    /// <param name="userRole">User's role</param>
    /// <returns>True if user can approve the document</returns>
    public static bool CanUserApproveDocument(Document document, UserId userId, UserRole userRole)
    {
        // Cannot approve own documents
        if (document.CreatedBy.Equals(userId))
            return false;

        // Only approvers and admins can approve
        if (userRole is not (UserRole.Approver or UserRole.Administrator))
            return false;

        // Document must be under review to be approved
        return document.Status == DocumentStatus.UnderReview;
    }

    /// <summary>
    /// Validates if a document can be published based on business rules
    /// </summary>
    /// <param name="document">The document to validate</param>
    /// <returns>True if document can be published</returns>
    public static bool CanPublishDocument(Document document)
    {
        // Document must be approved before publishing
        if (!document.ApprovalStatus.IsApproved)
            return false;

        // Document cannot already be published
        return document.Status != DocumentStatus.Published;
    }

    /// <summary>
    /// Validates if content updates are allowed for a document
    /// </summary>
    /// <param name="document">The document to check</param>
    /// <returns>True if content can be updated</returns>
    public static bool CanUpdateContent(Document document)
    {
        return document.Status != DocumentStatus.Archived;
    }

    /// <summary>
    /// Validates document title according to business rules
    /// </summary>
    /// <param name="title">Title to validate</param>
    /// <returns>True if title is valid</returns>
    public static bool IsValidTitle(string title)
    {
        return !string.IsNullOrWhiteSpace(title) && title.Length <= 200;
    }

    /// <summary>
    /// Validates document category according to business rules
    /// </summary>
    /// <param name="category">Category to validate</param>
    /// <returns>True if category is valid</returns>
    public static bool IsValidCategory(string category)
    {
        return !string.IsNullOrWhiteSpace(category) && category.Length <= 100;
    }

    /// <summary>
    /// Validates if a security classification change is allowed
    /// </summary>
    /// <param name="currentClassification">Current classification</param>
    /// <param name="newClassification">New classification</param>
    /// <returns>True if change is allowed</returns>
    public static bool CanChangeSecurityClassification(
        SecurityClassification currentClassification, 
        SecurityClassification newClassification)
    {
        return currentClassification.CanDowngradeTo(newClassification);
    }
}