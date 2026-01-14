
namespace Enterprise.Documentation.Core.Domain.Exceptions;

/// <summary>
/// Exception thrown when attempting an operation that is invalid for the current document status.
/// </summary>
public class InvalidDocumentStatusException : DomainException
{
    public InvalidDocumentStatusException(string message) : base("INVALID_DOCUMENT_STATUS", message) { }
    
    public InvalidDocumentStatusException(string message, Exception innerException) 
        : base("INVALID_DOCUMENT_STATUS", message, innerException) { }
}