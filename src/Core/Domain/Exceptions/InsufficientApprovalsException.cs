
namespace Enterprise.Documentation.Core.Domain.Exceptions;

/// <summary>
/// Exception thrown when a document does not have sufficient approvals for an operation.
/// </summary>
public class InsufficientApprovalsException : DomainException
{
    public InsufficientApprovalsException(string message) : base("INSUFFICIENT_APPROVALS", message) { }
    
    public InsufficientApprovalsException(string message, Exception innerException) 
        : base("INSUFFICIENT_APPROVALS", message, innerException) { }
}